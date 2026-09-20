using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Customers;

public sealed class GetCustomerTickets(ICustomerRepository customerRepository, ITicketRepository ticketRepository)
{
    public async Task<PagedResult<TicketSummaryDto>> ExecuteAsync(Guid customerId, int page, int pageSize, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Validation.RequirePagination(page, pageSize);
        if (actor.ActorType != ActorType.Administrator && actor.ActorType != ActorType.Employee && (actor.ActorType != ActorType.Customer || actor.CustomerId != customerId)) throw new ForbiddenException();
        _ = await customerRepository.GetByIdAsync(customerId, cancellationToken) ?? throw new NotFoundException(nameof(Customer), customerId);
        return await ticketRepository.SearchAsync(new TicketSearchRequest(customerId, null, null, null, null, page, pageSize), cancellationToken);
    }
}
