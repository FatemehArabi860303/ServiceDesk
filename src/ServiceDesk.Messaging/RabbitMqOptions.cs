namespace ServiceDesk.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = string.Empty;

    public int Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName))
        {
            throw new InvalidOperationException("RabbitMq:HostName is required.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("RabbitMq:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(VirtualHost))
        {
            throw new InvalidOperationException("RabbitMq:VirtualHost is required.");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new InvalidOperationException("RabbitMq:UserName is required.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("RabbitMq:Password is required.");
        }
    }
}
