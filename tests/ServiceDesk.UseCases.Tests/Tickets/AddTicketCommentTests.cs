using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Entities; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class AddTicketCommentTests { [Fact] public async Task AddTicketComment_WhenUnrelatedEmployee_ShouldNotPersist() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket)); var subject = new AddTicketComment(repository);
    // Act
    Func<Task> act = () => subject.ExecuteAsync(ticket.Id, new AddTicketCommentRequest("Update", "version-1"), new ActorContext("employee-2", ActorType.Employee, EmployeeId: Guid.NewGuid()));
    // Assert
    await act.Should().ThrowAsync<ForbiddenException>(); await repository.DidNotReceive().UpdateAsync(Arg.Any<Ticket>(), Arg.Any<string>(), Arg.Any<CancellationToken>()); }
    [Fact] public async Task AddTicketComment_WhenCustomerOwner_ShouldReturnCreatedComment() { // Arrange
    var customer = TestFixtures.Customer(); var ticket = TestFixtures.Ticket(customer.Id); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); var subject = new AddTicketComment(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new AddTicketCommentRequest("I restarted it.", "expected"), new ActorContext("customer-1", ActorType.Customer, customer.Id));
    // Assert
    result.Action.Should().Be(TicketHistoryAction.CommentAdded); result.Description.Should().Be("I restarted it."); } }
