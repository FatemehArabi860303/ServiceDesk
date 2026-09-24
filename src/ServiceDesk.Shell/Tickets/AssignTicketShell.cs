using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Tickets;

public sealed class AssignTicketShell(ITicketRepository ticketRepository)
{
    public async Task ExecuteAsync(
        Guid ticketId,
        Guid employeeUserId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        var assignmentHistoryId = Guid.NewGuid();
        AssignTicketCore.Execute(ticket, employeeUserId, assignmentHistoryId, DateTimeOffset.UtcNow);

        var assigned = await ticketRepository.TryAssignAsync(ticket!, assignmentHistoryId, cancellationToken);
        if (!assigned)
        {
            throw new AssignTicketException(AssignTicketFailureKind.TicketNoLongerAvailable);
        }
    }
}
