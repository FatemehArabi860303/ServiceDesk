namespace Notification.Contracts;

public sealed record NotificationRequestedV1(
    string Producer,
    string Id,
    string Recipient,
    string Subject,
    string Body)
{
    public const string Type = "notification.requested.v1";
}
