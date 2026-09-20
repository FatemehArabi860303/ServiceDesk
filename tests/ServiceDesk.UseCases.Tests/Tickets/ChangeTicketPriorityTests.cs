using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class ChangeTicketPriorityTests { [Fact] public async Task ChangeTicketPriority_WhenAssignedEmployee_ShouldPersistNewPriority() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var employee = TestFixtures.Employee(); ticket.Assign(employee, "admin-1"); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); var subject = new ChangeTicketPriority(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new ChangeTicketPriorityRequest(TicketPriority.Critical, "expected"), new ActorContext("employee-1", ActorType.Employee, EmployeeId: employee.Id));
    // Assert
    result.Priority.Should().Be(TicketPriority.Critical); } }
