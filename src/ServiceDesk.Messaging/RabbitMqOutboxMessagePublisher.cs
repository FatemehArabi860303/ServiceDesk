using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging;

public sealed class RabbitMqOutboxMessagePublisher(
    IConnectionFactory connectionFactory,
    RabbitMqOutboxRouting routing) : IOutboxMessagePublisher
{
    public static readonly TimeSpan ConfirmationTimeout =
        TimeSpan.FromSeconds(10);

    public Task PublishAsync(
        OutboxMessageToPublish message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var route = routing.Resolve(message.Type);
        var body = Encoding.UTF8.GetBytes(message.Payload);

        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            route.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(
            route.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueBind(
            route.QueueName,
            route.ExchangeName,
            route.RoutingKey,
            arguments: null);

        channel.ConfirmSelect();

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = message.Type;
        properties.MessageId = message.Id.ToString();

        var returned = 0;

        EventHandler<BasicReturnEventArgs> returnHandler = (_, _) =>
        {
            Interlocked.Exchange(ref returned, 1);
        };

        channel.BasicReturn += returnHandler;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            channel.BasicPublish(
                route.ExchangeName,
                route.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body);

            channel.WaitForConfirmsOrDie(ConfirmationTimeout);

            if (Volatile.Read(ref returned) != 0)
            {
                throw new IOException(
                    $"RabbitMQ returned Outbox message '{message.Id}' " +
                    $"from exchange '{route.ExchangeName}' " +
                    $"with routing key '{route.RoutingKey}'.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
        finally
        {
            channel.BasicReturn -= returnHandler;
        }
    }
}