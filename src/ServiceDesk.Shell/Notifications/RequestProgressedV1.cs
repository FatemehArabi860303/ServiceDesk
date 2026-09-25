using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Notifications;

public sealed record RequestProgressedV1(
    Guid EventId,
    Guid TicketId,
    string CustomerEmail,
    RequestProgressKind ProgressKind,
    DateTimeOffset OccurredAt,
    int SchemaVersion = 1)
{
    public const string Type = "servicedesk.request-progressed.v1";
}
