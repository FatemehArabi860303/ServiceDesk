using Notification.Contracts;

namespace NotificationService.Shell;

public sealed class ProcessNotificationShell(IEmailSender emailSender)
{
    public async Task ExecuteAsync(
        NotificationRequestedV1 notification,
        CancellationToken cancellationToken = default)
    {
        NotificationRequestedValidator.Validate(notification);
        await emailSender.SendAsync(
            notification.Recipient,
            notification.Subject,
            notification.Body,
            cancellationToken);
    }
}
