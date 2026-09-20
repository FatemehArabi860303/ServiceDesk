namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record ReassignTicketRequest(Guid EmployeeId, string ConcurrencyToken);
