using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Domain.Tests;

internal static class DomainTestData
{
    internal const string Actor = "employee-42";

    internal static Ticket CreateTicket(TicketPriority priority = TicketPriority.Medium) =>
        Ticket.Create(Guid.NewGuid(), "Printer is unavailable", "The main office printer does not respond.", priority, Actor);

    internal static Employee CreateEmployee() =>
        Employee.Create("Alex", "Agent", "alex.agent@example.test");
}
