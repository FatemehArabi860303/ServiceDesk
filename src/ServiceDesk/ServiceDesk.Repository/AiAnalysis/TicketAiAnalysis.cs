namespace ServiceDesk.Repository.AiAnalysis;

public sealed class TicketAiAnalysis
{
    public TicketAiAnalysis(
        Guid eventId,
        Guid ticketId,
        string resultJson,
        DateTimeOffset completedAt)
    {
        EventId = eventId;
        TicketId = ticketId;
        ResultJson = resultJson;
        CompletedAt = completedAt;
    }

    public Guid EventId { get; private set; }

    public Guid TicketId { get; private set; }

    public string ResultJson { get; private set; } = null!;

    public DateTimeOffset CompletedAt { get; private set; }
}