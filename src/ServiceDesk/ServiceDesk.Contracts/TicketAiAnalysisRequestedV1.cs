namespace ServiceDesk.Contracts;

public sealed record TicketAiAnalysisRequestedV1(
    Guid Id,
    Guid TicketId,
    DateTimeOffset OccurredAt)
{
    public const string Type = "ticket.ai-analysis.requested.v1";
}