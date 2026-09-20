using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class GetTicketTests { [Fact] public async Task GetTicket_WhenAssignedEmployeeRequests_ShouldReturnPersistenceToken() { // Arrange
    var customer = TestFixtures.Customer(); var employee = TestFixtures.Employee(); var ticket = TestFixtures.Ticket(customer.Id); ticket.Assign(employee, "admin-1"); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "version-9")); var subject = new GetTicket(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new ActorContext("employee-1", ActorType.Employee, EmployeeId: employee.Id));
    // Assert
    result.ConcurrencyToken.Should().Be("version-9"); result.AssignedEmployeeId.Should().Be(employee.Id); }
    [Fact] public async Task GetTicket_WhenUnrelatedCustomerRequests_ShouldThrowForbidden() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket)); var subject = new GetTicket(repository);
    // Act
    Func<Task> act = () => subject.ExecuteAsync(ticket.Id, new ActorContext("customer-2", ActorType.Customer, Guid.NewGuid()));
    // Assert
    await act.Should().ThrowAsync<ForbiddenException>(); } }
