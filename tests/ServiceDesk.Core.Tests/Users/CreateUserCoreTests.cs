using FluentAssertions;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Core.Tests.Users;

public sealed class CreateUserCoreTests
{
    private static readonly Guid UserId = Guid.Parse("d20e8c3f-5e61-4b9e-93f1-1b3647143f62");
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_WithValidCommand_CreatesActiveUserFromSuppliedValues()
    {
        var user = Execute(new CreateUserCommand(" Ada ", " Lovelace ", " Ada@Example.com ", UserRole.Customer));

        user.Should().Be(new User(UserId, "Ada", "Lovelace", "ADA@EXAMPLE.COM", UserRole.Customer, true, Now, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_WithInvalidFirstName_Throws(string? firstName)
    {
        var act = () => Execute(new CreateUserCommand(firstName, "Lovelace", "ada@example.com", UserRole.Customer));

        act.Should().Throw<CreateUserException>().Which.Failure.Should().Be(CreateUserFailureKind.InvalidFirstName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_WithInvalidLastName_Throws(string? lastName)
    {
        var act = () => Execute(new CreateUserCommand("Ada", lastName, "ada@example.com", UserRole.Customer));

        act.Should().Throw<CreateUserException>().Which.Failure.Should().Be(CreateUserFailureKind.InvalidLastName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("ada@@example.com")]
    [InlineData("ada@example")]
    public void Execute_WithInvalidEmail_Throws(string? email)
    {
        var act = () => Execute(new CreateUserCommand("Ada", "Lovelace", email, UserRole.Customer));

        act.Should().Throw<CreateUserException>().Which.Failure.Should().Be(CreateUserFailureKind.InvalidEmail);
    }

    [Fact]
    public void Execute_WhenEmailIsUnavailable_Throws()
    {
        var act = () => Execute(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer), new CreateUserFacts(false));

        act.Should().Throw<CreateUserException>().Which.Failure.Should().Be(CreateUserFailureKind.EmailUnavailable);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public void Execute_WithSupportedRole_CreatesUser(UserRole role)
    {
        var user = Execute(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", role));

        user.Role.Should().Be(role);
    }

    [Fact]
    public void Execute_WithUnsupportedRole_Throws()
    {
        var act = () => Execute(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", (UserRole)99));

        act.Should().Throw<CreateUserException>().Which.Failure.Should().Be(CreateUserFailureKind.UnsupportedRole);
    }

    [Fact]
    public void CanonicalizeEmail_TrimsAndUsesInvariantUpperCase()
    {
        CreateUserCore.CanonicalizeEmail(" Ada@Example.com ").Should().Be("ADA@EXAMPLE.COM");
    }

    private static User Execute(CreateUserCommand command, CreateUserFacts? facts = null) =>
        CreateUserCore.Execute(command, facts ?? new CreateUserFacts(true), UserId, Now);
}
