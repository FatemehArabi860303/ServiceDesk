using FluentAssertions;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Core.Tests.Tickets;

public sealed class StartWorkCoreTests
{
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");
    private static readonly Guid EmployeeUserId = Guid.Parse("45b3108f-e171-476a-85a6-9a0f3155ef2f");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_WithOpenTicketAssignedToEmployee_StartsWorkAndAppendsHistory()
    {
        // Arrange
        var ticket = CreateAssignedTicket();
        var workStartedAt = CreatedAt.AddMinutes(10);
        var workStartedHistoryId = Guid.NewGuid();

        // Act
        StartWorkCore.Execute(ticket, EmployeeUserId, workStartedHistoryId, workStartedAt);

        // Assert
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.AssignedEmployeeUserId.Should().Be(EmployeeUserId);
        ticket.CreatedAt.Should().Be(CreatedAt);
        ticket.UpdatedAt.Should().Be(workStartedAt);
        var history = ticket.History.Single(entry => entry.Id == workStartedHistoryId);
        history.Action.Should().Be(TicketHistoryAction.WorkStarted);
        history.ActorUserId.Should().Be(EmployeeUserId);
        history.OccurredAt.Should().Be(workStartedAt);
        history.AssignedEmployeeUserId.Should().BeNull();
    }

    [Fact]
    public void Execute_WithUnassignedTicket_RejectsWithoutChangingStateOrHistory()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        Action act = () => StartWorkCore.Execute(ticket, EmployeeUserId, Guid.NewGuid(), CreatedAt.AddMinutes(10));

        // Assert
        act.Should().Throw<StartWorkException>().Which.Failure.Should().Be(StartWorkFailureKind.TicketUnassigned);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.AssignedEmployeeUserId.Should().BeNull();
        ticket.History.Should().ContainSingle();
    }

    [Fact]
    public void Execute_WhenTicketIsAssignedToAnotherEmployee_RejectsWithoutChangingStateOrHistory()
    {
        // Arrange
        var ticket = CreateAssignedTicket();

        // Act
        Action act = () => StartWorkCore.Execute(ticket, Guid.NewGuid(), Guid.NewGuid(), CreatedAt.AddMinutes(10));

        // Assert
        act.Should().Throw<StartWorkException>().Which.Failure.Should().Be(StartWorkFailureKind.TicketAssignedToAnotherEmployee);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.History.Should().HaveCount(2);
    }

    [Fact]
    public void Execute_WhenWorkWasAlreadyStarted_RejectsWithoutAppendingAnotherHistoryEntry()
    {
        // Arrange
        var ticket = CreateAssignedTicket();
        StartWorkCore.Execute(ticket, EmployeeUserId, Guid.NewGuid(), CreatedAt.AddMinutes(10));

        // Act
        Action act = () => StartWorkCore.Execute(ticket, EmployeeUserId, Guid.NewGuid(), CreatedAt.AddMinutes(11));

        // Assert
        act.Should().Throw<StartWorkException>().Which.Failure.Should().Be(StartWorkFailureKind.TicketNotOpen);
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.History.Should().HaveCount(3);
    }

    [Fact]
    public void Execute_WithMissingTicket_Rejects()
    {
        // Arrange
        Ticket? ticket = null;

        // Act
        Action act = () => StartWorkCore.Execute(ticket, EmployeeUserId, Guid.NewGuid(), CreatedAt);

        // Assert
        act.Should().Throw<StartWorkException>().Which.Failure.Should().Be(StartWorkFailureKind.TicketNotFound);
    }

    private static Ticket CreateAssignedTicket()
    {
        var ticket = CreateTicket();
        AssignTicketCore.Execute(ticket, EmployeeUserId, Guid.NewGuid(), CreatedAt.AddMinutes(5));
        return ticket;
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
        new CreateTicketFacts(true),
        CustomerUserId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        CreatedAt);
}
