using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tickets;

public sealed class EmployeeWorkForbiddenException : Exception;

public sealed class EmployeeWorkShell(
    IUserRepository users, ITicketRepository tickets, IEmployeeWorkRepository workspace)
{
    private async Task AuthorizeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!EmployeeWorkCore.IsAuthorized(await users.GetByIdAsync(employeeId, cancellationToken)))
            throw new EmployeeWorkForbiddenException();
    }

    public async Task<EmployeeTicketPage> GetPageAsync(Guid employeeId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(employeeId, cancellationToken);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page, 1000000);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 50);
        return await workspace.GetPageAsync(page, pageSize, cancellationToken);
    }

    public async Task<EmployeeTicketDetails?> GetDetailsAsync(Guid employeeId, Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(employeeId, cancellationToken);
        EmployeeTicketDetails? details = await workspace.GetDetailsAsync(ticketId, cancellationToken);
        if (details is null) return null;
        return details with { CanStartWork = EmployeeWorkCore.CanStart(
            details.Ticket.Status, details.Ticket.AssignedEmployeeUserId, employeeId) };
    }

    public async Task StartAsync(Guid employeeId, Guid ticketId, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(employeeId, cancellationToken);
        Ticket? ticket = await tickets.GetByIdAsync(ticketId, cancellationToken);
        Guid? expectedAssignee = ticket?.AssignedEmployeeUserId;
        Guid assignmentId = Guid.NewGuid();
        Guid startedId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IReadOnlyList<RequestProgressed> progressed = EmployeeWorkCore.Execute(ticket, employeeId, assignmentId, startedId, now);
        Guid[] historyIds = expectedAssignee is null ? [assignmentId, startedId] : [startedId];
        TicketProgressNotification[] notifications = progressed
            .Select(item => new TicketProgressNotification(Guid.NewGuid(), item, now)).ToArray();
        if (!await workspace.TryClaimAndStartAsync(ticket!, expectedAssignee, historyIds, notifications, cancellationToken))
            throw new StartWorkException(StartWorkFailureKind.TicketNoLongerEligible);
    }
}
