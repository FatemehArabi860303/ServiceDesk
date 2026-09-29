namespace NotificationService.Messaging;

public sealed class RabbitMqNotificationOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = string.Empty;

    public int Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName)
            || string.IsNullOrWhiteSpace(VirtualHost)
            || string.IsNullOrWhiteSpace(UserName)
            || string.IsNullOrWhiteSpace(Password)
            || Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("RabbitMQ configuration is invalid.");
        }
    }
}
