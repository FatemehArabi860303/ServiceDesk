using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class ReopenTicket(ITicketRepository ticketRepository)
{
    public async Task<TicketDto> ExecuteAsync(Guid ticketId, TicketMutationRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        Validation.RequireConcurrencyToken(request.ConcurrencyToken);
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        loaded.Ticket.Reopen(actor.ActorReference);
        var persisted = await Validation.UpdateAndReloadAsync(ticketRepository, loaded.Ticket, request.ConcurrencyToken, cancellationToken);
        return DtoMapper.ToDto(persisted.Ticket, persisted.ConcurrencyToken);
    }
}
