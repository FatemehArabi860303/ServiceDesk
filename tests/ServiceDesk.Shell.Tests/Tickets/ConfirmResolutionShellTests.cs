using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class ConfirmResolutionShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithOwningCustomer_PersistsClosure()
    {
        // Arrange
        var ticket = CreateTicket();
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryConfirmResolutionAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        var shell = new ConfirmResolutionShell(repository);
        using var cancellation = new CancellationTokenSource();

        // Act
        await shell.ExecuteAsync(ticket.Id, ticket.CustomerUserId, UserRole.Customer, cancellation.Token);

        // Assert
        ticket.Status.Should().Be(TicketStatus.Closed);
        ticket.History.Last().ActorUserId.Should().Be(ticket.CustomerUserId);
        ticket.ClosedAt.Should().Be(ticket.UpdatedAt);
        ticket.History.Last().OccurredAt.Should().Be(ticket.UpdatedAt);
        await repository.Received(1).GetByIdAsync(ticket.Id, cancellation.Token);
        await repository.Received(1).TryConfirmResolutionAsync(ticket, ticket.History.Last().Id, cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingTicket_DoesNotPersist()
    {
        // Arrange
        var repository = Substitute.For<ITicketRepository>();
        var shell = new ConfirmResolutionShell(repository);
        var ticketId = Guid.NewGuid();
        var actor = Guid.NewGuid();

        // Act
        Func<Task> act = () => shell.ExecuteAsync(ticketId, actor, UserRole.Customer);

        // Assert
        (await act.Should().ThrowAsync<ConfirmResolutionException>()).Which.Failure.Should().Be(ConfirmResolutionFailureKind.TicketNotFound);
        await repository.DidNotReceive().TryConfirmResolutionAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceLosesRace_ThrowsConflict()
    {
        // Arrange
        var ticket = CreateTicket();
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        repository.TryConfirmResolutionAsync(ticket, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
        var shell = new ConfirmResolutionShell(repository);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(ticket.Id, ticket.CustomerUserId, UserRole.Customer);

        // Assert
        (await act.Should().ThrowAsync<ConfirmResolutionException>()).Which.Failure.Should().Be(ConfirmResolutionFailureKind.TicketNoLongerEligible);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public async Task ExecuteAsync_WithForbiddenActor_DoesNotPersist(UserRole role)
    {
        // Arrange
        var ticket = CreateTicket();
        var repository = Substitute.For<ITicketRepository>();
        repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        var shell = new ConfirmResolutionShell(repository);
        var actor = Guid.NewGuid();

        // Act
        Func<Task> act = () => shell.ExecuteAsync(ticket.Id, actor, role);

        // Assert
        (await act.Should().ThrowAsync<ConfirmResolutionException>()).Which.Failure.Should().Be(ConfirmResolutionFailureKind.ActorNotPermitted);
        await repository.DidNotReceive().TryConfirmResolutionAsync(Arg.Any<Ticket>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static Ticket CreateTicket()
    {
        var now = DateTimeOffset.UtcNow;
        var ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN", "Cannot connect", TicketPriority.High),
            new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now);
        var employee = Guid.NewGuid();
        AssignTicketCore.Execute(ticket, employee, Guid.NewGuid(), now);
        StartWorkCore.Execute(ticket, employee, Guid.NewGuid(), now);
        ResolveTicketCore.Execute(ticket, employee, UserRole.Employee, Guid.NewGuid(), now);
        return ticket;
    }
}
