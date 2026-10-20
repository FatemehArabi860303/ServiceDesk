using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;
using RabbitMq.Consumers;
using TestSupport;

namespace RabbitMq.Consumers.Tests;

public sealed class RabbitMqConsumerTests
{
    [Fact]
    public async Task Delivery_AcknowledgesOnlyAfterHandlerAndScopeComplete()
    {
        // Arrange
        TaskCompletionSource entered = NewSignal();
        TaskCompletionSource release = NewSignal();
        List<ProbeHandler> handlers = [];
        await using ConsumerSession<ProbeHandler> session = new(Options(), services =>
            services.AddScoped(_ =>
            {
                ProbeHandler handler = new(async token =>
                {
                    entered.SetResult();
                    await release.Task;
                });
                handlers.Add(handler);
                return handler;
            }));
        bool disposedAtAck = false;
        session.Channel.When(channel => channel.BasicAck(42, false))
            .Do(_ => disposedAtAck = handlers.Single().Disposed);
        session.Start();

        // Act
        Task delivery = session.DeliverAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        bool ackedBeforeCompletion = session.Channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "BasicAck");
        release.SetResult();
        await delivery;

        // Assert
        ackedBeforeCompletion.Should().BeFalse();
        disposedAtAck.Should().BeTrue();
        session.Channel.Received(1).BasicAck(42, false);
    }

    [Fact]
    public async Task Deliveries_AreSerializedAndUseSeparateDisposedScopes()
    {
        // Arrange
        TaskCompletionSource entered = NewSignal();
        TaskCompletionSource release = NewSignal();
        List<ProbeHandler> handlers = [];
        await using ConsumerSession<ProbeHandler> session = new(Options(), services =>
            services.AddScoped(_ =>
            {
                ProbeHandler handler = new(async token =>
                {
                    entered.TrySetResult();
                    await release.Task;
                });
                handlers.Add(handler);
                return handler;
            }));
        session.Start();

        // Act
        Task first = session.DeliverAsync(tag: 1);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task second = session.DeliverAsync(tag: 2);
        int handlersWhileBlocked = handlers.Count;
        release.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        handlersWhileBlocked.Should().Be(1);
        handlers.Should().HaveCount(2).And.OnlyContain(handler => handler.Disposed);
        session.Channel.Received(1).BasicAck(1, false);
        session.Channel.Received(1).BasicAck(2, false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandlerFailure_LeavesDeliveryUnacknowledgedAndHonorsFailurePolicy(bool failStop)
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(
            _ => Task.FromException(new InvalidDataException("Invalid payload")), failStop);
        session.Start();

        // Act
        await session.DeliverAsync();
        Exception? failure = failStop
            ? await Record.ExceptionAsync(() => session.Running.WaitAsync(TimeSpan.FromSeconds(10)))
            : null;

        // Assert
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        session.Channel.DidNotReceive().BasicNack(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<bool>());
        (failure?.GetType()).Should().Be(failStop ? typeof(IOException) : null);
        (failure?.InnerException?.GetType()).Should().Be(failStop ? typeof(InvalidDataException) : null);
        session.Running.IsCompleted.Should().Be(failStop);
    }

    [Fact]
    public async Task Shutdown_CancelsActiveHandlerWaitsForItAndRejectsQueuedCallbacks()
    {
        // Arrange
        TaskCompletionSource entered = NewSignal();
        TaskCompletionSource cancelled = NewSignal();
        TaskCompletionSource release = NewSignal();
        int invoked = 0;
        await using ConsumerSession<ProbeHandler> session = CreateSession(async token =>
        {
            Interlocked.Increment(ref invoked);
            using CancellationTokenRegistration registration = token.Register(() => cancelled.SetResult());
            entered.SetResult();
            await release.Task; // Deliberately ignore cancellation until cleanup finishes.
        });
        session.Start();

        // Act
        Task first = session.DeliverAsync(tag: 1);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task queued = session.DeliverAsync(tag: 2);
        await session.Cancellation.CancelAsync();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        bool disposedWhileActive = session.Channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Dispose");
        bool stoppedWhileActive = session.Running.IsCompleted;
        release.SetResult();
        await Task.WhenAll(first, queued, session.Running);
        await session.DeliverAsync(tag: 3);

        // Assert
        disposedWhileActive.Should().BeFalse();
        stoppedWhileActive.Should().BeFalse();
        invoked.Should().Be(1);
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        session.Channel.Received(1).Dispose();
        session.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task AcknowledgementFailure_StopsSessionWithoutRetryOrDuplicateAck()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);
        session.Channel.When(channel => channel.BasicAck(42, false))
            .Do(_ => throw new IOException("Connection lost after processing"));
        session.Start();

        // Act
        await session.DeliverAsync();
        Exception? failure = await Record.ExceptionAsync(() => session.Running.WaitAsync(TimeSpan.FromSeconds(10)));
        await session.DeliverAsync();

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Channel.Received(1).BasicAck(42, false);
        session.Channel.Received(1).Dispose();
    }

    [Fact]
    public async Task ConnectionFailure_PropagatesWithoutSubscribing()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);
        session.Factory.CreateConnection().Throws(new IOException("Broker unavailable"));

        // Act
        session.Start();
        Exception? failure = await Record.ExceptionAsync(() => session.Running);

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Connection.DidNotReceive().CreateModel();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransportShutdown_CancelsProcessingAndStopsSession(bool connectionShutdown)
    {
        // Arrange
        TaskCompletionSource entered = NewSignal();
        TaskCompletionSource cancelled = NewSignal();
        await using ConsumerSession<ProbeHandler> session = CreateSession(async token =>
        {
            entered.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { cancelled.SetResult(); }
        });
        session.Start();

        // Act
        Task delivery = session.DeliverAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        ShutdownEventArgs shutdown = new(ShutdownInitiator.Peer, 320, "Broker shutdown");
        if (connectionShutdown)
        {
            session.Connection.ConnectionShutdown += Raise.Event<EventHandler<ShutdownEventArgs>>(session.Connection, shutdown);
        }
        else
        {
            session.Channel.ModelShutdown += Raise.Event<EventHandler<ShutdownEventArgs>>(session.Channel, shutdown);
        }
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await delivery;
        Exception? failure = await Record.ExceptionAsync(() => session.Running.WaitAsync(TimeSpan.FromSeconds(10)));

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        session.Channel.Received(1).Dispose();
    }

    [Fact]
    public async Task Startup_DeclaresConfiguredTopologyAndManualAckPrefetch()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);

        // Act
        session.Start();

        // Assert
        session.Channel.Received(1).ExchangeDeclare("exchange", "direct", true, false, null);
        session.Channel.Received(1).QueueDeclare("queue", true, false, false, null);
        session.Channel.Received(1).QueueBind("queue", "exchange", "key", null);
        session.Channel.Received(1).BasicQos(0, 1, false);
        session.Channel.Received(1).BasicConsume("queue", false, "", false, false, null, session.Subscription);
    }

    [Fact]
    public async Task PartialStartupFailure_DisposesCreatedTransport()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);
        session.Channel.When(channel => channel.BasicQos(0, 1, false))
            .Do(_ => throw new IOException("Setup failed"));

        // Act
        session.Start();
        Exception? failure = await Record.ExceptionAsync(() => session.Running);

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Channel.Received(1).Dispose();
        session.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task BrokerSubscriptionCancellation_StopsSessionWithoutReconnect()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);
        session.Start();

        // Act
        await session.Subscription.HandleBasicCancel("consumer-tag");
        Exception? failure = await Record.ExceptionAsync(() => session.Running.WaitAsync(TimeSpan.FromSeconds(10)));

        // Assert
        failure.Should().BeOfType<IOException>();
        session.Factory.Received(1).CreateConnection();
        session.Channel.Received(1).Dispose();
    }

    [Fact]
    public async Task ChannelDisposalFailure_StillDisposesConnection()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);
        session.Channel.When(channel => channel.Dispose()).Do(_ => throw new IOException("Channel close failed"));
        session.Start();

        // Act
        await session.Cancellation.CancelAsync();
        await session.Running.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        session.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task CancellationBeforeStartup_DoesNotOpenTransport()
    {
        // Arrange
        await using ConsumerSession<ProbeHandler> session = CreateSession(_ => Task.CompletedTask);
        session.Cancellation.Cancel();

        // Act
        Exception? failure = await Record.ExceptionAsync(() => session.Consumer.RunAsync(session.Cancellation.Token));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        session.Factory.DidNotReceive().CreateConnection();
    }

    private static RabbitMqConsumerOptions Options() => new("exchange", "queue", "key");
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ConsumerSession<ProbeHandler> CreateSession(Func<CancellationToken, Task> action, bool failStop = true) =>
        new(Options() with { StopOnHandlerFailure = failStop }, services =>
            services.AddScoped(_ => new ProbeHandler(action)));

    public sealed class ProbeHandler(Func<CancellationToken, Task> action) : IMessageHandler, IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public Task HandleAsync(MessageContext message, CancellationToken cancellationToken) => action(cancellationToken);
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
