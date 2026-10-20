using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.AiAnalysis;

public sealed class AnalyzeRequestShell(
    ITicketRepository ticketRepository,
    ITicketAiAnalyzer analyzer)
{
    public async Task<TicketAiAnalysisResult> ExecuteAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ticket = await ticketRepository.GetByIdAsync(
            ticketId,
            cancellationToken);

        if (ticket is null)
        {
            throw new InvalidOperationException(
                "The ticket requested for AI analysis was not found.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var input = new TicketAiAnalysisInput(
            ticket.Id,
            ticket.Title,
            ticket.Description);

        var result = await analyzer.AnalyzeAsync(
            input,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        ValidateTicketAiAnalysisCore.Execute(result);

        return result;
    }
}