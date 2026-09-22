namespace ServiceDesk.Core.Tickets;

public enum CreateTicketFailureKind
{
    InvalidTitle,
    InvalidDescription,
    InvalidPriority,
    CustomerNotPermitted
}
