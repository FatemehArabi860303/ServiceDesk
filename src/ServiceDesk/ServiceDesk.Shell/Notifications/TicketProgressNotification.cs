using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Notifications;

public sealed record TicketProgressNotification(
    Guid EventId,
    RequestProgressed Progressed,
    DateTimeOffset CreatedAt);
