using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Domain.Entities;

public sealed class Ticket
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 5000;
    public const int CommentMaxLength = 5000;

    private readonly List<TicketHistory> _history = [];

    private Ticket(Guid id, Guid customerId, string title, string description, TicketPriority priority, DateTimeOffset now)
    {
        Id = id;
        CustomerId = customerId;
        Title = title;
        Description = description;
        Priority = priority;
        Status = TicketStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; }
    public Guid CustomerId { get; }
    public Guid? AssignedEmployeeId { get; private set; }
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public IReadOnlyList<TicketHistory> History => _history.AsReadOnly();

    public static Ticket Create(Guid customerId, string title, string description, TicketPriority priority, string actorReference)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A ticket must belong to a customer.", nameof(customerId));
        }

        ValidateRequired(title, nameof(title), TitleMaxLength);
        ValidateRequired(description, nameof(description), DescriptionMaxLength);
        ValidateEnum(priority, nameof(priority));
        ValidateActor(actorReference);

        var now = DateTimeOffset.UtcNow;
        var ticket = new Ticket(Guid.NewGuid(), customerId, title.Trim(), description.Trim(), priority, now);
        ticket.AppendHistory(TicketHistoryAction.TicketCreated, actorReference, now, "Ticket created", newValue: TicketStatus.Open.ToString());
        return ticket;
    }

    public void StartWork(string actorReference)
    {
        Transition(TicketStatus.Open, TicketStatus.InProgress, TicketHistoryAction.StatusChanged, actorReference, "Work started");
    }

    public void Resolve(string actorReference)
    {
        Transition(TicketStatus.InProgress, TicketStatus.Resolved, TicketHistoryAction.StatusChanged, actorReference, "Ticket resolved");
    }

    public void ConfirmResolution(string actorReference)
    {
        Transition(TicketStatus.Resolved, TicketStatus.Closed, TicketHistoryAction.Closed, actorReference, "Resolution confirmed; ticket closed");
    }

    public void Close(string actorReference) => ConfirmResolution(actorReference);

    public void RejectResolution(string actorReference)
    {
        Transition(TicketStatus.Resolved, TicketStatus.InProgress, TicketHistoryAction.StatusChanged, actorReference, "Resolution rejected; work resumed");
    }

    public void Reopen(string actorReference)
    {
        Transition(TicketStatus.Closed, TicketStatus.InProgress, TicketHistoryAction.Reopened, actorReference, "Ticket reopened");
    }

    public void Assign(Employee employee, string actorReference)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ValidateActor(actorReference);
        EnsureActive(employee);

        if (AssignedEmployeeId is not null)
        {
            throw new InvalidOperationException("A ticket with an assignee must be reassigned instead.");
        }

        AssignedEmployeeId = employee.Id;
        RecordChange(TicketHistoryAction.Assigned, actorReference, "Ticket assigned", null, employee.Id.ToString());
    }

    public void Reassign(Employee employee, string actorReference)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ValidateActor(actorReference);
        EnsureActive(employee);

        if (AssignedEmployeeId is null)
        {
            throw new InvalidOperationException("An unassigned ticket must be assigned instead.");
        }

        if (AssignedEmployeeId == employee.Id)
        {
            return;
        }

        var previousAssignee = AssignedEmployeeId.Value.ToString();
        AssignedEmployeeId = employee.Id;
        RecordChange(TicketHistoryAction.Reassigned, actorReference, "Ticket reassigned", previousAssignee, employee.Id.ToString());
    }

    public void ChangePriority(TicketPriority priority, string actorReference)
    {
        ValidateEnum(priority, nameof(priority));
        ValidateActor(actorReference);

        if (Priority == priority)
        {
            return;
        }

        var previousPriority = Priority.ToString();
        Priority = priority;
        RecordChange(TicketHistoryAction.PriorityChanged, actorReference, "Priority changed", previousPriority, priority.ToString());
    }

    public void ChangeTitle(string title, string actorReference)
    {
        ValidateRequired(title, nameof(title), TitleMaxLength);
        ValidateActor(actorReference);
        var normalizedTitle = title.Trim();

        if (Title == normalizedTitle)
        {
            return;
        }

        var previousTitle = Title;
        Title = normalizedTitle;
        RecordChange(TicketHistoryAction.TitleChanged, actorReference, "Title changed", previousTitle, normalizedTitle);
    }

    public void ChangeDescription(string description, string actorReference)
    {
        ValidateRequired(description, nameof(description), DescriptionMaxLength);
        ValidateActor(actorReference);
        var normalizedDescription = description.Trim();

        if (Description == normalizedDescription)
        {
            return;
        }

        var previousDescription = Description;
        Description = normalizedDescription;
        RecordChange(TicketHistoryAction.DescriptionChanged, actorReference, "Description changed", previousDescription, normalizedDescription);
    }

    public void AddComment(string comment, string actorReference)
    {
        ValidateRequired(comment, nameof(comment), CommentMaxLength);
        ValidateActor(actorReference);
        RecordChange(TicketHistoryAction.CommentAdded, actorReference, comment.Trim());
    }

    private void Transition(
        TicketStatus expectedStatus,
        TicketStatus nextStatus,
        TicketHistoryAction historyAction,
        string actorReference,
        string description)
    {
        ValidateActor(actorReference);

        if (Status != expectedStatus)
        {
            throw new InvalidOperationException($"Cannot transition a ticket from {Status} to {nextStatus}.");
        }

        var now = DateTimeOffset.UtcNow;
        var previousStatus = Status;
        Status = nextStatus;
        ClosedAt = nextStatus == TicketStatus.Closed ? now : null;
        UpdatedAt = now;
        AppendHistory(historyAction, actorReference, now, description, previousStatus.ToString(), nextStatus.ToString());
    }

    private void RecordChange(
        TicketHistoryAction action,
        string actorReference,
        string description,
        string? previousValue = null,
        string? newValue = null)
    {
        var now = DateTimeOffset.UtcNow;
        UpdatedAt = now;
        AppendHistory(action, actorReference, now, description, previousValue, newValue);
    }

    private void AppendHistory(
        TicketHistoryAction action,
        string actorReference,
        DateTimeOffset now,
        string? description = null,
        string? previousValue = null,
        string? newValue = null)
    {
        _history.Add(new TicketHistory(Id, action, actorReference.Trim(), now, description, previousValue, newValue));
    }

    private static void EnsureActive(Employee employee)
    {
        if (!employee.IsActive)
        {
            throw new InvalidOperationException("An inactive employee cannot receive a ticket assignment.");
        }
    }

    private static void ValidateActor(string actorReference)
    {
        if (string.IsNullOrWhiteSpace(actorReference))
        {
            throw new ArgumentException("An actor reference is required for auditable ticket operations.", nameof(actorReference));
        }
    }

    private static void ValidateRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }
    }

    private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value is not a defined enum member.");
        }
    }
}
