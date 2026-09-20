using FluentAssertions;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Domain.Tests;

public sealed class EmployeeTests
{
    [Fact]
    public void Create_WithValidValues_ShouldCreateActiveEmployeeAndTrimValues()
    {
        // Arrange
        const string firstName = " Alex ";
        const string lastName = " Agent ";
        const string email = " alex.agent@example.test ";

        // Act
        var employee = Employee.Create(firstName, lastName, email);

        // Assert
        employee.Id.Should().NotBeEmpty();
        employee.FirstName.Should().Be("Alex");
        employee.LastName.Should().Be("Agent");
        employee.Email.Should().Be("alex.agent@example.test");
        employee.IsActive.Should().BeTrue();
        employee.CreatedAt.Should().Be(employee.UpdatedAt);
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("email")]
    public void Create_WhenRequiredValueIsBlank_ShouldThrow(string field)
    {
        // Arrange
        // Act
        Action act = field switch
        {
            "firstName" => () => Employee.Create(" ", "Agent", "alex.agent@example.test"),
            "lastName" => () => Employee.Create("Alex", " ", "alex.agent@example.test"),
            _ => () => Employee.Create("Alex", "Agent", " ")
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("email")]
    public void Create_WhenValueExceedsMaximumLength_ShouldThrow(string field)
    {
        // Arrange
        // Act
        Action act = field switch
        {
            "firstName" => () => Employee.Create(new string('a', Employee.FirstNameMaxLength + 1), "Agent", "alex.agent@example.test"),
            "lastName" => () => Employee.Create("Alex", new string('a', Employee.LastNameMaxLength + 1), "alex.agent@example.test"),
            _ => () => Employee.Create("Alex", "Agent", new string('a', Employee.EmailMaxLength + 1))
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateProfile_WithValidValues_ShouldUpdateMutableFieldsAndPreserveCreatedAt()
    {
        // Arrange
        var employee = Employee.Create("Alex", "Agent", "alex.agent@example.test");
        var createdAt = employee.CreatedAt;
        var originalUpdatedAt = employee.UpdatedAt;
        Thread.Sleep(5);

        // Act
        employee.UpdateProfile("Blair", "Support", "blair.support@example.test");

        // Assert
        employee.FirstName.Should().Be("Blair");
        employee.LastName.Should().Be("Support");
        employee.Email.Should().Be("blair.support@example.test");
        employee.CreatedAt.Should().Be(createdAt);
        employee.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Deactivate_WhenEmployeeIsActive_ShouldSetInactiveAndUpdateTimestamp()
    {
        // Arrange
        var employee = DomainTestData.CreateEmployee();
        var originalUpdatedAt = employee.UpdatedAt;
        Thread.Sleep(5);

        // Act
        employee.Deactivate();

        // Assert
        employee.IsActive.Should().BeFalse();
        employee.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Activate_WhenEmployeeIsInactive_ShouldSetActive()
    {
        // Arrange
        var employee = DomainTestData.CreateEmployee();
        employee.Deactivate();

        // Act
        employee.Activate();

        // Assert
        employee.IsActive.Should().BeTrue();
    }
}
