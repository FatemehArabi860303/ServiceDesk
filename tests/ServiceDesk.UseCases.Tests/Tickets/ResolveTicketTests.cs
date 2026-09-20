using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class ResolveTicketTests { [Fact] public async Task ResolveTicket_WhenAssignedEmployee_ShouldResolveAndUseExpectedToken() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var employee = TestFixtures.Employee(); ticket.Assign(employee, "admin-1"); ticket.StartWork("employee-1"); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); var subject = new ResolveTicket(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new TicketMutationRequest("expected"), new ActorContext("employee-1", ActorType.Employee, EmployeeId: employee.Id));
    // Assert
    result.Status.Should().Be(TicketStatus.Resolved); await repository.Received(1).UpdateAsync(ticket, "expected", Arg.Any<CancellationToken>()); } }
