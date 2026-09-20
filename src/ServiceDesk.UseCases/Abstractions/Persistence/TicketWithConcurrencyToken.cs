using ServiceDesk.Domain.Entities;

namespace ServiceDesk.UseCases.Abstractions.Persistence;

public sealed record TicketWithConcurrencyToken(
    Ticket Ticket,
    string ConcurrencyToken);
