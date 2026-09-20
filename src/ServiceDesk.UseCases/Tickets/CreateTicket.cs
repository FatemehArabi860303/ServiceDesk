using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class CreateTicket(ICustomerRepository customerRepository, ITicketRepository ticketRepository)
{
    public async Task<TicketDto> ExecuteAsync(CreateTicketRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        if (actor.ActorType != ActorType.Administrator && (actor.ActorType != ActorType.Customer || actor.CustomerId != request.CustomerId)) throw new ForbiddenException();
        _ = await customerRepository.GetByIdAsync(request.CustomerId, cancellationToken) ?? throw new NotFoundException(nameof(Customer), request.CustomerId);
        var ticket = Ticket.Create(request.CustomerId, request.Title, request.Description, request.Priority, actor.ActorReference);
        var persisted = await Validation.AddAndReloadAsync(ticketRepository, ticket, cancellationToken);
        return DtoMapper.ToDto(persisted.Ticket, persisted.ConcurrencyToken);
    }
}
