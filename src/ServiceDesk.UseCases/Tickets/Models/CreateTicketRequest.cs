using ServiceDesk.Domain.Enums;
namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record CreateTicketRequest(Guid CustomerId, string Title, string Description, TicketPriority Priority);
