using FluentAssertions;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tests.Tickets;

public sealed class ConfirmResolutionCoreTests
{
    [Fact]
    public void Execute_WithOwningCustomer_ClosesAndPreservesIdentity()
    {
        // Arrange
        var ticket = CreateResolvedTicket();
        var createdAt = ticket.CreatedAt;
        var customer = ticket.CustomerUserId;
        var employee = ticket.AssignedEmployeeUserId;
        var now = createdAt.AddHours(1);
        var historyId = Guid.NewGuid();

        // Act
        ConfirmResolutionCore.Execute(ticket, customer, UserRole.Customer, historyId, now);

        // Assert
        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.ClosedAt.Should().Be(now);
        ticket.UpdatedAt.Should().Be(now);
        ticket.CreatedAt.Should().Be(createdAt);
        ticket.CustomerUserId.Should().Be(customer);
        ticket.AssignedEmployeeUserId.Should().Be(employee);
        ticket.History.Should().HaveCount(5);
        ticket.History.Single(h => h.Action == TicketHistoryAction.Closed).Should()
            .Be(new TicketHistory(historyId, ticket.Id, customer, TicketHistoryAction.Closed, now));
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public void Execute_WithForbiddenActor_DoesNotChangeTicket(UserRole role)
    {
        // Arrange
        var ticket = CreateResolvedTicket();
        var actor = role == UserRole.Customer ? Guid.NewGuid() : ticket.CustomerUserId;
        var originalTime = ticket.UpdatedAt;
        var historyId = Guid.NewGuid();
        var now = originalTime.AddHours(1);

        // Act
        Action act = () => ConfirmResolutionCore.Execute(ticket, actor, role, historyId, now);

        // Assert
        act.Should().Throw<ConfirmResolutionException>().Which.Failure.Should().Be(ConfirmResolutionFailureKind.ActorNotPermitted);
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.ClosedAt.Should().BeNull();
        ticket.UpdatedAt.Should().Be(originalTime);
        ticket.History.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed)]
    public void Execute_WithInvalidStatus_DoesNotChangeTicket(TicketStatus status)
    {
        // Arrange
        var ticket = CreateResolvedTicket();
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status);
        var now = ticket.UpdatedAt.AddHours(1);
        var historyId = Guid.NewGuid();

        // Act
        Action act = () => ConfirmResolutionCore.Execute(ticket, ticket.CustomerUserId, UserRole.Customer, historyId, now);

        // Assert
        act.Should().Throw<ConfirmResolutionException>().Which.Failure.Should().Be(ConfirmResolutionFailureKind.TicketNotResolved);
        ticket.Status.Should().Be(status);
        ticket.History.Should().HaveCount(4);
    }

    [Fact]
    public void Execute_WithMissingTicket_Rejects()
    {
        // Arrange
        var actor = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        Action act = () => ConfirmResolutionCore.Execute(null, actor, UserRole.Customer, historyId, now);

        // Assert
        act.Should().Throw<ConfirmResolutionException>().Which.Failure.Should().Be(ConfirmResolutionFailureKind.TicketNotFound);
    }

    private static Ticket CreateResolvedTicket()
    {
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        var employee = Guid.NewGuid();
        AssignTicketCore.Execute(ticket, employee, Guid.NewGuid(), now);
        StartWorkCore.Execute(ticket, employee, Guid.NewGuid(), now);
        ResolveTicketCore.Execute(ticket, employee, UserRole.Employee, Guid.NewGuid(), now);
        return ticket;
    }
}
