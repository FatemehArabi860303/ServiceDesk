using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class GetTicketHistoryTests { [Fact] public async Task GetTicketHistory_WhenOwnerRequests_ShouldMapHistory() { // Arrange
    var customer = TestFixtures.Customer(); var ticket = TestFixtures.Ticket(customer.Id); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket)); var subject = new GetTicketHistory(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new ActorContext("customer-1", ActorType.Customer, customer.Id));
    // Assert
    result.Should().ContainSingle(); result.Single().Action.Should().Be(TicketHistoryAction.TicketCreated); } }
