namespace ServiceDesk.Shell.AiAnalysis;

public sealed class ProcessTicketAiAnalysisShell(
    ITicketAiAnalysisRepository analysisRepository,
    AnalyzeRequestShell analyzeRequestShell)
{
    public async Task ExecuteAsync(
        Guid eventId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException(
                "An event ID is required.",
                nameof(eventId));
        }

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException(
                "A ticket ID is required.",
                nameof(ticketId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (await analysisRepository.IsCompletedAsync(
                eventId,
                ticketId,
                cancellationToken))
        {
            return;
        }

        var result = await analyzeRequestShell.ExecuteAsync(
            ticketId,
            cancellationToken);

        await analysisRepository.SaveAsync(
            eventId,
            ticketId,
            result,
            cancellationToken);
    }
}