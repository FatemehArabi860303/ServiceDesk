using System.Text;
using RabbitMq.Publishers;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging;

public sealed class RabbitMqOutboxMessagePublisher(
    IRabbitMqPublisher publisher,
    RabbitMqOutboxRouting routing) : IOutboxMessagePublisher
{
    public Task PublishAsync(
        OutboxMessageToPublish message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        var route = routing.Resolve(message.Type);
        var destination = new RabbitMqPublishDestination(route.ExchangeName, route.RoutingKey)
        {
            Queue = route.QueueName
        };
        var publication = new PublishMessage(message.Id.ToString(), message.Type,
            Encoding.UTF8.GetBytes(message.Payload), "application/json");
        return publisher.PublishAsync(publication, destination, cancellationToken);
    }
}
