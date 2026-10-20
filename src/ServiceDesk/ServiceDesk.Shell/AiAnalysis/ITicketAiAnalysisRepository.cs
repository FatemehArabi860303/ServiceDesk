using ServiceDesk.Core.AiAnalysis;

namespace ServiceDesk.Shell.AiAnalysis;

public interface ITicketAiAnalysisRepository
{
    Task<bool> IsCompletedAsync(
        Guid eventId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Guid eventId,
        Guid ticketId,
        TicketAiAnalysisResult result,
        CancellationToken cancellationToken = default);
}