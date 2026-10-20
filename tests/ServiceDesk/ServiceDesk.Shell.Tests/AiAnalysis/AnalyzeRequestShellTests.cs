using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.AiAnalysis;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.AiAnalysis;

public sealed class AnalyzeRequestShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithPersistedTicket_AnalyzesSavedContentAndReturnsValidatedResult()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Ticket ticket = CreateTicket();
        TicketAiAnalysisResult expectedResult = CreateValidResult();

        using CancellationTokenSource cancellationSource = new();
        CancellationToken cancellationToken = cancellationSource.Token;

        TicketAiAnalysisInput expectedInput = new(
            ticket.Id,
            ticket.Title,
            ticket.Description);

        repository.GetByIdAsync(ticket.Id, cancellationToken)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(expectedInput, cancellationToken)
            .Returns(Task.FromResult(expectedResult));

        AnalyzeRequestShell shell = new(repository, analyzer);

        // Act
        TicketAiAnalysisResult actual = await shell.ExecuteAsync(
            ticket.Id,
            cancellationToken);

        // Assert
        actual.Should().BeSameAs(expectedResult);

        await repository.Received(1).GetByIdAsync(
            ticket.Id,
            cancellationToken);

        await analyzer.Received(1).AnalyzeAsync(
            expectedInput,
            cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTicketIsMissing_DoesNotInvokeAnalyzer()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Guid ticketId = Guid.NewGuid();

        repository.GetByIdAsync(ticketId, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(null));

        AnalyzeRequestShell shell = new(repository, analyzer);
        Func<Task> expect = () => shell.ExecuteAsync(ticketId);

        // Act
        Exception? actual = await Record.ExceptionAsync(expect);

        // Assert
        actual.Should().BeOfType<InvalidOperationException>();

        await analyzer.DidNotReceive().AnalyzeAsync(
            Arg.Any<TicketAiAnalysisInput>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesFailureWithoutInvokingAnalyzer()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Guid ticketId = Guid.NewGuid();
        InvalidOperationException expectedException =
            new("Ticket loading failed.");

        repository.GetByIdAsync(ticketId, CancellationToken.None)
            .Returns(Task.FromException<Ticket?>(expectedException));

        AnalyzeRequestShell shell = new(repository, analyzer);
        Func<Task> operation = () => shell.ExecuteAsync(ticketId);

        // Act
        Exception? actual = await Record.ExceptionAsync(operation);

        // Assert
        actual.Should().BeSameAs(expectedException);

        await analyzer.DidNotReceive().AnalyzeAsync(
            Arg.Any<TicketAiAnalysisInput>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnalysisIsInvalid_ThrowsCoreValidationFailure()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Ticket ticket = CreateTicket();

        TicketAiAnalysisResult invalidResult = CreateValidResult() with
        {
            Summary = " "
        };

        repository.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromResult(invalidResult));

        AnalyzeRequestShell shell = new(repository, analyzer);
        Func<Task> operation = () => shell.ExecuteAsync(ticket.Id);

        // Act
        Exception? actual = await Record.ExceptionAsync(operation);

        // Assert
        actual.Should()
            .BeOfType<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingSummary);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnalyzerReturnsNull_ThrowsCoreValidationFailure()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Ticket ticket = CreateTicket();

        repository.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromResult<TicketAiAnalysisResult>(null!));

        AnalyzeRequestShell shell = new(repository, analyzer);
        Func<Task> operation = () => shell.ExecuteAsync(ticket.Id);

        // Act
        Exception? actual = await Record.ExceptionAsync(operation);

        // Assert
        actual.Should()
            .BeOfType<TicketAiAnalysisValidationException>()
            .Which.Failure.Should()
            .Be(TicketAiAnalysisValidationFailureKind.MissingResult);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnalyzerFails_PropagatesFailure()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Ticket ticket = CreateTicket();
        InvalidOperationException expectedException =
            new("Analysis provider failed.");

        repository.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromException<TicketAiAnalysisResult>(
                expectedException));

        AnalyzeRequestShell shell = new(repository, analyzer);
        Func<Task> operation = () => shell.ExecuteAsync(ticket.Id);

        // Act
        Exception? actual = await Record.ExceptionAsync(operation);

        // Assert
        actual.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadyCanceledToken_DoesNotLoadOrAnalyze()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        AnalyzeRequestShell shell = new(repository, analyzer);
        Guid ticketId = Guid.NewGuid();

        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();
        CancellationToken cancellationToken = cancellationSource.Token;

        Func<Task> operation = () => shell.ExecuteAsync(
            ticketId,
            cancellationToken);

        // Act
        Exception? actual = await Record.ExceptionAsync(operation);

        // Assert
        actual.Should()
            .BeAssignableTo<OperationCanceledException>()
            .Which.CancellationToken.Should()
            .Be(cancellationToken);

        await repository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());

        await analyzer.DidNotReceive().AnalyzeAsync(
            Arg.Any<TicketAiAnalysisInput>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnalyzerCancels_PropagatesCancellation()
    {
        // Arrange
        ITicketRepository repository = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        Ticket ticket = CreateTicket();

        using CancellationTokenSource cancellationSource = new();
        CancellationToken cancellationToken = cancellationSource.Token;

        OperationCanceledException expectedException =
            new(cancellationToken);

        repository.GetByIdAsync(ticket.Id, cancellationToken)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                cancellationToken)
            .Returns(Task.FromException<TicketAiAnalysisResult>(
                expectedException));

        AnalyzeRequestShell shell = new(repository, analyzer);

        Func<Task> operation = () => shell.ExecuteAsync(
            ticket.Id,
            cancellationToken);

        // Act
        Exception? actual = await Record.ExceptionAsync(operation);

        // Assert
        actual.Should().BeSameAs(expectedException);
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand(
            "Cannot access VPN",
            "The persisted ticket describes rejected VPN credentials.",
            TicketPriority.High),
        new CreateTicketFacts(true),
        Guid.Parse("0be030ef-8b48-4b49-91fe-2f4d49e070de"),
        Guid.Parse("247da287-7f60-4111-bbbf-cde1157402ef"),
        Guid.Parse("51605009-becf-46e1-a667-b6290819317f"),
        new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));

    private static TicketAiAnalysisResult CreateValidResult() => new(
        "The customer cannot authenticate to the VPN.",
        "Connectivity",
        TicketPriority.High,
        "The issue prevents remote access.",
        [
            "Check whether the VPN client shows a specific error code."
        ],
        [
            "Which VPN client version is installed?"
        ],
        TicketAiAnalysisConfidence.Medium);
}