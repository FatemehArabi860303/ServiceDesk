using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Entities; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Employees; using ServiceDesk.UseCases.Employees.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Employees;
public sealed class CreateEmployeeTests { [Fact] public async Task CreateEmployee_WhenAdministrator_ShouldPersistEmployee() { // Arrange
    var repository = Substitute.For<IEmployeeRepository>(); repository.GetByEmailAsync("alex@example.test", Arg.Any<CancellationToken>()).Returns((Employee?)null); var subject = new CreateEmployee(repository);
    // Act
    var result = await subject.ExecuteAsync(new CreateEmployeeRequest("Alex", "Agent", "alex@example.test"), TestFixtures.Admin);
    // Assert
    result.IsActive.Should().BeTrue(); await repository.Received(1).AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>()); } }
