namespace RabbitMq.Publishers;

public sealed record RabbitMqPublishDestination(string Exchange, string RoutingKey)
{
    public bool DeclareExchange { get; init; } = true;
    public string ExchangeType { get; init; } = "direct";
    public bool ExchangeDurable { get; init; } = true;
    public bool ExchangeAutoDelete { get; init; }

    // Null means the application manages its own queues/bindings.
    public string? Queue { get; init; }
    public bool QueueDurable { get; init; } = true;
    public bool QueueExclusive { get; init; }
    public bool QueueAutoDelete { get; init; }
}
