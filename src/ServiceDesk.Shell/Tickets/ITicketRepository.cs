using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Shell.Tickets;

public interface ITicketRepository
{
    Task<bool> TryResolveAsync(
        Ticket ticket,
        Guid historyId,
        TicketProgressNotification notification,
        CancellationToken cancellationToken = default);

    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketListItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> TryAssignAsync(
        Ticket ticket,
        Guid assignmentHistoryId,
        TicketProgressNotification notification,
        CancellationToken cancellationToken = default);

    Task<bool> TryStartWorkAsync(
        Ticket ticket,
        Guid workStartedHistoryId,
        TicketProgressNotification notification,
        CancellationToken cancellationToken = default);
}
