namespace ServiceDesk.Shell.Notifications;

public sealed class ProcessOutboxShell(
    IOutboxRepository outboxRepository,
    IRequestProgressPublisher publisher)
{
    private static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(5);

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var claimToken = Guid.NewGuid();
        var message = await outboxRepository.TryClaimNextAsync(
            claimToken,
            now,
            now.Add(ClaimDuration),
            cancellationToken);

        if (message is null)
        {
            return false;
        }

        await publisher.PublishAsync(message, cancellationToken);
        return await outboxRepository.MarkPublishedAsync(
            message.Id,
            claimToken,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }
}
