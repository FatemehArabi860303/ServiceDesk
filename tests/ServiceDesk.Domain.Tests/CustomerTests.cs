using FluentAssertions;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Domain.Tests;

public sealed class CustomerTests
{
    [Fact]
    public void Create_WithValidValues_ShouldCreateCustomerAndTrimContactValues()
    {
        // Arrange
        const string firstName = " Ada ";
        const string lastName = " Lovelace ";
        const string email = " ada@example.test ";
        const string phone = " +968 12345678 ";

        // Act
        var customer = Customer.Create(firstName, lastName, email, phone);

        // Assert
        customer.Id.Should().NotBeEmpty();
        customer.FirstName.Should().Be("Ada");
        customer.LastName.Should().Be("Lovelace");
        customer.Email.Should().Be("ada@example.test");
        customer.Phone.Should().Be("+968 12345678");
        customer.CreatedAt.Should().Be(customer.UpdatedAt);
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
            "firstName" => () => Customer.Create(" ", "Lovelace", "ada@example.test"),
            "lastName" => () => Customer.Create("Ada", " ", "ada@example.test"),
            _ => () => Customer.Create("Ada", "Lovelace", " ")
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("firstName")]
    [InlineData("lastName")]
    [InlineData("email")]
    [InlineData("phone")]
    public void Create_WhenValueExceedsMaximumLength_ShouldThrow(string field)
    {
        // Arrange
        // Act
        Action act = field switch
        {
            "firstName" => () => Customer.Create(new string('a', Customer.FirstNameMaxLength + 1), "Lovelace", "ada@example.test"),
            "lastName" => () => Customer.Create("Ada", new string('a', Customer.LastNameMaxLength + 1), "ada@example.test"),
            "email" => () => Customer.Create("Ada", "Lovelace", new string('a', Customer.EmailMaxLength + 1)),
            _ => () => Customer.Create("Ada", "Lovelace", "ada@example.test", new string('a', Customer.PhoneMaxLength + 1))
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateContactInformation_WithValidValues_ShouldUpdateMutableFieldsAndPreserveCreatedAt()
    {
        // Arrange
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.test", "+968 12345678");
        var createdAt = customer.CreatedAt;
        var originalUpdatedAt = customer.UpdatedAt;
        Thread.Sleep(5);

        // Act
        customer.UpdateContactInformation("Grace", "Hopper", "grace@example.test", null);

        // Assert
        customer.FirstName.Should().Be("Grace");
        customer.LastName.Should().Be("Hopper");
        customer.Email.Should().Be("grace@example.test");
        customer.Phone.Should().BeNull();
        customer.CreatedAt.Should().Be(createdAt);
        customer.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }
}
