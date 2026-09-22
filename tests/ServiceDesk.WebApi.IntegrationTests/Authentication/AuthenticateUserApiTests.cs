using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.WebApi.Authentication;
using ServiceDesk.WebApi.Controllers;
using ServiceDesk.WebApi.IntegrationTests.Users;

namespace ServiceDesk.WebApi.IntegrationTests.Authentication;

public sealed class AuthenticateUserApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly ServiceDeskApiFactory factory;
    private readonly HttpClient client;

    public AuthenticateUserApiTests(ServiceDeskApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task PostLogin_WithValidCredential_ReturnsAccessTokenWithUserIdentityAndRole()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        await SeedAsync(user, "correct password");
        var request = new AuthenticateUserHttpRequest(user.Email, "correct password");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<AuthenticateUserResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        token.Subject.Should().Be(user.Id.ToString("D"));
        token.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == user.Role.ToString());
    }

    [Fact]
    public async Task PostLogin_WithUnknownEmail_ReturnsGenericUnauthorized()
    {
        // Arrange
        var request = new AuthenticateUserHttpRequest("unknown@example.com", "password");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        await AssertGenericAuthenticationFailureAsync(response);
    }

    [Fact]
    public async Task PostLogin_WithWrongPassword_ReturnsGenericUnauthorized()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        await SeedAsync(user, "correct password");
        var request = new AuthenticateUserHttpRequest(user.Email, "wrong password");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        await AssertGenericAuthenticationFailureAsync(response);
    }

    [Fact]
    public async Task PostLogin_WithMissingCredential_ReturnsGenericUnauthorized()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        await SeedAsync(user, password: null);
        var request = new AuthenticateUserHttpRequest(user.Email, "password");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        await AssertGenericAuthenticationFailureAsync(response);
    }

    [Fact]
    public async Task PostLogin_WithInactiveUser_ReturnsGenericUnauthorized()
    {
        // Arrange
        var user = CreateUser(isActive: false);
        await SeedAsync(user, "correct password");
        var request = new AuthenticateUserHttpRequest(user.Email, "correct password");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        await AssertGenericAuthenticationFailureAsync(response);
    }

    [Fact]
    public void Issue_WithExplicitTime_ExpiresExactlyThirtyMinutesLater()
    {
        // Arrange
        var options = new JwtOptions(ServiceDeskApiFactory.JwtIssuer, ServiceDeskApiFactory.JwtAudience, ServiceDeskApiFactory.JwtSigningKey);
        var issuer = new JwtAccessTokenIssuer(options);
        var user = CreateUser(isActive: true);
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

        // Act
        var token = issuer.Issue(user, now);

        // Assert
        token.ExpiresAt.Should().Be(now.AddMinutes(30));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidSignature_ReturnsUnauthorized()
    {
        // Arrange
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(
                ServiceDeskApiFactory.JwtIssuer,
                ServiceDeskApiFactory.JwtAudience,
                DateTimeOffset.UtcNow.AddMinutes(5),
                "different-integration-test-signing-key"));

        // Act
        var response = await client.GetAsync("/test/authentication-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithWrongIssuer_ReturnsUnauthorized()
    {
        // Arrange
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken("wrong-issuer", ServiceDeskApiFactory.JwtAudience, DateTimeOffset.UtcNow.AddMinutes(5)));

        // Act
        var response = await client.GetAsync("/test/authentication-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithWrongAudience_ReturnsUnauthorized()
    {
        // Arrange
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(ServiceDeskApiFactory.JwtIssuer, "wrong-audience", DateTimeOffset.UtcNow.AddMinutes(5)));

        // Act
        var response = await client.GetAsync("/test/authentication-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(ServiceDeskApiFactory.JwtIssuer, ServiceDeskApiFactory.JwtAudience, DateTimeOffset.UtcNow.AddMinutes(-1)));

        // Act
        var response = await client.GetAsync("/test/authentication-probe");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task SeedAsync(User user, string? password)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        context.Users.Add(user);

        if (password is not null)
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            context.UserCredentials.Add(new UserCredential(user.Id, passwordHasher.HashPassword(user, password)));
        }

        await context.SaveChangesAsync();
    }

    private static User CreateUser(bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ada-{Guid.NewGuid():N}@example.com".ToUpperInvariant(),
        UserRole.Administrator,
        isActive,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private static string CreateToken(
        string issuer,
        string audience,
        DateTimeOffset expiresAt,
        string? signingKey = null)
    {
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim(ClaimTypes.Role, UserRole.Administrator.ToString())],
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey ?? ServiceDeskApiFactory.JwtSigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task AssertGenericAuthenticationFailureAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var failure = await response.Content.ReadFromJsonAsync<AuthenticationFailureResponse>();
        failure.Should().Be(new AuthenticationFailureResponse("Authentication failed."));
    }
}
