using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Shell.Tickets;

public interface IEmployeeWorkRepository
{
    Task<EmployeeTicketPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<EmployeeTicketDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> TryClaimAndStartAsync(Ticket ticket, Guid? expectedAssignee,
        IReadOnlyList<Guid> historyIds, IReadOnlyList<TicketProgressNotification> notifications,
        CancellationToken cancellationToken);
}

public sealed record EmployeeTicketItem(Guid Id, string Title, string CustomerEmail,
    Guid? AssignedEmployeeUserId, string? AssignedEmployeeEmail, TicketPriority Priority,
    TicketStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public EmployeeTicketItem() : this(default, "", "", null, null, default, default, default, default) { }
}

public sealed record EmployeeTicketPage(IReadOnlyList<EmployeeTicketItem> Items, int Page, int PageSize, bool HasMore);

public sealed record EmployeeTicketDetails(EmployeeTicketItem Ticket, string Description,
    IReadOnlyList<TicketHistory> History, bool CanStartWork = false);
