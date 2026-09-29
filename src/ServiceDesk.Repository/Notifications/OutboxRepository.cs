using Microsoft.EntityFrameworkCore;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Repository.Notifications;

public sealed class OutboxRepository(ServiceDeskDbContext dbContext) : IOutboxRepository
{
    public async Task<OutboxMessageToPublish?> TryClaimNextAsync(
        Guid claimToken,
        DateTimeOffset now,
        DateTimeOffset claimExpiresAt,
        CancellationToken cancellationToken = default)
    {
        var messageId = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.PublishedAt == null
                && (message.ClaimExpiresAt == null || message.ClaimExpiresAt <= now))
            .Select(message => (Guid?)message.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (messageId is null)
        {
            return null;
        }

        var rowsAffected = await dbContext.OutboxMessages
            .Where(message => message.Id == messageId
                && message.PublishedAt == null
                && (message.ClaimExpiresAt == null || message.ClaimExpiresAt <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.ClaimToken, claimToken)
                .SetProperty(message => message.ClaimExpiresAt, claimExpiresAt), cancellationToken);

        if (rowsAffected != 1)
        {
            return null;
        }

        return await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Id == messageId && message.ClaimToken == claimToken)
            .Select(message => new OutboxMessageToPublish(message.Id, message.Type, message.Payload))
            .SingleAsync(cancellationToken);
    }

    public async Task<bool> MarkPublishedAsync(
        Guid messageId,
        Guid claimToken,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken = default)
    {
        var rowsAffected = await dbContext.OutboxMessages
            .Where(message => message.Id == messageId
                && message.PublishedAt == null
                && message.ClaimToken == claimToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.PublishedAt, publishedAt)
                .SetProperty(message => message.ClaimToken, (Guid?)null)
                .SetProperty(message => message.ClaimExpiresAt, (DateTimeOffset?)null), cancellationToken);

        return rowsAffected == 1;
    }
}
