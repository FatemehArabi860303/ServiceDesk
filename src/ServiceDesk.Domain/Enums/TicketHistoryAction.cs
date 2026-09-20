namespace ServiceDesk.Domain.Enums;

public enum TicketHistoryAction
{
    TicketCreated,
    Assigned,
    Reassigned,
    PriorityChanged,
    StatusChanged,
    CommentAdded,
    TitleChanged,
    DescriptionChanged,
    Reopened,
    Closed
}
