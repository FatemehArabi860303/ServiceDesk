using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;
using BootstrapCommand = ServiceDesk.WebApi.Bootstrap.BootstrapAdministratorCommand;
using WebApplicationStartup = ServiceDesk.WebApi.Bootstrap.WebApplicationStartup;

namespace ServiceDesk.WebApi.IntegrationTests.Bootstrap;

public sealed class BootstrapAdministratorCommandTests
{
    [Fact]
    public void IsRequested_WithExplicitBootstrapArgument_ReturnsTrue()
    {
        // Arrange
        var arguments = new[] { "--bootstrap-administrator" };

        // Act
        var requested = BootstrapCommand.IsRequested(arguments);

        // Assert
        requested.Should().BeTrue();
    }

    [Fact]
    public void IsRequested_WithoutExplicitBootstrapArgument_ReturnsFalse()
    {
        // Arrange
        var arguments = Array.Empty<string>();

        // Act
        var requested = BootstrapCommand.IsRequested(arguments);

        // Assert
        requested.Should().BeFalse();
    }

    [Fact]
    public void CreateInput_ReadsBootstrapConfiguration()
    {
        // Arrange
        var configuration = CreateConfiguration();

        // Act
        var input = BootstrapCommand.CreateInput(configuration);

        // Assert
        input.FirstName.Should().Be("Ada");
        input.LastName.Should().Be("Lovelace");
        input.Email.Should().Be("ada@example.com");
        input.Password.Should().Be("123456789012345");
    }

    [Fact]
    public async Task RunAsync_WithExplicitBootstrapArgument_PerformsBootstrapWithoutStartingHttpApplication()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var bootstrapRepository = Substitute.For<IAdministratorBootstrapRepository>();
        userRepository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        bootstrapRepository.IsInstallationEmptyAsync(Arg.Any<CancellationToken>()).Returns(true);
        bootstrapRepository.TryAddAsync(Arg.Any<User>(), Arg.Any<UserCredential>(), Arg.Any<CancellationToken>()).Returns(true);
        using var services = new ServiceCollection()
            .AddScoped<IUserRepository>(_ => userRepository)
            .AddScoped<IAdministratorBootstrapRepository>(_ => bootstrapRepository)
            .AddScoped<IPasswordHasher<User>, PasswordHasher<User>>()
            .AddScoped<BootstrapAdministratorShell>()
            .BuildServiceProvider();
        var configuration = CreateConfiguration();
        var httpApplicationStarted = false;

        // Act
        await WebApplicationStartup.RunAsync(
            ["--bootstrap-administrator"], configuration, services, () =>
            {
                httpApplicationStarted = true;
                return Task.CompletedTask;
            });

        // Assert
        await bootstrapRepository.Received(1).TryAddAsync(
            Arg.Is<User>(user => user.Role == UserRole.Administrator),
            Arg.Is<UserCredential>(credential => credential.UserId != Guid.Empty),
            Arg.Any<CancellationToken>());
        httpApplicationStarted.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WithoutBootstrapArgument_StartsHttpApplication()
    {
        // Arrange
        var configuration = CreateConfiguration();
        using var services = new ServiceCollection().BuildServiceProvider();
        var httpApplicationStarted = false;

        // Act
        await WebApplicationStartup.RunAsync(Array.Empty<string>(), configuration, services, () =>
        {
            httpApplicationStarted = true;
            return Task.CompletedTask;
        });

        // Assert
        httpApplicationStarted.Should().BeTrue();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BootstrapAdministrator:FirstName"] = "Ada",
                ["BootstrapAdministrator:LastName"] = "Lovelace",
                ["BootstrapAdministrator:Email"] = "ada@example.com",
                ["BootstrapAdministrator:Password"] = "123456789012345"
            })
            .Build();

}
