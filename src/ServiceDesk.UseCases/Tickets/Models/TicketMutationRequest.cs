namespace ServiceDesk.UseCases.Tickets.Models;
public sealed record TicketMutationRequest(string ConcurrencyToken);
