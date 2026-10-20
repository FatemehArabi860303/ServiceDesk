namespace ServiceDesk.Core.Tickets;

public sealed class CreateTicketException(CreateTicketFailureKind failure) : Exception
{
    public CreateTicketFailureKind Failure { get; } = failure;
}
