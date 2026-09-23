using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Authentication;

public sealed class ActivateUserAccountShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithEligibleProvision_HashesNormalizedPasswordAndActivates()
    {
        // Arrange
        var tokenBytes = CreateTokenBytes();
        var token = ToBase64Url(tokenBytes);
        var tokenHash = SHA256.HashData(tokenBytes);
        var user = CreateUser(isActive: true);
        var provision = new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1));
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var provisions = CreateProvisionRepository(provision, hasCredential: false, activationResult: true);
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        passwordHasher.HashPassword(user, "pass\u00E9word with enough length")
            .Returns("hashed-password");
        var shell = new ActivateUserAccountShell(users, provisions, passwordHasher);

        // Act
        await shell.ExecuteAsync(new ActivateUserAccountInput(token, "passe\u0301word with enough length"));

        // Assert
        passwordHasher.Received(1).HashPassword(user, "pass\u00E9word with enough length");
        await provisions.Received(1).TryActivateAsync(
            Arg.Is<UserCredential>(credential => credential.UserId == user.Id && credential.PasswordHash == "hashed-password"),
            Arg.Is<byte[]>(hash => hash.SequenceEqual(tokenHash)),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("invalid*")]
    [InlineData("AA")]
    public async Task ExecuteAsync_WithMalformedToken_ThrowsGenericFailureWithoutRepositoryActivation(string token)
    {
        // Arrange
        var users = Substitute.For<IUserRepository>();
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        var shell = new ActivateUserAccountShell(users, provisions, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new ActivateUserAccountInput(token, "password with enough length"));

        // Assert
        (await act.Should().ThrowAsync<ActivateUserAccountException>()).Which.Failure.Should()
            .Be(ActivateUserAccountFailureKind.ActivationNotPermitted);
        await provisions.DidNotReceive().TryActivateAsync(
            Arg.Any<UserCredential>(),
            Arg.Any<byte[]>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ActivationState.Unknown)]
    [InlineData(ActivationState.Expired)]
    [InlineData(ActivationState.InactiveUser)]
    [InlineData(ActivationState.CredentialedUser)]
    public async Task ExecuteAsync_WithIneligibleActivationState_ThrowsGenericFailure(ActivationState state)
    {
        // Arrange
        var tokenBytes = CreateTokenBytes();
        var tokenHash = SHA256.HashData(tokenBytes);
        var user = CreateUser(isActive: state != ActivationState.InactiveUser);
        var provision = state == ActivationState.Unknown
            ? null
            : new UserAccessProvision(
                user.Id,
                tokenHash,
                state == ActivationState.Expired ? DateTimeOffset.UtcNow.AddMinutes(-1) : DateTimeOffset.UtcNow.AddHours(1));
        var users = Substitute.For<IUserRepository>();
        if (provision is not null)
        {
            users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        }

        var provisions = CreateProvisionRepository(
            provision,
            hasCredential: state == ActivationState.CredentialedUser,
            activationResult: true);
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        var shell = new ActivateUserAccountShell(users, provisions, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new ActivateUserAccountInput(ToBase64Url(tokenBytes), "password with enough length"));

        // Assert
        (await act.Should().ThrowAsync<ActivateUserAccountException>()).Which.Failure.Should()
            .Be(ActivateUserAccountFailureKind.ActivationNotPermitted);
        await provisions.DidNotReceive().TryActivateAsync(
            Arg.Any<UserCredential>(),
            Arg.Any<byte[]>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPassword_ThrowsPasswordPolicyExceptionWithoutPersistence()
    {
        // Arrange
        var tokenBytes = CreateTokenBytes();
        var tokenHash = SHA256.HashData(tokenBytes);
        var user = CreateUser(isActive: true);
        var provision = new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1));
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var provisions = CreateProvisionRepository(provision, hasCredential: false, activationResult: true);
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        var shell = new ActivateUserAccountShell(users, provisions, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new ActivateUserAccountInput(ToBase64Url(tokenBytes), "short"));

        // Assert
        await act.Should().ThrowAsync<PasswordPolicyException>();
        passwordHasher.DidNotReceive().HashPassword(Arg.Any<User>(), Arg.Any<string>());
        await provisions.DidNotReceive().TryActivateAsync(
            Arg.Any<UserCredential>(),
            Arg.Any<byte[]>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFinalActivationRecheckFails_ThrowsGenericFailure()
    {
        // Arrange
        var tokenBytes = CreateTokenBytes();
        var tokenHash = SHA256.HashData(tokenBytes);
        var user = CreateUser(isActive: true);
        var provision = new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1));
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var provisions = CreateProvisionRepository(provision, hasCredential: false, activationResult: false);
        var passwordHasher = Substitute.For<IPasswordHasher<User>>();
        passwordHasher.HashPassword(user, Arg.Any<string>()).Returns("hashed-password");
        var shell = new ActivateUserAccountShell(users, provisions, passwordHasher);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new ActivateUserAccountInput(ToBase64Url(tokenBytes), "password with enough length"));

        // Assert
        (await act.Should().ThrowAsync<ActivateUserAccountException>()).Which.Failure.Should()
            .Be(ActivateUserAccountFailureKind.ActivationNotPermitted);
    }

    private static IUserAccessProvisionRepository CreateProvisionRepository(
        UserAccessProvision? provision,
        bool hasCredential,
        bool activationResult)
    {
        var repository = Substitute.For<IUserAccessProvisionRepository>();
        repository.FindByActivationTokenHashAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(provision);
        repository.HasCredentialAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(hasCredential);
        repository.TryActivateAsync(
                Arg.Any<UserCredential>(),
                Arg.Any<byte[]>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(activationResult);
        return repository;
    }

    private static byte[] CreateTokenBytes() => Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static User CreateUser(bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
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
