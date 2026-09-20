using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Tickets; using ServiceDesk.UseCases.Tickets.Models;
namespace ServiceDesk.UseCases.Tests.Tickets;
public sealed class SearchTicketsTests { [Fact] public async Task SearchTickets_WhenCustomerSuppliesOtherCustomerFilter_ShouldForceOwnCustomerScope() { // Arrange
    var ownCustomerId = Guid.NewGuid(); var repository = Substitute.For<ITicketRepository>(); repository.SearchAsync(Arg.Any<TicketSearchRequest>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<TicketSummaryDto>([], 1, 25, 0)); var subject = new SearchTickets(repository); var request = new TicketSearchRequest(Guid.NewGuid(), null, null, null, null);
    // Act
    await subject.ExecuteAsync(request, new ActorContext("customer-1", ActorType.Customer, ownCustomerId));
    // Assert
    await repository.Received(1).SearchAsync(Arg.Is<TicketSearchRequest>(x => x.CustomerId == ownCustomerId), Arg.Any<CancellationToken>()); }
    [Fact] public async Task SearchTickets_WhenEmployeeSuppliesOtherEmployeeFilter_ShouldForceOwnEmployeeScope() { // Arrange
    var ownEmployeeId = Guid.NewGuid(); var repository = Substitute.For<ITicketRepository>(); repository.SearchAsync(Arg.Any<TicketSearchRequest>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<TicketSummaryDto>([], 1, 25, 0)); var subject = new SearchTickets(repository);
    // Act
    await subject.ExecuteAsync(new TicketSearchRequest(null, Guid.NewGuid(), null, null, null), new ActorContext("employee-1", ActorType.Employee, EmployeeId: ownEmployeeId));
    // Assert
    await repository.Received(1).SearchAsync(Arg.Is<TicketSearchRequest>(x => x.AssignedEmployeeId == ownEmployeeId), Arg.Any<CancellationToken>()); } }
