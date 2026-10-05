using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Shell.Notifications;
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
        TicketProgressNotification notification,
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

        await AddProgressNotificationAsync(notification, cancellationToken);
        dbContext.TicketHistories.Add(assignmentHistory);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryStartWorkAsync(
        Ticket ticket,
        Guid workStartedHistoryId,
        TicketProgressNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var workStartedHistory = ticket.History.Single(history => history.Id == workStartedHistoryId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var rowsAffected = await dbContext.Tickets
            .Where(storedTicket => storedTicket.Id == ticket.Id
                && storedTicket.Status == TicketStatus.Open
                && storedTicket.AssignedEmployeeUserId == ticket.AssignedEmployeeUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(storedTicket => storedTicket.Status, ticket.Status)
                .SetProperty(storedTicket => storedTicket.UpdatedAt, ticket.UpdatedAt), cancellationToken);

        if (rowsAffected != 1)
        {
            return false;
        }

        await AddProgressNotificationAsync(notification, cancellationToken);
        dbContext.TicketHistories.Add(workStartedHistory);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryResolveAsync(
        Ticket ticket,
        Guid historyId,
        TicketProgressNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var history = ticket.History.Single(item => item.Id == historyId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var rowsAffected = await dbContext.Tickets
            .Where(stored => stored.Id == ticket.Id
                && stored.Status == TicketStatus.InProgress
                && stored.AssignedEmployeeUserId != null
                && stored.AssignedEmployeeUserId == ticket.AssignedEmployeeUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(stored => stored.Status, ticket.Status)
                .SetProperty(stored => stored.UpdatedAt, ticket.UpdatedAt), cancellationToken);
        if (rowsAffected != 1)
        {
            return false;
        }

        await AddProgressNotificationAsync(notification, cancellationToken);
        dbContext.TicketHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task AddProgressNotificationAsync(
        TicketProgressNotification notification,
        CancellationToken cancellationToken)
    {
        var customerEmail = await dbContext.Users
            .Where(user => user.Id == notification.Progressed.CustomerUserId)
            .Select(user => user.Email)
            .SingleAsync(cancellationToken);
        var notificationRequested = new NotificationRequestedV1(
            "servicedesk",
            notification.EventId.ToString(),
            customerEmail,
            GetSubject(notification.Progressed.Kind),
            GetBody(notification.Progressed.Kind));
        var outboxMessage = new OutboxMessage(
            notification.EventId,
            NotificationRequestedV1.Type,
            JsonSerializer.Serialize(notificationRequested),
            notification.Progressed.OccurredAt,
            notification.CreatedAt);

        dbContext.OutboxMessages.Add(outboxMessage);
    }

    private static string GetSubject(RequestProgressKind kind) => kind switch
    {
        RequestProgressKind.Resolved => "Your support request has been resolved",
        RequestProgressKind.Assigned => "Your service request has been assigned",
        RequestProgressKind.WorkStarted => "Work has started on your service request",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported request-progress notification.")
    };

    private static string GetBody(RequestProgressKind kind) => kind switch
    {
        RequestProgressKind.Resolved => "Your support request has been marked as resolved. Please review the outcome.",
        RequestProgressKind.Assigned => "Your service request has been assigned to a support employee.",
        RequestProgressKind.WorkStarted => "A support employee has started work on your service request.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported request-progress notification.")
    };
}
