using FluentAssertions;
using ServiceDesk.Core.Authentication;

namespace ServiceDesk.Core.Tests.Authentication;

public sealed class ProvisionUserAccessCoreTests
{
    [Fact]
    public void Execute_WithEligibleCallerAndTarget_Succeeds()
    {
        // Arrange
        var facts = new ProvisionUserAccessFacts(true, true, true, false);

        // Act
        var act = () => ProvisionUserAccessCore.Execute(facts);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Execute_WithCallerNotPermitted_ThrowsCallerNotPermitted()
    {
        // Arrange
        var facts = new ProvisionUserAccessFacts(false, true, true, false);

        // Act
        var act = () => ProvisionUserAccessCore.Execute(facts);

        // Assert
        act.Should().Throw<ProvisionUserAccessException>().Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.CallerNotPermitted);
    }

    [Fact]
    public void Execute_WithMissingTarget_ThrowsTargetNotFound()
    {
        // Arrange
        var facts = new ProvisionUserAccessFacts(true, false, false, false);

        // Act
        var act = () => ProvisionUserAccessCore.Execute(facts);

        // Assert
        act.Should().Throw<ProvisionUserAccessException>().Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.TargetNotFound);
    }

    [Fact]
    public void Execute_WithInactiveTarget_ThrowsTargetInactive()
    {
        // Arrange
        var facts = new ProvisionUserAccessFacts(true, true, false, false);

        // Act
        var act = () => ProvisionUserAccessCore.Execute(facts);

        // Assert
        act.Should().Throw<ProvisionUserAccessException>().Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.TargetInactive);
    }

    [Fact]
    public void Execute_WithCredentialedTarget_ThrowsTargetAlreadyCredentialed()
    {
        // Arrange
        var facts = new ProvisionUserAccessFacts(true, true, true, true);

        // Act
        var act = () => ProvisionUserAccessCore.Execute(facts);

        // Assert
        act.Should().Throw<ProvisionUserAccessException>().Which.Failure.Should()
            .Be(ProvisionUserAccessFailureKind.TargetAlreadyCredentialed);
    }
}
