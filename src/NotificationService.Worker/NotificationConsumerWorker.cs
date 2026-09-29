using Microsoft.Extensions.Hosting;
using NotificationService.Messaging;

namespace NotificationService.Worker;

public sealed class NotificationConsumerWorker(RabbitMqNotificationConsumer consumer) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Start(stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        consumer.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
