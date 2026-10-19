using System.Collections.Concurrent;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.LiveIntegration;

// This type exists only in the test executable. Production projects never reference it.
internal sealed class ControlledAnalyzer : ITicketAiAnalyzer
{
    public static TicketAiAnalysisResult Result { get; } = new(
        "Deterministic verification analysis", "Verification", TicketPriority.Low,
        "A controlled test result", ["Inspect the test correlation ID"],
        ["No additional information required"], TicketAiAnalysisConfidence.High);

    public ConcurrentDictionary<Guid, int> Invocations { get; } = new();
    public string Mode { get; set; } = "Success";
    public TaskCompletionSource Entered { get; private set; } = Signal();
    public TaskCompletionSource Cancelled { get; private set; } = Signal();

    public void Reset(string mode)
    {
        Mode = mode;
        Entered = Signal();
        Cancelled = Signal();
    }

    public async Task<TicketAiAnalysisResult> AnalyzeAsync(TicketAiAnalysisInput input, CancellationToken cancellationToken = default)
    {
        Invocations.AddOrUpdate(input.TicketId, 1, (_, count) => count + 1);
        Entered.TrySetResult();
        if (Mode == "Fail") { throw new InvalidOperationException("Controlled analyzer failure"); }
        if (Mode == "Hold")
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Result;
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
