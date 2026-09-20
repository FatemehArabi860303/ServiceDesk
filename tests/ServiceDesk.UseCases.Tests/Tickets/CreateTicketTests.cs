using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Entities; using ServiceDesk.Domain.Enums; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class CreateTicketTests { [Fact] public async Task CreateTicket_WhenOwningCustomer_ShouldPersistThenReloadToken() { // Arrange
    var customer = TestFixtures.Customer(); var repository = Substitute.For<ITicketRepository>(); var customers = Substitute.For<ICustomerRepository>(); customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer); repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => TestFixtures.Loaded(TestFixtures.Ticket(customer.Id), "version-2")); var subject = new CreateTicket(customers, repository);
    // Act
    var result = await subject.ExecuteAsync(new CreateTicketRequest(customer.Id, "Printer", "Unavailable", TicketPriority.High), new ActorContext("customer-1", ActorType.Customer, customer.Id));
    // Assert
    result.ConcurrencyToken.Should().Be("version-2"); await repository.Received(1).AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()); }
    [Fact] public async Task CreateTicket_WhenCustomerDoesNotOwnRequestedCustomer_ShouldThrowForbidden() { // Arrange
    var customers = Substitute.For<ICustomerRepository>(); var tickets = Substitute.For<ITicketRepository>(); var subject = new CreateTicket(customers, tickets);
    // Act
    Func<Task> act = () => subject.ExecuteAsync(new CreateTicketRequest(Guid.NewGuid(), "Printer", "Unavailable", TicketPriority.Medium), new ActorContext("customer-1", ActorType.Customer, Guid.NewGuid()));
    // Assert
    await act.Should().ThrowAsync<ForbiddenException>(); await tickets.DidNotReceive().AddAsync(Arg.Any<Ticket>(), Arg.Any<CancellationToken>()); } }
