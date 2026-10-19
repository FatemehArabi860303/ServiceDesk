using System.Text;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMq.Publishers.Tests;

public sealed class RabbitMqPublisherTests
{
    [Fact]
    public async Task PublishAsync_DeclaresTopologyAndConfirmsPersistentMandatoryPublication()
    {
        // Arrange
        await using Transport fixture = new();
        PublishMessage message = Message();
        ReadOnlyMemory<byte> publishedBody = default;
        fixture.Channel.When(channel => channel.BasicPublish(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>()))
            .Do(call => publishedBody = call.ArgAt<ReadOnlyMemory<byte>>(4));

        // Act
        await fixture.Publisher.PublishAsync(message, Destination());

        // Assert
        Received.InOrder(() =>
        {
            fixture.Channel.ExchangeDeclare("events", "direct", true, false, null);
            fixture.Channel.QueueDeclare("application.v1", true, false, false, null);
            fixture.Channel.QueueBind("application.v1", "events", "created", null);
            fixture.Channel.ConfirmSelect();
            fixture.Channel.BasicPublish("events", "created", true, fixture.Properties, Arg.Any<ReadOnlyMemory<byte>>());
            fixture.Channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
            fixture.Channel.Dispose();
        });
        fixture.Properties.Persistent.Should().BeTrue();
        fixture.Properties.MessageId.Should().Be(message.MessageId);
        fixture.Properties.Type.Should().Be(message.MessageType);
        fixture.Properties.ContentType.Should().Be(message.ContentType);
        publishedBody.ToArray().Should().Equal(message.Body.ToArray());
        fixture.Connection.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task PublishAsync_AllowsApplicationManagedTopologyAndBinaryPayload()
    {
        // Arrange
        await using Transport fixture = new();
        RabbitMqPublishDestination destination = new("", "existing-queue") { DeclareExchange = false };
        PublishMessage message = new("id", "binary.v1", new byte[] { 0, 255, 128 }, "application/octet-stream");

        // Act
        await fixture.Publisher.PublishAsync(message, destination);

        // Assert
        fixture.Channel.DidNotReceive().ExchangeDeclare(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object>>());
        fixture.Channel.DidNotReceive().QueueDeclare(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<IDictionary<string, object>>());
        fixture.Channel.Received(1).BasicPublish("", "existing-queue", true, fixture.Properties, message.Body);
        fixture.Properties.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task PublishAsync_UsesConfiguredTopologyAndConfirmationTimeout()
    {
        // Arrange
        RabbitMqPublisherOptions options = new() { ConfirmationTimeout = TimeSpan.FromSeconds(3) };
        await using Transport fixture = new(options);
        RabbitMqPublishDestination destination = new("topics", "audit.*")
        {
            ExchangeType = "topic",
            ExchangeDurable = false,
            ExchangeAutoDelete = true,
            Queue = "audit-queue",
            QueueDurable = false,
            QueueExclusive = true,
            QueueAutoDelete = true
        };

        // Act
        await fixture.Publisher.PublishAsync(Message(), destination);

        // Assert
        fixture.Channel.Received(1).ExchangeDeclare("topics", "topic", false, true, null);
        fixture.Channel.Received(1).QueueDeclare("audit-queue", false, true, true, null);
        fixture.Channel.Received(1).QueueBind("audit-queue", "topics", "audit.*", null);
        fixture.Channel.Received(1).WaitForConfirmsOrDie(options.ConfirmationTimeout);
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("channel")]
    [InlineData("topology")]
    [InlineData("publish")]
    [InlineData("nack")]
    [InlineData("timeout")]
    [InlineData("channel-closed")]
    [InlineData("connection-lost")]
    [InlineData("channel-dispose")]
    public async Task PublishAsync_PropagatesFailuresAndDiscardsFailedConnection(string stage)
    {
        // Arrange
        await using Transport fixture = new();
        IOException expected = new(stage);
        switch (stage)
        {
            case "connection": fixture.Factory.CreateConnection().Throws(expected); break;
            case "channel": fixture.Connection.CreateModel().Throws(expected); break;
            case "topology":
                fixture.Channel.When(channel => channel.QueueBind("application.v1", "events", "created", null)).Do(_ => throw expected);
                break;
            case "publish":
                fixture.Channel.When(channel => channel.BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>()))
                    .Do(_ => throw expected);
                break;
            case "channel-dispose": fixture.Channel.When(channel => channel.Dispose()).Do(_ => throw expected); break;
            default: fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ => throw expected); break;
        }
        int expectedConnectionsDisposed = stage == "connection" ? 0 : 1;
        int expectedChannelsDisposed = stage is "connection" or "channel" ? 0 : 1;

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination()));

        // Assert
        failure.Should().BeSameAs(expected);
        fixture.Factory.Received(1).CreateConnection();
        fixture.Channel.Received(expectedChannelsDisposed).Dispose();
        fixture.Connection.Received(expectedConnectionsDisposed).Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_MandatoryReturnFailsEvenWhenBrokerAlsoConfirms(bool returnDuringWait)
    {
        // Arrange
        await using Transport fixture = new();
        Action raiseReturn = () => fixture.Channel.BasicReturn += Raise.Event<EventHandler<BasicReturnEventArgs>>(
            fixture.Channel, new BasicReturnEventArgs { ReplyCode = 312, ReplyText = "NO_ROUTE" });
        if (returnDuringWait)
        {
            fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ => raiseReturn());
        }
        else
        {
            fixture.Channel.When(channel => channel.BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>()))
                .Do(_ => raiseReturn());
        }

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination()));

        // Assert
        failure.Should().BeOfType<IOException>().Which.Message.Should().Contain("returned message");
        fixture.Channel.Received(1).WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        fixture.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task PublishAsync_DoesNotCompleteBeforeConfirmationAndSerializesConcurrentCalls()
    {
        // Arrange
        await using Transport fixture = new();
        IModel secondChannel = Transport.CreateChannel();
        fixture.Connection.CreateModel().Returns(fixture.Channel, secondChannel);
        TaskCompletionSource waiting = Signal();
        TaskCompletionSource confirmed = Signal();
        fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
        {
            waiting.SetResult();
            confirmed.Task.GetAwaiter().GetResult();
        });

        // Act
        Task first = Task.Run(() => fixture.Publisher.PublishAsync(Message(), Destination()));
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task second = fixture.Publisher.PublishAsync(Message() with { MessageId = "second-id" }, Destination());
        bool completedBeforeConfirm = first.IsCompleted;
        int modelsBeforeConfirm = fixture.Connection.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "CreateModel");
        confirmed.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        completedBeforeConfirm.Should().BeFalse();
        modelsBeforeConfirm.Should().Be(1);
        fixture.Factory.Received(1).CreateConnection();
        fixture.Channel.Received(1).Dispose();
        secondChannel.Received(1).WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        secondChannel.Received(1).Dispose();
        secondChannel.CreateBasicProperties().MessageId.Should().Be("second-id");
    }

    [Fact]
    public async Task PublishAsync_CancellationBeforePublishingDoesNotConnect()
    {
        // Arrange
        await using Transport fixture = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination(), cancellation.Token));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        fixture.Factory.DidNotReceive().CreateConnection();
    }

    [Fact]
    public async Task PublishAsync_CancellationDuringConfirmationCannotReportSuccessOrDisposeActiveChannel()
    {
        // Arrange
        await using Transport fixture = new();
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource waiting = Signal();
        TaskCompletionSource confirmed = Signal();
        fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
        {
            waiting.SetResult();
            confirmed.Task.GetAwaiter().GetResult();
        });

        // Act
        Task publishing = Task.Run(() => fixture.Publisher.PublishAsync(Message(), Destination(), cancellation.Token));
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        bool disposedDuringWait = fixture.Channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Dispose");
        confirmed.SetResult();
        Exception? failure = await Record.ExceptionAsync(() => publishing.WaitAsync(TimeSpan.FromSeconds(10)));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        disposedDuringWait.Should().BeFalse();
        fixture.Channel.Received(1).Dispose();
        fixture.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task PublishAsync_CancellationWhileQueuedDoesNotAffectActivePublication()
    {
        // Arrange
        await using Transport fixture = new();
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource waiting = Signal();
        TaskCompletionSource confirmed = Signal();
        fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
        {
            waiting.SetResult();
            confirmed.Task.GetAwaiter().GetResult();
        });

        // Act
        Task first = Task.Run(() => fixture.Publisher.PublishAsync(Message(), Destination()));
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task queued = fixture.Publisher.PublishAsync(Message(), Destination(), cancellation.Token);
        cancellation.Cancel();
        Exception? failure = await Record.ExceptionAsync(() => queued.WaitAsync(TimeSpan.FromSeconds(10)));
        confirmed.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        fixture.Connection.Received(1).CreateModel();
        fixture.Connection.DidNotReceive().Dispose();
    }

    [Fact]
    public async Task DisposeAsync_WaitsForActivePublicationAndRejectsLaterCalls()
    {
        // Arrange
        await using Transport fixture = new();
        TaskCompletionSource waiting = Signal();
        TaskCompletionSource confirmed = Signal();
        fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
        {
            waiting.SetResult();
            confirmed.Task.GetAwaiter().GetResult();
        });

        // Act
        Task publishing = Task.Run(() => fixture.Publisher.PublishAsync(Message(), Destination()));
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Task disposing = fixture.Publisher.DisposeAsync().AsTask();
        bool disposedDuringWait = disposing.IsCompleted;
        confirmed.SetResult();
        await Task.WhenAll(publishing, disposing).WaitAsync(TimeSpan.FromSeconds(10));
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination()));
        fixture.Publisher.Dispose();

        // Assert
        disposedDuringWait.Should().BeFalse();
        failure.Should().BeOfType<ObjectDisposedException>();
        fixture.Factory.Received(1).CreateConnection();
        fixture.Connection.Received(1).Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LaterPublication_ReplacesFailedOrClosedConnectionWithoutReplayingMessages(bool failedPublication)
    {
        // Arrange
        await using Transport fixture = new();
        IConnection secondConnection = Substitute.For<IConnection>();
        IModel secondChannel = Transport.CreateChannel();
        secondConnection.IsOpen.Returns(true);
        secondConnection.CreateModel().Returns(secondChannel);
        fixture.Factory.CreateConnection().Returns(fixture.Connection, secondConnection);
        if (failedPublication)
        {
            fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
                fixture.Channel.BasicReturn += Raise.Event<EventHandler<BasicReturnEventArgs>>(fixture.Channel, new BasicReturnEventArgs()));
        }

        // Act
        Exception? firstFailure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination()));
        fixture.Connection.IsOpen.Returns(false);
        await fixture.Publisher.PublishAsync(Message() with { MessageId = "later" }, Destination());

        // Assert
        (firstFailure is IOException).Should().Be(failedPublication);
        fixture.Factory.Received(2).CreateConnection();
        fixture.Connection.Received(1).Dispose();
        fixture.Channel.Received(1).BasicPublish("events", "created", true, fixture.Properties, Arg.Any<ReadOnlyMemory<byte>>());
        secondChannel.CreateBasicProperties().MessageId.Should().Be("later");
        secondChannel.Received(1).WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransportClosesBeforeCompletion_FailsConservatively(bool connectionClosed)
    {
        // Arrange
        await using Transport fixture = new();
        fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ =>
        {
            if (connectionClosed) fixture.Connection.IsOpen.Returns(false);
            else fixture.Channel.IsOpen.Returns(false);
        });

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination()));

        // Assert
        failure.Should().BeOfType<IOException>();
        fixture.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task CancellationDuringTopology_DoesNotSendMessage()
    {
        // Arrange
        await using Transport fixture = new();
        using CancellationTokenSource cancellation = new();
        fixture.Channel.When(channel => channel.ConfirmSelect()).Do(_ => cancellation.Cancel());

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination(), cancellation.Token));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        fixture.Channel.DidNotReceive().BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>());
        fixture.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task CancellationDuringChannelCleanup_DoesNotReportSuccess()
    {
        // Arrange
        await using Transport fixture = new();
        using CancellationTokenSource cancellation = new();
        fixture.Channel.When(channel => channel.Dispose()).Do(_ => cancellation.Cancel());

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination(), cancellation.Token));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        fixture.Channel.Received(1).WaitForConfirmsOrDie(TimeSpan.FromSeconds(10));
        fixture.Connection.Received(1).Dispose();
    }

    [Fact]
    public async Task PublicationAndChannelCleanupFailures_PreserveBothErrors()
    {
        // Arrange
        await using Transport fixture = new();
        IOException publicationFailure = new("Confirmation failed");
        IOException disposalFailure = new("Channel cleanup failed");
        fixture.Channel.When(channel => channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(10))).Do(_ => throw publicationFailure);
        fixture.Channel.When(channel => channel.Dispose()).Do(_ => throw disposalFailure);

        // Act
        Exception? failure = await Record.ExceptionAsync(() => fixture.Publisher.PublishAsync(Message(), Destination()));

        // Assert
        failure.Should().BeOfType<AggregateException>().Which.InnerExceptions.Should().Equal(publicationFailure, disposalFailure);
        fixture.Connection.Received(1).Dispose();
    }

    private static PublishMessage Message() => new("event-id", "created.v1", Encoding.UTF8.GetBytes("{\"text\":\"original\"}"), "application/json");
    private static RabbitMqPublishDestination Destination() => new("events", "created") { Queue = "application.v1" };
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Transport : IAsyncDisposable
    {
        public IConnectionFactory Factory { get; } = Substitute.For<IConnectionFactory>();
        public IConnection Connection { get; } = Substitute.For<IConnection>();
        public IModel Channel { get; } = CreateChannel();
        public IBasicProperties Properties { get; }
        public RabbitMqPublisher Publisher { get; }

        public Transport(RabbitMqPublisherOptions? options = null)
        {
            Properties = Channel.CreateBasicProperties();
            Factory.CreateConnection().Returns(Connection);
            Connection.IsOpen.Returns(true);
            Connection.CreateModel().Returns(Channel);
            Publisher = new(Factory, options ?? new());
        }

        public static IModel CreateChannel()
        {
            IModel channel = Substitute.For<IModel>();
            channel.IsOpen.Returns(true);
            channel.CreateBasicProperties().Returns(Substitute.For<IBasicProperties>());
            return channel;
        }

        public ValueTask DisposeAsync() => Publisher.DisposeAsync();
    }
}
