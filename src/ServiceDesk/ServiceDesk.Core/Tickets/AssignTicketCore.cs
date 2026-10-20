namespace ServiceDesk.Core.Tickets;

public static class AssignTicketCore
{
    public static RequestProgressed Execute(
        Ticket? ticket,
        Guid employeeUserId,
        Guid assignmentHistoryId,
        DateTimeOffset now)
    {
        if (ticket is null)
        {
            throw new AssignTicketException(AssignTicketFailureKind.TicketNotFound);
        }

        ticket.Assign(employeeUserId, assignmentHistoryId, now);
        return new RequestProgressed(ticket.Id, ticket.CustomerUserId, RequestProgressKind.Assigned, now);
    }
}
