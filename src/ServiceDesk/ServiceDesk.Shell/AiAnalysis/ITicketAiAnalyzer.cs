using ServiceDesk.Core.AiAnalysis;

namespace ServiceDesk.Shell.AiAnalysis;

public interface ITicketAiAnalyzer
{
    Task<TicketAiAnalysisResult> AnalyzeAsync(
        TicketAiAnalysisInput input,
        CancellationToken cancellationToken);
}