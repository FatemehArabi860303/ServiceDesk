using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Repository.Tickets;

public sealed class TicketRepository(ServiceDeskDbContext dbContext) : ITicketRepository
{
    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
