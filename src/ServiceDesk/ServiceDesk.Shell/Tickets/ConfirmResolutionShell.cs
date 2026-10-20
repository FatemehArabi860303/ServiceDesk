using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Tickets;

public sealed class ConfirmResolutionShell(ITicketRepository ticketRepository)
{
    public async Task ExecuteAsync(Guid ticketId, Guid actorUserId, UserRole role, CancellationToken cancellationToken = default)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, cancellationToken);
        var historyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        ConfirmResolutionCore.Execute(ticket, actorUserId, role, historyId, now);

        if (!await ticketRepository.TryConfirmResolutionAsync(ticket!, historyId, cancellationToken))
        {
            throw new ConfirmResolutionException(ConfirmResolutionFailureKind.TicketNoLongerEligible);
        }
    }
}
