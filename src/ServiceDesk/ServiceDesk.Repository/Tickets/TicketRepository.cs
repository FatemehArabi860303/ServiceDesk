using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using ServiceDesk.Contracts;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Repository.Notifications;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Repository.Tickets;

public sealed class TicketRepository(ServiceDeskDbContext dbContext) : ITicketRepository, IEmployeeWorkRepository
{
    private IQueryable<EmployeeTicketItem> EmployeeItems =>
        from ticket in dbContext.Tickets.AsNoTracking()
        join customer in dbContext.Users on ticket.CustomerUserId equals customer.Id
        join employee in dbContext.Users on ticket.AssignedEmployeeUserId equals (Guid?)employee.Id into employees
        from employee in employees.DefaultIfEmpty()
        select new EmployeeTicketItem
        {
            Id = ticket.Id, Title = ticket.Title, CustomerEmail = customer.Email,
            AssignedEmployeeUserId = ticket.AssignedEmployeeUserId,
            AssignedEmployeeEmail = employee == null ? null : employee.Email,
            Priority = ticket.Priority, Status = ticket.Status,
            CreatedAt = ticket.CreatedAt, UpdatedAt = ticket.UpdatedAt
        };

    public async Task<EmployeeTicketPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        // Validate at the persistence boundary too; never materialize an unbounded result.
        if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 50)
            throw new ArgumentOutOfRangeException(nameof(page));
        EmployeeTicketItem[] items = await EmployeeItems.OrderBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        return new EmployeeTicketPage(items.Take(pageSize).ToArray(), page, pageSize, items.Length > pageSize);
    }

    public async Task<EmployeeTicketDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        Ticket? ticket = await GetByIdAsync(id, cancellationToken);
        if (ticket is null) return null;
        string customer = await dbContext.Users.Where(user => user.Id == ticket.CustomerUserId)
            .Select(user => user.Email).SingleAsync(cancellationToken);
        string? employee = ticket.AssignedEmployeeUserId is null ? null : await dbContext.Users
            .Where(user => user.Id == ticket.AssignedEmployeeUserId).Select(user => user.Email).SingleAsync(cancellationToken);
        EmployeeTicketItem item = new(ticket.Id, ticket.Title, customer, ticket.AssignedEmployeeUserId,
            employee, ticket.Priority, ticket.Status, ticket.CreatedAt, ticket.UpdatedAt);
        return new EmployeeTicketDetails(item, ticket.Description,
            ticket.History.OrderBy(history => history.OccurredAt).ThenBy(history => history.Action).ToArray());
    }

    public async Task<bool> TryClaimAndStartAsync(Ticket ticket, Guid? expectedAssignee,
        IReadOnlyList<Guid> historyIds, IReadOnlyList<TicketProgressNotification> notifications,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        int rowsAffected = await dbContext.Tickets
            .Where(stored => stored.Id == ticket.Id && stored.Status == TicketStatus.Open
                && stored.AssignedEmployeeUserId == expectedAssignee)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(stored => stored.AssignedEmployeeUserId, ticket.AssignedEmployeeUserId)
                .SetProperty(stored => stored.Status, ticket.Status)
                .SetProperty(stored => stored.UpdatedAt, ticket.UpdatedAt), cancellationToken);
        if (rowsAffected != 1) return false;
        foreach (TicketProgressNotification notification in notifications)
            await AddProgressNotificationAsync(notification, cancellationToken);
        dbContext.TicketHistories.AddRange(ticket.History.Where(history => historyIds.Contains(history.Id)));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryConfirmResolutionAsync(
        Ticket ticket,
        Guid historyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var history = ticket.History.Single(item => item.Id == historyId);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var rowsAffected = await dbContext.Tickets
            .Where(stored => stored.Id == ticket.Id
                && stored.Status == TicketStatus.Resolved
                && stored.CustomerUserId == history.ActorUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(stored => stored.Status, ticket.Status)
                .SetProperty(stored => stored.ClosedAt, ticket.ClosedAt)
                .SetProperty(stored => stored.UpdatedAt, ticket.UpdatedAt), cancellationToken);
        if (rowsAffected != 1)
        {
            return false;
        }

        dbContext.TicketHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public async Task AddAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        cancellationToken.ThrowIfCancellationRequested();

        var analysisRequested = new TicketAiAnalysisRequestedV1(
            Guid.NewGuid(),
            ticket.Id,
            ticket.CreatedAt);

        var outboxMessage = new OutboxMessage(
            analysisRequested.Id,
            TicketAiAnalysisRequestedV1.Type,
            JsonSerializer.Serialize(analysisRequested),
            analysisRequested.OccurredAt,
            DateTimeOffset.UtcNow);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Tickets.Add(ticket);
        dbContext.OutboxMessages.Add(outboxMessage);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<Ticket?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.History)
            .SingleOrDefaultAsync(
                ticket => ticket.Id == id,
                cancellationToken);

    public async Task<IReadOnlyList<TicketListItem>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await (
            from ticket in dbContext.Tickets.AsNoTracking()
            join customer in dbContext.Users.AsNoTracking()
                on ticket.CustomerUserId equals customer.Id
            join assignedEmployee in dbContext.Users.AsNoTracking()
                on ticket.AssignedEmployeeUserId
                equals (Guid?)assignedEmployee.Id into assignedEmployees
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

        var assignmentHistory = ticket.History.Single(
            history => history.Id == assignmentHistoryId);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var rowsAffected = await dbContext.Tickets
            .Where(storedTicket => storedTicket.Id == ticket.Id
                && storedTicket.Status == TicketStatus.Open
                && storedTicket.AssignedEmployeeUserId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    storedTicket => storedTicket.AssignedEmployeeUserId,
                    ticket.AssignedEmployeeUserId)
                .SetProperty(
                    storedTicket => storedTicket.UpdatedAt,
                    ticket.UpdatedAt),
                cancellationToken);

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

        var workStartedHistory = ticket.History.Single(
            history => history.Id == workStartedHistoryId);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var rowsAffected = await dbContext.Tickets
            .Where(storedTicket => storedTicket.Id == ticket.Id
                && storedTicket.Status == TicketStatus.Open
                && storedTicket.AssignedEmployeeUserId
                    == ticket.AssignedEmployeeUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    storedTicket => storedTicket.Status,
                    ticket.Status)
                .SetProperty(
                    storedTicket => storedTicket.UpdatedAt,
                    ticket.UpdatedAt),
                cancellationToken);

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

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var rowsAffected = await dbContext.Tickets
            .Where(stored => stored.Id == ticket.Id
                && stored.Status == TicketStatus.InProgress
                && stored.AssignedEmployeeUserId != null
                && stored.AssignedEmployeeUserId
                    == ticket.AssignedEmployeeUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(stored => stored.Status, ticket.Status)
                .SetProperty(stored => stored.UpdatedAt, ticket.UpdatedAt),
                cancellationToken);

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
        RequestProgressKind.Resolved =>
            "Your support request has been resolved",
        RequestProgressKind.Assigned =>
            "Your service request has been assigned",
        RequestProgressKind.WorkStarted =>
            "Work has started on your service request",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Unsupported request-progress notification.")
    };

    private static string GetBody(RequestProgressKind kind) => kind switch
    {
        RequestProgressKind.Resolved =>
            "Your support request has been marked as resolved. Please review the outcome.",
        RequestProgressKind.Assigned =>
            "Your service request has been assigned to a support employee.",
        RequestProgressKind.WorkStarted =>
            "A support employee has started work on your service request.",
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Unsupported request-progress notification.")
    };
}
