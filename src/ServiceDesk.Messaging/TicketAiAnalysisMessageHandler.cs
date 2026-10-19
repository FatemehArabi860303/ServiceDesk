using System.Text.Json;
using RabbitMq.Consumers;
using ServiceDesk.Contracts;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.Messaging;

public sealed class TicketAiAnalysisMessageHandler(ProcessTicketAiAnalysisShell shell) : IMessageHandler
{
    public static RabbitMqConsumerOptions ConsumerOptions { get; } = new(
        RabbitMqOutboxRoute.AiAnalysis.ExchangeName,
        RabbitMqOutboxRoute.AiAnalysis.QueueName,
        RabbitMqOutboxRoute.AiAnalysis.RoutingKey);

    public async Task HandleAsync(MessageContext message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (message.Type != TicketAiAnalysisRequestedV1.Type
            || message.ContentType != "application/json"
            || message.Exchange != ConsumerOptions.Exchange
            || message.RoutingKey != ConsumerOptions.RoutingKey)
        {
            throw new InvalidDataException("Invalid AI analysis message envelope.");
        }

        var request = JsonSerializer.Deserialize<TicketAiAnalysisRequestedV1>(message.Body.Span)
            ?? throw new InvalidDataException("AI analysis payload is required.");
        if (request.Id == Guid.Empty || request.TicketId == Guid.Empty
            || request.OccurredAt == default
            || !Guid.TryParse(message.MessageId, out var messageId) || messageId != request.Id)
        {
            throw new InvalidDataException("Invalid AI analysis event identity.");
        }

        // ExecuteAsync completes only after persistence commits (or confirms this event completed).
        await shell.ExecuteAsync(request.Id, request.TicketId, cancellationToken);
    }
}
