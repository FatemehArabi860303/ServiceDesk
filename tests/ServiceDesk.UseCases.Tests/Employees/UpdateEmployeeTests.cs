using FluentAssertions; using NSubstitute; using ServiceDesk.Domain.Entities; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Employees; using ServiceDesk.UseCases.Employees.Models; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Employees;
public sealed class UpdateEmployeeTests { [Fact] public async Task UpdateEmployee_WhenEmailBelongsToAnotherEmployee_ShouldThrowConflict() { // Arrange
    var employee = TestFixtures.Employee(); var otherEmployee = TestFixtures.Employee(); var repository = Substitute.For<IEmployeeRepository>(); repository.GetByIdAsync(employee.Id, Arg.Any<CancellationToken>()).Returns(employee); repository.GetByEmailAsync("other@example.test", Arg.Any<CancellationToken>()).Returns(otherEmployee); var subject = new UpdateEmployee(repository);
    // Act
    Func<Task> act = () => subject.ExecuteAsync(employee.Id, new UpdateEmployeeRequest("Alex", "Agent", "other@example.test"), TestFixtures.Admin);
    // Assert
    await act.Should().ThrowAsync<ConflictException>(); await repository.DidNotReceive().UpdateAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>()); } }
