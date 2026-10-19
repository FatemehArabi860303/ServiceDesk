# RabbitMQ consumers

This library targets .NET 10 and RabbitMQ.Client 6.8.1. It has no application project references.
Implement `IMessageHandler`; successful completion means the application's work is complete.
The transport creates and asynchronously disposes a DI scope for every delivery, then issues one
manual acknowledgement. The message body may only be used during `HandleAsync`.

For example, a third application can register:

```csharp
services.AddScoped<MyMessageHandler>(); // Implements IMessageHandler
services.AddSingleton(new RabbitMqConsumerOptions("my-exchange", "my-queue", "my-key"));
services.AddSingleton<IConnectionFactory>(new ConnectionFactory
{
    HostName = "localhost",
    DispatchConsumersAsync = true,
    AutomaticRecoveryEnabled = false
});
services.AddSingleton<RabbitMqConsumer<MyMessageHandler>>();
services.AddHostedService<MyConsumerWorker>();

// In the application's hosting project:
sealed class MyConsumerWorker(RabbitMqConsumer<MyMessageHandler> consumer) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.RunAsync(stoppingToken);

    public override Task StopAsync(CancellationToken cancellationToken) =>
        base.StopAsync(CancellationToken.None);
}
```

A hosted service calls `RunAsync(stoppingToken)` and awaits it through shutdown before disposing
the service provider. Configure credentials through the application's configuration/secret store.
Custom `IConnectionFactory` implementations must also disable recovery and use async dispatch.

Topology, prefetch and handler-failure policy are configured using `RabbitMqConsumerOptions`.
The default policy stops the session on handler failure. `StopOnHandlerFailure = false` leaves
the failed delivery unacknowledged and permits later deliveries, as required by notifications.
Transport/acknowledgement failures always stop the session. There are no retries, nacks, DLQs or
automatic reconnects. A failed session requires an explicit operator restart; configure external
supervision accordingly to avoid a poison-message restart loop.

All channel calls are serialized with delivery processing. Shutdown cancels active processing,
waits for it to finish, and disposes transport. Queued callbacks cannot use the disposed channel.
Handlers must cooperate with cancellation; the host must not dispose dependencies during active work.
RabbitMQ connection creation in version 6.8.1 is synchronous and is bounded by the factory's
connection/socket timeouts rather than by the handler cancellation token.

Acknowledgements cannot be atomic with application effects. Handlers that require durable
deduplication must provide it themselves. SMTP delivery can repeat if acknowledgement fails;
the AI handler uses the existing persisted event-ID completion record on redelivery.
