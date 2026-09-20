namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record UpdateTicketDetailsRequest(string? Title, string? Description, string ConcurrencyToken);
