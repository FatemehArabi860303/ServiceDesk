using FluentAssertions;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Domain.Tests;

public sealed class TicketBehaviorTests
{
    [Fact]
    public void Create_WhenNoEmployeeIsAssigned_ShouldBeUnassigned()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Act
        var ticket = Ticket.Create(customerId, "Network issue", "The network is intermittent.", TicketPriority.Medium, DomainTestData.Actor);

        // Assert
        ticket.AssignedEmployeeId.Should().BeNull();
    }

    [Fact]
    public void Assign_WhenEmployeeIsActive_ShouldAssignEmployeeAndAddHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var employee = DomainTestData.CreateEmployee();

        // Act
        ticket.Assign(employee, DomainTestData.Actor);

        // Assert
        ticket.AssignedEmployeeId.Should().Be(employee.Id);
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.Assigned);
        ticket.History.Last().PreviousValue.Should().BeNull();
        ticket.History.Last().NewValue.Should().Be(employee.Id.ToString());
    }

    [Fact]
    public void Assign_WhenEmployeeIsInactive_ShouldThrowAndNotAddHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var employee = DomainTestData.CreateEmployee();
        employee.Deactivate();
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.Assign(employee, DomainTestData.Actor);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.AssignedEmployeeId.Should().BeNull();
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void Reassign_WhenEmployeeIsActive_ShouldReplaceAssigneeAndAddHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var firstEmployee = DomainTestData.CreateEmployee();
        var secondEmployee = Employee.Create("Blair", "Agent", "blair.agent@example.test");
        ticket.Assign(firstEmployee, DomainTestData.Actor);

        // Act
        ticket.Reassign(secondEmployee, DomainTestData.Actor);

        // Assert
        ticket.AssignedEmployeeId.Should().Be(secondEmployee.Id);
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.Reassigned);
        ticket.History.Last().PreviousValue.Should().Be(firstEmployee.Id.ToString());
        ticket.History.Last().NewValue.Should().Be(secondEmployee.Id.ToString());
    }

    [Fact]
    public void Reassign_WhenEmployeeIsInactive_ShouldThrowAndLeaveAssigneeUnchanged()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        var assignedEmployee = DomainTestData.CreateEmployee();
        var inactiveEmployee = Employee.Create("Casey", "Agent", "casey.agent@example.test");
        inactiveEmployee.Deactivate();
        ticket.Assign(assignedEmployee, DomainTestData.Actor);
        var historyCount = ticket.History.Count;

        // Act
        Action act = () => ticket.Reassign(inactiveEmployee, DomainTestData.Actor);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        ticket.AssignedEmployeeId.Should().Be(assignedEmployee.Id);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void ChangePriority_WhenPriorityDiffers_ShouldUpdatePriorityAndRecordValues()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket(TicketPriority.Low);

        // Act
        ticket.ChangePriority(TicketPriority.Critical, DomainTestData.Actor);

        // Assert
        ticket.Priority.Should().Be(TicketPriority.Critical);
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.PriorityChanged);
        ticket.History.Last().PreviousValue.Should().Be(TicketPriority.Low.ToString());
        ticket.History.Last().NewValue.Should().Be(TicketPriority.Critical.ToString());
    }

    [Fact]
    public void ChangePriority_WhenPriorityIsUnchanged_ShouldNotAddHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket(TicketPriority.Medium);
        var historyCount = ticket.History.Count;

        // Act
        ticket.ChangePriority(TicketPriority.Medium, DomainTestData.Actor);

        // Assert
        ticket.Priority.Should().Be(TicketPriority.Medium);
        ticket.History.Should().HaveCount(historyCount);
    }

    [Fact]
    public void ChangeTitle_WhenValueDiffers_ShouldUpdateTitleAndAddHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();

        // Act
        ticket.ChangeTitle("Printer queue is stuck", DomainTestData.Actor);

        // Assert
        ticket.Title.Should().Be("Printer queue is stuck");
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.TitleChanged);
        ticket.History.Last().PreviousValue.Should().Be("Printer is unavailable");
        ticket.History.Last().NewValue.Should().Be("Printer queue is stuck");
    }

    [Fact]
    public void ChangeDescription_WhenValueDiffers_ShouldUpdateDescriptionAndAddHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();

        // Act
        ticket.ChangeDescription("The printer queue remains paused after restart.", DomainTestData.Actor);

        // Assert
        ticket.Description.Should().Be("The printer queue remains paused after restart.");
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.DescriptionChanged);
        ticket.History.Last().PreviousValue.Should().Be("The main office printer does not respond.");
    }

    [Fact]
    public void AddComment_WithValidComment_ShouldAppendAttributableHistory()
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();

        // Act
        ticket.AddComment("Restarted the print spooler.", DomainTestData.Actor);

        // Assert
        ticket.History.Last().Action.Should().Be(TicketHistoryAction.CommentAdded);
        ticket.History.Last().Description.Should().Be("Restarted the print spooler.");
        ticket.History.Last().ActorReference.Should().Be(DomainTestData.Actor);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("comment")]
    public void TicketContent_WhenRequiredValueIsBlank_ShouldThrow(string field)
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        // Act
        Action act = field switch
        {
            "title" => () => ticket.ChangeTitle(" ", DomainTestData.Actor),
            "description" => () => ticket.ChangeDescription(" ", DomainTestData.Actor),
            _ => () => ticket.AddComment(" ", DomainTestData.Actor)
        };

        // Assert
        act.Should().Throw<ArgumentException>();
        ticket.History.Should().ContainSingle();
    }

    [Theory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("comment")]
    public void TicketContent_WhenValueExceedsMaximumLength_ShouldThrow(string field)
    {
        // Arrange
        var ticket = DomainTestData.CreateTicket();
        // Act
        Action act = field switch
        {
            "title" => () => ticket.ChangeTitle(new string('a', Ticket.TitleMaxLength + 1), DomainTestData.Actor),
            "description" => () => ticket.ChangeDescription(new string('a', Ticket.DescriptionMaxLength + 1), DomainTestData.Actor),
            _ => () => ticket.AddComment(new string('a', Ticket.CommentMaxLength + 1), DomainTestData.Actor)
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
        ticket.History.Should().ContainSingle();
    }
}
