using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Customers; using ServiceDesk.UseCases.Customers.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Customers;
public sealed class UpdateCustomerTests { [Fact] public async Task UpdateCustomer_WhenAdministrator_ShouldPersistDomainMutation() { // Arrange
    var customer = TestFixtures.Customer(); var repository = Substitute.For<ICustomerRepository>(); repository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(customer); var subject = new UpdateCustomer(repository);
    // Act
    var result = await subject.ExecuteAsync(customer.Id, new UpdateCustomerRequest("Grace", "Hopper", "grace@example.test", null), TestFixtures.Admin);
    // Assert
    result.FirstName.Should().Be("Grace"); await repository.Received(1).UpdateAsync(customer, Arg.Any<CancellationToken>()); } }
