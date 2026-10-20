using FluentAssertions;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;

namespace ServiceDesk.Core.Tests.AiAnalysis;

public sealed class ValidateTicketAiAnalysisCoreTests
{
    [Fact]
    public void Execute_WithValidResult_DoesNotThrow()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult();

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Execute_WithNullResult_ThrowsMissingResult()
    {
        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(null);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingResult);
    }

    [Fact]
    public void Execute_WithBlankSummary_ThrowsMissingSummary()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with { Summary = " " };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingSummary);
    }

    [Fact]
    public void Execute_WithBlankCategory_ThrowsMissingSuggestedCategory()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with { SuggestedCategory = " " };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingSuggestedCategory);
    }

    [Fact]
    public void Execute_WithUndefinedPriority_ThrowsInvalidSuggestedPriority()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            SuggestedPriority = (TicketPriority)(-1)
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.InvalidSuggestedPriority);
    }

    [Fact]
    public void Execute_WithBlankPriorityReason_ThrowsMissingPriorityReason()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with { PriorityReason = " " };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingPriorityReason);
    }

    [Fact]
    public void Execute_WithNullSuggestions_ThrowsMissingTroubleshootingSuggestions()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            TroubleshootingSuggestions = null!
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingTroubleshootingSuggestions);
    }

    [Fact]
    public void Execute_WithMoreThanTwoSuggestions_ThrowsTooManyTroubleshootingSuggestions()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            TroubleshootingSuggestions = ["First", "Second", "Third"]
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.TooManyTroubleshootingSuggestions);
    }

    [Fact]
    public void Execute_WithBlankSuggestion_ThrowsInvalidTroubleshootingSuggestion()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            TroubleshootingSuggestions = ["Check the connection", " "]
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.InvalidTroubleshootingSuggestion);
    }

    [Fact]
    public void Execute_WithNullMissingInformation_ThrowsMissingInformationList()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            MissingInformation = null!
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingInformationList);
    }

    [Fact]
    public void Execute_WithBlankMissingInformationEntry_ThrowsInvalidMissingInformationEntry()
    {
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            MissingInformation = ["Which operating system are you using?", " "]
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.InvalidMissingInformationEntry);
    }

    [Fact]
    public void Execute_WithUndefinedConfidence_ThrowsInvalidConfidence()
    {    
        // Arrange
        TicketAiAnalysisResult someAiAnalysisResult = createValidResult() with
        {
            Confidence = (TicketAiAnalysisConfidence)(-1)
        };

        // Act
        Action act = () => ValidateTicketAiAnalysisCore.Execute(someAiAnalysisResult);

        // Assert
        act.Should().Throw<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.InvalidConfidence);
    }

    private static TicketAiAnalysisResult createValidResult()
    {
        return new TicketAiAnalysisResult(
            Summary: "The customer cannot connect to the VPN.",
            SuggestedCategory: "Connectivity",
            SuggestedPriority: TicketPriority.High,
            PriorityReason: "The issue prevents remote access.",
            TroubleshootingSuggestions: ["Check the network connection."],
            MissingInformation: ["Which VPN client version is installed?"],
            Confidence: TicketAiAnalysisConfidence.Medium);
    }
}