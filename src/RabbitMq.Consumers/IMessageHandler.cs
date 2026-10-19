namespace RabbitMq.Consumers;

public interface IMessageHandler
{
    Task HandleAsync(MessageContext message, CancellationToken cancellationToken);
}
