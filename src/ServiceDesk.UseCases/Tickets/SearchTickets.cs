using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class SearchTickets(ITicketRepository ticketRepository)
{
    public Task<PagedResult<TicketSummaryDto>> ExecuteAsync(TicketSearchRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Validation.RequirePagination(request.Page, request.PageSize);
        var scopedRequest = actor.ActorType switch
        {
            ActorType.Administrator => request,
            ActorType.Customer when actor.CustomerId is not null => request with { CustomerId = actor.CustomerId },
            ActorType.Employee when actor.EmployeeId is not null => request with { AssignedEmployeeId = actor.EmployeeId },
            _ => throw new ForbiddenException()
        };
        return ticketRepository.SearchAsync(scopedRequest, cancellationToken);
    }
}
