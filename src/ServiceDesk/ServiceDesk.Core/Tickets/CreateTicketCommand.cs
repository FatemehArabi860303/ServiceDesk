namespace ServiceDesk.Core.Tickets;

public sealed record CreateTicketCommand(
    string? Title,
    string? Description,
    TicketPriority Priority);
