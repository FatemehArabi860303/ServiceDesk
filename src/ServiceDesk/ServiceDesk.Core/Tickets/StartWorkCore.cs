namespace ServiceDesk.Core.Tickets;

public static class StartWorkCore
{
    public static RequestProgressed Execute(
        Ticket? ticket,
        Guid employeeUserId,
        Guid workStartedHistoryId,
        DateTimeOffset now)
    {
        if (ticket is null)
        {
            throw new StartWorkException(StartWorkFailureKind.TicketNotFound);
        }

        ticket.StartWork(employeeUserId, workStartedHistoryId, now);
        return new RequestProgressed(ticket.Id, ticket.CustomerUserId, RequestProgressKind.WorkStarted, now);
    }
}
