using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class ResolveTicketShellTests
{
    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public async Task ExecuteAsync_WithEligibleTicket_PersistsResolutionAndNotification(UserRole role)
    {
        // Arrange
        var ticket = CreateTicket();
        var actor = role == UserRole.Employee ? ticket.AssignedEmployeeUserId!.Value : Guid.NewGuid();
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryResolveAsync(ticket, Arg.Any<Guid>(), Arg.Any<TicketProgressNotification>(), Arg.Any<CancellationToken>()).Returns(true);
        var shell = new ResolveTicketShell(repository);

        // Act
        await shell.ExecuteAsync(ticket.Id, actor, role);

        // Assert
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.History.Last().ActorUserId.Should().Be(actor);
        await repository.Received(1).TryResolveAsync(ticket, ticket.History.Last().Id,
            Arg.Is<TicketProgressNotification>(n => n.EventId != Guid.Empty && n.Progressed.Kind == RequestProgressKind.Resolved
                && n.Progressed.CustomerUserId == ticket.CustomerUserId && n.Progressed.OccurredAt == ticket.UpdatedAt), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingTicket_DoesNotPersist()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var shell = new ResolveTicketShell(repository);
        var ticketId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        // Act
        Func<Task> act = () => shell.ExecuteAsync(ticketId, actor, UserRole.Employee);

        // Assert
        (await act.Should().ThrowAsync<ResolveTicketException>()).Which.Failure.Should().Be(ResolveTicketFailureKind.TicketNotFound);
        await repository.DidNotReceive().TryResolveAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<TicketProgressNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceLosesRace_ThrowsConflict()
    {
        // Arrange
        var ticket = CreateTicket();
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryResolveAsync(ticket, Arg.Any<Guid>(), Arg.Any<TicketProgressNotification>(), Arg.Any<CancellationToken>()).Returns(false);
        var shell = new ResolveTicketShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(ticket.Id, ticket.AssignedEmployeeUserId!.Value, UserRole.Employee);

        // Assert
        (await act.Should().ThrowAsync<ResolveTicketException>()).Which.Failure.Should().Be(ResolveTicketFailureKind.TicketNoLongerEligible);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnotherEmployee_DoesNotPersist()
    {
        // Arrange
        var ticket = CreateTicket();
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        var shell = new ResolveTicketShell(repository);
        var actor = Guid.NewGuid();

        // Act
        Func<Task> act = () => shell.ExecuteAsync(ticket.Id, actor, UserRole.Employee);

        // Assert
        (await act.Should().ThrowAsync<ResolveTicketException>()).Which.Failure.Should().Be(ResolveTicketFailureKind.ActorNotPermitted);
        await repository.DidNotReceive().TryResolveAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<TicketProgressNotification>(), Arg.Any<CancellationToken>());
    }

    private static Ticket CreateTicket()
    {
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var employee = Guid.NewGuid();
        AssignTicketCore.Execute(ticket, employee, Guid.NewGuid(), DateTimeOffset.UtcNow);
        StartWorkCore.Execute(ticket, employee, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return ticket;
    }
}
