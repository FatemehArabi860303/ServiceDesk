namespace ServiceDesk.Shell.Notifications;

public interface IOutboxMessagePublisher
{
    Task PublishAsync(
        OutboxMessageToPublish message,
        CancellationToken cancellationToken = default);
}