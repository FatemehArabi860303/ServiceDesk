using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class GetTicket(ITicketRepository ticketRepository)
{
    public async Task<TicketDto> ExecuteAsync(Guid ticketId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        Authorization.RequireTicketAccess(loaded.Ticket, actor);
        return DtoMapper.ToDto(loaded.Ticket, loaded.ConcurrencyToken);
    }
}
