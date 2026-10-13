namespace ServiceDesk.Core.AiAnalysis;

public sealed class TicketAiAnalysisValidationException(
    TicketAiAnalysisValidationFailureKind failure) : Exception
{
    public TicketAiAnalysisValidationFailureKind Failure { get; } = failure;
}