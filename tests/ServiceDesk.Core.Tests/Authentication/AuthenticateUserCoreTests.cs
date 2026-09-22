using FluentAssertions;
using ServiceDesk.Core.Authentication;

namespace ServiceDesk.Core.Tests.Authentication;

public sealed class AuthenticateUserCoreTests
{
    [Fact]
    public void Execute_WhenUserIsPermittedAndCredentialsAreValid_Succeeds()
    {
        // Arrange
        var facts = new AuthenticateUserFacts(true, true);

        // Act
        Action act = () => AuthenticateUserCore.Execute(facts);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Execute_WhenUserIsNotPermitted_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var facts = new AuthenticateUserFacts(false, true);

        // Act
        Action act = () => AuthenticateUserCore.Execute(facts);

        // Assert
        act.Should().Throw<AuthenticateUserException>().Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
    }

    [Fact]
    public void Execute_WhenCredentialsAreInvalid_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var facts = new AuthenticateUserFacts(true, false);

        // Act
        Action act = () => AuthenticateUserCore.Execute(facts);

        // Assert
        act.Should().Throw<AuthenticateUserException>().Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
    }

    [Fact]
    public void Execute_WhenUserIsNotPermittedAndCredentialsAreInvalid_ThrowsGenericAuthenticationFailure()
    {
        // Arrange
        var facts = new AuthenticateUserFacts(false, false);

        // Act
        Action act = () => AuthenticateUserCore.Execute(facts);

        // Assert
        act.Should().Throw<AuthenticateUserException>().Which.Failure.Should()
            .Be(AuthenticateUserFailureKind.AuthenticationFailed);
    }
}
