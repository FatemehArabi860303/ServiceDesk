using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class StartWorkTests { [Fact] public async Task StartWork_WhenAssignedEmployee_ShouldMutateAndPersist() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var employee = TestFixtures.Employee(); ticket.Assign(employee, "admin-1"); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "version-1"), TestFixtures.Loaded(ticket, "version-2")); var subject = new StartWork(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new TicketMutationRequest("version-1"), new ActorContext("employee-1", ActorType.Employee, EmployeeId: employee.Id));
    // Assert
    result.Status.Should().Be(TicketStatus.InProgress); await repository.Received(1).UpdateAsync(ticket, "version-1", Arg.Any<CancellationToken>()); }
    [Fact] public async Task StartWork_WhenConcurrencyTokenIsBlank_ShouldRejectBeforeRepositoryAccess() { // Arrange
    var repository = Substitute.For<ITicketRepository>(); var subject = new StartWork(repository);
    // Act
    Func<Task> act = () => subject.ExecuteAsync(Guid.NewGuid(), new TicketMutationRequest(" "), TestFixtures.Admin);
    // Assert
    await act.Should().ThrowAsync<ArgumentException>(); await repository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()); } }
