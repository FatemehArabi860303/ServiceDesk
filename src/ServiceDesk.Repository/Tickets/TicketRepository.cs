using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Repository.Tickets;

public sealed class TicketRepository(ServiceDeskDbContext dbContext) : ITicketRepository
{
    public async Task AddAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.History)
            .SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TicketListItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await (
            from ticket in dbContext.Tickets.AsNoTracking()
            join customer in dbContext.Users.AsNoTracking() on ticket.CustomerUserId equals customer.Id
            join assignedEmployee in dbContext.Users.AsNoTracking()
                on ticket.AssignedEmployeeUserId equals (Guid?)assignedEmployee.Id into assignedEmployees
            from assignedEmployee in assignedEmployees.DefaultIfEmpty()
            select new TicketListItem(
                ticket.Id,
                customer.Email,
                assignedEmployee == null ? null : assignedEmployee.Email,
                ticket.Title,
                ticket.Description,
                ticket.Priority,
                ticket.Status,
                ticket.CreatedAt,
                ticket.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> TryAssignAsync(
        Ticket ticket,
        Guid assignmentHistoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var assignmentHistory = ticket.History.Single(history => history.Id == assignmentHistoryId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var rowsAffected = await dbContext.Tickets
            .Where(storedTicket => storedTicket.Id == ticket.Id
                && storedTicket.Status == TicketStatus.Open
                && storedTicket.AssignedEmployeeUserId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(storedTicket => storedTicket.AssignedEmployeeUserId, ticket.AssignedEmployeeUserId)
                .SetProperty(storedTicket => storedTicket.UpdatedAt, ticket.UpdatedAt), cancellationToken);

        if (rowsAffected != 1)
        {
            return false;
        }

        dbContext.TicketHistories.Add(assignmentHistory);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
