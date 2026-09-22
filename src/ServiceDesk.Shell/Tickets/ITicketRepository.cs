using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Tickets;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
