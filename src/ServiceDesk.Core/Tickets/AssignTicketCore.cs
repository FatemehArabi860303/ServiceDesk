namespace ServiceDesk.Core.Tickets;

public static class AssignTicketCore
{
    public static void Execute(
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
    }
}
