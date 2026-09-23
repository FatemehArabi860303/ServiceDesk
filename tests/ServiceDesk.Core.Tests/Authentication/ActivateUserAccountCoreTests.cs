using FluentAssertions;
using ServiceDesk.Core.Authentication;

namespace ServiceDesk.Core.Tests.Authentication;

public sealed class ActivateUserAccountCoreTests
{
    [Fact]
    public void Execute_WithEligibleFacts_Succeeds()
    {
        // Arrange
        var facts = new ActivateUserAccountFacts(true, true, true, true, false);

        // Act
        var act = () => ActivateUserAccountCore.Execute(facts);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void Execute_WithIneligibleFacts_ThrowsGenericFailure(
        bool provisionFound,
        bool provisionNotExpired,
        bool userExists,
        bool userActive,
        bool userAlreadyCredentialed)
    {
        // Arrange
        var facts = new ActivateUserAccountFacts(
            provisionFound,
            provisionNotExpired,
            userExists,
            userActive,
            userAlreadyCredentialed);

        // Act
        var act = () => ActivateUserAccountCore.Execute(facts);

        // Assert
        act.Should().Throw<ActivateUserAccountException>().Which.Failure.Should()
            .Be(ActivateUserAccountFailureKind.ActivationNotPermitted);
    }
}
