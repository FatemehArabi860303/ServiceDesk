using ServiceDesk.Domain.Enums;
namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record ChangeTicketPriorityRequest(TicketPriority Priority, string ConcurrencyToken);
