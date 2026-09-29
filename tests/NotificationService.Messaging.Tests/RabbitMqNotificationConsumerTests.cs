using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Contracts;
using NotificationService.Messaging;
using NotificationService.Shell;
using NSubstitute;
using RabbitMQ.Client;

namespace NotificationService.Messaging.Tests;

public sealed class RabbitMqNotificationConsumerTests
{
    [Fact]
    public async Task HandleDeliveryAsync_WhenEmailSucceeds_AcknowledgesTheRabbitMqMessage()
    {
        // Arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var channel = Substitute.For<IModel>();
        var emailSender = Substitute.For<IEmailSender>();
        var shell = new ProcessNotificationShell(emailSender);
        var consumer = new RabbitMqNotificationConsumer(
            connectionFactory,
            CreateOptions(),
            shell,
            NullLogger<RabbitMqNotificationConsumer>.Instance);
        var notification = new NotificationRequestedV1(
            "servicedesk", "event-id", "customer@example.com", "Subject", "Body");
        var payload = JsonSerializer.SerializeToUtf8Bytes(notification);

        // Act
        await consumer.HandleDeliveryAsync(channel, 42, payload);

        // Assert
        await emailSender.Received(1).SendAsync("customer@example.com", "Subject", "Body", Arg.Any<CancellationToken>());
        channel.Received(1).BasicAck(42, multiple: false);
    }

    [Fact]
    public async Task HandleDeliveryAsync_WhenEmailFails_DoesNotAcknowledgeTheRabbitMqMessage()
    {
        // Arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var channel = Substitute.For<IModel>();
        var emailSender = Substitute.For<IEmailSender>();
        emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable.")));
        var shell = new ProcessNotificationShell(emailSender);
        var consumer = new RabbitMqNotificationConsumer(
            connectionFactory,
            CreateOptions(),
            shell,
            NullLogger<RabbitMqNotificationConsumer>.Instance);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new NotificationRequestedV1(
            "servicedesk", "event-id", "customer@example.com", "Subject", "Body"));

        // Act
        await consumer.HandleDeliveryAsync(channel, 42, payload);

        // Assert
        channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
    }

    [Fact]
    public void Start_DeclaresTheApprovedGenericTopology()
    {
        // Arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IModel>();
        connectionFactory.CreateConnection().Returns(connection);
        connection.CreateModel().Returns(channel);
        var shell = new ProcessNotificationShell(Substitute.For<IEmailSender>());
        var consumer = new RabbitMqNotificationConsumer(
            connectionFactory,
            CreateOptions(),
            shell,
            NullLogger<RabbitMqNotificationConsumer>.Instance);

        // Act
        consumer.Start();

        // Assert
        channel.Received(1).ExchangeDeclare(
            RabbitMqNotificationConsumer.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null);
        channel.Received(1).QueueDeclare(
            RabbitMqNotificationConsumer.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.Received(1).QueueBind(
            RabbitMqNotificationConsumer.QueueName,
            RabbitMqNotificationConsumer.ExchangeName,
            NotificationRequestedV1.Type,
            arguments: null);
        consumer.Dispose();
    }

    private static RabbitMqNotificationOptions CreateOptions() => new()
    {
        HostName = "localhost",
        UserName = "guest",
        Password = "guest"
    };
}
