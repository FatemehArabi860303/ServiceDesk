using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Shell.Tickets;

public sealed class GetAllTicketsShell(ITicketRepository ticketRepository)
{
    public Task<IReadOnlyList<TicketListItem>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        ticketRepository.GetAllAsync(cancellationToken);
}
