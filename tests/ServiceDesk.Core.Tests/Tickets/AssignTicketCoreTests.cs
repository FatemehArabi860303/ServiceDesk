using FluentAssertions;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Core.Tests.Tickets;

public sealed class AssignTicketCoreTests
{
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");
    private static readonly Guid EmployeeUserId = Guid.Parse("45b3108f-e171-476a-85a6-9a0f3155ef2f");
    private static readonly Guid TicketId = Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef");
    private static readonly Guid CreationHistoryId = Guid.Parse("4e5bb9c5-a15a-4d8b-a0e0-1f8624ecb01d");
    private static readonly Guid AssignmentHistoryId = Guid.Parse("41683ceb-4529-4aa6-9810-608e5a290b46");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AssignedAt = CreatedAt.AddMinutes(5);

    [Fact]
    public void Execute_WithOpenUnassignedTicket_AssignsEmployeeAndAppendsHistory()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        AssignTicketCore.Execute(ticket, EmployeeUserId, AssignmentHistoryId, AssignedAt);

        // Assert
        ticket.AssignedEmployeeUserId.Should().Be(EmployeeUserId);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.Priority.Should().Be(TicketPriority.High);
        ticket.CreatedAt.Should().Be(CreatedAt);
        ticket.UpdatedAt.Should().Be(AssignedAt);
        ticket.History.Should().HaveCount(2);
        var history = ticket.History.Single(entry => entry.Id == AssignmentHistoryId);
        history.Action.Should().Be(TicketHistoryAction.Assigned);
        history.ActorUserId.Should().Be(EmployeeUserId);
        history.AssignedEmployeeUserId.Should().Be(EmployeeUserId);
        history.OccurredAt.Should().Be(AssignedAt);
    }

    [Fact]
    public void Execute_WithAlreadyAssignedTicket_RejectsWithoutAppendingHistory()
    {
        // Arrange
        var ticket = CreateTicket();
        AssignTicketCore.Execute(ticket, EmployeeUserId, AssignmentHistoryId, AssignedAt);

        // Act
        Action act = () => AssignTicketCore.Execute(ticket, Guid.NewGuid(), Guid.NewGuid(), AssignedAt.AddMinutes(1));

        // Assert
        act.Should().Throw<AssignTicketException>().Which.Failure.Should().Be(AssignTicketFailureKind.TicketAlreadyAssigned);
        ticket.History.Should().HaveCount(2);
        ticket.AssignedEmployeeUserId.Should().Be(EmployeeUserId);
    }

    [Fact]
    public void Execute_WithMissingTicket_Rejects()
    {
        // Arrange
        Ticket? ticket = null;

        // Act
        Action act = () => AssignTicketCore.Execute(ticket, EmployeeUserId, AssignmentHistoryId, AssignedAt);

        // Assert
        act.Should().Throw<AssignTicketException>().Which.Failure.Should().Be(AssignTicketFailureKind.TicketNotFound);
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
        new CreateTicketFacts(true),
        CustomerUserId,
        TicketId,
        CreationHistoryId,
        CreatedAt);
}
