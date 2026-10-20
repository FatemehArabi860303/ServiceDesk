using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Users;

public sealed class CreateUserShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithActiveAdministrator_PersistsTheCoreCreatedUserWithCanonicalEmail()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var repository = CreateRepository(caller);
        var cancellationToken = new CancellationTokenSource().Token;
        repository.IsEmailAvailableAsync("ADA@EXAMPLE.COM", cancellationToken).Returns(true);
        User? persistedUser = null;
        repository.AddAsync(Arg.Any<User>(), cancellationToken).Returns(callInfo =>
        {
            persistedUser = callInfo.Arg<User>();
            return Task.CompletedTask;
        });
        var shell = new CreateUserShell(repository);

        // Act
        var user = await shell.ExecuteAsync(
            new CreateUserCommand("Ada", "Lovelace", " ada@example.com ", UserRole.Administrator),
            caller.Id,
            cancellationToken);

        // Assert
        persistedUser.Should().BeSameAs(user);
        user.Email.Should().Be("ADA@EXAMPLE.COM");
        await repository.Received(1).GetByIdAsync(caller.Id, cancellationToken);
        await repository.Received(1).IsEmailAvailableAsync("ADA@EXAMPLE.COM", cancellationToken);
        await repository.Received(1).AddAsync(user, cancellationToken);
    }

    [Theory]
    [InlineData(UserRole.Customer, true)]
    [InlineData(UserRole.Employee, true)]
    [InlineData(UserRole.Administrator, false)]
    public async Task ExecuteAsync_WhenPersistedCallerIsNotAnActiveAdministrator_RejectsWithoutPersistence(UserRole role, bool isActive)
    {
        // Arrange
        var caller = CreateUser(role, isActive);
        var repository = CreateRepository(caller);
        repository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var shell = new CreateUserShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(
            new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer),
            caller.Id);

        // Assert
        (await act.Should().ThrowAsync<CreateUserException>()).Which.Failure.Should().Be(CreateUserFailureKind.CallerNotPermitted);
        await repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistedCallerDoesNotExist_RejectsWithoutPersistence()
    {
        // Arrange
        var repository = CreateRepository(null);
        repository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var shell = new CreateUserShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(
            new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer),
            Guid.NewGuid());

        // Assert
        (await act.Should().ThrowAsync<CreateUserException>()).Which.Failure.Should().Be(CreateUserFailureKind.CallerNotPermitted);
        await repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailIsUnavailable_DoesNotPersist()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var repository = CreateRepository(caller);
        repository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var shell = new CreateUserShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(
            new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer),
            caller.Id);

        // Assert
        (await act.Should().ThrowAsync<CreateUserException>()).Which.Failure.Should().Be(CreateUserFailureKind.EmailUnavailable);
        await repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceDetectsEmailRace_PropagatesPersistenceException()
    {
        // Arrange
        var caller = CreateUser(UserRole.Administrator, isActive: true);
        var repository = CreateRepository(caller);
        repository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new UserEmailAlreadyExistsException(new Exception("Unique constraint violation.")));
        var shell = new CreateUserShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(
            new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer),
            caller.Id);

        // Assert
        await act.Should().ThrowAsync<UserEmailAlreadyExistsException>();
    }

    private static IUserRepository CreateRepository(User? caller)
    {
        var repository = Substitute.For<IUserRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(caller);
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
}
