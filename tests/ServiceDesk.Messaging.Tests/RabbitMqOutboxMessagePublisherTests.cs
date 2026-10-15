using System.Text;
using FluentAssertions;
using Notification.Contracts;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ServiceDesk.Contracts;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging.Tests;

public sealed class RabbitMqOutboxMessagePublisherTests
{
    [Theory]
    [InlineData(
        NotificationRequestedV1.Type,
        "notifications",
        "notification-service.email.v1")]
    [InlineData(
        TicketAiAnalysisRequestedV1.Type,
        "servicedesk.ai-analysis",
        "servicedesk.ai-analysis.v1")]
    public async Task PublishAsync_WithSupportedEvent_DeclaresDestinationAndPublishesOriginalPayload(
        string eventType,
        string expectedExchange,
        string expectedQueue)
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        IConnection connection = Substitute.For<IConnection>();
        IModel channel = Substitute.For<IModel>();
        IBasicProperties properties = Substitute.For<IBasicProperties>();

        factory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);

        RabbitMqOutboxRouting routing = createRouting();
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            eventType,
            """{"original":"payload","unchanged":true}""");

        string? actualPayload = null;

        channel.When(model => model.BasicPublish(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<IBasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>()))
            .Do(call =>
            {
                actualPayload = Encoding.UTF8.GetString(
                    call.ArgAt<ReadOnlyMemory<byte>>(4).Span);
            });

        // Act
        await publisher.PublishAsync(message);

        // Assert
        Received.InOrder(() =>
        {
            channel.ExchangeDeclare(
                expectedExchange,
                ExchangeType.Direct,
                true,
                false,
                null);

            channel.QueueDeclare(
                expectedQueue,
                true,
                false,
                false,
                null);

            channel.QueueBind(
                expectedQueue,
                expectedExchange,
                eventType,
                null);

            channel.ConfirmSelect();

            channel.BasicPublish(
                expectedExchange,
                eventType,
                true,
                properties,
                Arg.Any<ReadOnlyMemory<byte>>());

            channel.WaitForConfirmsOrDie(
                RabbitMqOutboxMessagePublisher.ConfirmationTimeout);
        });

        properties.Persistent.Should().BeTrue();
        properties.ContentType.Should().Be("application/json");
        properties.Type.Should().Be(eventType);
        properties.MessageId.Should().Be(message.Id.ToString());
        actualPayload.Should().Be(message.Payload);
    }

    [Fact]
    public async Task PublishAsync_WithUnsupportedEvent_FailsBeforeConnecting()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        RabbitMqOutboxRouting routing = createRouting();
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            "unsupported.event.v1",
            "{}");

        Func<Task> operation = () => publisher.PublishAsync(message);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeOfType<NotSupportedException>();
        factory.DidNotReceive().CreateConnection();
    }

    [Fact]
    public async Task PublishAsync_WhenBrokerReturnsMessage_FailsEvenIfConfirmationSucceeds()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        IConnection connection = Substitute.For<IConnection>();
        IModel channel = Substitute.For<IModel>();
        IBasicProperties properties = Substitute.For<IBasicProperties>();

        factory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);

        RabbitMqOutboxRouting routing = createRouting();
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            TicketAiAnalysisRequestedV1.Type,
            "{}");

        BasicReturnEventArgs returned = new()
        {
            ReplyCode = 312,
            ReplyText = "NO_ROUTE",
            Exchange = "servicedesk.ai-analysis",
            RoutingKey = TicketAiAnalysisRequestedV1.Type
        };

        channel.When(model => model.WaitForConfirmsOrDie(
                RabbitMqOutboxMessagePublisher.ConfirmationTimeout))
            .Do(_ =>
            {
                channel.BasicReturn +=
                    Raise.Event<EventHandler<BasicReturnEventArgs>>(
                        channel,
                        returned);
            });

        Func<Task> operation = () => publisher.PublishAsync(message);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeOfType<IOException>();

        channel.Received(1).BasicPublish(
            "servicedesk.ai-analysis",
            TicketAiAnalysisRequestedV1.Type,
            true,
            properties,
            Arg.Any<ReadOnlyMemory<byte>>());

        channel.Received(1).WaitForConfirmsOrDie(
            RabbitMqOutboxMessagePublisher.ConfirmationTimeout);
    }

    [Fact]
    public async Task PublishAsync_WhenConfirmationFails_PropagatesFailure()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        IConnection connection = Substitute.For<IConnection>();
        IModel channel = Substitute.For<IModel>();
        IBasicProperties properties = Substitute.For<IBasicProperties>();

        factory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);

        IOException expectedException = new("Confirmation failed.");

        channel.When(model => model.WaitForConfirmsOrDie(
                RabbitMqOutboxMessagePublisher.ConfirmationTimeout))
            .Do(_ => throw expectedException);

        RabbitMqOutboxRouting routing = createRouting();
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            TicketAiAnalysisRequestedV1.Type,
            "{}");

        Func<Task> operation = () => publisher.PublishAsync(message);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeSameAs(expectedException);
    }

    [Fact]
    public async Task PublishAsync_WhenTopologyDeclarationFails_DoesNotPublish()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        IConnection connection = Substitute.For<IConnection>();
        IModel channel = Substitute.For<IModel>();

        factory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);

        IOException expectedException = new("Topology declaration failed.");

        channel.When(model => model.QueueBind(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, object>>()))
            .Do(_ => throw expectedException);

        RabbitMqOutboxRouting routing = createRouting();
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            TicketAiAnalysisRequestedV1.Type,
            "{}");

        Func<Task> operation = () => publisher.PublishAsync(message);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeSameAs(expectedException);

        channel.DidNotReceive().BasicPublish(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<IBasicProperties>(),
            Arg.Any<ReadOnlyMemory<byte>>());
    }

    [Fact]
    public async Task PublishAsync_WithCanceledToken_DoesNotConnect()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        RabbitMqOutboxRouting routing = createRouting();
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            TicketAiAnalysisRequestedV1.Type,
            "{}");

        using CancellationTokenSource source = new();
        source.Cancel();

        Func<Task> operation = () =>
            publisher.PublishAsync(message, source.Token);

        // Act
        Exception? exception = await Record.ExceptionAsync(operation);

        // Assert
        exception.Should().BeAssignableTo<OperationCanceledException>();
        factory.DidNotReceive().CreateConnection();
    }

    [Fact]
    public async Task PublishAsync_WithAdditionalRegisteredRoute_UsesItWithoutPublisherChanges()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        IConnection connection = Substitute.For<IConnection>();
        IModel channel = Substitute.For<IModel>();
        IBasicProperties properties = Substitute.For<IBasicProperties>();

        factory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);

        RabbitMqOutboxRoute additionalRoute = new(
            "additional.event.v1",
            "additional-events",
            "additional-events.v1",
            "additional.event.v1");

        RabbitMqOutboxRouting routing = new([additionalRoute]);
        RabbitMqOutboxMessagePublisher publisher = new(factory, routing);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            additionalRoute.EventType,
            "{}");

        // Act
        await publisher.PublishAsync(message);

        // Assert
        channel.Received(1).QueueBind(
            additionalRoute.QueueName,
            additionalRoute.ExchangeName,
            additionalRoute.RoutingKey,
            null);

        channel.Received(1).BasicPublish(
            additionalRoute.ExchangeName,
            additionalRoute.RoutingKey,
            true,
            properties,
            Arg.Any<ReadOnlyMemory<byte>>());
    }

    private static RabbitMqOutboxRouting createRouting() => new(
    [
        RabbitMqOutboxRoute.Notification,
        RabbitMqOutboxRoute.AiAnalysis
    ]);
}