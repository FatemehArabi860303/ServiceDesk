using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class RejectResolutionTests { [Fact] public async Task RejectResolution_WhenOwningCustomer_ShouldReturnTicketToInProgress() { // Arrange
    var customer = TestFixtures.Customer(); var ticket = TestFixtures.Ticket(customer.Id); ticket.StartWork("employee-1"); ticket.Resolve("employee-1"); var repository = Substitute.For<ITicketRepository>(); repository.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(TestFixtures.Loaded(ticket, "old"), TestFixtures.Loaded(ticket, "new")); var subject = new RejectResolution(repository);
    // Act
    var result = await subject.ExecuteAsync(ticket.Id, new TicketMutationRequest("expected"), new ActorContext("customer-1", ActorType.Customer, customer.Id));
    // Assert
    result.Status.Should().Be(TicketStatus.InProgress); } }
