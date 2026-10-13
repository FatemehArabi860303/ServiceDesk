namespace ServiceDesk.Shell.AiAnalysis;

public interface ITicketSummaryAnalyzer
{
    Task<string> SummarizeAsync(
        string title,
        string description,
        CancellationToken cancellationToken);
}