using Microsoft.Extensions.Hosting;
using NotificationService.Messaging;
using RabbitMq.Consumers;

namespace NotificationService.Worker;

public sealed class NotificationConsumerWorker(RabbitMqConsumer<NotificationMessageHandler> consumer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => consumer.RunAsync(stoppingToken);

    // Scoped dependencies must outlive handler cleanup, even after the host deadline expires.
    public override Task StopAsync(CancellationToken cancellationToken) => base.StopAsync(CancellationToken.None);
}
