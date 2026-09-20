using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Employees; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Employees;
public sealed class ActivateEmployeeTests { [Fact] public async Task ActivateEmployee_WhenAdministrator_ShouldActivateAndPersist() { // Arrange
    var employee = TestFixtures.Employee(); employee.Deactivate(); var repository = Substitute.For<IEmployeeRepository>(); repository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee); var subject = new ActivateEmployee(repository);
    // Act
    var result = await subject.ExecuteAsync(employee.Id, TestFixtures.Admin);
    // Assert
    result.IsActive.Should().BeTrue(); await repository.Received(1).UpdateAsync(employee, Arg.Any<CancellationToken>()); } }
