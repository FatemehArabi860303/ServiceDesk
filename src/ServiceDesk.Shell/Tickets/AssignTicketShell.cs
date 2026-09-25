using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Notifications;

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
        var now = DateTimeOffset.UtcNow;
        var progressed = AssignTicketCore.Execute(ticket, employeeUserId, assignmentHistoryId, now);
        var notification = new TicketProgressNotification(Guid.NewGuid(), progressed, now);

        var assigned = await ticketRepository.TryAssignAsync(ticket!, assignmentHistoryId, notification, cancellationToken);
        if (!assigned)
        {
            throw new AssignTicketException(AssignTicketFailureKind.TicketNoLongerAvailable);
        }
    }
}
