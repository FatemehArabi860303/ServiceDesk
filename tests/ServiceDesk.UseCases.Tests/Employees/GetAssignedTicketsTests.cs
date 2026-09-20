using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Employees; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Employees;
public sealed class GetAssignedTicketsTests { [Fact] public async Task GetAssignedTickets_WhenEmployeeRequestsOwnTickets_ShouldApplyEmployeeFilter() { // Arrange
    var employee = TestFixtures.Employee(); var employees = Substitute.For<IEmployeeRepository>(); var tickets = Substitute.For<ITicketRepository>(); employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee); tickets.SearchAsync(Arg.Any<TicketSearchRequest>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<TicketSummaryDto>([], 1, 25, 0)); var subject = new GetAssignedTickets(employees, tickets);
    // Act
    await subject.ExecuteAsync(employee.Id, 1, 25, new ActorContext("employee-1", ActorType.Employee, EmployeeId: employee.Id));
    // Assert
    await tickets.Received(1).SearchAsync(Arg.Is<TicketSearchRequest>(x => x.AssignedEmployeeId == employee.Id), Arg.Any<CancellationToken>()); } }
