namespace NotificationService.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 25;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public string FromAddress { get; init; } = string.Empty;

    public bool EnableSsl { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Smtp:Host is required.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Smtp:Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress))
        {
            throw new InvalidOperationException("Smtp:FromAddress is required.");
        }

        if (string.IsNullOrWhiteSpace(UserName) != string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("Smtp:UserName and Smtp:Password must be configured together.");
        }
    }
}
