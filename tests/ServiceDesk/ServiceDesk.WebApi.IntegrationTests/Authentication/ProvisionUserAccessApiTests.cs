using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.Controllers;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Authentication;

public sealed class ProvisionUserAccessApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public ProvisionUserAccessApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/users/{targetUserId}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostWithCustomerCaller_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Customer, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(caller, target);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithAdministratorRoleClaimButPersistedCustomer_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Customer, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(caller, target);
        SetBearerToken(caller with { Role = UserRole.Administrator });

        // Act
        var response = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithInactiveAdministrator_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: false);
        var target = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(caller, target);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithMissingTarget_ReturnsNotFound()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        await SeedUsersAsync(caller);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/users/{Guid.NewGuid()}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostWithInactiveTarget_ReturnsConflict()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Employee, isActive: false);
        await SeedUsersAsync(caller, target);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostWithCredentialedTarget_ReturnsConflict()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(caller, target, credentialedUser: target);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostWithEligibleAdministratorAndTarget_ReturnsOneTimeTokenAndExpiration()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(caller, target);
        SetBearerToken(caller);

        // Act
        var response = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var result = await response.Content.ReadFromJsonAsync<ProvisionUserAccessResponse>();
        result.Should().NotBeNull();
        result!.ActivationToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("activationTokenHash");
        body.Should().NotContain("password");
    }

    [Fact]
    public async Task PostWhenReProvisioning_ReturnsNewTokenAndReplacesPersistedHash()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        await SeedUsersAsync(caller, target);
        SetBearerToken(caller);

        // Act
        var firstResponse = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);
        var secondResponse = await client.PostAsync($"/api/users/{target.Id}/access-provisioning", null);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = await firstResponse.Content.ReadFromJsonAsync<ProvisionUserAccessResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<ProvisionUserAccessResponse>();
        second!.ActivationToken.Should().NotBe(first!.ActivationToken);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var stored = context.UserAccessProvisions.Single(provision => provision.UserId == target.Id);
        stored.ActivationTokenHash.Should().Equal(SHA256.HashData(DecodeBase64Url(second.ActivationToken)));
    }

    private async Task SeedUsersAsync(params User[] users) => await SeedUsersAsync(users, credentialedUser: null);

    private async Task SeedUsersAsync(User caller, User target, User? credentialedUser) =>
        await SeedUsersAsync([caller, target], credentialedUser);

    private async Task SeedUsersAsync(User[] users, User? credentialedUser)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.AddRange(users);

        if (credentialedUser is not null)
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            context.UserCredentials.Add(new UserCredential(
                credentialedUser.Id,
                passwordHasher.HashPassword(credentialedUser, "correct password")));
        }

        await context.SaveChangesAsync();
    }

    private void SetBearerToken(User user)
    {
        var issuer = new JwtAccessTokenIssuer(new JwtOptions
        {
            Issuer = ServiceDeskApiFactory.JwtIssuer,
            Audience = ServiceDeskApiFactory.JwtAudience,
            SigningKey = ServiceDeskApiFactory.JwtSigningKey
        });
        var token = issuer.Issue(user, DateTimeOffset.UtcNow).Value;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static User CreateUser(UserRole role, bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        role,
        isActive,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
