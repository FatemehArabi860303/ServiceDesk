using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging;

public sealed class RabbitMqRequestProgressPublisher(IConnectionFactory connectionFactory) : IRequestProgressPublisher
{
    public const string ExchangeName = "servicedesk.request-progress";

    public Task PublishAsync(OutboxMessageToPublish message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var integrationEvent = JsonSerializer.Deserialize<RequestProgressedV1>(message.Payload)
            ?? throw new InvalidOperationException("Outbox message payload is invalid.");
        var routingKey = GetRoutingKey(integrationEvent.ProgressKind);

        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
        channel.ConfirmSelect();

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = message.Type;
        properties.MessageId = message.Id.ToString();

        channel.BasicPublish(
            ExchangeName,
            routingKey,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload));
        channel.WaitForConfirmsOrDie();

        return Task.CompletedTask;
    }

    public static string GetRoutingKey(RequestProgressKind kind) => kind switch
    {
        RequestProgressKind.Assigned => "request.progress.assigned",
        RequestProgressKind.WorkStarted => "request.progress.work-started",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported request progress kind.")
    };
}
