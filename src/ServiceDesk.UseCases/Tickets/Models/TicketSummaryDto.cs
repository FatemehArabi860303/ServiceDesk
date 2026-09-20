using ServiceDesk.Domain.Enums;
namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record TicketSummaryDto(Guid Id, Guid CustomerId, Guid? AssignedEmployeeId, TicketStatus Status, TicketPriority Priority, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ClosedAt, string ConcurrencyToken);
