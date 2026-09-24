namespace ServiceDesk.Core.Tickets;

public sealed class Ticket
{
    private readonly List<TicketHistory> history = [];

    private Ticket()
    {
    }

    internal Ticket(
        Guid id,
        Guid customerUserId,
        string title,
        string description,
        TicketPriority priority,
        TicketStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        TicketHistory creationHistory)
    {
        Id = id;
        CustomerUserId = customerUserId;
        Title = title;
        Description = description;
        Priority = priority;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        history.Add(creationHistory);
    }

    public Guid Id { get; private set; }

    public Guid CustomerUserId { get; private set; }

    public Guid? AssignedEmployeeUserId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<TicketHistory> History => history.AsReadOnly();

    internal void Assign(Guid employeeUserId, Guid assignmentHistoryId, DateTimeOffset now)
    {
        if (Status != TicketStatus.Open)
        {
            throw new AssignTicketException(AssignTicketFailureKind.TicketNotOpen);
        }

        if (AssignedEmployeeUserId is not null)
        {
            throw new AssignTicketException(AssignTicketFailureKind.TicketAlreadyAssigned);
        }

        AssignedEmployeeUserId = employeeUserId;
        UpdatedAt = now;
        history.Add(new TicketHistory(
            assignmentHistoryId,
            Id,
            employeeUserId,
            TicketHistoryAction.Assigned,
            now,
            employeeUserId));
    }
}
