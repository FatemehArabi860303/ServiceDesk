using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMq.Consumers;
using ServiceDesk.AiWorker;
using ServiceDesk.Core.AiAnalysis;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Contracts;
using ServiceDesk.Repository;
using ServiceDesk.Shell.AiAnalysis;
using ServiceDesk.Shell.Tickets;
using TestSupport;

namespace ServiceDesk.Messaging.Tests;

public sealed class TicketAiAnalysisMessageHandlerTests
{
    [Fact]
    public async Task Delivery_WaitsForDurableCommitBeforeAcknowledgement()
    {
        // Arrange
        AnalysisScenario scenario = new();
        TaskCompletionSource saving = NewSignal();
        TaskCompletionSource committed = NewSignal();
        scenario.Analyses.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<TicketAiAnalysisResult>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                saving.SetResult();
                await committed.Task;
            });
        await using ConsumerSession<TicketAiAnalysisMessageHandler> session = scenario.Session();
        session.Start();

        // Act
        Task delivery = scenario.Deliver(session);
        await saving.Task.WaitAsync(TimeSpan.FromSeconds(10));
        bool ackedBeforeCommit = session.Channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "BasicAck");
        committed.SetResult();
        await delivery;

        // Assert
        ackedBeforeCommit.Should().BeFalse();
        session.Channel.Received(1).BasicAck(42, false);
        await scenario.Analyses.Received(1).SaveAsync(scenario.Request.Id, scenario.Request.TicketId,
            scenario.Result, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CommitThenAckFailure_RedeliveryUsesEventIdWithoutAnalyzingAgain()
    {
        // Arrange
        AnalysisScenario scenario = new();
        bool committed = false;
        scenario.Analyses.IsCompletedAsync(scenario.Request.Id, scenario.Request.TicketId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(committed));
        scenario.Analyses.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<TicketAiAnalysisResult>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                committed = true;
                return Task.CompletedTask;
            });
        await using ConsumerSession<TicketAiAnalysisMessageHandler> firstSession = scenario.Session();
        firstSession.Channel.When(channel => channel.BasicAck(42, false))
            .Do(_ => throw new IOException("Ack lost after commit"));
        firstSession.Start();

        // Act
        await scenario.Deliver(firstSession);
        Exception? failure = await Record.ExceptionAsync(() => firstSession.Running.WaitAsync(TimeSpan.FromSeconds(10)));
        await using ConsumerSession<TicketAiAnalysisMessageHandler> secondSession = scenario.Session();
        secondSession.Start();
        await scenario.Deliver(secondSession, redelivered: true);

        // Assert
        committed.Should().BeTrue();
        failure.Should().BeOfType<IOException>();
        await scenario.Analyzer.Received(1).AnalyzeAsync(Arg.Any<TicketAiAnalysisInput>(), Arg.Any<CancellationToken>());
        await scenario.Analyses.Received(1).SaveAsync(scenario.Request.Id, scenario.Request.TicketId,
            scenario.Result, Arg.Any<CancellationToken>());
        secondSession.Channel.Received(1).BasicAck(42, false);
    }

    [Fact]
    public async Task PersistenceFailure_StopsSessionWithoutAcknowledgementOrNextDelivery()
    {
        // Arrange
        AnalysisScenario scenario = new();
        scenario.Analyses.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<TicketAiAnalysisResult>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Commit failed")));
        await using ConsumerSession<TicketAiAnalysisMessageHandler> session = scenario.Session();
        session.Start();

        // Act
        await scenario.Deliver(session);
        Exception? failure = await Record.ExceptionAsync(() => session.Running.WaitAsync(TimeSpan.FromSeconds(10)));
        await scenario.Deliver(session);

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        await scenario.Analyzer.Received(1).AnalyzeAsync(Arg.Any<TicketAiAnalysisInput>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("malformed-json")]
    [InlineData("null")]
    [InlineData("empty-event-id")]
    [InlineData("empty-ticket-id")]
    [InlineData("missing-time")]
    [InlineData("wrong-message-id")]
    [InlineData("missing-message-id")]
    [InlineData("wrong-type")]
    [InlineData("wrong-content-type")]
    [InlineData("wrong-routing-key")]
    [InlineData("wrong-exchange")]
    public async Task InvalidMessage_FailsSessionBeforeApplicationProcessing(string invalidPart)
    {
        // Arrange
        AnalysisScenario scenario = new();
        TicketAiAnalysisRequestedV1 request = invalidPart switch
        {
            "empty-event-id" => scenario.Request with { Id = Guid.Empty },
            "empty-ticket-id" => scenario.Request with { TicketId = Guid.Empty },
            "missing-time" => scenario.Request with { OccurredAt = default },
            _ => scenario.Request
        };
        byte[] body = invalidPart switch
        {
            "malformed-json" => Encoding.UTF8.GetBytes("{"),
            "null" => Encoding.UTF8.GetBytes("null"),
            _ => JsonSerializer.SerializeToUtf8Bytes(request)
        };
        IBasicProperties properties = scenario.Properties();
        if (invalidPart == "wrong-message-id") properties.MessageId = Guid.NewGuid().ToString();
        if (invalidPart == "missing-message-id") properties.MessageId = null;
        if (invalidPart == "wrong-type") properties.Type = "wrong";
        if (invalidPart == "wrong-content-type") properties.ContentType = "text/plain";
        await using ConsumerSession<TicketAiAnalysisMessageHandler> session = scenario.Session();
        session.Start();

        // Act
        await session.DeliverAsync(body, properties: properties,
            exchange: invalidPart == "wrong-exchange" ? "wrong" : RabbitMqOutboxRoute.AiAnalysis.ExchangeName,
            routingKey: invalidPart == "wrong-routing-key" ? "wrong" : TicketAiAnalysisRequestedV1.Type);
        Exception? failure = await Record.ExceptionAsync(() => session.Running.WaitAsync(TimeSpan.FromSeconds(10)));

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        await scenario.Analyses.DidNotReceive().IsCompletedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await scenario.Analyzer.DidNotReceive().AnalyzeAsync(Arg.Any<TicketAiAnalysisInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Shutdown_DeliversCancellationToPersistenceWithoutAcknowledging()
    {
        // Arrange
        AnalysisScenario scenario = new();
        TaskCompletionSource saving = NewSignal();
        CancellationToken observedToken = default;
        scenario.Analyses.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<TicketAiAnalysisResult>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                observedToken = call.Arg<CancellationToken>();
                saving.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, observedToken);
            });
        await using ConsumerSession<TicketAiAnalysisMessageHandler> session = scenario.Session();
        session.Start();

        // Act
        Task delivery = scenario.Deliver(session);
        await saving.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await session.Cancellation.CancelAsync();
        await Task.WhenAll(delivery, session.Running).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        observedToken.IsCancellationRequested.Should().BeTrue();
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        session.Channel.Received(1).Dispose();
    }

    [Fact]
    public async Task WorkerComposition_ResolvesScopedHandlerAndDbContextWithoutCapturingThem()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDesk"] = "Server=(localdb)\\MSSQLLocalDB;Database=ServiceDeskLocal;Trusted_Connection=True",
            ["RabbitMq:HostName"] = "localhost",
            ["RabbitMq:UserName"] = "guest",
            ["RabbitMq:Password"] = "guest",
            ["OpenAI:ApiKey"] = "test-key",
            ["OpenAI:Model"] = "test-model",
            ["OpenAI:BaseUrl"] = "https://api.openai.com/v1/"
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddAiWorker(configuration);
        await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using AsyncServiceScope first = provider.CreateAsyncScope();
        await using AsyncServiceScope second = provider.CreateAsyncScope();

        // Act
        IHostedService worker = provider.GetRequiredService<IHostedService>();
        TicketAiAnalysisMessageHandler firstHandler = first.ServiceProvider.GetRequiredService<TicketAiAnalysisMessageHandler>();
        TicketAiAnalysisMessageHandler sameHandler = first.ServiceProvider.GetRequiredService<TicketAiAnalysisMessageHandler>();
        TicketAiAnalysisMessageHandler secondHandler = second.ServiceProvider.GetRequiredService<TicketAiAnalysisMessageHandler>();
        ServiceDeskDbContext firstContext = first.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        ServiceDeskDbContext secondContext = second.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        ConnectionFactory factory = (ConnectionFactory)provider.GetRequiredService<IConnectionFactory>();

        // Assert
        worker.Should().BeOfType<AiAnalysisConsumerWorker>();
        firstHandler.Should().BeSameAs(sameHandler).And.NotBeSameAs(secondHandler);
        firstContext.Should().NotBeSameAs(secondContext);
        factory.AutomaticRecoveryEnabled.Should().BeFalse();
        factory.DispatchConsumersAsync.Should().BeTrue();
    }

    [Fact]
    public async Task WorkerShutdown_ExpiredHostDeadlineStillWaitsForHandlerCleanup()
    {
        // Arrange
        AnalysisScenario scenario = new();
        TaskCompletionSource saving = NewSignal();
        TaskCompletionSource cancelled = NewSignal();
        TaskCompletionSource release = NewSignal();
        scenario.Analyses.SaveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<TicketAiAnalysisResult>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                using CancellationTokenRegistration registration = call.Arg<CancellationToken>().Register(() => cancelled.SetResult());
                saving.SetResult();
                await release.Task;
            });
        await using ConsumerSession<TicketAiAnalysisMessageHandler> session = scenario.Session();
        using AiAnalysisConsumerWorker worker = new(session.Consumer);
        using CancellationTokenSource deadline = new();
        deadline.Cancel();

        // Act
        await worker.StartAsync(CancellationToken.None);
        await session.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task delivery = scenario.Deliver(session);
        await saving.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task stopping = worker.StopAsync(deadline.Token);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        bool stoppedBeforeCleanup = stopping.IsCompleted;
        bool disposedBeforeCleanup = session.Channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Dispose");
        release.SetResult();
        await Task.WhenAll(stopping, delivery).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        stoppedBeforeCleanup.Should().BeFalse();
        disposedBeforeCleanup.Should().BeFalse();
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        session.Channel.Received(1).Dispose();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class AnalysisScenario
    {
        public ITicketRepository Tickets { get; } = Substitute.For<ITicketRepository>();
        public ITicketAiAnalyzer Analyzer { get; } = Substitute.For<ITicketAiAnalyzer>();
        public ITicketAiAnalysisRepository Analyses { get; } = Substitute.For<ITicketAiAnalysisRepository>();
        public TicketAiAnalysisRequestedV1 Request { get; }
        public TicketAiAnalysisResult Result { get; } = new("VPN unavailable", "Connectivity", TicketPriority.High,
            "Remote access unavailable", ["Check VPN"], ["Which version?"], TicketAiAnalysisConfidence.Medium);

        public AnalysisScenario()
        {
            Ticket ticket = CreateTicketCore.Execute(new CreateTicketCommand("VPN unavailable", "VPN authentication fails", TicketPriority.High),
                new CreateTicketFacts(true), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            Request = new(Guid.NewGuid(), ticket.Id, DateTimeOffset.UtcNow);
            Tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Ticket?>(ticket));
            Analyzer.AnalyzeAsync(Arg.Any<TicketAiAnalysisInput>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result));
        }

        public ConsumerSession<TicketAiAnalysisMessageHandler> Session() => new(TicketAiAnalysisMessageHandler.ConsumerOptions,
            services =>
            {
                services.AddScoped(_ => Tickets);
                services.AddScoped(_ => Analyzer);
                services.AddScoped(_ => Analyses);
                services.AddScoped<AnalyzeRequestShell>();
                services.AddScoped<ProcessTicketAiAnalysisShell>();
                services.AddScoped<TicketAiAnalysisMessageHandler>();
            });

        public IBasicProperties Properties()
        {
            IBasicProperties properties = Substitute.For<IBasicProperties>();
            properties.MessageId = Request.Id.ToString();
            properties.Type = TicketAiAnalysisRequestedV1.Type;
            properties.ContentType = "application/json";
            return properties;
        }

        public Task Deliver(ConsumerSession<TicketAiAnalysisMessageHandler> session, bool redelivered = false) =>
            session.DeliverAsync(JsonSerializer.SerializeToUtf8Bytes(Request),
                exchange: RabbitMqOutboxRoute.AiAnalysis.ExchangeName,
                routingKey: TicketAiAnalysisRequestedV1.Type, properties: Properties(), redelivered: redelivered);
    }
}
