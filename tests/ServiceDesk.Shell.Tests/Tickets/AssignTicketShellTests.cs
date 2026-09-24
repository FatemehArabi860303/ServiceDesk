using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class AssignTicketShellTests
{
    private static readonly Guid TicketId = Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef");
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");
    private static readonly Guid EmployeeUserId = Guid.Parse("45b3108f-e171-476a-85a6-9a0f3155ef2f");

    [Fact]
    public async Task ExecuteAsync_WithAvailableTicket_PersistsSelfAssignment()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var ticket = CreateTicket();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryAssignAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var shell = new AssignTicketShell(repository);

        // Act
        await shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        ticket.AssignedEmployeeUserId.Should().Be(EmployeeUserId);
        ticket.History.Should().Contain(entry => entry.Action == TicketHistoryAction.Assigned
            && entry.ActorUserId == EmployeeUserId
            && entry.AssignedEmployeeUserId == EmployeeUserId);
        await repository.Received(1).TryAssignAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingTicket_DoesNotPersist()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
        var shell = new AssignTicketShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        (await act.Should().ThrowAsync<AssignTicketException>()).Which.Failure.Should().Be(AssignTicketFailureKind.TicketNotFound);
        await repository.DidNotReceive().TryAssignAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAtomicClaimLosesRace_ThrowsConflict()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var ticket = CreateTicket();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryAssignAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var shell = new AssignTicketShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        (await act.Should().ThrowAsync<AssignTicketException>()).Which.Failure.Should().Be(AssignTicketFailureKind.TicketNoLongerAvailable);
        await repository.Received(1).TryAssignAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
        new CreateTicketFacts(true),
        CustomerUserId,
        TicketId,
        Guid.Parse("4e5bb9c5-a15a-4d8b-a0e0-1f8624ecb01d"),
        new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero));
}
