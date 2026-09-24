using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Tickets;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);

    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketListItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> TryAssignAsync(
        Ticket ticket,
        Guid assignmentHistoryId,
        CancellationToken cancellationToken = default);

    Task<bool> TryStartWorkAsync(
        Ticket ticket,
        Guid workStartedHistoryId,
        CancellationToken cancellationToken = default);
}
