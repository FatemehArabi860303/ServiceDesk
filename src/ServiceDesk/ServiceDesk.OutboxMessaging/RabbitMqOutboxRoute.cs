using Notification.Contracts;
using ServiceDesk.Contracts;

namespace ServiceDesk.Messaging;

public sealed record RabbitMqOutboxRoute(
    string EventType,
    string ExchangeName,
    string QueueName,
    string RoutingKey)
{
    public static RabbitMqOutboxRoute Notification { get; } = new(
        NotificationRequestedV1.Type,
        "notifications",
        "notification-service.email.v1",
        NotificationRequestedV1.Type);

    public static RabbitMqOutboxRoute AiAnalysis { get; } = new(
        TicketAiAnalysisRequestedV1.Type,
        "servicedesk.ai-analysis",
        "servicedesk.ai-analysis.v1",
        TicketAiAnalysisRequestedV1.Type);
}