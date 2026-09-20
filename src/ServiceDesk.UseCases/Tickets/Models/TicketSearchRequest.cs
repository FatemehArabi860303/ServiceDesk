using ServiceDesk.Domain.Enums;
namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record TicketSearchRequest(Guid? CustomerId, Guid? AssignedEmployeeId, TicketStatus? Status, TicketPriority? Priority, string? SearchText, int Page = 1, int PageSize = 25, TicketSortField SortBy = TicketSortField.CreatedAt, SortDirection SortDirection = SortDirection.Descending);
