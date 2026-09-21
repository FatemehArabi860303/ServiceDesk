using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Users;

public sealed class CreateUserShellTests
{
    [Fact]
    public async Task ExecuteAsync_WhenEmailIsAvailable_PersistsTheCoreCreatedUserWithCanonicalEmail()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        var cancellationToken = new CancellationTokenSource().Token;
        repository.IsEmailAvailableAsync("ADA@EXAMPLE.COM", cancellationToken).Returns(true);
        User? persistedUser = null;
        repository.AddAsync(Arg.Any<User>(), cancellationToken)
            .Returns(callInfo =>
            {
                persistedUser = callInfo.Arg<User>();
                return Task.CompletedTask;
            });
        var shell = new CreateUserShell(repository);

        // Act
        var user = await shell.ExecuteAsync(
            new CreateUserCommand("Ada", "Lovelace", " ada@example.com ", UserRole.Administrator),
            cancellationToken);

        // Assert
        persistedUser.Should().BeSameAs(user);
        user.Email.Should().Be("ADA@EXAMPLE.COM");
        await repository.Received(1).IsEmailAvailableAsync("ADA@EXAMPLE.COM", cancellationToken);
        await repository.Received(1).AddAsync(user, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreThrows_DoesNotPersist()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        repository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var shell = new CreateUserShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer));

        // Assert
        (await act.Should().ThrowAsync<CreateUserException>()).Which.Failure.Should().Be(CreateUserFailureKind.EmailUnavailable);
        await repository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceDetectsEmailRace_PropagatesPersistenceException()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        repository.IsEmailAvailableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        repository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new UserEmailAlreadyExistsException(new Exception("Unique constraint violation.")));
        var shell = new CreateUserShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer));

        // Assert
        await act.Should().ThrowAsync<UserEmailAlreadyExistsException>();
    }
}
