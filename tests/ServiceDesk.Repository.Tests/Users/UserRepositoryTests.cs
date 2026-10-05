using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Users;

namespace ServiceDesk.Repository.Tests.Users;

public sealed class UserRepositoryTests
{
    [Fact]
    public async Task GetUsersAsync_WithRoleFilter_PerformsDatabaseFiltering()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ServiceDeskDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid():N}")
            .Options;

        await using var context = new ServiceDeskDbContext(options);
        context.Users.AddRange(
            new User(Guid.NewGuid(), "A", "B", "a@x.com", UserRole.Customer, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new User(Guid.NewGuid(), "C", "D", "c@x.com", UserRole.Employee, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        );
        await context.SaveChangesAsync();

        var repo = new UserRepository(context);

        // Act
        var customers = await repo.GetUsersAsync(new UserFilter(UserRole.Customer));
        var all = await repo.GetUsersAsync(null);

        // Assert
        customers.Should().OnlyContain(u => u.Role == UserRole.Customer);
        all.Should().HaveCount(2);
    }
}