namespace ServiceDesk.Core.Tickets;

public enum AssignTicketFailureKind
{
    TicketNotFound,
    TicketNotOpen,
    TicketAlreadyAssigned,
    TicketNoLongerAvailable
}
