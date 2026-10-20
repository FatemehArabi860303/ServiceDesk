using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Tickets;

public sealed record TicketListItem(
    Guid Id,
    string CustomerEmail,
    string? AssignedEmployeeEmail,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
