using System.Text;
using Notification.Contracts;
using RabbitMQ.Client;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging;

public sealed class RabbitMqNotificationRequestedPublisher(IConnectionFactory connectionFactory) : INotificationRequestedPublisher
{
    public const string ExchangeName = "notifications";
    public const string RoutingKey = NotificationRequestedV1.Type;

    public Task PublishAsync(OutboxMessageToPublish message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
        channel.ConfirmSelect();

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = message.Type;
        properties.MessageId = message.Id.ToString();

        channel.BasicPublish(
            ExchangeName,
            RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload));
        channel.WaitForConfirmsOrDie();

        return Task.CompletedTask;
    }
}
