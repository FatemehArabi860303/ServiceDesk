using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Customers.Models;
using ServiceDesk.UseCases.Employees.Models;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Common;

internal static class DtoMapper
{
    internal static CustomerDto ToDto(Customer customer) => new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt);
    internal static EmployeeDto ToDto(Employee employee) => new(employee.Id, employee.FirstName, employee.LastName, employee.Email, employee.IsActive, employee.CreatedAt, employee.UpdatedAt);
    internal static TicketDto ToDto(Ticket ticket, string token) => new(ticket.Id, ticket.CustomerId, ticket.AssignedEmployeeId, ticket.Status, ticket.Priority, ticket.Title, ticket.Description, ticket.CreatedAt, ticket.UpdatedAt, ticket.ClosedAt, token);
    internal static TicketHistoryDto ToDto(TicketHistory history) => new(history.Id, history.TicketId, history.Action, history.ActorReference, history.Description, history.PreviousValue, history.NewValue, history.CreatedAt);
}
