using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Core.Users;
using ServiceDesk.Repository.AiAnalysis;
using ServiceDesk.Repository.Tickets;
using ServiceDesk.Shell.AiAnalysis;

namespace ServiceDesk.Repository.IntegrationTests.AiAnalysis;

public sealed class TicketAiAnalysisRepositoryTests : IAsyncLifetime
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"servicedesk-ai-{Guid.NewGuid():N}.db");

    private DbContextOptions<ServiceDeskDbContext> options = null!;
    private Ticket ticket = null!;

    public async Task InitializeAsync()
    {
        options = new DbContextOptionsBuilder<ServiceDeskDbContext>()
            .UseSqlite(
                $"Data Source={databasePath};Pooling=False;Default Timeout=30")
            .Options;

        DateTimeOffset now = DateTimeOffset.UtcNow;

        User customer = new(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ADA@EXAMPLE.COM",
            UserRole.Customer,
            true,
            now,
            now);

        ticket = CreateTicketCore.Execute(
            new CreateTicketCommand(
                "VPN unavailable",
                "VPN authentication fails.",
                TicketPriority.High),
            new CreateTicketFacts(true),
            customer.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            now);

        await using ServiceDeskDbContext context = new(options);
        await context.Database.EnsureCreatedAsync();

        context.Users.Add(customer);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        File.Delete(databasePath);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_PersistsAllResultFieldsAndCompletionIdentity()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        TicketAiAnalysisResult expectedResult = CreateResult();

        CountingAnalyzer analyzer = new(
            _ => Task.FromResult(expectedResult));

        await using ServiceDeskDbContext context = new(options);
        ProcessTicketAiAnalysisShell shell = CreateShell(context, analyzer);

        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        // Act
        await shell.ExecuteAsync(eventId, ticket.Id);

        await using ServiceDeskDbContext verification = new(options);

        TicketAiAnalysis stored = await verification.TicketAiAnalyses
            .AsNoTracking()
            .SingleAsync();

        TicketAiAnalysisResult? restored =
            JsonSerializer.Deserialize<TicketAiAnalysisResult>(
                stored.ResultJson,
                jsonOptions);

        Ticket storedTicket = await verification.Tickets
            .AsNoTracking()
            .Include(item => item.History)
            .SingleAsync();

        // Assert
        stored.EventId.Should().Be(eventId);
        stored.TicketId.Should().Be(ticket.Id);
        stored.CompletedAt.Should().NotBe(default);
        restored.Should().BeEquivalentTo(expectedResult);
        analyzer.Calls.Should().Be(1);

        storedTicket.Status.Should().Be(TicketStatus.Open);
        storedTicket.Priority.Should().Be(TicketPriority.High);
        storedTicket.AssignedEmployeeUserId.Should().BeNull();
        storedTicket.History.Should().ContainSingle();
    }

    [Fact]
    public async Task ExecuteAsync_WithPreviouslyCompletedEvent_SkipsAnalyzerAfterContextRestart()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        TicketAiAnalysisResult result = CreateResult();

        CountingAnalyzer analyzer = new(
            _ => Task.FromResult(result));

        await using (ServiceDeskDbContext firstContext = new(options))
        {
            ProcessTicketAiAnalysisShell firstShell =
                CreateShell(firstContext, analyzer);

            await firstShell.ExecuteAsync(eventId, ticket.Id);
        }

        await using ServiceDeskDbContext secondContext = new(options);
        ProcessTicketAiAnalysisShell secondShell =
            CreateShell(secondContext, analyzer);

        // Act
        await secondShell.ExecuteAsync(eventId, ticket.Id);

        await using ServiceDeskDbContext verification = new(options);
        int count = await verification.TicketAiAnalyses.CountAsync();

        // Assert
        count.Should().Be(1);
        analyzer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_WithDuplicateEvent_PreservesOriginalResult()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        TicketAiAnalysisResult original = CreateResult();

        TicketAiAnalysisResult replacement = original with
        {
            Summary = "This must not replace the original result."
        };

        await using (ServiceDeskDbContext firstContext = new(options))
        {
            TicketAiAnalysisRepository firstRepository = new(firstContext);
            await firstRepository.SaveAsync(eventId, ticket.Id, original);
        }

        await using ServiceDeskDbContext secondContext = new(options);
        TicketAiAnalysisRepository repository = new(secondContext);
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        // Act
        await repository.SaveAsync(eventId, ticket.Id, replacement);

        await using ServiceDeskDbContext verification = new(options);

        TicketAiAnalysis stored = await verification.TicketAiAnalyses
            .AsNoTracking()
            .SingleAsync();

        TicketAiAnalysisResult? restored =
            JsonSerializer.Deserialize<TicketAiAnalysisResult>(
                stored.ResultJson,
                jsonOptions);

        // Assert
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task ExecuteAsync_WithConcurrentDuplicateWorkers_CommitsOnlyOneResult()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        TicketAiAnalysisResult result = CreateResult();

        TaskCompletionSource<bool> bothAnalyzing = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        int entered = 0;

        CountingAnalyzer analyzer = new(async token =>
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                bothAnalyzing.TrySetResult(true);
            }

            await bothAnalyzing.Task.WaitAsync(token);
            return result;
        });

        await using ServiceDeskDbContext firstContext = new(options);
        await using ServiceDeskDbContext secondContext = new(options);

        ProcessTicketAiAnalysisShell firstShell =
            CreateShell(firstContext, analyzer);

        ProcessTicketAiAnalysisShell secondShell =
            CreateShell(secondContext, analyzer);

        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(30));

        // Act
        Task first = Task.Run(() =>
            firstShell.ExecuteAsync(eventId, ticket.Id, timeout.Token));

        Task second = Task.Run(() =>
            secondShell.ExecuteAsync(eventId, ticket.Id, timeout.Token));

        await Task.WhenAll(first, second);

        await using ServiceDeskDbContext verification = new(options);

        int count = await verification.TicketAiAnalyses.CountAsync();

        TicketAiAnalysis stored = await verification.TicketAiAnalyses
            .AsNoTracking()
            .SingleAsync();

        // Assert
        first.IsCompletedSuccessfully.Should().BeTrue();
        second.IsCompletedSuccessfully.Should().BeTrue();
        analyzer.Calls.Should().Be(2);
        count.Should().Be(1);
        stored.EventId.Should().Be(eventId);
        stored.TicketId.Should().Be(ticket.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInsertFails_DoesNotRecordCompletionAndCanBeRetried()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        TicketAiAnalysisResult result = CreateResult();

        CountingAnalyzer analyzer = new(
            _ => Task.FromResult(result));

        await using (ServiceDeskDbContext setup = new(options))
        {
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER reject_analysis
                AFTER INSERT ON TicketAiAnalyses
                BEGIN
                    SELECT RAISE(ABORT, 'Forced analysis persistence failure');
                END;
                """);
        }

        await using ServiceDeskDbContext failedContext = new(options);

        ProcessTicketAiAnalysisShell failedShell =
            CreateShell(failedContext, analyzer);

        Func<Task> operation = () =>
            failedShell.ExecuteAsync(eventId, ticket.Id);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        await using ServiceDeskDbContext verification = new(options);

        int countAfterFailure =
            await verification.TicketAiAnalyses.CountAsync();

        TicketAiAnalysisRepository verificationRepository = new(verification);

        bool completedAfterFailure =
            await verificationRepository.IsCompletedAsync(
                eventId,
                ticket.Id);

        await verification.Database.ExecuteSqlRawAsync(
            "DROP TRIGGER reject_analysis;");

        await using ServiceDeskDbContext retryContext = new(options);

        ProcessTicketAiAnalysisShell retryShell =
            CreateShell(retryContext, analyzer);

        await retryShell.ExecuteAsync(eventId, ticket.Id);

        await using ServiceDeskDbContext finalVerification = new(options);
        int countAfterRetry =
            await finalVerification.TicketAiAnalyses.CountAsync();

        // Assert
        exception.Should().BeOfType<DbUpdateException>();
        countAfterFailure.Should().Be(0);
        completedAfterFailure.Should().BeFalse();
        countAfterRetry.Should().Be(1);
        analyzer.Calls.Should().Be(2);
    }

    [Fact]
    public async Task IsCompletedAsync_WithConflictingTicketIdentity_Fails()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        Guid differentTicketId = Guid.NewGuid();
        TicketAiAnalysisResult result = CreateResult();

        await using ServiceDeskDbContext context = new(options);
        TicketAiAnalysisRepository repository = new(context);

        await repository.SaveAsync(eventId, ticket.Id, result);

        Func<Task> operation = () => repository.IsCompletedAsync(
            eventId,
            differentTicketId);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_WhenTicketDoesNotExist_DoesNotRecordCompletion()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        Guid missingTicketId = Guid.NewGuid();
        TicketAiAnalysisResult result = CreateResult();

        await using ServiceDeskDbContext context = new(options);
        TicketAiAnalysisRepository repository = new(context);

        Func<Task> operation = () => repository.SaveAsync(
            eventId,
            missingTicketId,
            result);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        await using ServiceDeskDbContext verification = new(options);
        int count = await verification.TicketAiAnalyses.CountAsync();

        // Assert
        exception.Should().BeOfType<DbUpdateException>();
        count.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_WithCanceledToken_DoesNotRecordCompletion()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        TicketAiAnalysisResult result = CreateResult();

        await using ServiceDeskDbContext context = new(options);
        TicketAiAnalysisRepository repository = new(context);

        using CancellationTokenSource source = new();
        source.Cancel();

        Func<Task> operation = () => repository.SaveAsync(
            eventId,
            ticket.Id,
            result,
            source.Token);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        await using ServiceDeskDbContext verification = new(options);
        int count = await verification.TicketAiAnalyses.CountAsync();

        // Assert
        exception.Should().BeAssignableTo<OperationCanceledException>();
        count.Should().Be(0);
    }

    private static ProcessTicketAiAnalysisShell CreateShell(
        ServiceDeskDbContext context,
        ITicketAiAnalyzer analyzer)
    {
        TicketRepository tickets = new(context);
        TicketAiAnalysisRepository analyses = new(context);
        AnalyzeRequestShell analyzeShell = new(tickets, analyzer);

        return new ProcessTicketAiAnalysisShell(analyses, analyzeShell);
    }

    private static JsonSerializerOptions CreateJsonOptions() => new(
        JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(allowIntegerValues: false)
        }
    };

    private static TicketAiAnalysisResult CreateResult() => new(
        "The customer cannot authenticate to VPN.",
        "Connectivity",
        TicketPriority.High,
        "Remote access is unavailable.",
        new string[]
        {
            "Check the VPN client error code.",
            "Check whether the network connection is available."
        },
        new string[]
        {
            "Which VPN client version is installed?"
        },
        TicketAiAnalysisConfidence.Medium);

    private sealed class CountingAnalyzer(
        Func<CancellationToken, Task<TicketAiAnalysisResult>> analyze)
        : ITicketAiAnalyzer
    {
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public Task<TicketAiAnalysisResult> AnalyzeAsync(
            TicketAiAnalysisInput input,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return analyze(cancellationToken);
        }
    }
}