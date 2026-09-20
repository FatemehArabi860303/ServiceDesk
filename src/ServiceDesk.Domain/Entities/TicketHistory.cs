using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Domain.Entities;

public sealed class TicketHistory
{
    internal TicketHistory(
        Guid ticketId,
        TicketHistoryAction action,
        string actorReference,
        DateTimeOffset createdAt,
        string? description = null,
        string? previousValue = null,
        string? newValue = null)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        Action = action;
        ActorReference = actorReference;
        CreatedAt = createdAt;
        Description = description;
        PreviousValue = previousValue;
        NewValue = newValue;
    }

    public Guid Id { get; }
    public Guid TicketId { get; }
    public TicketHistoryAction Action { get; }
    public string ActorReference { get; }
    public string? Description { get; }
    public string? PreviousValue { get; }
    public string? NewValue { get; }
    public DateTimeOffset CreatedAt { get; }
}
