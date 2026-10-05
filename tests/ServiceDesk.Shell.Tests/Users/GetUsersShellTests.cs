using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Users;

public sealed class GetUsersShellTests
{
    [Fact]
    public async Task ExecuteAsync_NoFilter_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new(UserId(), "A", "B", "a@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(UserId(), "C", "D", "c@x.com", UserRole.Employee, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };
        var repo = Substitute.For<IUserRepository>();
        repo.GetUsersAsync(null, Arg.Any<CancellationToken>()).Returns(users);
        var shell = new GetUsersShell(repo);

        // Act
        var result = await shell.ExecuteAsync(null);

        // Assert
        result.Should().HaveCount(2);
        await repo.Received(1).GetUsersAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_FilterByRole_ReturnsOnlyMatching()
    {
        // Arrange
        var users = new List<User>
        {
            new(UserId(), "A", "B", "a@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new(UserId(), "C", "D", "c@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };
        var repo = Substitute.For<IUserRepository>();
        repo.GetUsersAsync(Arg.Any<UserFilter>(), Arg.Any<CancellationToken>()).Returns(users);
        var shell = new GetUsersShell(repo);

        // Act
        var result = await shell.ExecuteAsync(new UserFilter(UserRole.Customer));

        // Assert
        result.Should().OnlyContain(u => u.Role == UserRole.Customer);
        await repo.Received(1).GetUsersAsync(Arg.Any<UserFilter>(), Arg.Any<CancellationToken>());
    }

    private static Guid UserId() => Guid.NewGuid();
}
