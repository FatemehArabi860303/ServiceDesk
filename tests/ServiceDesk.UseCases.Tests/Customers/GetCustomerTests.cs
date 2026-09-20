using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Customers; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Customers;
public sealed class GetCustomerTests { [Fact] public async Task GetCustomer_WhenCallerOwnsRecord_ShouldReturnMappedCustomer() { // Arrange
    var customer = TestFixtures.Customer(); var repository = Substitute.For<ICustomerRepository>(); repository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer); var subject = new GetCustomer(repository);
    // Act
    var result = await subject.ExecuteAsync(customer.Id, new ActorContext("customer-1", ActorType.Customer, customer.Id));
    // Assert
    result.Id.Should().Be(customer.Id); result.Email.Should().Be(customer.Email); } }
