using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tickets;

public static class ConfirmResolutionCore
{
    public static void Execute(Ticket? ticket, Guid actorUserId, UserRole role, Guid historyId, DateTimeOffset now)
    {
        if (ticket is null)
        {
            throw new ConfirmResolutionException(ConfirmResolutionFailureKind.TicketNotFound);
        }

        ticket.ConfirmResolution(actorUserId, role, historyId, now);
    }
}
