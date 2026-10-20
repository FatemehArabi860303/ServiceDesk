namespace RabbitMq.Publishers;

public sealed record RabbitMqPublisherOptions
{
    public TimeSpan ConfirmationTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
