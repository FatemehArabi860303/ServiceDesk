namespace ServiceDesk.Core.AiAnalysis;

public static class ValidateTicketAiAnalysisCore
{
    public static void Execute(TicketAiAnalysisResult? result)
    {
        if (result is null)
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.MissingResult);
        }

        if (string.IsNullOrWhiteSpace(result.Summary))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.MissingSummary);
        }

        if (string.IsNullOrWhiteSpace(result.SuggestedCategory))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.MissingSuggestedCategory);
        }

        if (!Enum.IsDefined(result.SuggestedPriority))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.InvalidSuggestedPriority);
        }

        if (string.IsNullOrWhiteSpace(result.PriorityReason))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.MissingPriorityReason);
        }

        if (result.TroubleshootingSuggestions is null)
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.MissingTroubleshootingSuggestions);
        }

        if (result.TroubleshootingSuggestions.Count > 2)
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.TooManyTroubleshootingSuggestions);
        }

        if (result.TroubleshootingSuggestions.Any(string.IsNullOrWhiteSpace))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.InvalidTroubleshootingSuggestion);
        }

        if (result.MissingInformation is null)
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.MissingInformationList);
        }

        if (result.MissingInformation.Any(string.IsNullOrWhiteSpace))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.InvalidMissingInformationEntry);
        }

        if (!Enum.IsDefined(result.Confidence))
        {
            throw new TicketAiAnalysisValidationException(
                TicketAiAnalysisValidationFailureKind.InvalidConfidence);
        }
    }
}