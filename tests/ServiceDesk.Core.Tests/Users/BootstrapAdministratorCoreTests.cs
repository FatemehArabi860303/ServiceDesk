using FluentAssertions;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tests.Users;

public sealed class BootstrapAdministratorCoreTests
{
    private static readonly Guid UserId = Guid.Parse("0c7bc93d-030a-453e-ba2e-faf66b9be6f5");
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_WhenInstallationIsNotEmpty_Throws()
    {
        // Arrange
        var facts = new BootstrapAdministratorFacts(false, true);

        // Act
        Action act = () => Execute(facts: facts);

        // Assert
        act.Should().Throw<BootstrapAdministratorException>().Which.Failure.Should().Be(BootstrapAdministratorFailureKind.InstallationNotEmpty);
    }

    [Fact]
    public void Execute_WithInvalidFirstName_Throws()
    {
        // Arrange
        var command = new BootstrapAdministratorCommand(" ", "Lovelace", "ada@example.com");

        // Act
        Action act = () => Execute(command);

        // Assert
        act.Should().Throw<BootstrapAdministratorException>().Which.Failure.Should().Be(BootstrapAdministratorFailureKind.InvalidFirstName);
    }

    [Fact]
    public void Execute_WithInvalidLastName_Throws()
    {
        // Arrange
        var command = new BootstrapAdministratorCommand("Ada", " ", "ada@example.com");

        // Act
        Action act = () => Execute(command);

        // Assert
        act.Should().Throw<BootstrapAdministratorException>().Which.Failure.Should().Be(BootstrapAdministratorFailureKind.InvalidLastName);
    }

    [Fact]
    public void Execute_WithInvalidEmail_Throws()
    {
        // Arrange
        var command = new BootstrapAdministratorCommand("Ada", "Lovelace", "invalid-email");

        // Act
        Action act = () => Execute(command);

        // Assert
        act.Should().Throw<BootstrapAdministratorException>().Which.Failure.Should().Be(BootstrapAdministratorFailureKind.InvalidEmail);
    }

    [Fact]
    public void Execute_WhenEmailIsUnavailable_Throws()
    {
        // Arrange
        var facts = new BootstrapAdministratorFacts(true, false);

        // Act
        Action act = () => Execute(facts: facts);

        // Assert
        act.Should().Throw<BootstrapAdministratorException>().Which.Failure.Should().Be(BootstrapAdministratorFailureKind.EmailUnavailable);
    }

    [Fact]
    public void Execute_WithValidCommand_CreatesAnActiveAdministrator()
    {
        // Arrange
        var command = new BootstrapAdministratorCommand(" Ada ", " Lovelace ", " ada@example.com ");

        // Act
        var user = Execute(command);

        // Assert
        user.Should().Be(new User(UserId, "Ada", "Lovelace", "ADA@EXAMPLE.COM", UserRole.Administrator, true, Now, Now));
    }

    private static User Execute(
        BootstrapAdministratorCommand? command = null,
        BootstrapAdministratorFacts? facts = null) =>
        BootstrapAdministratorCore.Execute(
            command ?? new BootstrapAdministratorCommand("Ada", "Lovelace", "ada@example.com"),
            facts ?? new BootstrapAdministratorFacts(true, true),
            UserId,
            Now);
}
