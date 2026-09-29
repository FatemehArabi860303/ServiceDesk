using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Notifications;

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
        var now = DateTimeOffset.UtcNow;
        var progressed = StartWorkCore.Execute(ticket, employeeUserId, workStartedHistoryId, now);
        var notification = new TicketProgressNotification(Guid.NewGuid(), progressed, now);

        var started = await ticketRepository.TryStartWorkAsync(ticket!, workStartedHistoryId, notification, cancellationToken);
        if (!started)
        {
            throw new StartWorkException(StartWorkFailureKind.TicketNoLongerEligible);
        }
    }
}
