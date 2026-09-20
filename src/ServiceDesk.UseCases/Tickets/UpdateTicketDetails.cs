using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class UpdateTicketDetails(ITicketRepository ticketRepository)
{
    public async Task<TicketDto> ExecuteAsync(Guid ticketId, UpdateTicketDetailsRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (request.Title is null && request.Description is null) throw new ArgumentException("At least one ticket detail must be supplied.", nameof(request));
        Validation.RequireConcurrencyToken(request.ConcurrencyToken);
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        Authorization.RequireCustomerOwnerOrAdministrator(loaded.Ticket, actor);
        if (request.Title is not null) loaded.Ticket.ChangeTitle(request.Title, actor.ActorReference);
        if (request.Description is not null) loaded.Ticket.ChangeDescription(request.Description, actor.ActorReference);
        var persisted = await Validation.UpdateAndReloadAsync(ticketRepository, loaded.Ticket, request.ConcurrencyToken, cancellationToken);
        return DtoMapper.ToDto(persisted.Ticket, persisted.ConcurrencyToken);
    }
}
