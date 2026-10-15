using System.Text;
using FluentAssertions;
using Notification.Contracts;
using NSubstitute;
using RabbitMQ.Client;
using ServiceDesk.Shell.Notifications;

namespace ServiceDesk.Messaging.Tests;

public sealed class RabbitMqNotificationRequestedPublisherTests
{
    [Fact]
    public async Task PublishAsync_PreservesNotificationDestinationAndPayload()
    {
        // Arrange
        IConnectionFactory factory = Substitute.For<IConnectionFactory>();
        IConnection connection = Substitute.For<IConnection>();
        IModel channel = Substitute.For<IModel>();
        IBasicProperties properties = Substitute.For<IBasicProperties>();

        factory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        channel.CreateBasicProperties().Returns(properties);

        OutboxMessageToPublish message = new(
            Guid.NewGuid(),
            NotificationRequestedV1.Type,
            """{"original":"notification"}""");

        RabbitMqNotificationRequestedPublisher publisher = new(factory);
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
        channel.Received(1).QueueBind(
            "notification-service.email.v1",
            "notifications",
            NotificationRequestedV1.Type,
            null);

        channel.Received(1).BasicPublish(
            "notifications",
            NotificationRequestedV1.Type,
            true,
            properties,
            Arg.Any<ReadOnlyMemory<byte>>());

        channel.Received(1).WaitForConfirmsOrDie(
            RabbitMqOutboxMessagePublisher.ConfirmationTimeout);

        properties.Persistent.Should().BeTrue();
        actualPayload.Should().Be(message.Payload);
    }
}