namespace ServiceDesk.Shell.Notifications;

public interface IRequestProgressPublisher
{
    Task PublishAsync(
        OutboxMessageToPublish message,
        CancellationToken cancellationToken = default);
}
