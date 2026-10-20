using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Notification.Contracts;
using NotificationService.Messaging;
using NotificationService.Shell;
using NSubstitute;
using RabbitMQ.Client;
using TestSupport;

namespace NotificationService.Messaging.Tests;

public sealed class NotificationMessageHandlerTests
{
    [Fact]
    public async Task Delivery_WaitsForEmailThenAcknowledges()
    {
        // Arrange
        IEmailSender sender = Substitute.For<IEmailSender>();
        TaskCompletionSource sent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(sent.Task);
        await using ConsumerSession<NotificationMessageHandler> session = CreateSession(sender);
        session.Start();

        // Act
        Task delivery = session.DeliverAsync(Payload());
        bool acknowledgedEarly = session.Channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "BasicAck");
        sent.SetResult();
        await delivery;

        // Assert
        acknowledgedEarly.Should().BeFalse();
        await sender.Received(1).SendAsync("customer@example.com", "Subject", "Body", Arg.Any<CancellationToken>());
        session.Channel.Received(1).BasicAck(42, false);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    public async Task InvalidNotification_RemainsUnacknowledgedWithoutStoppingSession(string json)
    {
        // Arrange
        IEmailSender sender = Substitute.For<IEmailSender>();
        await using ConsumerSession<NotificationMessageHandler> session = CreateSession(sender);
        session.Start();

        // Act
        await session.DeliverAsync(Encoding.UTF8.GetBytes(json));

        // Assert
        session.Running.IsCompleted.Should().BeFalse();
        session.Channel.DidNotReceive().BasicAck(Arg.Any<ulong>(), Arg.Any<bool>());
        await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailFailure_RemainsUnacknowledgedAndAllowsNextDelivery()
    {
        // Arrange
        IEmailSender sender = Substitute.For<IEmailSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("SMTP unavailable")), Task.CompletedTask);
        await using ConsumerSession<NotificationMessageHandler> session = CreateSession(sender);
        session.Start();

        // Act
        await session.DeliverAsync(Payload(), tag: 1);
        await session.DeliverAsync(Payload(), tag: 2);

        // Assert
        session.Running.IsCompleted.Should().BeFalse();
        session.Channel.DidNotReceive().BasicAck(1, false);
        session.Channel.Received(1).BasicAck(2, false);
    }

    [Fact]
    public async Task Startup_PreservesNotificationTopologyAndUnlimitedPrefetch()
    {
        // Arrange
        await using ConsumerSession<NotificationMessageHandler> session = CreateSession(Substitute.For<IEmailSender>());

        // Act
        session.Start();

        // Assert
        session.Channel.Received(1).ExchangeDeclare("notifications", "direct", true, false, null);
        session.Channel.Received(1).QueueDeclare("notification-service.email.v1", true, false, false, null);
        session.Channel.Received(1).QueueBind("notification-service.email.v1", "notifications", NotificationRequestedV1.Type, null);
        session.Channel.Received(1).BasicQos(0, 0, false);
    }

    private static byte[] Payload() => JsonSerializer.SerializeToUtf8Bytes(new NotificationRequestedV1(
        "servicedesk", "event-id", "customer@example.com", "Subject", "Body"));

    private static ConsumerSession<NotificationMessageHandler> CreateSession(IEmailSender sender) =>
        new(NotificationMessageHandler.ConsumerOptions, services =>
        {
            services.AddSingleton(sender);
            services.AddScoped<ProcessNotificationShell>();
            services.AddScoped<NotificationMessageHandler>();
        });
}
