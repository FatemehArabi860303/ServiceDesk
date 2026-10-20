namespace ServiceDesk.Core.Tickets;

public enum StartWorkFailureKind
{
    TicketNotFound,
    TicketNotOpen,
    TicketUnassigned,
    TicketAssignedToAnotherEmployee,
    TicketNoLongerEligible
}
