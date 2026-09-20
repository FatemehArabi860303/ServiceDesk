using ServiceDesk.Domain.Entities;
using ServiceDesk.UseCases.Common;
using ServiceDesk.UseCases.Tickets.Models;

namespace ServiceDesk.UseCases.Abstractions.Persistence;

public interface ITicketRepository
{
    Task<TicketWithConcurrencyToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task UpdateAsync(Ticket ticket, string concurrencyToken, CancellationToken cancellationToken = default);
    Task<PagedResult<TicketSummaryDto>> SearchAsync(TicketSearchRequest request, CancellationToken cancellationToken = default);
}
