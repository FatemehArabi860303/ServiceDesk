using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.WebApi.Controllers;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Authentication;

public sealed class ActivateUserAccountApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public ActivateUserAccountApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostActivate_WithoutJwt_WithValidProvision_ReturnsNoContentAndAllowsLogin()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var tokenBytes = CreateTokenBytes();
        await SeedAsync(user, tokenBytes);
        var request = new ActivateUserAccountHttpRequest(ToBase64Url(tokenBytes), "correct password");

        // Act
        var activationResponse = await client.PostAsJsonAsync("/api/auth/activate", request);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthenticateUserHttpRequest(user.Email, "correct password"));

        // Assert
        activationResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.UserCredentials.Any(credential => credential.UserId == user.Id).Should().BeTrue();
        context.UserAccessProvisions.Any(provision => provision.UserId == user.Id).Should().BeFalse();
    }

    [Fact]
    public async Task PostActivate_WithConsumedToken_ReturnsGenericBadRequest()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var tokenBytes = CreateTokenBytes();
        await SeedAsync(user, tokenBytes);
        var request = new ActivateUserAccountHttpRequest(ToBase64Url(tokenBytes), "password with enough length");

        // Act
        var firstResponse = await client.PostAsJsonAsync("/api/auth/activate", request);
        var response = await client.PostAsJsonAsync("/api/auth/activate", request);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AssertGenericActivationFailureAsync(response);
    }

    [Theory]
    [InlineData(ActivationState.Unknown)]
    [InlineData(ActivationState.Expired)]
    [InlineData(ActivationState.InactiveUser)]
    [InlineData(ActivationState.CredentialedUser)]
    public async Task PostActivate_WithSensitiveActivationFailure_ReturnsGenericBadRequest(ActivationState state)
    {
        // Arrange
        var user = CreateUser(isActive: state != ActivationState.InactiveUser);
        var tokenBytes = CreateTokenBytes();
        if (state != ActivationState.Unknown)
        {
            await SeedAsync(
                user,
                tokenBytes,
                expiresAt: state == ActivationState.Expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1),
                credentialPassword: state == ActivationState.CredentialedUser ? "existing password with enough length" : null);
        }

        var request = new ActivateUserAccountHttpRequest(ToBase64Url(tokenBytes), "password with enough length");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/activate", request);

        // Assert
        await AssertGenericActivationFailureAsync(response);
    }

    [Fact]
    public async Task PostActivate_WithMalformedToken_ReturnsSameGenericBadRequest()
    {
        // Arrange
        var request = new ActivateUserAccountHttpRequest("not-a-valid-token*", "password with enough length");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/activate", request);

        // Assert
        await AssertGenericActivationFailureAsync(response);
    }

    [Fact]
    public async Task PostActivate_WithInvalidPassword_ReturnsExplicitBadRequest()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var tokenBytes = CreateTokenBytes();
        await SeedAsync(user, tokenBytes);
        var request = new ActivateUserAccountHttpRequest(ToBase64Url(tokenBytes), "short");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/activate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Password validation failed.");
        body.Should().NotContain("Activation failed.");
    }

    [Fact]
    public async Task PostActivate_WithMalformedJson_ReturnsBadRequest()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/activate")
        {
            Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json")
        };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task SeedAsync(
        User user,
        byte[] tokenBytes,
        DateTimeOffset? expiresAt = null,
        string? credentialPassword = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.Add(user);
        context.UserAccessProvisions.Add(new UserAccessProvision(
            user.Id,
            SHA256.HashData(tokenBytes),
            expiresAt ?? DateTimeOffset.UtcNow.AddHours(1)));

        if (credentialPassword is not null)
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            context.UserCredentials.Add(new UserCredential(
                user.Id,
                passwordHasher.HashPassword(user, credentialPassword)));
        }

        await context.SaveChangesAsync();
    }

    private static async Task AssertGenericActivationFailureAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var failure = await response.Content.ReadFromJsonAsync<ActivationFailureResponse>();
        failure.Should().Be(new ActivationFailureResponse("Activation failed."));
    }

    private static byte[] CreateTokenBytes() => RandomNumberGenerator.GetBytes(32);

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static User CreateUser(bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM".ToUpperInvariant(),
        UserRole.Customer,
        isActive,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    public enum ActivationState
    {
        Unknown,
        Expired,
        InactiveUser,
        CredentialedUser
    }
}
