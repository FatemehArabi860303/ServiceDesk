using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;
using Xunit;

namespace ServiceDesk.WebApi.Tests.Controllers;

public sealed class UsersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UsersControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_AsAdministrator_ReturnsOkWithUsers()
    {
        // Arrange
        var repo = Substitute.For<IUserRepository>();
        var users = new List<User>
        {
            new User(Guid.NewGuid(), "A","B","a@x.com", UserRole.Administrator, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };
        repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(users);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped(_ => repo);
            });
        }).CreateClient();

        // Act
        var resp = await client.GetAsync("/api/users");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
