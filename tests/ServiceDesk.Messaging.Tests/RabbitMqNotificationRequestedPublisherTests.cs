using System.Text;
using System.Text.Json;
using FluentAssertions;
using Notification.Contracts;
using NSubstitute;
using RabbitMQ.Client;
using ServiceDesk.Messaging;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging.Tests;

public sealed class RabbitMqNotificationRequestedPublisherTests
{
    [Fact]
    public async Task PublishAsync_PublishesPersistentGenericPayloadAndWaitsForConfirmation()
    {
        // Arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IModel>();
        var properties = Substitute.For<IBasicProperties>();
        connectionFactory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);
        var notification = new NotificationRequestedV1(
            "servicedesk", "notification-id", "customer@example.com", "Request assigned", "Your request was assigned.");
        var message = new OutboxMessageToPublish(
            Guid.Parse("71bbd91d-0a36-4ef9-9947-21ca9d6bc80b"),
            NotificationRequestedV1.Type,
            JsonSerializer.Serialize(notification));
        var publisher = new RabbitMqNotificationRequestedPublisher(connectionFactory);
        string? publishedPayload = null;
        channel.When(call => call.BasicPublish(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>()))
            .Do(call => publishedPayload = Encoding.UTF8.GetString(call.ArgAt<ReadOnlyMemory<byte>>(4).Span));

        // Act
        await publisher.PublishAsync(message);

        // Assert
        channel.Received(1).ExchangeDeclare(
            RabbitMqNotificationRequestedPublisher.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null);
        channel.Received(1).ConfirmSelect();
        channel.Received(1).BasicPublish(
            RabbitMqNotificationRequestedPublisher.ExchangeName,
            NotificationRequestedV1.Type,
            mandatory: false,
            properties,
            Arg.Any<ReadOnlyMemory<byte>>());
        channel.Received(1).WaitForConfirmsOrDie();
        properties.Persistent.Should().BeTrue();
        properties.MessageId.Should().Be(message.Id.ToString());
        publishedPayload.Should().Be(message.Payload);
    }
}
