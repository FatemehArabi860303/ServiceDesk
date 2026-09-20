using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Abstractions.Persistence;

namespace ServiceDesk.UseCases.Common;

internal static class Validation
{
    internal static void RequirePagination(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
    }

    internal static void RequireConcurrencyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("A concurrency token is required.", nameof(token));
    }

    internal static async Task<TicketWithConcurrencyToken> LoadTicketAsync(ITicketRepository repository, Guid ticketId, CancellationToken cancellationToken)
        => await repository.GetByIdAsync(ticketId, cancellationToken) ?? throw new NotFoundException(nameof(Ticket), ticketId);

    internal static async Task<TicketWithConcurrencyToken> UpdateAndReloadAsync(ITicketRepository repository, Ticket ticket, string concurrencyToken, CancellationToken cancellationToken)
    {
        await repository.UpdateAsync(ticket, concurrencyToken, cancellationToken);
        return await LoadTicketAsync(repository, ticket.Id, cancellationToken);
    }

    internal static async Task<TicketWithConcurrencyToken> AddAndReloadAsync(ITicketRepository repository, Ticket ticket, CancellationToken cancellationToken)
    {
        await repository.AddAsync(ticket, cancellationToken);
        return await LoadTicketAsync(repository, ticket.Id, cancellationToken);
    }
}
