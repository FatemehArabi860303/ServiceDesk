using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class ReopenTicketTests { [Fact] public async Task ReopenTicket_WhenAdministrator_ShouldReopenClosedTicket() { // Arrange
    var ticket = TestFixtures.Ticket(Guid.NewGuid()); ticket.StartWork("employee-1"); ticket.Resolve("employee-1"); ticket.Close("customer-1"); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); var subject = new ReopenTicket(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new TicketMutationRequest("expected"), TestFixtures.Admin);
    // Assert
    result.Status.Should().Be(TicketStatus.InProgress); result.ClosedAt.Should().BeNull(); } }
