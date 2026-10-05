using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Tests.Users;

public sealed class UsersFixture
{
    public IReadOnlyList<User> Users { get; } = new List<User>
    {
        new(UserId(), "A", "B", "a@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        new(UserId(), "C", "D", "c@x.com", UserRole.Employee, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
    };

    public IReadOnlyList<User> CustomerUsers { get; } = new List<User>
    {
        new(UserId(), "A", "B", "a@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        new(UserId(), "C", "D", "c@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
    };

    private static Guid UserId() => Guid.NewGuid();
}
