namespace ServiceDesk.Core.Tickets;

public sealed class ResolveTicketException(ResolveTicketFailureKind failure) : Exception
{
    public ResolveTicketFailureKind Failure { get; } = failure;
}
