using System.Collections.Concurrent;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.LiveIntegration;

internal sealed class OutboxEvidence
{
    public ConcurrentDictionary<Guid, bool> Confirmed { get; } = new();
    public ConcurrentDictionary<Guid, bool> Failed { get; } = new();
}

internal sealed class ObservedOutboxPublisher(IOutboxMessagePublisher inner, OutboxEvidence evidence) : IOutboxMessagePublisher
{
    public async Task PublishAsync(OutboxMessageToPublish message, CancellationToken cancellationToken = default)
    {
        try
        {
            await inner.PublishAsync(message, cancellationToken);
            evidence.Confirmed[message.Id] = true;
        }
        catch
        {
            evidence.Failed[message.Id] = true;
            throw;
        }
    }
}
