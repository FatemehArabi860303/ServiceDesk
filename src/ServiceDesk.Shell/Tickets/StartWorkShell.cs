using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Tickets;

public sealed class StartWorkShell(ITicketRepository ticketRepository)
{
    public async Task ExecuteAsync(
        Guid ticketId,
        Guid employeeUserId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        var workStartedHistoryId = Guid.NewGuid();
        StartWorkCore.Execute(ticket, employeeUserId, workStartedHistoryId, DateTimeOffset.UtcNow);

        var started = await ticketRepository.TryStartWorkAsync(ticket!, workStartedHistoryId, cancellationToken);
        if (!started)
        {
            throw new StartWorkException(StartWorkFailureKind.TicketNoLongerEligible);
        }
    }
}
