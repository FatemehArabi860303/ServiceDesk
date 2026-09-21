using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Users;
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
        var response = await client.PostAsJsonAsync("/api/users", new CreateUserHttpRequest(
            "Ada", "Lovelace", "ada@example.com", UserRole.Administrator));

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
        var response = await client.PostAsJsonAsync("/api/users", new CreateUserHttpRequest(
            " ", "Lovelace", "ada@example.com", UserRole.Customer));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WithDuplicateEmail_ReturnsConflict()
    {
        var request = new CreateUserHttpRequest("Grace", "Hopper", "grace@example.com", UserRole.Customer);
        (await client.PostAsJsonAsync("/api/users", request)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync("/api/users", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_WithUnsupportedRole_ReturnsBadRequest()
    {
        var response = await client.PostAsJsonAsync("/api/users", new CreateUserHttpRequest(
            "Ada", "Lovelace", "ada@example.com", (UserRole)99));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public sealed class ServiceDeskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlite(connection).Options;
        await using var context = new ServiceDeskDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        Dispose();
        await connection.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ServiceDeskDbContext>>();
            services.RemoveAll<IUserRepository>();
            services.AddDbContext<ServiceDeskDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IUserRepository, UserRepository>();
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
}
