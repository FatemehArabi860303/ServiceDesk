using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class ChangeTicketPriority(ITicketRepository ticketRepository)
{
    public async Task<TicketDto> ExecuteAsync(Guid ticketId, ChangeTicketPriorityRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Validation.RequireConcurrencyToken(request.ConcurrencyToken);
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        Authorization.RequireAssignedEmployeeOrAdministrator(loaded.Ticket, actor);
        loaded.Ticket.ChangePriority(request.Priority, actor.ActorReference);
        var persisted = await Validation.UpdateAndReloadAsync(ticketRepository, loaded.Ticket, request.ConcurrencyToken, cancellationToken);
        return DtoMapper.ToDto(persisted.Ticket, persisted.ConcurrencyToken);
    }
}
