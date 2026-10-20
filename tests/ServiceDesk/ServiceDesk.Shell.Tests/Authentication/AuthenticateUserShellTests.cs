using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Shell.Tests.Authentication;

public sealed class AuthenticateUserShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithActiveUserAndCorrectPassword_ReturnsUser()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync("ADA@EXAMPLE.COM", Arg.Any<CancellationToken>())
            .Returns(new AuthenticationUser(user, new UserCredential(user.Id, "hash")));
        passwordHasher.VerifyHashedPassword(user, "hash", "password").Returns(PasswordVerificationResult.Success);
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        var result = await shell.ExecuteAsync(new AuthenticateUserInput("ada@example.com", "password"));

        // Assert
        result.Should().Be(user);
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationUser(user, new UserCredential(user.Id, "hash")));
        passwordHasher.VerifyHashedPassword(user, "hash", Arg.Any<string>()).Returns(PasswordVerificationResult.Failed);
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new AuthenticateUserInput("ada@example.com", "password"));

        // Assert
        (await act.Should().ThrowAsync<AuthenticateUserException>()).Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownUser_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AuthenticationUser?)null);
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new AuthenticateUserInput("unknown@example.com", "password"));

        // Assert
        (await act.Should().ThrowAsync<AuthenticateUserException>()).Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
        passwordHasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCredential_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationUser(user, null));
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new AuthenticateUserInput("ada@example.com", "password"));

        // Assert
        (await act.Should().ThrowAsync<AuthenticateUserException>()).Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
        passwordHasher.DidNotReceive().VerifyHashedPassword(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInactiveUser_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var user = CreateUser(isActive: false);
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationUser(user, new UserCredential(user.Id, "hash")));
        passwordHasher.VerifyHashedPassword(user, "hash", Arg.Any<string>()).Returns(PasswordVerificationResult.Success);
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new AuthenticateUserInput("ada@example.com", "password"));

        // Assert
        (await act.Should().ThrowAsync<AuthenticateUserException>()).Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesPasswordBeforeVerification()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationUser(user, new UserCredential(user.Id, "hash")));
        passwordHasher.VerifyHashedPassword(user, "hash", "é").Returns(PasswordVerificationResult.Success);
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        await shell.ExecuteAsync(new AuthenticateUserInput("ada@example.com", "e\u0301"));

        // Assert
        passwordHasher.Received(1).VerifyHashedPassword(user, "hash", "é");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRehashIsNeeded_AuthenticatesWithoutUpdatingCredential()
    {
        // Arrange
        var user = CreateUser(isActive: true);
        var repository = Substitute.For<IAuthenticationRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        repository.FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationUser(user, new UserCredential(user.Id, "hash")));
        passwordHasher.VerifyHashedPassword(user, "hash", Arg.Any<string>())
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);
        var shell = new AuthenticateUserShell(repository, passwordHasher);

        // Act
        var result = await shell.ExecuteAsync(new AuthenticateUserInput("ada@example.com", "password"));

        // Assert
        result.Should().Be(user);
        await repository.Received(1).FindByCanonicalEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static User CreateUser(bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        "ADA@EXAMPLE.COM",
        UserRole.Administrator,
        isActive,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
}
