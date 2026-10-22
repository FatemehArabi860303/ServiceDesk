using FluentAssertions;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tests.Tickets;

public sealed class EmployeeWorkCoreTests
{
    [Theory]
    [InlineData(UserRole.Employee, true, true)]
    [InlineData(UserRole.Employee, false, false)]
    [InlineData(UserRole.Customer, true, false)]
    [InlineData(UserRole.Administrator, true, false)]
    public void IsAuthorized_RequiresPersistedActiveEmployee(UserRole role, bool active, bool expected)
    {
        // Arrange
        User employee = new(Guid.NewGuid(), "Test", "Employee", "EMPLOYEE@EXAMPLE.INVALID", role,
            active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        // Act
        bool authorized = EmployeeWorkCore.IsAuthorized(employee);
        // Assert
        authorized.Should().Be(expected);
        EmployeeWorkCore.IsAuthorized(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Execute_ComposesExistingRulesForUnassignedOrOwnAssignedTicket(bool alreadyAssigned)
    {
        // Arrange
        Guid employeeId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Ticket ticket = CreateTicket();
        if (alreadyAssigned) AssignTicketCore.Execute(ticket, employeeId, Guid.NewGuid(), now);
        // Act
        IReadOnlyList<RequestProgressed> events = EmployeeWorkCore.Execute(ticket, employeeId, Guid.NewGuid(), Guid.NewGuid(), now);
        // Assert
        ticket.Status.Should().Be(TicketStatus.InProgress);
        ticket.AssignedEmployeeUserId.Should().Be(employeeId);
        ticket.History.Select(item => item.Action).Should().Equal(TicketHistoryAction.Created, TicketHistoryAction.Assigned, TicketHistoryAction.WorkStarted);
        events.Should().HaveCount(alreadyAssigned ? 1 : 2);
        EmployeeWorkCore.CanStart(ticket, employeeId).Should().BeFalse();
    }

    [Fact]
    public void Execute_RejectsAnotherEmployeesAssignmentWithoutMutation()
    {
        // Arrange
        Ticket ticket = CreateTicket();
        Guid owner = Guid.NewGuid();
        AssignTicketCore.Execute(ticket, owner, Guid.NewGuid(), DateTimeOffset.UtcNow);
        // Act
        Action act = () => EmployeeWorkCore.Execute(ticket, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        // Assert
        act.Should().Throw<StartWorkException>();
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.AssignedEmployeeUserId.Should().Be(owner);
        ticket.History.Should().HaveCount(2);
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand("Employee test", "Synthetic ticket", TicketPriority.Medium),
        new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
}
