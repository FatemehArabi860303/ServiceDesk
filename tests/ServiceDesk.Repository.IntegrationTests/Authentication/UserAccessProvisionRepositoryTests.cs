using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository.Authentication;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.IntegrationTests.Authentication;

public sealed class UserAccessProvisionRepositoryTests : IAsyncLifetime
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
    public async Task ReplaceAsync_WithNewProvision_PersistsOnlyTheTokenHash()
    {
        // Arrange
        var user = CreateUser();
        var tokenHash = CreateHash(1);
        var provision = new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(24));
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);

        // Act
        await repository.ReplaceAsync(provision);

        // Assert
        var stored = await context.UserAccessProvisions.SingleAsync();
        stored.UserId.Should().Be(user.Id);
        stored.ActivationTokenHash.Should().Equal(tokenHash);
        stored.ActivationTokenHash.Should().HaveCount(32);
    }

    [Fact]
    public async Task ReplaceAsync_ForSameUser_ReplacesThePreviousPendingProvision()
    {
        // Arrange
        var user = CreateUser();
        var first = new UserAccessProvision(user.Id, CreateHash(1), DateTimeOffset.UtcNow.AddHours(24));
        var second = new UserAccessProvision(user.Id, CreateHash(2), DateTimeOffset.UtcNow.AddHours(24));
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);

        // Act
        await repository.ReplaceAsync(first);
        await repository.ReplaceAsync(second);

        // Assert
        var stored = await context.UserAccessProvisions.SingleAsync();
        stored.ActivationTokenHash.Should().Equal(second.ActivationTokenHash);
    }

    [Fact]
    public async Task SaveChangesAsync_WithDuplicateTokenHash_RejectsTheSecondProvision()
    {
        // Arrange
        var firstUser = CreateUser();
        var secondUser = CreateUser();
        var tokenHash = CreateHash(1);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.AddRange(firstUser, secondUser);
        context.UserAccessProvisions.Add(new UserAccessProvision(firstUser.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(24)));
        await context.SaveChangesAsync();
        context.UserAccessProvisions.Add(new UserAccessProvision(secondUser.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(24)));

        // Act
        Func<Task> act = () => context.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task HasCredentialAsync_WhenCredentialExists_ReturnsTrue()
    {
        // Arrange
        var user = CreateUser();
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserCredentials.Add(new UserCredential(user.Id, "hash"));
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);

        // Act
        var result = await repository.HasCredentialAsync(user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FindByActivationTokenHashAsync_WhenProvisionExists_ReturnsProvision()
    {
        // Arrange
        var user = CreateUser();
        var tokenHash = CreateHash(3);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserAccessProvisions.Add(new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1)));
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);

        // Act
        var provision = await repository.FindByActivationTokenHashAsync(tokenHash);

        // Assert
        provision.Should().NotBeNull();
        provision!.UserId.Should().Be(user.Id);
        provision.ActivationTokenHash.Should().Equal(tokenHash);
    }

    [Fact]
    public async Task TryActivateAsync_WithEligibleProvision_CreatesCredentialAndConsumesProvision()
    {
        // Arrange
        var user = CreateUser();
        var tokenHash = CreateHash(4);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserAccessProvisions.Add(new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1)));
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);
        var credential = new UserCredential(user.Id, "password-hash");

        // Act
        var activated = await repository.TryActivateAsync(credential, tokenHash, DateTimeOffset.UtcNow);
        context.ChangeTracker.Clear();

        // Assert
        activated.Should().BeTrue();
        (await context.UserCredentials.SingleAsync()).Should().Be(credential);
        (await context.UserAccessProvisions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task TryActivateAsync_WithExpiredProvision_PreservesProvision()
    {
        // Arrange
        var user = CreateUser();
        var tokenHash = CreateHash(5);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserAccessProvisions.Add(new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);

        // Act
        var activated = await repository.TryActivateAsync(new UserCredential(user.Id, "password-hash"), tokenHash, DateTimeOffset.UtcNow);
        context.ChangeTracker.Clear();

        // Assert
        activated.Should().BeFalse();
        (await context.UserCredentials.AnyAsync()).Should().BeFalse();
        (await context.UserAccessProvisions.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task TryActivateAsync_WhenCredentialAlreadyExists_PreservesProvision()
    {
        // Arrange
        var user = CreateUser();
        var tokenHash = CreateHash(6);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserCredentials.Add(new UserCredential(user.Id, "existing-password-hash"));
        context.UserAccessProvisions.Add(new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1)));
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);

        // Act
        var activated = await repository.TryActivateAsync(new UserCredential(user.Id, "new-password-hash"), tokenHash, DateTimeOffset.UtcNow);
        context.ChangeTracker.Clear();

        // Assert
        activated.Should().BeFalse();
        (await context.UserCredentials.SingleAsync()).PasswordHash.Should().Be("existing-password-hash");
        (await context.UserAccessProvisions.AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task TryActivateAsync_WhenTokenIsUsedTwice_AllowsOnlyTheFirstActivation()
    {
        // Arrange
        var user = CreateUser();
        var tokenHash = CreateHash(7);
        await using var context = new ServiceDeskDbContext(options);
        context.Users.Add(user);
        context.UserAccessProvisions.Add(new UserAccessProvision(user.Id, tokenHash, DateTimeOffset.UtcNow.AddHours(1)));
        await context.SaveChangesAsync();
        var repository = new UserAccessProvisionRepository(context);
        var credential = new UserCredential(user.Id, "password-hash");

        // Act
        var firstActivation = await repository.TryActivateAsync(credential, tokenHash, DateTimeOffset.UtcNow);
        var secondActivation = await repository.TryActivateAsync(credential, tokenHash, DateTimeOffset.UtcNow);

        // Assert
        firstActivation.Should().BeTrue();
        secondActivation.Should().BeFalse();
    }

    private static User CreateUser() => new(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        $"ADA-{Guid.NewGuid():N}@EXAMPLE.COM",
        UserRole.Customer,
        true,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow);

    private static byte[] CreateHash(byte value) => Enumerable.Repeat(value, 32).ToArray();
}
