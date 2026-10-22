using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tickets;

public static class EmployeeWorkCore
{
    public static bool IsAuthorized(User? employee) =>
        employee is { IsActive: true, Role: UserRole.Employee };

    public static bool CanStart(Ticket ticket, Guid employeeId) =>
        CanStart(ticket.Status, ticket.AssignedEmployeeUserId, employeeId);

    public static bool CanStart(TicketStatus status, Guid? assignee, Guid employeeId) =>
        status == TicketStatus.Open && (assignee is null || assignee == employeeId);

    // Compose the existing domain operations without introducing new lifecycle rules.
    public static IReadOnlyList<RequestProgressed> Execute(
        Ticket? ticket, Guid employeeId, Guid assignmentHistoryId,
        Guid startedHistoryId, DateTimeOffset now)
    {
        if (ticket is null) throw new StartWorkException(StartWorkFailureKind.TicketNotFound);
        List<RequestProgressed> progressed = [];
        if (ticket.AssignedEmployeeUserId is null)
            progressed.Add(AssignTicketCore.Execute(ticket, employeeId, assignmentHistoryId, now));
        progressed.Add(StartWorkCore.Execute(ticket, employeeId, startedHistoryId, now));
        return progressed;
    }
}
