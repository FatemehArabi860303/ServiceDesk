namespace RabbitMq.Consumers;

// The body is valid only for the duration of HandleAsync; handlers must copy it to retain it.
public sealed record MessageContext(
    ReadOnlyMemory<byte> Body,
    string Exchange,
    string RoutingKey,
    string? MessageId,
    string? Type,
    string? ContentType,
    bool Redelivered);
