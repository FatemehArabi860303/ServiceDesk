using System.Text.Json;
using FluentAssertions;
using Notification.Contracts;
using NotificationService.Shell;
using NSubstitute;

namespace NotificationService.Shell.Tests;

public sealed class ProcessNotificationShellTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidNotification_SendsTheRequestedEmail()
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notification = CreateNotification();
        var shell = new ProcessNotificationShell(emailSender);

        // Act
        await shell.ExecuteAsync(notification);

        // Assert
        await emailSender.Received(1).SendAsync(
            notification.Recipient,
            notification.Subject,
            notification.Body,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceProducer_RejectsTheNotification()
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notification = CreateNotification() with { Producer = " " };
        var shell = new ProcessNotificationShell(emailSender);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(notification);

        // Assert
        await act.Should().ThrowAsync<InvalidNotificationRequestedException>();
        await emailSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRecipient_RejectsTheNotification()
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notification = CreateNotification() with { Recipient = "not-an-email" };
        var shell = new ProcessNotificationShell(emailSender);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(notification);

        // Assert
        await act.Should().ThrowAsync<InvalidNotificationRequestedException>();
        await emailSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Subject")]
    [InlineData("Body")]
    public async Task ExecuteAsync_WithMissingRequiredText_RejectsTheNotification(string field)
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notification = field switch
        {
            "Id" => CreateNotification() with { Id = " " },
            "Subject" => CreateNotification() with { Subject = " " },
            "Body" => CreateNotification() with { Body = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
        var shell = new ProcessNotificationShell(emailSender);

        // Act
        Func<Task> act = () => shell.ExecuteAsync(notification);

        // Assert
        await act.Should().ThrowAsync<InvalidNotificationRequestedException>();
        await emailSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithDeserializedNotification_CompletesSuccessfully()
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notification = JsonSerializer.Deserialize<NotificationRequestedV1>(JsonSerializer.Serialize(CreateNotification()));
        var shell = new ProcessNotificationShell(emailSender);

        // Act
        await shell.ExecuteAsync(notification!);

        // Assert
        await emailSender.Received(1).SendAsync(
            "customer@example.com",
            "Your service request has been assigned",
            "Your service request has been assigned to a support employee.",
            Arg.Any<CancellationToken>());
    }

    private static NotificationRequestedV1 CreateNotification() => new(
        "servicedesk",
        "d6835507-f1da-4a4f-b73a-c81caebb2efc",
        "customer@example.com",
        "Your service request has been assigned",
        "Your service request has been assigned to a support employee.");
}
