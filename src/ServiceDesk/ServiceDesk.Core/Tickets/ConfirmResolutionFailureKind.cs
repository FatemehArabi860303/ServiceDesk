namespace ServiceDesk.Core.Tickets;

public enum ConfirmResolutionFailureKind
{
    TicketNotFound,
    ActorNotPermitted,
    TicketNotResolved,
    TicketNoLongerEligible
}
