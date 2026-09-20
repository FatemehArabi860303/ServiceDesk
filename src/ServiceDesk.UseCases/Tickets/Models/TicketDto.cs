using ServiceDesk.Domain.Enums;
namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record TicketDto(Guid Id, Guid CustomerId, Guid? AssignedEmployeeId, TicketStatus Status, TicketPriority Priority, string Title, string Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ClosedAt, string ConcurrencyToken);
