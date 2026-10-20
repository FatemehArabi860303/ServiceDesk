namespace ServiceDesk.Core.Tickets;

public sealed class AssignTicketException(AssignTicketFailureKind failure) : Exception
{
    public AssignTicketFailureKind Failure { get; } = failure;
}
