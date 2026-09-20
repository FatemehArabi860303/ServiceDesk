using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.UseCases.Abstractions.Persistence;
using ServiceDesk.UseCases.Common;

namespace ServiceDesk.UseCases.Tests.TestSupport;

internal static class TestFixtures
{
    internal static readonly ActorContext Admin = new("admin-1", ActorType.Administrator);

    internal static Customer Customer() => ServiceDesk.Domain.Entities.Customer.Create("Ada", "Lovelace", "ada@example.test");

    internal static Employee Employee() => ServiceDesk.Domain.Entities.Employee.Create("Alex", "Agent", "alex@example.test");

    internal static Ticket Ticket(Guid customerId, string actor = "customer-1") => ServiceDesk.Domain.Entities.Ticket.Create(customerId, "Printer", "Printer is unavailable.", TicketPriority.Medium, actor);

    internal static TicketWithConcurrencyToken Loaded(Ticket ticket, string token = "version-2") => new(ticket, token);
}
