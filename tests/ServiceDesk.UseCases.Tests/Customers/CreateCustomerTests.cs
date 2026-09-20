using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Entities; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Customers; using ServiceDesk.UseCases.Customers.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Customers;
public sealed class CreateCustomerTests
{
    [Fact] public async Task CreateCustomer_WhenAdministrator_ShouldPersistAndReturnDto() { // Arrange
        var repository = Substitute.For<ICustomerRepository>(); repository.GetByEmailAsync("ada@example.test", Arg.Any<CancellationToken>()).Returns((Customer?)null); var subject = new CreateCustomer(repository);
        // Act
        var result = await subject.ExecuteAsync(new CreateCustomerRequest("Ada", "Lovelace", "ada@example.test", null), TestFixtures.Admin);
        // Assert
        result.Email.Should().Be("ada@example.test"); await repository.Received(1).AddAsync(Arg.Is<Customer>(x => x.Email == result.Email), Arg.Any<CancellationToken>()); }
    [Fact] public async Task CreateCustomer_WhenEmailExists_ShouldThrowConflictAndNotPersist() { // Arrange
        var repository = Substitute.For<ICustomerRepository>(); repository.GetByEmailAsync("ada@example.test", Arg.Any<CancellationToken>()).Returns(TestFixtures.Customer()); var subject = new CreateCustomer(repository);
        // Act
        Func<Task> act = () => subject.ExecuteAsync(new CreateCustomerRequest("Ada", "Lovelace", "ada@example.test", null), TestFixtures.Admin);
        // Assert
        await act.Should().ThrowAsync<ConflictException>(); await repository.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()); }
}
