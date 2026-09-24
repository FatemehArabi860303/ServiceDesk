namespace ServiceDesk.Core.Tickets;

public static class StartWorkCore
{
    public static void Execute(
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
    }
}
