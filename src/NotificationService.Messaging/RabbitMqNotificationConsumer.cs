using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notification.Contracts;
using NotificationService.Shell;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Messaging;

public sealed class RabbitMqNotificationConsumer(
    IConnectionFactory connectionFactory,
    RabbitMqNotificationOptions options,
    ProcessNotificationShell processNotificationShell,
    ILogger<RabbitMqNotificationConsumer> logger) : IDisposable
{
    public const string ExchangeName = "notifications";
    public const string QueueName = "notification-service.email.v1";
    public const string RoutingKey = NotificationRequestedV1.Type;

    private IConnection? connection;
    private IModel? channel;

    public void Start(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        connection = connectionFactory.CreateConnection();
        channel = connection.CreateModel();
        channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, durable: true);
        channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(QueueName, ExchangeName, RoutingKey);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, eventArgs) => await HandleDeliveryAsync(
            channel,
            eventArgs.DeliveryTag,
            eventArgs.Body,
            cancellationToken);
        channel.BasicConsume(QueueName, autoAck: false, consumer);
    }

    public async Task HandleDeliveryAsync(
        IModel deliveryChannel,
        ulong deliveryTag,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<NotificationRequestedV1>(body.Span)
                ?? throw new InvalidNotificationRequestedException("Notification payload is required.");
            await processNotificationShell.ExecuteAsync(notification, cancellationToken);
            deliveryChannel.BasicAck(deliveryTag, multiple: false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Notification delivery failed and was not acknowledged.");
        }
    }

    public void Dispose()
    {
        channel?.Dispose();
        connection?.Dispose();
    }
}
