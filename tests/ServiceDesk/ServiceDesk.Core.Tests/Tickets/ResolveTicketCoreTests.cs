using FluentAssertions;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tests.Tickets;

public sealed class ResolveTicketCoreTests
{
    private static readonly Guid EmployeeId = Guid.Parse("45b3108f-e171-476a-85a6-9a0f3155ef2f");
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public void Execute_WithPermittedActor_ResolvesAndProducesProgress(UserRole role)
    {
        // Arrange
        var ticket = CreateTicket();
        var actor = role == UserRole.Employee ? EmployeeId : Guid.NewGuid();
        var now = CreatedAt.AddHours(1);
        var historyId = Guid.NewGuid();

        // Act
        var progressed = ResolveTicketCore.Execute(ticket, actor, role, historyId, now);

        // Assert
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.AssignedEmployeeUserId.Should().Be(EmployeeId);
        ticket.CreatedAt.Should().Be(CreatedAt);
        ticket.UpdatedAt.Should().Be(now);
        ticket.History.Should().HaveCount(4);
        var history = ticket.History.Single(item => item.Action == TicketHistoryAction.Resolved);
        history.Id.Should().Be(historyId);
        history.ActorUserId.Should().Be(actor);
        history.OccurredAt.Should().Be(now);
        history.AssignedEmployeeUserId.Should().BeNull();
        progressed.Should().Be(new RequestProgressed(ticket.Id, ticket.CustomerUserId, RequestProgressKind.Resolved, now));
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Customer)]
    [InlineData((UserRole)99)]
    public void Execute_WithForbiddenActor_DoesNotChangeTicket(UserRole role)
    {
        // Arrange
        var ticket = CreateTicket();
        var originalTime = ticket.UpdatedAt;
        var actor = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var now = CreatedAt.AddHours(1);

        // Act
        Action act = () => ResolveTicketCore.Execute(ticket, actor, role, historyId, now);

        // Assert
        act.Should().Throw<ResolveTicketException>().Which.Failure.Should().Be(ResolveTicketFailureKind.ActorNotPermitted);
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.UpdatedAt.Should().Be(originalTime);
        ticket.History.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Execute_WithInvalidStatus_DoesNotChangeTicket(TicketStatus status)
    {
        // Arrange
        var ticket = CreateTicket();
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status);
        var now = CreatedAt.AddHours(1);
        var historyId = Guid.NewGuid();

        // Act
        Action act = () => ResolveTicketCore.Execute(ticket, EmployeeId, UserRole.Employee, historyId, now);

        // Assert
        act.Should().Throw<ResolveTicketException>().Which.Failure.Should().Be(ResolveTicketFailureKind.TicketNotInProgress);
        ticket.Status.Should().Be(status);
        ticket.History.Should().HaveCount(3);
    }

    [Fact]
    public void Execute_WithUnassignedInProgressTicket_RejectsEvenAdministrator()
    {
        // Arrange
        var ticket = CreateTicket();
        typeof(Ticket).GetProperty(nameof(Ticket.AssignedEmployeeUserId))!.SetValue(ticket, null);
        var historyId = Guid.NewGuid();

        // Act
        Action act = () => ResolveTicketCore.Execute(ticket, EmployeeId, UserRole.Administrator, historyId, CreatedAt);

        // Assert
        act.Should().Throw<ResolveTicketException>().Which.Failure.Should().Be(ResolveTicketFailureKind.TicketUnassigned);
        ticket.History.Should().HaveCount(3);
    }

    private static Ticket CreateTicket()
    {
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CreatedAt);
        AssignTicketCore.Execute(ticket, EmployeeId, Guid.NewGuid(), CreatedAt.AddMinutes(1));
        StartWorkCore.Execute(ticket, EmployeeId, Guid.NewGuid(), CreatedAt.AddMinutes(2));
        return ticket;
    }
}
