# RabbitMQ publishers

Application-independent .NET 10 transport using RabbitMQ.Client 6.8.1, with no application
project references or serializer dependencies. Applications provide serialized bytes, message
metadata and a destination. All publications are persistent, mandatory and confirmed.

## Registration and use

```csharp
services.AddSingleton<IConnectionFactory>(new ConnectionFactory
{
    HostName = "localhost",
    AutomaticRecoveryEnabled = false
});
services.AddSingleton(new RabbitMqPublisherOptions()); // 10-second confirmation timeout
services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// In your application's adapter (serialization belongs here):
await publisher.PublishAsync(
    new PublishMessage(eventId.ToString(), "order.created.v1", jsonBytes, "application/json"),
    new RabbitMqPublishDestination("orders", "created") { Queue = "billing.orders.v1" },
    cancellationToken);
```

Use your application's configuration/secret store for connection settings. The host owns
publisher disposal. Custom factories must disable automatic recovery as well. Callers must
keep the message body's bytes unchanged until the publish task finishes. Destinations can
configure exchange type and durability, queue flags, omit queue declaration, or disable
exchange declaration for externally managed topology (including the default exchange).
Do not request exclusive/auto-delete queues when durable delivery is required.

## Lifecycle decision

ServiceDesk's Outbox worker processes one claimed message at a time. The publisher keeps one
lazy connection across successful calls, avoiding a connection handshake for each event.
Each publication gets a fresh channel, with a single outstanding message, so mandatory returns
and publisher confirms cannot be attributed to a different publication. A semaphore serializes
publishing and both synchronous/asynchronous disposal. This intentionally favors simple
correctness over parallel publishing throughput; there is no pooling or batching.

The channel is disposed after confirmation, then success is reported. Failure closes the
connection and propagates to the caller. A later application publish call can establish a
new connection; the library never retries or replays a failed publication itself. Disposal
waits for active publishing, closes the connection and prevents subsequent calls.

## Confirmation and failure contract

Success requires `WaitForConfirmsOrDie` to succeed, no mandatory return, an open transport,
and no cancellation. Nacks, timeout, connection/channel loss, returns, topology errors and
disposal failures propagate. RabbitMQ sends mandatory returns before confirmation on the
same channel; 6.8.1 dispatches `BasicReturn` synchronously before processing the later confirm.
The return callback only records a flag and never touches transport resources.

Publisher confirmation means broker acceptance, **not consumer processing**. See RabbitMQ's
[confirmation ordering and persistence contract](https://www.rabbitmq.com/docs/confirms#when-publishes-are-confirmed)
and the [6.8.1 confirmation implementation](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/v6.8.1/projects/RabbitMQ.Client/client/impl/ModelBase.cs).

6.8.1 connection creation, topology operations and confirmation waits are synchronous and cannot
be interrupted using a cancellation token. Queued calls cancel promptly; active calls check
cancellation before sending and after confirmation/channel cleanup. They never return success
when cancellation is observed. Shutdown may wait for the configured confirmation timeout plus
client socket/RPC/close timeouts. Broker flow control can also delay a synchronous send until
the broker unblocks or the connection closes. No cancellation callback disposes an in-use channel.

Publication and application database commits are not atomic. A confirmed publication followed
by cancellation or database failure can be published again. Applications must retain message
identity and provide deduplication when needed. ServiceDesk preserves event IDs, its existing
AI completion record and its existing SMTP delivery semantics.

## ServiceDesk boundary

`RabbitMqOutboxMessagePublisher` resolves `RabbitMqOutboxRouting` and maps the original UTF-8
Outbox payload and metadata into this transport. `ProcessOutboxShell` still owns claiming and
marking `PublishedAt`, only after successful publishing. Neither Outbox state nor event routing
belongs in this library. The unused notification-only publisher/interface have been removed;
the adapter publishes both notification and AI events using their existing topology.
