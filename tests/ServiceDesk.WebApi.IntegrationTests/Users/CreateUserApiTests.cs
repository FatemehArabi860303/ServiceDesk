using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Authentication;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;
using ServiceDesk.WebApi.IntegrationTests.Authentication;
using ServiceDesk.WebApi.Controllers;

namespace ServiceDesk.WebApi.IntegrationTests.Users;

public sealed class CreateUserApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly HttpClient client;

    public CreateUserApiTests(ServiceDeskApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_WithValidUser_ReturnsCreatedUser()
    {
        // Arrange
        var request = new CreateUserHttpRequest(
            "Ada", "Lovelace", "ada@example.com", UserRole.Administrator);

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().NotBeEmpty();
        created.Email.Should().Be("ADA@EXAMPLE.COM");
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserHttpRequest(
            " ", "Lovelace", "ada@example.com", UserRole.Customer);

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange
        var request = new CreateUserHttpRequest("Grace", "Hopper", "grace@example.com", UserRole.Customer);

        // Act
        var firstResponse = await client.PostAsJsonAsync("/api/users", request);
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_WithUnsupportedRole_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserHttpRequest(
            "Ada", "Lovelace", "ada@example.com", (UserRole)99);

        // Act
        var response = await client.PostAsJsonAsync("/api/users", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public sealed class ServiceDeskApiFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "ServiceDesk.WebApi.IntegrationTests";
    public const string JwtAudience = "ServiceDesk.WebApi.IntegrationTests";
    public const string JwtSigningKey = "integration-test-signing-key-32-bytes";
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:SigningKey"] = JwtSigningKey
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ServiceDeskDbContext>>();
            services.RemoveAll<DbContextOptions<ServiceDeskDbContext>>();
            services.RemoveAll<ServiceDeskDbContext>();
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IAuthenticationRepository>();
            services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
            services.AddControllers().AddApplicationPart(typeof(AuthenticationProbeController).Assembly);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
