using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Users;
using ServiceDesk.WebApi.Controllers;
using Xunit.Abstractions;

namespace ServiceDesk.WebApi.IntegrationTests.Users;

public sealed class CreateUserApiTests : IClassFixture<ServiceDeskApiFactory>
{
    private readonly HttpClient client;
    private readonly ITestOutputHelper output;

    public CreateUserApiTests(ServiceDeskApiFactory factory, ITestOutputHelper output)
    {
        client = factory.CreateClient();
        this.output = output;
    }

    [Fact]
    public async Task Post_WithValidUser_ReturnsCreatedUser()
    {
        var response = await client.PostAsJsonAsync("/api/users", new CreateUserHttpRequest(
            "Ada", "Lovelace", "ada@example.com", UserRole.Administrator));
        output.WriteLine($"Response status: {(int)response.StatusCode} {response.StatusCode}");
        output.WriteLine($"Response body: {await response.Content.ReadAsStringAsync()}");

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

public sealed class ServiceDeskApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<ServiceDeskDbContext>>();
            services.RemoveAll<DbContextOptions<ServiceDeskDbContext>>();
            services.RemoveAll<ServiceDeskDbContext>();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
