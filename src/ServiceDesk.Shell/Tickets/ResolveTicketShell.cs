using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Shell.Tickets;

public sealed class ResolveTicketShell(ITicketRepository ticketRepository)
{
    public async Task ExecuteAsync(Guid ticketId, Guid actorUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        var historyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var progressed = ResolveTicketCore.Execute(ticket, actorUserId, role, historyId, now);
        var notification = new TicketProgressNotification(Guid.NewGuid(), progressed, now);
        if (!await ticketRepository.TryResolveAsync(ticket!, historyId, notification, cancellationToken))
        {
            throw new ResolveTicketException(ResolveTicketFailureKind.TicketNoLongerEligible);
        }
    }
}
