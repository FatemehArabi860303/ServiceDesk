using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class AssignTicketTests { [Fact] public async Task AssignTicket_WhenAdministrator_ShouldPassClientTokenAndReload() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var employee = TestFixtures.Employee(); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "version-1"), TestFixtures.Loaded(ticket, "version-2")); var employees = Substitute.For<IEmployeeRepository>(); employees.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee); var subject = new AssignTicket(repository, employees);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new AssignTicketRequest(employee.Id, "client-version"), TestFixtures.Admin);
    // Assert
    result.ConcurrencyToken.Should().Be("version-2"); await repository.Received(1).UpdateAsync(ticket, "client-version", Arg.Any<CancellationToken>()); } }
