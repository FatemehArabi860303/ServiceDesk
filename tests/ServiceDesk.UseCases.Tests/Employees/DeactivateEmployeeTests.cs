using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Employees; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Employees;
public sealed class DeactivateEmployeeTests { [Fact] public async Task DeactivateEmployee_WhenAdministrator_ShouldPersistInactiveState() { // Arrange
    var employee = TestFixtures.Employee(); var repository = Substitute.For<IEmployeeRepository>(); repository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee); var subject = new DeactivateEmployee(repository);
    // Act
    var result = await subject.ExecuteAsync(employee.Id, TestFixtures.Admin);
    // Assert
    result.IsActive.Should().BeFalse(); await repository.Received(1).UpdateAsync(employee, Arg.Any<CancellationToken>()); } }
