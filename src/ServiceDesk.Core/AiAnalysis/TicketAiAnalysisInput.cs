    namespace ServiceDesk.Core.AiAnalysis;

public sealed record TicketAiAnalysisInput(
    Guid TicketId,
    string Title,
    string Description);