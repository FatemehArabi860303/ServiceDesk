using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class AddTicketComment(ITicketRepository ticketRepository)
{
    public async Task<TicketHistoryDto> ExecuteAsync(Guid ticketId, AddTicketCommentRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Validation.RequireConcurrencyToken(request.ConcurrencyToken);
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        Authorization.RequireTicketParticipantOrAdministrator(loaded.Ticket, actor);
        loaded.Ticket.AddComment(request.Comment, actor.ActorReference);
        var commentHistoryId = loaded.Ticket.History[^1].Id;
        var persisted = await Validation.UpdateAndReloadAsync(ticketRepository, loaded.Ticket, request.ConcurrencyToken, cancellationToken);
        var commentHistory = persisted.Ticket.History.Single(history => history.Id == commentHistoryId);
        return DtoMapper.ToDto(commentHistory);
    }
}
