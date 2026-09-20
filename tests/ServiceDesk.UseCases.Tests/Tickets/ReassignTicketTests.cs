using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Entities; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class ReassignTicketTests { [Fact] public async Task ReassignTicket_WhenAdministrator_ShouldSetNewEmployee() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var firstEmployee = TestFixtures.Employee(); var secondEmployee = Employee.Create("Beth", "Agent", "beth@example.test"); ticket.Assign(firstEmployee, "admin-1"); var tickets = Substitute.For<ITicketRepository>(); var employees = Substitute.For<IEmployeeRepository>(); tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); employees.GetByIdAsync(secondEmployee.Id, Arg.Any<CancellationToken>()).Returns(secondEmployee); var subject = new ReassignTicket(tickets, employees);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new ReassignTicketRequest(secondEmployee.Id, "expected"), TestFixtures.Admin);
    // Assert
    result.AssignedEmployeeId.Should().Be(secondEmployee.Id); } }
