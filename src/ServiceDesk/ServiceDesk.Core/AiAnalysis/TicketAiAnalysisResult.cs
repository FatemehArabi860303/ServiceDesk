using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Core.AiAnalysis;

public sealed record TicketAiAnalysisResult(
    string Summary,
    string SuggestedCategory,
    TicketPriority SuggestedPriority,
    string PriorityReason,
    IReadOnlyList<string> TroubleshootingSuggestions,
    IReadOnlyList<string> MissingInformation,
    TicketAiAnalysisConfidence Confidence);