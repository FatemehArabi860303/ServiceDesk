using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class RejectResolution(ITicketRepository ticketRepository)
{
    public async Task<TicketDto> ExecuteAsync(Guid ticketId, TicketMutationRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Validation.RequireConcurrencyToken(request.ConcurrencyToken);
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        Authorization.RequireCustomerOwnerOrAdministrator(loaded.Ticket, actor);
        loaded.Ticket.RejectResolution(actor.ActorReference);
        var persisted = await Validation.UpdateAndReloadAsync(ticketRepository, loaded.Ticket, request.ConcurrencyToken, cancellationToken);
        return DtoMapper.ToDto(persisted.Ticket, persisted.ConcurrencyToken);
    }
}
