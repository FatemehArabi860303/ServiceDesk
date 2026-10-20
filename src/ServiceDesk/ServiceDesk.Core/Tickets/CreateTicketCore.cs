namespace ServiceDesk.Core.Tickets;

public static class CreateTicketCore
{
    public static Ticket Execute(
        CreateTicketCommand command,
        CreateTicketFacts facts,
        Guid customerUserId,
        Guid ticketId,
        Guid creationHistoryId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.CustomerPermitted)
        {
            throw new CreateTicketException(CreateTicketFailureKind.CustomerNotPermitted);
        }

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new CreateTicketException(CreateTicketFailureKind.InvalidTitle);
        }

        if (string.IsNullOrWhiteSpace(command.Description))
        {
            throw new CreateTicketException(CreateTicketFailureKind.InvalidDescription);
        }

        if (!Enum.IsDefined(command.Priority))
        {
            throw new CreateTicketException(CreateTicketFailureKind.InvalidPriority);
        }

        var creationHistory = new TicketHistory(
            creationHistoryId,
            ticketId,
            customerUserId,
            TicketHistoryAction.Created,
            now);

        return new Ticket(
            ticketId,
            customerUserId,
            command.Title,
            command.Description,
            command.Priority,
            TicketStatus.Open,
            now,
            now,
            creationHistory);
    }
}
