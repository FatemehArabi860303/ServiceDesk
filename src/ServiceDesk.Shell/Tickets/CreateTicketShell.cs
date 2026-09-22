using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tickets;

public sealed class CreateTicketShell(IUserRepository userRepository, ITicketRepository ticketRepository)
{
    public async Task<Ticket> ExecuteAsync(
        CreateTicketCommand command,
        Guid customerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var user = await userRepository.GetByIdAsync(customerUserId, cancellationToken);
        var customerPermitted = user is not null
            && user.Role == UserRole.Customer
            && user.IsActive;
        var facts = new CreateTicketFacts(customerPermitted);
        var ticket = CreateTicketCore.Execute(
            command,
            facts,
            customerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await ticketRepository.AddAsync(ticket, cancellationToken);

        return ticket;
    }
}
