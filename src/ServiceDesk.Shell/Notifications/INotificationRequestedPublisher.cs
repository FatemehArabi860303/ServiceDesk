namespace ServiceDesk.Shell.Notifications;

public interface INotificationRequestedPublisher
{
    Task PublishAsync(
        OutboxMessageToPublish message,
        CancellationToken cancellationToken = default);
}
