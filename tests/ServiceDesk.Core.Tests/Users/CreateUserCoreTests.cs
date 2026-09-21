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
        var outcome = Execute(new CreateUserCommand(" Ada ", " Lovelace ", " Ada@Example.com ", UserRole.Customer));

        var created = outcome.Should().BeOfType<UserCreated>().Subject;
        created.User.Should().Be(new User(UserId, "Ada", "Lovelace", "ADA@EXAMPLE.COM", UserRole.Customer, true, Now, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_WithInvalidFirstName_Rejects(string? firstName)
    {
        Execute(new CreateUserCommand(firstName, "Lovelace", "ada@example.com", UserRole.Customer))
            .Should().Be(new UserCreationRejected(CreateUserFailureKind.InvalidFirstName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_WithInvalidLastName_Rejects(string? lastName)
    {
        Execute(new CreateUserCommand("Ada", lastName, "ada@example.com", UserRole.Customer))
            .Should().Be(new UserCreationRejected(CreateUserFailureKind.InvalidLastName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("ada@@example.com")]
    [InlineData("ada@example")]
    public void Execute_WithInvalidEmail_Rejects(string? email)
    {
        Execute(new CreateUserCommand("Ada", "Lovelace", email, UserRole.Customer))
            .Should().Be(new UserCreationRejected(CreateUserFailureKind.InvalidEmail));
    }

    [Fact]
    public void Execute_WhenEmailIsUnavailable_Rejects()
    {
        Execute(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", UserRole.Customer), new CreateUserFacts(false))
            .Should().Be(new UserCreationRejected(CreateUserFailureKind.EmailUnavailable));
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.Administrator)]
    public void Execute_WithSupportedRole_CreatesUser(UserRole role)
    {
        var outcome = Execute(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", role));

        outcome.Should().BeOfType<UserCreated>().Which.User.Role.Should().Be(role);
    }

    [Fact]
    public void Execute_WithUnsupportedRole_Rejects()
    {
        Execute(new CreateUserCommand("Ada", "Lovelace", "ada@example.com", (UserRole)99))
            .Should().Be(new UserCreationRejected(CreateUserFailureKind.UnsupportedRole));
    }

    [Fact]
    public void CanonicalizeEmail_TrimsAndUsesInvariantUpperCase()
    {
        CreateUserCore.CanonicalizeEmail(" Ada@Example.com ").Should().Be("ADA@EXAMPLE.COM");
    }

    private static CreateUserOutcome Execute(CreateUserCommand command, CreateUserFacts? facts = null) =>
        CreateUserCore.Execute(command, facts ?? new CreateUserFacts(true), UserId, Now);
}
