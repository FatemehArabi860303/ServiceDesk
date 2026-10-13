namespace ServiceDesk.Core.AiAnalysis;

public enum TicketAiAnalysisValidationFailureKind
{
    MissingResult,
    MissingSummary,
    MissingSuggestedCategory,
    InvalidSuggestedPriority,
    MissingPriorityReason,
    MissingTroubleshootingSuggestions,
    TooManyTroubleshootingSuggestions,
    InvalidTroubleshootingSuggestion,
    MissingInformationList,
    InvalidMissingInformationEntry,
    InvalidConfidence
}