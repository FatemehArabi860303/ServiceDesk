using FluentAssertions;
using System.Text.Json;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.LiveIntegration;

namespace ServiceDesk.LiveIntegration.Tests;

public sealed class VerificationSafetyTests
{
    [Theory]
    [InlineData(0, 0, 0, true)]
    [InlineData(1, 0, 0, false)]
    [InlineData(0, 1, 0, false)]
    [InlineData(0, 0, 1, false)]
    [InlineData(-1, 0, 0, false)]
    [InlineData(0, -1, 0, false)]
    [InlineData(0, 0, -1, false)]
    public void RealProviderPrecondition_RequiresObservedEmptyQueueAndNoConsumers(
        long ready, long unacknowledged, int consumers, bool expected)
    {
        VerificationRun.IsRealQueueReady(ready, unacknowledged, consumers).Should().Be(expected);
    }

    [Fact]
    public async Task ReportReplacement_PreservesCompleteReportWhileWindowsReaderTemporarilyHoldsFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "sdverify-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "report.json");
        string temporary = destination + ".tmp";
        try
        {
            await File.WriteAllTextAsync(destination, "old report");
            await File.WriteAllTextAsync(temporary, "new complete report");
            Task replacement;
            using (var reader = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                replacement = VerificationRun.ReplaceReportAsync(temporary, destination);
                if (OperatingSystem.IsWindows()) replacement.IsCompleted.Should().BeFalse();
            }
            await replacement.WaitAsync(TimeSpan.FromSeconds(5));

            (await File.ReadAllTextAsync(destination)).Should().Be("new complete report");
            File.Exists(temporary).Should().BeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Readiness_RetriesHttpTimeoutOnlyWhileStageDeadlineRemains(bool cancelled, bool expected)
    {
        using CancellationTokenSource cancellation = new();
        if (cancelled) cancellation.Cancel();

        RunEnvironment.IsTransientReadinessFailure(new TaskCanceledException(), cancellation.Token)
            .Should().Be(expected);
    }

    [Fact]
    public void Readiness_DoesNotRetryUnexpectedFailures()
    {
        RunEnvironment.IsTransientReadinessFailure(new InvalidOperationException(), CancellationToken.None)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("CUSTOMER@EXAMPLE.INVALID", true)]
    [InlineData("customer@example.invalid", true)]
    [InlineData("another@example.invalid", false)]
    public void MailpitRecipient_MatchesCanonicalizedApplicationEmail(string address, bool expected)
    {
        // Arrange
        using JsonDocument message = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            To = new[] { new { Address = address } }
        }));

        // Act
        bool matches = VerificationRun.HasRecipient(message.RootElement, "customer@example.invalid");

        // Assert
        matches.Should().Be(expected);
    }

    [Theory]
    [InlineData("ServiceDeskLocal")]
    [InlineData("sdverify../../shared")]
    [InlineData("sdverify")]
    public void Isolation_RejectsNonGeneratedProjectIdentities(string id)
    {
        // Arrange
        string root = Path.GetFullPath(".");
        string report = Path.Combine(root, "test-results", "LiveIntegration", id, "report.json");
        string compose = Path.Combine(root, "tests", "LiveIntegration", "compose.yaml");

        // Act
        Exception? failure = Record.Exception(() => RunIsolation.Validate(id, root, report, compose));

        // Assert
        failure.Should().BeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Isolation_RejectsUnownedReportOrComposePaths(bool unsafeReport)
    {
        // Arrange
        string id = "sdverify" + Guid.NewGuid().ToString("N");
        string root = Path.GetFullPath(".");
        string report = Path.Combine(root, "test-results", "LiveIntegration", id, "report.json");
        string compose = Path.Combine(root, "tests", "LiveIntegration", "compose.yaml");
        if (unsafeReport) report = Path.Combine(root, "developer-data.json");
        else compose = Path.Combine(root, "developer-compose.yaml");

        // Act
        Exception? failure = Record.Exception(() => RunIsolation.Validate(id, root, report, compose));

        // Assert
        failure.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task DeterministicAnalyzer_ReturnsCoreValidResultAndTracksTicketIdentity()
    {
        // Arrange
        ControlledAnalyzer analyzer = new();
        TicketAiAnalysisInput input = new(Guid.NewGuid(), "Verification", "Synthetic data");

        // Act
        TicketAiAnalysisResult result = await analyzer.AnalyzeAsync(input);
        Exception? validation = Record.Exception(() => ValidateTicketAiAnalysisCore.Execute(result));

        // Assert
        validation.Should().BeNull();
        result.Should().BeSameAs(ControlledAnalyzer.Result);
        analyzer.Invocations[input.TicketId].Should().Be(1);
    }

    [Fact]
    public async Task HeldAnalyzer_ReceivesCancellationAndCannotReturnSuccessfulResult()
    {
        // Arrange
        ControlledAnalyzer analyzer = new();
        analyzer.Reset("Hold");
        using CancellationTokenSource cancellation = new();
        TicketAiAnalysisInput input = new(Guid.NewGuid(), "Verification", "Synthetic data");

        // Act
        Task<TicketAiAnalysisResult> pending = analyzer.AnalyzeAsync(input, cancellation.Token);
        await analyzer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        bool completedEarly = pending.IsCompleted;
        cancellation.Cancel();
        Exception? failure = await Record.ExceptionAsync(() => pending);
        await analyzer.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        completedEarly.Should().BeFalse();
        failure.Should().BeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public void Report_StartsEveryScenarioAsNotRun()
    {
        // Arrange
        string id = "sdverify" + Guid.NewGuid().ToString("N");

        // Act
        RunReport report = new(id);

        // Assert
        report.Overall.Should().Be("NOT RUN");
        report.Scenarios.Should().OnlyContain(scenario => scenario.Status == "NOT RUN");
    }
}
