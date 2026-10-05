namespace ServiceDesk.Core.Tickets;

public enum ResolveTicketFailureKind
{
    TicketNotFound,
    ActorNotPermitted,
    TicketNotInProgress,
    TicketUnassigned,
    TicketNoLongerEligible
}
