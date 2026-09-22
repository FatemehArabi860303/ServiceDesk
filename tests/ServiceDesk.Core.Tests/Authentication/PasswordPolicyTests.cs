using FluentAssertions;
using ServiceDesk.Core.Authentication;

namespace ServiceDesk.Core.Tests.Authentication;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void NormalizeAndValidate_WithNullPassword_ThrowsInvalidLength()
    {
        // Arrange
        const string? password = null;

        // Act
        Action act = () => PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        act.Should().Throw<PasswordPolicyException>().Which.Failure.Should().Be(PasswordPolicyFailureKind.InvalidLength);
    }

    [Fact]
    public void NormalizeAndValidate_WithFewerThanFifteenCodePoints_ThrowsInvalidLength()
    {
        // Arrange
        const string password = "12345678901234";

        // Act
        Action act = () => PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        act.Should().Throw<PasswordPolicyException>().Which.Failure.Should().Be(PasswordPolicyFailureKind.InvalidLength);
    }

    [Fact]
    public void NormalizeAndValidate_WithExactlyFifteenCodePoints_ReturnsPassword()
    {
        // Arrange
        const string password = "123456789012345";

        // Act
        var normalized = PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        normalized.Should().Be(password);
    }

    [Fact]
    public void NormalizeAndValidate_WithExactlyOneHundredTwentyEightCodePoints_ReturnsPassword()
    {
        // Arrange
        var password = new string('a', 128);

        // Act
        var normalized = PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        normalized.Should().Be(password);
    }

    [Fact]
    public void NormalizeAndValidate_WithMoreThanOneHundredTwentyEightCodePoints_ThrowsInvalidLength()
    {
        // Arrange
        var password = new string('a', 129);

        // Act
        Action act = () => PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        act.Should().Throw<PasswordPolicyException>().Which.Failure.Should().Be(PasswordPolicyFailureKind.InvalidLength);
    }

    [Fact]
    public void NormalizeAndValidate_CountsSupplementaryUnicodeCharactersAsOneCodePoint()
    {
        // Arrange
        var password = string.Concat(Enumerable.Repeat("😀", 15));

        // Act
        var normalized = PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        normalized.Should().Be(password);
    }

    [Fact]
    public void NormalizeAndValidate_NormalizesToNfc()
    {
        // Arrange
        const string password = "e\u0301abcdefghijklmn";

        // Act
        var normalized = PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        normalized.Should().Be("éabcdefghijklmn");
    }

    [Fact]
    public void NormalizeAndValidate_PreservesWhitespace()
    {
        // Arrange
        const string password = " password value  ";

        // Act
        var normalized = PasswordPolicy.NormalizeAndValidate(password);

        // Assert
        normalized.Should().Be(password);
    }
}
