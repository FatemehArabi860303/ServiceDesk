using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using ServiceDesk.Contracts;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Repository.IntegrationTests.Notifications;

public sealed class OutboxPublicationTests : IAsyncLifetime
{
    private readonly SqliteConnection connection =
        new("Data Source=:memory:");

    private DbContextOptions<ServiceDeskDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();

        options = new DbContextOptionsBuilder<ServiceDeskDbContext>()
            .UseSqlite(connection)
            .Options;

        await using ServiceDeskDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => connection.DisposeAsync().AsTask();

    [Theory]
    [InlineData(NotificationRequestedV1.Type)]
    [InlineData(TicketAiAnalysisRequestedV1.Type)]
    public async Task ProcessNextAsync_AfterSuccessfulPublication_PersistsPublishedAt(
        string eventType)
    {
        // Arrange
        OutboxMessage message = await seedAsync(eventType);

        await using ServiceDeskDbContext context = new(options);
        OutboxRepository repository = new(context);
        TestPublisher publisher = new(null);
        ProcessOutboxShell shell = new(repository, publisher);

        // Act
        bool processed = await shell.ProcessNextAsync();

        await using ServiceDeskDbContext verification = new(options);
        OutboxMessage stored = await verification.OutboxMessages.SingleAsync();

        // Assert
        processed.Should().BeTrue();
        stored.Id.Should().Be(message.Id);
        stored.PublishedAt.Should().NotBeNull();
        stored.ClaimToken.Should().BeNull();
        stored.ClaimExpiresAt.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessNextAsync_AfterFailedPublication_RetainsEventAndAllowsExpiredClaimRecovery(
        bool unsupported)
    {
        // Arrange
        string eventType = unsupported
            ? "unsupported.event.v1"
            : TicketAiAnalysisRequestedV1.Type;

        OutboxMessage message = await seedAsync(eventType);

        Exception expectedException = unsupported
            ? new NotSupportedException("Unsupported event.")
            : new IOException("Returned or unconfirmed publication.");

        await using ServiceDeskDbContext context = new(options);
        OutboxRepository repository = new(context);
        TestPublisher publisher = new(expectedException);
        ProcessOutboxShell shell = new(repository, publisher);

        Func<Task> operation = () => shell.ProcessNextAsync();
        Guid recoveryToken = Guid.NewGuid();

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        await using ServiceDeskDbContext verification = new(options);
        OutboxMessage stored = await verification.OutboxMessages
            .AsNoTracking()
            .SingleAsync();

        DateTimeOffset recoveryTime =
            stored.ClaimExpiresAt!.Value.AddSeconds(1);

        await using ServiceDeskDbContext recoveryContext = new(options);
        OutboxRepository recoveryRepository = new(recoveryContext);

        OutboxMessageToPublish? recovered =
            await recoveryRepository.TryClaimNextAsync(
                recoveryToken,
                recoveryTime,
                recoveryTime.AddMinutes(5));

        // Assert
        exception.Should().BeSameAs(expectedException);
        stored.PublishedAt.Should().BeNull();
        stored.ClaimToken.Should().NotBeNull();

        recovered.Should().NotBeNull();
        recovered!.Id.Should().Be(message.Id);
        recovered.Type.Should().Be(eventType);
        recovered.Payload.Should().Be(message.Payload);
    }

    private async Task<OutboxMessage> seedAsync(string eventType)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        OutboxMessage message = new(
            Guid.NewGuid(),
            eventType,
            "{}",
            now,
            now);

        await using ServiceDeskDbContext context = new(options);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        return message;
    }

    private sealed class TestPublisher(Exception? failure)
        : IOutboxMessagePublisher
    {
        public Task PublishAsync(
            OutboxMessageToPublish message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return failure is null
                ? Task.CompletedTask
                : Task.FromException(failure);
        }
    }
}