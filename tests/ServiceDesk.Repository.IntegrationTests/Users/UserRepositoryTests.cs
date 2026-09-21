using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Repository.IntegrationTests.Users;

public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private DbContextOptions<ServiceDeskDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlite(connection).Options;
        await using var context = new ServiceDeskDbContext(options);
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Fact]
    public async Task AddAsync_PersistsCanonicalUserAndMakesEmailUnavailable()
    {
        var user = CreateUser("ADA@EXAMPLE.COM");
        await using var context = new ServiceDeskDbContext(options);
        var repository = new UserRepository(context);

        await repository.AddAsync(user);

        var stored = await context.Users.SingleAsync();
        stored.Should().Be(user);
        (await repository.IsEmailAvailableAsync(" ada@example.com ")).Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_WhenEquivalentCanonicalEmailAlreadyExists_ThrowsKnownPersistenceException()
    {
        await using (var firstContext = new ServiceDeskDbContext(options))
        {
            await new UserRepository(firstContext).AddAsync(CreateUser("ADA@EXAMPLE.COM"));
        }

        await using var secondContext = new ServiceDeskDbContext(options);
        var repository = new UserRepository(secondContext);

        var action = () => repository.AddAsync(CreateUser("ada@example.com"));

        await action.Should().ThrowAsync<UserEmailAlreadyExistsException>();
    }

    private static User CreateUser(string email)
    {
        var outcome = CreateUserCore.Execute(
            new CreateUserCommand("Ada", "Lovelace", email, UserRole.Administrator),
            new CreateUserFacts(true),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));

        return outcome.Should().BeOfType<UserCreated>().Subject.User;
    }
}
