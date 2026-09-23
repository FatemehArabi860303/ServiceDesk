using System.Security.Cryptography;
using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Authentication;

public sealed class ProvisionUserAccessShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithEligibleAdministratorAndTarget_ReturnsRawTokenAndPersistsOnlyItsHash()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        var users = CreateUserRepository(caller, target);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        provisions.HasCredentialAsync(target.Id, Arg.Any<CancellationToken>()).Returns(false);
        UserAccessProvision? storedProvision = null;
        provisions.ReplaceAsync(Arg.Do<UserAccessProvision>(provision => storedProvision = provision), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        var result = await shell.ExecuteAsync(target.Id, caller.Id);

        // Assert
        result.ActivationToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromSeconds(2));
        storedProvision.Should().NotBeNull();
        storedProvision!.UserId.Should().Be(target.Id);
        storedProvision.ActivationTokenHash.Should().Equal(SHA256.HashData(DecodeBase64Url(result.ActivationToken)));
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingCaller_ThrowsCallerNotPermittedWithoutPersisting()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var target = CreateUser(UserRole.Customer, isActive: true);
        var users = CreateUserRepository(null, target, callerId);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(target.Id, callerId);

        // Assert
        (await act.Should().ThrowAsync<ProvisionUserAccessException>()).Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.CallerNotPermitted);
        await provisions.DidNotReceive().ReplaceAsync(Arg.Any<UserAccessProvision>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInactiveCaller_ThrowsCallerNotPermitted()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: false);
        var target = CreateUser(UserRole.Customer, isActive: true);
        var users = CreateUserRepository(caller, target);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(target.Id, caller.Id);

        // Assert
        (await act.Should().ThrowAsync<ProvisionUserAccessException>()).Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.CallerNotPermitted);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonAdministratorCaller_ThrowsCallerNotPermitted()
    {
        // Arrange
        var caller = CreateUser(UserRole.Employee, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        var users = CreateUserRepository(caller, target);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(target.Id, caller.Id);

        // Assert
        (await act.Should().ThrowAsync<ProvisionUserAccessException>()).Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.CallerNotPermitted);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingTarget_ThrowsTargetNotFound()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var targetId = Guid.NewGuid();
        var users = CreateUserRepository(caller, null, targetId);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(targetId, caller.Id);

        // Assert
        (await act.Should().ThrowAsync<ProvisionUserAccessException>()).Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.TargetNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WithInactiveTarget_ThrowsTargetInactive()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: false);
        var users = CreateUserRepository(caller, target);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(target.Id, caller.Id);

        // Assert
        (await act.Should().ThrowAsync<ProvisionUserAccessException>()).Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.TargetInactive);
    }

    [Fact]
    public async Task ExecuteAsync_WithCredentialedTarget_ThrowsTargetAlreadyCredentialed()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        var users = CreateUserRepository(caller, target);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        provisions.HasCredentialAsync(target.Id, Arg.Any<CancellationToken>()).Returns(true);
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(target.Id, caller.Id);

        // Assert
        (await act.Should().ThrowAsync<ProvisionUserAccessException>()).Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.TargetAlreadyCredentialed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReProvisioning_ReplacesThePendingProvisionWithNewToken()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var target = CreateUser(UserRole.Customer, isActive: true);
        var users = CreateUserRepository(caller, target);
        var provisions = Substitute.For<IUserAccessProvisionRepository>();
        provisions.HasCredentialAsync(target.Id, Arg.Any<CancellationToken>()).Returns(false);
        var persistedProvisions = new List<UserAccessProvision>();
        provisions.ReplaceAsync(Arg.Do<UserAccessProvision>(provision => persistedProvisions.Add(provision)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var shell = new ProvisionUserAccessShell(users, provisions);

        // Act
        var first = await shell.ExecuteAsync(target.Id, caller.Id);
        var second = await shell.ExecuteAsync(target.Id, caller.Id);

        // Assert
        second.ActivationToken.Should().NotBe(first.ActivationToken);
        persistedProvisions.Should().HaveCount(2);
        persistedProvisions[1].ActivationTokenHash.Should().Equal(SHA256.HashData(DecodeBase64Url(second.ActivationToken)));
    }

    private static IUserRepository CreateUserRepository(User? caller, User? target, Guid? targetId = null)
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var id = call.Arg<Guid>();
            return Task.FromResult(caller?.Id == id ? caller : target?.Id == id ? target : null);
        });

        return repository;
    }

    private static User CreateUser(UserRole role, bool isActive) => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        role,
        isActive,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
