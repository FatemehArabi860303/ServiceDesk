using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using FluentAssertions;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Authentication;
using ServiceDesk.Shell.Authentication;

namespace ServiceDesk.Repository.IntegrationTests.Authentication;

public sealed class AdministratorBootstrapRepositoryTests : IAsyncLifetime
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
    public async Task TryAddAsync_WhenInstallationIsEmpty_PersistsAdministratorAndCredential()
    {
        // Arrange
        var administrator = CreateAdministrator();
        var credential = new UserCredential(administrator.Id, "hash");
        await using var context = new ServiceDeskDbContext(options);
        var repository = new AdministratorBootstrapRepository(context);

        // Act
        var added = await repository.TryAddAsync(administrator, credential);

        // Assert
        added.Should().BeTrue();
        (await context.Users.SingleAsync()).Should().Be(administrator);
        (await context.UserCredentials.SingleAsync()).Should().Be(credential);
    }

    [Fact]
    public async Task TryAddAsync_WhenUserAlreadyExists_ReturnsFalseWithoutChanges()
    {
        // Arrange
        var existingUser = CreateAdministrator();
        await using var setupContext = new ServiceDeskDbContext(options);
        setupContext.Users.Add(existingUser);
        await setupContext.SaveChangesAsync();
        var administrator = CreateAdministrator();
        var credential = new UserCredential(administrator.Id, "hash");
        await using var context = new ServiceDeskDbContext(options);
        var repository = new AdministratorBootstrapRepository(context);

        // Act
        var added = await repository.TryAddAsync(administrator, credential);

        // Assert
        added.Should().BeFalse();
        (await context.Users.CountAsync()).Should().Be(1);
        (await context.UserCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TryAddAsync_WhenCredentialPersistenceFails_RollsBackAdministratorCreation()
    {
        // Arrange
        var failureOptions = new DbContextOptionsBuilder<ServiceDeskDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new CredentialSaveFailureInterceptor())
            .Options;
        var administrator = CreateAdministrator();
        var credential = new UserCredential(administrator.Id, "hash");
        await using var context = new ServiceDeskDbContext(failureOptions);
        var repository = new AdministratorBootstrapRepository(context);

        // Act
        Func<Task> act = () => repository.TryAddAsync(administrator, credential);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var verificationContext = new ServiceDeskDbContext(options);
        (await verificationContext.Users.CountAsync()).Should().Be(0);
        (await verificationContext.UserCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UserCredential_WhenUserIsDeleted_IsDeletedByCascade()
    {
        // Arrange
        var administrator = CreateAdministrator();
        var credential = new UserCredential(administrator.Id, "hash");
        await using var setupContext = new ServiceDeskDbContext(options);
        var repository = new AdministratorBootstrapRepository(setupContext);
        await repository.TryAddAsync(administrator, credential);
        await using var context = new ServiceDeskDbContext(options);

        // Act
        context.Users.Remove(await context.Users.SingleAsync());
        await context.SaveChangesAsync();

        // Assert
        (await context.UserCredentials.CountAsync()).Should().Be(0);
    }

    private static User CreateAdministrator() =>
        BootstrapAdministratorCore.Execute(
            new BootstrapAdministratorCommand("Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.com"),
            new BootstrapAdministratorFacts(true, true),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    private sealed class CredentialSaveFailureInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<UserCredential>().Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new InvalidOperationException("Credential persistence failed.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
