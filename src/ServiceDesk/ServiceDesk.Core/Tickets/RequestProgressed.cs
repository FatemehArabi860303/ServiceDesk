namespace ServiceDesk.Core.Tickets;

public sealed record RequestProgressed(
    Guid TicketId,
    Guid CustomerUserId,
    RequestProgressKind Kind,
    DateTimeOffset OccurredAt);
