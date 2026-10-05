using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tickets;

public static class ResolveTicketCore
{
    public static RequestProgressed Execute(Ticket? ticket, Guid actorUserId, UserRole role, Guid historyId, DateTimeOffset now)
    {
        if (ticket is null)
        {
            throw new ResolveTicketException(ResolveTicketFailureKind.TicketNotFound);
        }

        ticket.Resolve(actorUserId, role, historyId, now);
        return new RequestProgressed(ticket.Id, ticket.CustomerUserId, RequestProgressKind.Resolved, now);
    }
}
