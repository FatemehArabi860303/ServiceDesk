using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.Controllers;

namespace ServiceDesk.WebApi.IntegrationTests.Users;

public sealed class CreateUserApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public CreateUserApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWithoutJwt_ReturnsUnauthorized()
    {
        // Arrange
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    public async Task PostWithNonAdministratorCaller_ReturnsForbidden(UserRole role)
    {
        // Arrange
        var caller = CreateUser(role, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithInactiveAdministrator_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: false);
        await SeedUserAsync(caller);
        SetBearerToken(caller);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithMissingPersistedCaller_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        SetBearerToken(caller);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithAdministratorJwtButPersistedCustomer_ReturnsForbidden()
    {
        // Arrange
        var caller = CreateUser(UserRole.Customer, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller with { Role = UserRole.Administrator });
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWithActiveAdministrator_ReturnsCreatedUser()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller);
        var request = CreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Email.Should().Be(request.Email!.ToUpperInvariant());
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task PostWithActiveAdministratorAndInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller);
        var request = new CreateUserHttpRequest(" ", "Lovelace", "ada@example.com", UserRole.Customer);

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostWithActiveAdministratorAndDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        await SeedUserAsync(caller);
        SetBearerToken(caller);
        var request = CreateRequest();

        // Act
        var firstResponse = await client.PostAsJsonAsync("/api/users", request);
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task SeedUserAsync(User user)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.Add(user);
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

    private static CreateUserHttpRequest CreateRequest() => new(
        "Ada",
        "Lovelace",
        $"ada-{Guid.NewGuid():N}@example.com",
        UserRole.Customer);

    private static User CreateUser(UserRole role, bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        role,
        isActive,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);
}
