namespace RabbitMq.Consumers;

public sealed record RabbitMqConsumerOptions(string Exchange, string Queue, string RoutingKey)
{
    public string ExchangeType { get; init; } = "direct";
    public bool Durable { get; init; } = true;
    public bool Exclusive { get; init; }
    public bool AutoDelete { get; init; }
    public ushort PrefetchCount { get; init; } = 1;
    public bool StopOnHandlerFailure { get; init; } = true;
}
