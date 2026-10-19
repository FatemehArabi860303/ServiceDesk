using RabbitMq.Consumers;
using ServiceDesk.Messaging;

namespace ServiceDesk.AiWorker;

public sealed class AiAnalysisConsumerWorker(RabbitMqConsumer<TicketAiAnalysisMessageHandler> consumer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => consumer.RunAsync(stoppingToken);

    // Scoped dependencies must outlive handler cleanup, even after the host deadline expires.
    public override Task StopAsync(CancellationToken cancellationToken) => base.StopAsync(CancellationToken.None);
}
