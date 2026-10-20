using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository.Authentication;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.IntegrationTests.Authentication;

public sealed class AuthenticationRepositoryTests : IAsyncLifetime
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
    public async Task FindByCanonicalEmailAsync_WhenCredentialExists_ReturnsUserAndCredential()
    {
        // Arrange
        var user = CreateUser();
        var credential = new UserCredential(user.Id, "hash");
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        var repository = new AuthenticationRepository(context);

        // Act
        var result = await repository.FindByCanonicalEmailAsync(user.Email);

        // Assert
        result.Should().Be(new AuthenticationUser(user, credential));
    }

    [Fact]
    public async Task FindByCanonicalEmailAsync_WhenCredentialDoesNotExist_ReturnsUserWithoutCredential()
    {
        // Arrange
        var user = CreateUser();
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new AuthenticationRepository(context);

        // Act
        var result = await repository.FindByCanonicalEmailAsync(user.Email);

        // Assert
        result.Should().Be(new AuthenticationUser(user, null));
    }

    [Fact]
    public async Task FindByCanonicalEmailAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = new ServiceDeskDbContext(options);
        var repository = new AuthenticationRepository(context);

        // Act
        var result = await repository.FindByCanonicalEmailAsync("UNKNOWN@EXAMPLE.COM");

        // Assert
        result.Should().BeNull();
    }

    private static User CreateUser() => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        "ADA@EXAMPLE.COM",
        UserRole.Administrator,
        true,
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));
}
