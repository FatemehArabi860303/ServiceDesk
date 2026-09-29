namespace ServiceDesk.Shell.Notifications;

public interface IOutboxRepository
{
    Task<OutboxMessageToPublish?> TryClaimNextAsync(
        Guid claimToken,
        DateTimeOffset now,
        DateTimeOffset claimExpiresAt,
        CancellationToken cancellationToken = default);

    Task<bool> MarkPublishedAsync(
        Guid messageId,
        Guid claimToken,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken = default);
}
