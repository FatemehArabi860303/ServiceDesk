namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record AssignTicketRequest(Guid EmployeeId, string ConcurrencyToken);
