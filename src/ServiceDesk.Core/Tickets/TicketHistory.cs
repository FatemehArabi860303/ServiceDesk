namespace ServiceDesk.Core.Tickets;

public sealed record TicketHistory(
    Guid Id,
    Guid TicketId,
    Guid ActorUserId,
    TicketHistoryAction Action,
    DateTimeOffset OccurredAt,
    Guid? AssignedEmployeeUserId = null);
