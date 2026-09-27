using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Repository;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Repository.IntegrationTests.Notifications;

public sealed class OutboxRepositoryTests : IAsyncLifetime
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
    public async Task TryClaimNextAsync_ClaimsAPendingMessage()
    {
        // Arrange
        var message = CreateMessage();
        await SeedAsync(message);
        var claimToken = Guid.NewGuid();
        await using var context = new ServiceDeskDbContext(options);
        var repository = new OutboxRepository(context);

        // Act
        var claimed = await repository.TryClaimNextAsync(claimToken, message.CreatedAt, message.CreatedAt.AddMinutes(5));

        // Assert
        claimed.Should().Be(new ServiceDesk.Shell.Notifications.OutboxMessageToPublish(
            message.Id,
            message.Type,
            message.Payload));
    }

    [Fact]
    public async Task TryClaimNextAsync_ClaimedMessageCannotBeClaimedConcurrently()
    {
        // Arrange
        var message = CreateMessage();
        await SeedAsync(message);
        var now = message.CreatedAt;
        await using var firstContext = new ServiceDeskDbContext(options);
        await using var secondContext = new ServiceDeskDbContext(options);
        var firstRepository = new OutboxRepository(firstContext);
        var secondRepository = new OutboxRepository(secondContext);

        // Act
        var firstClaim = await firstRepository.TryClaimNextAsync(Guid.NewGuid(), now, now.AddMinutes(5));
        var secondClaim = await secondRepository.TryClaimNextAsync(Guid.NewGuid(), now, now.AddMinutes(5));

        // Assert
        firstClaim.Should().NotBeNull();
        secondClaim.Should().BeNull();
    }

    [Fact]
    public async Task TryClaimNextAsync_ExpiredLeaseCanBeReclaimed()
    {
        // Arrange
        var message = CreateMessage();
        await SeedAsync(message);
        await using (var firstContext = new ServiceDeskDbContext(options))
        {
            var firstRepository = new OutboxRepository(firstContext);
            await firstRepository.TryClaimNextAsync(Guid.NewGuid(), message.CreatedAt, message.CreatedAt.AddMinutes(1));
        }

        await using var secondContext = new ServiceDeskDbContext(options);
        var secondRepository = new OutboxRepository(secondContext);

        // Act
        var reclaimed = await secondRepository.TryClaimNextAsync(
            Guid.NewGuid(),
            message.CreatedAt.AddMinutes(2),
            message.CreatedAt.AddMinutes(7));

        // Assert
        reclaimed!.Id.Should().Be(message.Id);
    }

    [Fact]
    public async Task TryClaimNextAsync_PublishedMessageCannotBeClaimed()
    {
        // Arrange
        var message = CreateMessage();
        await SeedAsync(message);
        var claimToken = Guid.NewGuid();
        await using (var claimContext = new ServiceDeskDbContext(options))
        {
            var claimRepository = new OutboxRepository(claimContext);
            await claimRepository.TryClaimNextAsync(claimToken, message.CreatedAt, message.CreatedAt.AddMinutes(5));
        }

        await using (var publishContext = new ServiceDeskDbContext(options))
        {
            var publishRepository = new OutboxRepository(publishContext);
            await publishRepository.MarkPublishedAsync(message.Id, claimToken, message.CreatedAt.AddMinutes(1));
        }

        await using var context = new ServiceDeskDbContext(options);
        var repository = new OutboxRepository(context);

        // Act
        var claimed = await repository.TryClaimNextAsync(Guid.NewGuid(), message.CreatedAt.AddMinutes(2), message.CreatedAt.AddMinutes(7));

        // Assert
        claimed.Should().BeNull();
    }

    [Fact]
    public async Task MarkPublishedAsync_WithWrongClaimToken_DoesNotPublish()
    {
        // Arrange
        var message = CreateMessage();
        await SeedAsync(message);
        await using (var claimContext = new ServiceDeskDbContext(options))
        {
            var claimRepository = new OutboxRepository(claimContext);
            await claimRepository.TryClaimNextAsync(Guid.NewGuid(), message.CreatedAt, message.CreatedAt.AddMinutes(5));
        }

        await using var context = new ServiceDeskDbContext(options);
        var repository = new OutboxRepository(context);

        // Act
        var marked = await repository.MarkPublishedAsync(message.Id, Guid.NewGuid(), message.CreatedAt.AddMinutes(1));

        // Assert
        marked.Should().BeFalse();
        await using var verificationContext = new ServiceDeskDbContext(options);
        (await verificationContext.OutboxMessages.SingleAsync()).PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task MarkPublishedAsync_WithCorrectClaimToken_SetsPublishedAt()
    {
        // Arrange
        var message = CreateMessage();
        await SeedAsync(message);
        var claimToken = Guid.NewGuid();
        await using (var claimContext = new ServiceDeskDbContext(options))
        {
            var claimRepository = new OutboxRepository(claimContext);
            await claimRepository.TryClaimNextAsync(claimToken, message.CreatedAt, message.CreatedAt.AddMinutes(5));
        }

        var publishedAt = message.CreatedAt.AddMinutes(1);
        await using var context = new ServiceDeskDbContext(options);
        var repository = new OutboxRepository(context);

        // Act
        var marked = await repository.MarkPublishedAsync(message.Id, claimToken, publishedAt);

        // Assert
        marked.Should().BeTrue();
        await using var verificationContext = new ServiceDeskDbContext(options);
        var stored = await verificationContext.OutboxMessages.SingleAsync();
        stored.PublishedAt.Should().Be(publishedAt);
        stored.ClaimToken.Should().BeNull();
        stored.ClaimExpiresAt.Should().BeNull();
    }

    private async Task SeedAsync(OutboxMessage message)
    {
        await using var context = new ServiceDeskDbContext(options);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();
    }

    private static OutboxMessage CreateMessage() => new(
        Guid.NewGuid(),
        RequestProgressedV1.Type,
        "{\"schemaVersion\":1}",
        new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));
}
