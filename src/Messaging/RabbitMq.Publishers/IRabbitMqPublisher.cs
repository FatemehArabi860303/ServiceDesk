namespace RabbitMq.Publishers;

public interface IRabbitMqPublisher
{
    // Success means a persistent mandatory publication was confirmed without a return.
    // It does not mean that any consumer has processed the message.
    Task PublishAsync(
        PublishMessage message,
        RabbitMqPublishDestination destination,
        CancellationToken cancellationToken = default);
}
