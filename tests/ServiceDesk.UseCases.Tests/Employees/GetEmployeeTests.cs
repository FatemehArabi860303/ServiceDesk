using FluentAssertions; using NSubstitute; using ServiceDesk.UseCases.Abstractions.Persistence; using ServiceDesk.UseCases.Common; using ServiceDesk.UseCases.Employees; using ServiceDesk.UseCases.Tests.TestSupport;
namespace ServiceDesk.UseCases.Tests.Employees;
public sealed class GetEmployeeTests { [Fact] public async Task GetEmployee_WhenAnotherEmployeeRequests_ShouldThrowForbidden() { // Arrange
    var employee = TestFixtures.Employee(); var repository = Substitute.For<IEmployeeRepository>(); var subject = new GetEmployee(repository);
    // Act
    Func<Task> act = () => subject.ExecuteAsync(employee.Id, new ActorContext("employee-2", ActorType.Employee, EmployeeId: Guid.NewGuid()));
    // Assert
    await act.Should().ThrowAsync<ForbiddenException>(); } }
