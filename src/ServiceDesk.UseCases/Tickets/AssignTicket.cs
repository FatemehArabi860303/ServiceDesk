using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Tickets;

public sealed class AssignTicket(ITicketRepository ticketRepository, IEmployeeRepository employeeRepository)
{
    public async Task<TicketDto> ExecuteAsync(Guid ticketId, AssignTicketRequest request, ActorContext actor, CancellationToken cancellationToken = default)
    {
        Authorization.RequireAdministrator(actor);
        Validation.RequireConcurrencyToken(request.ConcurrencyToken);
        var loaded = await Validation.LoadTicketAsync(ticketRepository, ticketId, cancellationToken);
        var employee = await employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken) ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);
        loaded.Ticket.Assign(employee, actor.ActorReference);
        var persisted = await Validation.UpdateAndReloadAsync(ticketRepository, loaded.Ticket, request.ConcurrencyToken, cancellationToken);
        return DtoMapper.ToDto(persisted.Ticket, persisted.ConcurrencyToken);
    }
}
