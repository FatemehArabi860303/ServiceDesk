using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Employees;

public sealed class GetAssignedTickets(IEmployeeRepository employeeRepository, ITicketRepository ticketRepository)
{
    public async Task<PagedResult<TicketSummaryDto>> ExecuteAsync(Guid employeeId, int page, int pageSize, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Validation.RequirePagination(page, pageSize);
        if (actor.ActorType != ActorType.Administrator && (actor.ActorType != ActorType.Employee || actor.EmployeeId != employeeId)) throw new ForbiddenException();
        _ = await employeeRepository.GetByIdAsync(employeeId, cancellationToken) ?? throw new NotFoundException(nameof(Employee), employeeId);
        return await ticketRepository.SearchAsync(new TicketSearchRequest(null, employeeId, null, null, null, page, pageSize), cancellationToken);
    }
}
