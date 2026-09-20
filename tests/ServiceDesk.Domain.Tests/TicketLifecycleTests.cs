using FluentAssertions;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Domain.Tests;

public sealed class TicketLifecycleTests
{
    [Fact]
    public void Create_WithValidValues_ShouldStartOpenAndAddCreationHistory()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Act
        var ticket = Ticket.Create(customerId, "Printer is unavailable", "The main office printer does not respond.", TicketPriority.High, DomainTestData.Actor);

        // Assert
        ticket.CustomerId.Should().Be(customerId);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.Priority.Should().Be(TicketPriority.High);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Should().ContainSingle();
        ticket.History.Single().Action.Should().Be(TicketHistoryAction.TicketCreated);
        ticket.History.Single().TicketId.Should().Be(ticket.Id);
        ticket.History.Single().ActorReference.Should().Be(DomainTestData.Actor);
    }

    [Fact]
    public void Create_WhenCustomerIdIsEmpty_ShouldThrow()
    {
        // Arrange
        // Act
        Action act = () => Ticket.Create(Guid.Empty, "Printer is unavailable", "The main office printer does not respond.", TicketPriority.Medium, DomainTestData.Actor);

        // Assert
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.ParamName.Should().Be("customerId");
    }

    [Fact]
    public void Create_WhenPriorityIsUndefined_ShouldThrow()
    {
        // Arrange
        // Act
        Action act = () => Ticket.Create(Guid.NewGuid(), "Printer is unavailable", "The main office printer does not respond.", (TicketPriority)999, DomainTestData.Actor);

        // Assert
        var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("priority");
    }

    [Fact]
    public void StartWork_WhenTicketIsOpen_ShouldChangeStatusToInProgress()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();

        // Act
        ticket.StartWork(DomainTestData.Actor);

        // Assert
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.StatusChanged);
        ticket.History.Last().PreviousValue.Should().Be(TicketStatus.Open.ToString());
        ticket.History.Last().NewValue.Should().Be(TicketStatus.InProgress.ToString());
    }

    [Fact]
    public void Resolve_WhenTicketIsInProgress_ShouldChangeStatusToResolved()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        ticket.StartWork(DomainTestData.Actor);

        // Act
        ticket.Resolve(DomainTestData.Actor);

        // Assert
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Last().PreviousValue.Should().Be(TicketStatus.InProgress.ToString());
        ticket.History.Last().NewValue.Should().Be(TicketStatus.Resolved.ToString());
    }

    [Fact]
    public void ConfirmResolution_WhenTicketIsResolved_ShouldCloseTicketAndSetClosedAt()
    {
        // Arrange
        var ticket = CreateResolvedTicket();

        // Act
        ticket.ConfirmResolution("customer-17");

        // Assert
        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ClosedAt.Should().NotBeNull();
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.Closed);
        ticket.History.Last().ActorReference.Should().Be("customer-17");
    }

    [Fact]
    public void RejectResolution_WhenTicketIsResolved_ShouldReturnToInProgress()
    {
        // Arrange
        var ticket = CreateResolvedTicket();

        // Act
        ticket.RejectResolution("customer-17");

        // Assert
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.StatusChanged);
        ticket.History.Last().PreviousValue.Should().Be(TicketStatus.Resolved.ToString());
        ticket.History.Last().NewValue.Should().Be(TicketStatus.InProgress.ToString());
    }

    [Fact]
    public void Reopen_WhenTicketIsClosed_ShouldClearClosedAtAndReturnToInProgress()
    {
        // Arrange
        var ticket = CreateClosedTicket();
        ticket.ClosedAt.Should().NotBeNull();

        // Act
        ticket.Reopen("administrator-1");

        // Assert
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.Reopened);
    }

    [Fact]
    public void Resolve_WhenTicketIsOpen_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.Resolve(DomainTestData.Actor);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void ConfirmResolution_WhenTicketIsOpen_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.ConfirmResolution("customer-17");

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void ConfirmResolution_WhenTicketIsInProgress_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        ticket.StartWork(DomainTestData.Actor);
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.Close("customer-17");

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.ClosedAt.Should().BeNull();
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void StartWork_WhenTicketIsAlreadyInProgress_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        ticket.StartWork(DomainTestData.Actor);
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.StartWork(DomainTestData.Actor);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void StartWork_WhenTicketIsResolved_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = CreateResolvedTicket();
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.StartWork(DomainTestData.Actor);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void StartWork_WhenTicketIsClosed_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = CreateClosedTicket();
        var closedAt = ticket.ClosedAt;
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.StartWork(DomainTestData.Actor);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ClosedAt.Should().Be(closedAt);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void Reopen_WhenTicketIsNotClosed_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.Reopen("administrator-1");

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void RejectResolution_WhenTicketIsNotResolved_ShouldThrowAndLeaveTicketUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.RejectResolution("customer-17");

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.History.Should().HaveCount(historyCount);
    }

    private static Ticket CreateResolvedTicket()
    {
        var ticket = DomainTestData.CreateTicket();
        ticket.StartWork(DomainTestData.Actor);
        ticket.Resolve(DomainTestData.Actor);
        return ticket;
    }

    private static Ticket CreateClosedTicket()
    {
        var ticket = CreateResolvedTicket();
        ticket.ConfirmResolution("customer-17");
        return ticket;
    }
}
