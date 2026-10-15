using Notification.Contracts;
using RabbitMQ.Client;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging;

public sealed class RabbitMqNotificationRequestedPublisher(
    IConnectionFactory connectionFactory) : INotificationRequestedPublisher
{
    public const string ExchangeName = "notifications";
    public const string RoutingKey = NotificationRequestedV1.Type;

    private readonly RabbitMqOutboxMessagePublisher publisher = new(
        connectionFactory,
        new RabbitMqOutboxRouting(
            [RabbitMqOutboxRoute.Notification]));

    public Task PublishAsync(
        OutboxMessageToPublish message,
        CancellationToken cancellationToken = default) =>
        publisher.PublishAsync(message, cancellationToken);
}