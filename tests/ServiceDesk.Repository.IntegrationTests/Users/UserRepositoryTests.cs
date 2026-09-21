using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Users;
using ServiceDesk.Shell.Users;
using Xunit.Abstractions;

namespace ServiceDesk.Repository.IntegrationTests.Users;

public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ITestOutputHelper output;
    private DbContextOptions<ServiceDeskDbContext> options = null!;

    public UserRepositoryTests(ITestOutputHelper output)
    {
        this.output = output;
    }

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
    public async Task AddAsync_WhenEquivalentCanonicalEmailAlreadyExists_ThrowsKnownPersistenceException()
    {
        // Arrange
        var firstUser = CreateUser("ADA@EXAMPLE.COM");
        var secondUser = CreateUser("ada@example.com");
        output.WriteLine($"First email: {firstUser.Email}");
        output.WriteLine($"Second email: {secondUser.Email}");

        // Act
        bool firstContextUsesConnection;
        string storedFirstEmail;
        bool isEmailIndexUnique;
        await using (var firstContext = new ServiceDeskDbContext(options))
        {
            firstContextUsesConnection = ReferenceEquals(firstContext.Database.GetDbConnection(), connection);
            await new UserRepository(firstContext).AddAsync(firstUser);

            storedFirstEmail = (await firstContext.Users.SingleAsync()).Email;

            var emailIndex = firstContext.Model.FindEntityType(typeof(User))!
                .GetIndexes()
                .Single(index => index.Properties.Single().Name == nameof(User.Email));
            output.WriteLine($"EF index: {emailIndex.Name}; unique: {emailIndex.IsUnique}");
            isEmailIndexUnique = emailIndex.IsUnique;
        }

        var uniqueIndexNames = await GetUniqueIndexNamesAsync();
        output.WriteLine($"SQLite unique indexes: {string.Join(", ", uniqueIndexNames)}");

        await using var secondContext = new ServiceDeskDbContext(options);
        var secondContextUsesConnection = ReferenceEquals(secondContext.Database.GetDbConnection(), connection);
        var repository = new UserRepository(secondContext);

        Func<Task> action = () => repository.AddAsync(secondUser);

        // Assert
        firstUser.Email.Should().Be("ADA@EXAMPLE.COM");
        secondUser.Email.Should().Be("ADA@EXAMPLE.COM");
        firstContextUsesConnection.Should().BeTrue();
        storedFirstEmail.Should().Be("ADA@EXAMPLE.COM");
        isEmailIndexUnique.Should().BeTrue();
        uniqueIndexNames.Should().Contain("UX_Users_Email");
        secondContextUsesConnection.Should().BeTrue();
        await action.Should().ThrowAsync<UserEmailAlreadyExistsException>();
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
