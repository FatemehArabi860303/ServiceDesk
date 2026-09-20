using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class UpdateTicketDetailsTests { [Fact] public async Task UpdateTicketDetails_WhenOwner_ShouldChangeBothValues() { // Arrange
    var customer = TestFixtures.Customer(); var ticket = TestFixtures.Ticket(customer.Id); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); var subject = new UpdateTicketDetails(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new UpdateTicketDetailsRequest("Scanner", "Scanner is unavailable.", "expected"), new ActorContext("customer-1", ActorType.Customer, customer.Id));
    // Assert
    result.Title.Should().Be("Scanner"); result.Description.Should().Be("Scanner is unavailable."); } }
