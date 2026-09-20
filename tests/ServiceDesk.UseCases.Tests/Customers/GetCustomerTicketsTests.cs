using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Customers; using ServiceDesk.UseCases.Tickets.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Customers;
public sealed class GetCustomerTicketsTests { [Fact] public async Task GetCustomerTickets_WhenEmployeeRequestsExistingCustomer_ShouldReturnRepositoryPage() { // Arrange
    var customer = TestFixtures.Customer(); var expected = new PagedResult<TicketSummaryDto>([], 1, 25, 0); var customers = Substitute.For<ICustomerRepository>(); var tickets = Substitute.For<ITicketRepository>(); customers.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer); tickets.SearchAsync(Arg.Any<TicketSearchRequest>(), Arg.Any<CancellationToken>()).Returns(expected); var subject = new GetCustomerTickets(customers, tickets);
    // Act
    var result = await subject.ExecuteAsync(customer.Id, 1, 25, new ActorContext("employee-1", ActorType.Employee));
    // Assert
    result.Should().BeSameAs(expected); await tickets.Received(1).SearchAsync(Arg.Is<TicketSearchRequest>(x => x.CustomerId == customer.Id), Arg.Any<CancellationToken>()); } }
