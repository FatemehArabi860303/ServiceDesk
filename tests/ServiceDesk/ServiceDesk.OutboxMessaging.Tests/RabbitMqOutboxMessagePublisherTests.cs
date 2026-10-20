using System.Text;
using FluentAssertions;
using Notification.Contracts;
using NSubstitute;
using RabbitMq.Publishers;
using ServiceDesk.Contracts;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging.Tests;

public sealed class RabbitMqOutboxMessagePublisherTests
{
    [Theory]
    [InlineData(NotificationRequestedV1.Type, "notifications", "notification-service.email.v1")]
    [InlineData(TicketAiAnalysisRequestedV1.Type, "servicedesk.ai-analysis", "servicedesk.ai-analysis.v1")]
    [InlineData("additional.event.v1", "additional-events", "additional-events.v1")]
    public async Task PublishAsync_MapsEventRouteAndOriginalPayload(string type, string exchange, string queue)
    {
        // Arrange
        IRabbitMqPublisher transport = Substitute.For<IRabbitMqPublisher>();
        RabbitMqOutboxRouting routing = new([
            RabbitMqOutboxRoute.Notification,
            RabbitMqOutboxRoute.AiAnalysis,
            new("additional.event.v1", "additional-events", "additional-events.v1", "additional.event.v1")]);
        RabbitMqOutboxMessagePublisher publisher = new(transport, routing);
        OutboxMessageToPublish message = new(Guid.NewGuid(), type, """{"original":"unchanged"}""");
        using CancellationTokenSource cancellation = new();
        PublishMessage? actual = null;
        RabbitMqPublishDestination? destination = null;
        transport.PublishAsync(Arg.Any<PublishMessage>(), Arg.Any<RabbitMqPublishDestination>(), cancellation.Token)
            .Returns(call =>
            {
                actual = call.Arg<PublishMessage>();
                destination = call.Arg<RabbitMqPublishDestination>();
                return Task.CompletedTask;
            });

        // Act
        await publisher.PublishAsync(message, cancellation.Token);

        // Assert
        actual.Should().NotBeNull();
        actual!.MessageId.Should().Be(message.Id.ToString());
        actual.MessageType.Should().Be(type);
        actual.ContentType.Should().Be("application/json");
        Encoding.UTF8.GetString(actual.Body.Span).Should().Be(message.Payload);
        destination.Should().Be(new RabbitMqPublishDestination(exchange, type) { Queue = queue });
        await transport.Received(1).PublishAsync(actual, destination!, cancellation.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_PropagatesTransportFailureAndDoesNotMarkOutbox(bool cancelled)
    {
        // Arrange
        IRabbitMqPublisher transport = Substitute.For<IRabbitMqPublisher>();
        IOutboxRepository repository = Substitute.For<IOutboxRepository>();
        RabbitMqOutboxMessagePublisher publisher = new(transport, new([RabbitMqOutboxRoute.AiAnalysis]));
        OutboxMessageToPublish message = new(Guid.NewGuid(), TicketAiAnalysisRequestedV1.Type, "{}");
        Exception expected = cancelled ? new OperationCanceledException() : new IOException("Broker publication failed");
        transport.PublishAsync(Arg.Any<PublishMessage>(), Arg.Any<RabbitMqPublishDestination>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(expected));
        repository.TryClaimNextAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<OutboxMessageToPublish?>(message));
        ProcessOutboxShell shell = new(repository, publisher);

        // Act
        Exception? failure = await Record.ExceptionAsync(() => shell.ProcessNextAsync());

        // Assert
        failure.Should().BeSameAs(expected);
        await repository.DidNotReceive().MarkPublishedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_UnsupportedEventDoesNotCallTransport()
    {
        // Arrange
        IRabbitMqPublisher transport = Substitute.For<IRabbitMqPublisher>();
        RabbitMqOutboxMessagePublisher publisher = new(transport, new([RabbitMqOutboxRoute.Notification]));
        OutboxMessageToPublish message = new(Guid.NewGuid(), "unsupported", "{}");

        // Act
        Exception? failure = await Record.ExceptionAsync(() => publisher.PublishAsync(message));

        // Assert
        failure.Should().BeOfType<NotSupportedException>();
        await transport.DidNotReceive().PublishAsync(Arg.Any<PublishMessage>(), Arg.Any<RabbitMqPublishDestination>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_CancellationBeforePublishingDoesNotCallTransport()
    {
        // Arrange
        IRabbitMqPublisher transport = Substitute.For<IRabbitMqPublisher>();
        RabbitMqOutboxMessagePublisher publisher = new(transport, new([RabbitMqOutboxRoute.Notification]));
        OutboxMessageToPublish message = new(Guid.NewGuid(), NotificationRequestedV1.Type, "{}");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        // Act
        Exception? failure = await Record.ExceptionAsync(() => publisher.PublishAsync(message, cancellation.Token));

        // Assert
        failure.Should().BeAssignableTo<OperationCanceledException>();
        await transport.DidNotReceive().PublishAsync(Arg.Any<PublishMessage>(), Arg.Any<RabbitMqPublishDestination>(), Arg.Any<CancellationToken>());
    }
}
