using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class StartWorkShellTests
{
    private static readonly Guid TicketId = Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef");
    private static readonly Guid CustomerUserId = Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de");
    private static readonly Guid EmployeeUserId = Guid.Parse("45b3108f-e171-476a-85a6-9a0f3155ef2f");

    [Fact]
    public async Task ExecuteAsync_WithOpenTicketAssignedToEmployee_PersistsStartedWork()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var ticket = CreateAssignedTicket();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryStartWorkAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var shell = new StartWorkShell(repository);

        // Act
        await shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.AssignedEmployeeUserId.Should().Be(EmployeeUserId);
        ticket.History.Should().Contain(entry => entry.Action == TicketHistoryAction.WorkStarted
            && entry.ActorUserId == EmployeeUserId
            && entry.AssignedEmployeeUserId == null);
        await repository.Received(1).TryStartWorkAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingTicket_DoesNotPersist()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns((Ticket?)null);
        var shell = new StartWorkShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        (await act.Should().ThrowAsync<StartWorkException>()).Which.Failure.Should().Be(StartWorkFailureKind.TicketNotFound);
        await repository.DidNotReceive().TryStartWorkAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTicketIsNotEligible_DoesNotPersist()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var ticket = CreateTicket();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns(ticket);
        var shell = new StartWorkShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        (await act.Should().ThrowAsync<StartWorkException>()).Which.Failure.Should().Be(StartWorkFailureKind.TicketUnassigned);
        await repository.DidNotReceive().TryStartWorkAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAtomicPersistenceLosesRace_ThrowsConflict()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var ticket = CreateAssignedTicket();
        repository.GetByIdAsync(TicketId, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryStartWorkAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var shell = new StartWorkShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(TicketId, EmployeeUserId);

        // Assert
        (await act.Should().ThrowAsync<StartWorkException>()).Which.Failure.Should().Be(StartWorkFailureKind.TicketNoLongerEligible);
        await repository.Received(1).TryStartWorkAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Ticket CreateAssignedTicket()
    {
        var ticket = CreateTicket();
        AssignTicketCore.Execute(ticket, EmployeeUserId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        return ticket;
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand("Cannot access VPN", "The VPN rejects my credentials.", TicketPriority.High),
        new CreateTicketFacts(true),
        CustomerUserId,
        TicketId,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
