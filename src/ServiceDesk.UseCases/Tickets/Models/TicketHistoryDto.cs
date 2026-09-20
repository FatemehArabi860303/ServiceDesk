using ServiceDesk.Domain.Enums;
namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record TicketHistoryDto(Guid Id, Guid TicketId, TicketHistoryAction Action, string ActorReference, string? Description, string? PreviousValue, string? NewValue, DateTimeOffset CreatedAt);
