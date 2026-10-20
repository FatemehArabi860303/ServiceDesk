namespace RabbitMq.Publishers;

public sealed record PublishMessage(
    string MessageId,
    string MessageType,
    ReadOnlyMemory<byte> Body,
    string ContentType);
