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
        // Arrange
        var user = CreateUser("ADA@EXAMPLE.COM");
        await using var context = new ServiceDeskDbContext(options);
        var repository = new UserRepository(context);

        // Act
        await repository.AddAsync(user);

        // Assert
        var stored = await context.Users.SingleAsync();
        stored.Should().Be(user);
        (await repository.IsEmailAvailableAsync(" ada@example.com ")).Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = CreateUser("ADA@EXAMPLE.COM");
        await using var context = new ServiceDeskDbContext(options);
        var repository = new UserRepository(context);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        var found = await repository.GetByIdAsync(user.Id);

        // Assert
        found.Should().Be(user);
    }

    [Fact]
    public async Task AddAsync_WhenEquivalentCanonicalEmailAlreadyExists_ThrowsKnownPersistenceException()
    {
        // Arrange
        var firstUser = CreateUser("ADA@EXAMPLE.COM");
        var secondUser = CreateUser("ada@example.com");
        await using var firstContext = new ServiceDeskDbContext(options);
        var firstRepository = new UserRepository(firstContext);
        await using var secondContext = new ServiceDeskDbContext(options);
        var secondRepository = new UserRepository(secondContext);

        await firstRepository.AddAsync(firstUser);

        // Act
        Func<Task> act = () => secondRepository.AddAsync(secondUser);

        // Assert
        await act.Should().ThrowAsync<UserEmailAlreadyExistsException>();
    }

    [Fact]
    public async Task Database_EmailIndex_IsUnique()
    {
        // Arrange
        const string expectedIndexName = "UX_Users_Email";

        // Act
        var uniqueIndexNames = await GetUniqueIndexNamesAsync();

        // Assert
        uniqueIndexNames.Should().Contain(expectedIndexName);
    }

    private static User CreateUser(string email)
    {
        return CreateUserCore.Execute(
            new CreateUserCommand("Ada", "Lovelace", email, UserRole.Administrator),
            new CreateUserFacts(true),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero));
    }

    private async Task<IReadOnlyList<string>> GetUniqueIndexNamesAsync()
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list('Users');";
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();

        while (await reader.ReadAsync())
        {
            if (reader.GetInt64(2) == 1)
            {
                names.Add(reader.GetString(1));
            }
        }

        return names;
    }
}
