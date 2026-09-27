using System.Text;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using RabbitMQ.Client;
using ServiceDesk.Core.Tickets;
using ServiceDesk.Messaging;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging.Tests;

public sealed class RabbitMqRequestProgressPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithAssignedEvent_PublishesPersistentUnchangedPayloadAndWaitsForConfirmation()
    {
        // Arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IModel>();
        var properties = Substitute.For<IBasicProperties>();
        connectionFactory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);
        var message = CreateMessage(RequestProgressKind.Assigned);
        var publisher = new RabbitMqRequestProgressPublisher(connectionFactory);
        string? publishedPayload = null;
        channel
            .When(call => call.BasicPublish(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<IBasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>()))
            .Do(call => publishedPayload = Encoding.UTF8.GetString(
                call.ArgAt<ReadOnlyMemory<byte>>(4).Span));

        // Act
        await publisher.PublishAsync(message);

        // Assert
        channel.Received(1).ExchangeDeclare(
            RabbitMqRequestProgressPublisher.ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);
        channel.Received(1).ConfirmSelect();
        channel.Received(1).BasicPublish(
            RabbitMqRequestProgressPublisher.ExchangeName,
            "request.progress.assigned",
            mandatory: false,
            properties,
            Arg.Any<ReadOnlyMemory<byte>>());
        channel.Received(1).WaitForConfirmsOrDie();
        properties.Persistent.Should().BeTrue();
        properties.MessageId.Should().Be(message.Id.ToString());
        publishedPayload.Should().Be(message.Payload);
    }

    [Fact]
    public void GetRoutingKey_WithWorkStartedEvent_ReturnsTheApprovedRoutingKey()
    {
        // Arrange

        // Act
        var routingKey = RabbitMqRequestProgressPublisher.GetRoutingKey(RequestProgressKind.WorkStarted);

        // Assert
        routingKey.Should().Be("request.progress.work-started");
    }

    private static OutboxMessageToPublish CreateMessage(RequestProgressKind kind) => new(
        Guid.Parse("71bbd91d-0a36-4ef9-9947-21ca9d6bc80b"),
        RequestProgressedV1.Type,
        JsonSerializer.Serialize(new RequestProgressedV1(
            Guid.Parse("71bbd91d-0a36-4ef9-9947-21ca9d6bc80b"),
            Guid.Parse("2e8bd8b0-5d33-42d3-b4ce-6a50f33ee5ae"),
            "customer@example.com",
            kind,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero))));
}
