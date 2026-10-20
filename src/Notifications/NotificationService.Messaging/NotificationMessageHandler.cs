using System.Text.Json;
using Notification.Contracts;
using NotificationService.Shell;
using RabbitMq.Consumers;

namespace NotificationService.Messaging;

public sealed class NotificationMessageHandler(ProcessNotificationShell shell) : IMessageHandler
{
    public static RabbitMqConsumerOptions ConsumerOptions { get; } = new(
        "notifications", "notification-service.email.v1", NotificationRequestedV1.Type)
    {
        PrefetchCount = 0,
        StopOnHandlerFailure = false
    };

    public async Task HandleAsync(MessageContext message, CancellationToken cancellationToken)
    {
        var notification = JsonSerializer.Deserialize<NotificationRequestedV1>(message.Body.Span)
            ?? throw new InvalidNotificationRequestedException("Notification payload is required.");
        await shell.ExecuteAsync(notification, cancellationToken);
    }
}
