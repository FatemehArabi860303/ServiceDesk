using ServiceDesk.AI.AiAnalysis;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.LiveIntegration;

internal sealed class RealAnalyzerEvidence
{
    public int Attempts;
    public int Successes;
    public bool Unavailable;
    public int? HttpStatus;
}

// Test-only observation of the unchanged provider adapter; no retry or fabricated result.
internal sealed class ObservedRealAnalyzer(OpenAiTicketAnalyzer inner, RealAnalyzerEvidence evidence) : ITicketAiAnalyzer
{
    public async Task<TicketAiAnalysisResult> AnalyzeAsync(TicketAiAnalysisInput input, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref evidence.Attempts) != 1)
            throw new InvalidOperationException("Only one real provider request is allowed per verification run.");
        try
        {
            var result = await inner.AnalyzeAsync(input, cancellationToken);
            Interlocked.Increment(ref evidence.Successes);
            return result;
        }
        catch (HttpRequestException exception)
        {
            evidence.Unavailable = true;
            evidence.HttpStatus = (int?)exception.StatusCode;
            throw;
        }
        catch (OperationCanceledException)
        {
            evidence.Unavailable = true;
            throw;
        }
    }
}
