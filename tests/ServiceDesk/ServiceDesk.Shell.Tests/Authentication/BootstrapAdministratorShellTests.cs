using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Authentication;

public sealed class BootstrapAdministratorShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidInput_HashesNormalizedPasswordAndPersistsAdministratorAndCredential()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var bootstrapRepository = Substitute.For<IAdministratorBootstrapRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        const string normalizedPassword = "éabcdefghijklmn";
        userRepository.IsEmailAvailableAsync("ADA@EXAMPLE.COM", Arg.Any<CancellationToken>()).Returns(true);
        bootstrapRepository.IsInstallationEmptyAsync(Arg.Any<CancellationToken>()).Returns(true);
        bootstrapRepository.TryAddAsync(Arg.Any<User>(), Arg.Any<UserCredential>(), Arg.Any<CancellationToken>()).Returns(true);
        passwordHasher.HashPassword(Arg.Any<User>(), normalizedPassword).Returns("hash");
        var shell = new BootstrapAdministratorShell(userRepository, bootstrapRepository, passwordHasher);
        var input = new BootstrapAdministratorInput("Ada", "Lovelace", "ada@example.com", "e\u0301abcdefghijklmn");

        // Act
        var user = await shell.ExecuteAsync(input);

        // Assert
        user.Role.Should().Be(UserRole.Administrator);
        passwordHasher.Received(1).HashPassword(user, normalizedPassword);
        await bootstrapRepository.Received(1).TryAddAsync(user, new UserCredential(user.Id, "hash"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPasswordPolicyRejects_DoesNotHashOrPersist()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var bootstrapRepository = Substitute.For<IAdministratorBootstrapRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        var shell = new BootstrapAdministratorShell(userRepository, bootstrapRepository, passwordHasher);
        var input = new BootstrapAdministratorInput("Ada", "Lovelace", "ada@example.com", "too short");

        // Act
        Func<Task> act = () => shell.ExecuteAsync(input);

        // Assert
        await act.Should().ThrowAsync<PasswordPolicyException>();
        passwordHasher.DidNotReceive().HashPassword(Arg.Any<User>(), Arg.Any<string>());
        await bootstrapRepository.DidNotReceive().TryAddAsync(Arg.Any<User>(), Arg.Any<UserCredential>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAtomicPersistenceRejectsBootstrap_ThrowsInstallationNotEmpty()
    {
        // Arrange
        var userRepository = Substitute.For<IUserRepository>();
        var bootstrapRepository = Substitute.For<IAdministratorBootstrapRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        userRepository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        bootstrapRepository.IsInstallationEmptyAsync(Arg.Any<CancellationToken>()).Returns(true);
        bootstrapRepository.TryAddAsync(Arg.Any<User>(), Arg.Any<UserCredential>(), Arg.Any<CancellationToken>()).Returns(false);
        passwordHasher.HashPassword(Arg.Any<User>(), Arg.Any<string>()).Returns("hash");
        var shell = new BootstrapAdministratorShell(userRepository, bootstrapRepository, passwordHasher);
        var input = new BootstrapAdministratorInput("Ada", "Lovelace", "ada@example.com", "123456789012345");

        // Act
        Func<Task> act = () => shell.ExecuteAsync(input);

        // Assert
        (await act.Should().ThrowAsync<BootstrapAdministratorException>()).Which.Failure.Should()
            .Be(BootstrapAdministratorFailureKind.InstallationNotEmpty);
    }
}
