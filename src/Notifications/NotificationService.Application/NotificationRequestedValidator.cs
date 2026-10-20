using System.Net.Mail;
using Notification.Contracts;

namespace NotificationService.Shell;

public static class NotificationRequestedValidator
{
    public static void Validate(NotificationRequestedV1 notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        RequireText(notification.Producer, nameof(notification.Producer));
        RequireText(notification.Id, nameof(notification.Id));
        RequireText(notification.Recipient, nameof(notification.Recipient));
        RequireText(notification.Subject, nameof(notification.Subject));
        RequireText(notification.Body, nameof(notification.Body));

        try
        {
            var address = new MailAddress(notification.Recipient);
            if (!string.Equals(address.Address, notification.Recipient, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidNotificationRequestedException("Recipient must be a syntactically valid email address.");
            }
        }
        catch (FormatException)
        {
            throw new InvalidNotificationRequestedException("Recipient must be a syntactically valid email address.");
        }
    }

    private static void RequireText(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidNotificationRequestedException($"{fieldName} is required.");
        }
    }
}
