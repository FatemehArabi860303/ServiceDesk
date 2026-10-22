using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Notifications;
using ServiceDesk.Shell.Tickets;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Tests.Tickets;

public sealed class EmployeeWorkShellTests
{
    [Fact]
    public async Task StartAsync_WhenConditionalWriteLosesRace_ReturnsConflict()
    {
        // Arrange
        IUserRepository users = Substitute.For<IUserRepository>();
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        IEmployeeWorkRepository workspace = Substitute.For<IEmployeeWorkRepository>();
        Guid employeeId = Guid.NewGuid();
        User employee = new(employeeId, "Test", "Employee", "EMPLOYEE@EXAMPLE.INVALID", UserRole.Employee,
            true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        users.GetByIdAsync(employeeId, Arg.Any<CancellationToken>()).Returns(employee);
        Ticket ticket = CreateTicket();
        tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        EmployeeWorkShell shell = new(users, tickets, workspace);
        // Act
        Func<Task> act = () => shell.StartAsync(employeeId, ticket.Id);
        // Assert
        (await act.Should().ThrowAsync<StartWorkException>()).Which.Failure.Should().Be(StartWorkFailureKind.TicketNoLongerEligible);
        await workspace.Received(1).TryClaimAndStartAsync(ticket, null,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 2),
            Arg.Is<IReadOnlyList<TicketProgressNotification>>(items => items.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenEmployeeMissing_DoesNotReadOrMutateTicket()
    {
        // Arrange
        IUserRepository users = Substitute.For<IUserRepository>();
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        IEmployeeWorkRepository workspace = Substitute.For<IEmployeeWorkRepository>();
        EmployeeWorkShell shell = new(users, tickets, workspace);
        // Act
        Func<Task> act = () => shell.StartAsync(Guid.NewGuid(), Guid.NewGuid());
        // Assert
        await act.Should().ThrowAsync<EmployeeWorkForbiddenException>();
        await tickets.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand("Employee test", "Synthetic ticket", TicketPriority.Medium),
        new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
}
