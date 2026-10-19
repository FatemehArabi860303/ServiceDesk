using FluentAssertions;
using NSubstitute;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Shell.AiAnalysis;
using ServiceDesk.Shell.Tickets;

namespace ServiceDesk.Shell.Tests.AiAnalysis;

public sealed class ProcessTicketAiAnalysisShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithCompletedEvent_DoesNotLoadAnalyzeOrSave()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        Guid eventId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();

        analyses.IsCompletedAsync(
                eventId,
                ticketId,
                CancellationToken.None)
            .Returns(Task.FromResult(true));

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);

        // Act
        await shell.ExecuteAsync(eventId, ticketId);

        // Assert
        await tickets.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());

        await analyzer.DidNotReceive().AnalyzeAsync(
            Arg.Any<TicketAiAnalysisInput>(),
            Arg.Any<CancellationToken>());

        await analyses.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<TicketAiAnalysisResult>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNewEvent_SavesValidatedResultAndIdentity()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        Guid eventId = Guid.NewGuid();
        Ticket ticket = CreateTicket();
        TicketAiAnalysisResult expectedResult = CreateResult();

        using CancellationTokenSource source = new();
        CancellationToken token = source.Token;

        tickets.GetByIdAsync(ticket.Id, token)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                token)
            .Returns(Task.FromResult(expectedResult));

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);

        // Act
        await shell.ExecuteAsync(eventId, ticket.Id, token);

        // Assert
        await analyses.Received(1).IsCompletedAsync(
            eventId,
            ticket.Id,
            token);

        await analyses.Received(1).SaveAsync(
            eventId,
            ticket.Id,
            expectedResult,
            token);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnalysisFails_DoesNotSaveCompletion()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        Guid eventId = Guid.NewGuid();
        Ticket ticket = CreateTicket();
        IOException expectedException = new("Provider failed.");

        tickets.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromException<TicketAiAnalysisResult>(
                expectedException));

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);

        Func<Task> operation = () =>
            shell.ExecuteAsync(eventId, ticket.Id);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeSameAs(expectedException);

        await analyses.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<TicketAiAnalysisResult>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreRejectsResult_DoesNotSaveCompletion()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        Guid eventId = Guid.NewGuid();
        Ticket ticket = CreateTicket();

        TicketAiAnalysisResult invalidResult = CreateResult() with
        {
            Summary = " "
        };

        tickets.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromResult(invalidResult));

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);

        Func<Task> operation = () =>
            shell.ExecuteAsync(eventId, ticket.Id);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeOfType<TicketAiAnalysisValidationException>();

        await analyses.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<TicketAiAnalysisResult>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceFails_PropagatesFailure()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        Guid eventId = Guid.NewGuid();
        Ticket ticket = CreateTicket();
        TicketAiAnalysisResult result = CreateResult();
        IOException expectedException = new("Commit failed.");

        tickets.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromResult(result));

        analyses.SaveAsync(
                eventId,
                ticket.Id,
                result,
                CancellationToken.None)
            .Returns(Task.FromException(expectedException));

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);

        Func<Task> operation = () =>
            shell.ExecuteAsync(eventId, ticket.Id);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCompleteBeforePersistenceCompletes()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        Guid eventId = Guid.NewGuid();
        Ticket ticket = CreateTicket();
        TicketAiAnalysisResult result = CreateResult();

        TaskCompletionSource<bool> persistence = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        tickets.GetByIdAsync(ticket.Id, CancellationToken.None)
            .Returns(Task.FromResult<Ticket?>(ticket));

        analyzer.AnalyzeAsync(
                Arg.Any<TicketAiAnalysisInput>(),
                CancellationToken.None)
            .Returns(Task.FromResult(result));

        analyses.SaveAsync(
                eventId,
                ticket.Id,
                result,
                CancellationToken.None)
            .Returns(persistence.Task);

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);

        // Act
        Task processing = shell.ExecuteAsync(eventId, ticket.Id);
        bool completedBeforePersistence = processing.IsCompleted;

        persistence.SetResult(true);
        await processing;

        // Assert
        completedBeforePersistence.Should().BeFalse();
        processing.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanceled_DoesNotQueryOrSave()
    {
        // Arrange
        ITicketRepository tickets = Substitute.For<ITicketRepository>();
        ITicketAiAnalyzer analyzer = Substitute.For<ITicketAiAnalyzer>();
        ITicketAiAnalysisRepository analyses =
            Substitute.For<ITicketAiAnalysisRepository>();

        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);
        ProcessTicketAiAnalysisShell shell = new(analyses, analyzeShell);
        Guid eventId = Guid.NewGuid();
        Guid ticketId = Guid.NewGuid();

        using CancellationTokenSource source = new();
        source.Cancel();

        Func<Task> operation = () =>
            shell.ExecuteAsync(eventId, ticketId, source.Token);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeAssignableTo<OperationCanceledException>();

        await analyses.DidNotReceive().IsCompletedAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());

        await analyses.DidNotReceive().SaveAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<TicketAiAnalysisResult>(),
            Arg.Any<CancellationToken>());
    }

    private static Ticket CreateTicket() => CreateTicketCore.Execute(
        new CreateTicketCommand(
            "VPN unavailable",
            "VPN authentication fails.",
            TicketPriority.High),
        new CreateTicketFacts(true),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);

    private static TicketAiAnalysisResult CreateResult() => new(
        "VPN authentication fails.",
        "Connectivity",
        TicketPriority.High,
        "Remote access is unavailable.",
        new string[] { "Check the VPN error code." },
        new string[] { "Which VPN client version is installed?" },
        TicketAiAnalysisConfidence.Medium);
}