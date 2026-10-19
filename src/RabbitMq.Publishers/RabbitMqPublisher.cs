using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMq.Publishers;

/// <summary>
/// A reusable connection with one isolated, confirm-enabled channel per publication.
/// Publishing and disposal are serialized. Failed publications are never retried here.
/// </summary>
public sealed class RabbitMqPublisher(
    IConnectionFactory connectionFactory,
    RabbitMqPublisherOptions options) : IRabbitMqPublisher, IDisposable, IAsyncDisposable
{
    // Keep the gate alive so calls queued during disposal can observe the disposed flag safely.
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private bool disposed;

    public async Task PublishAsync(
        PublishMessage message,
        RabbitMqPublishDestination destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.MessageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.ContentType);
        ArgumentNullException.ThrowIfNull(destination.Exchange);
        ArgumentNullException.ThrowIfNull(destination.RoutingKey);
        if (options.ConfirmationTimeout <= TimeSpan.Zero
            || options.ConfirmationTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "A finite positive confirmation timeout is required.");
        }
        if (connectionFactory is ConnectionFactory { AutomaticRecoveryEnabled: true })
        {
            throw new InvalidOperationException("Automatic RabbitMQ recovery must be disabled.");
        }

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (connection is null || !connection.IsOpen)
                {
                    disposeConnection();
                    connection = connectionFactory.CreateConnection();
                }

                var channel = connection.CreateModel();
                Exception? publicationFailure = null;
                try
                {
                    if (destination.DeclareExchange)
                    {
                        channel.ExchangeDeclare(destination.Exchange, destination.ExchangeType,
                            destination.ExchangeDurable, destination.ExchangeAutoDelete);
                    }
                    if (destination.Queue is not null)
                    {
                        channel.QueueDeclare(destination.Queue, destination.QueueDurable,
                            destination.QueueExclusive, destination.QueueAutoDelete);
                        channel.QueueBind(destination.Queue, destination.Exchange, destination.RoutingKey);
                    }

                    channel.ConfirmSelect();
                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;
                    properties.MessageId = message.MessageId;
                    properties.Type = message.MessageType;
                    properties.ContentType = message.ContentType;

                    var returned = 0;
                    EventHandler<BasicReturnEventArgs> onReturn = (_, _) => Interlocked.Exchange(ref returned, 1);
                    channel.BasicReturn += onReturn;
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        channel.BasicPublish(destination.Exchange, destination.RoutingKey,
                            mandatory: true, properties, message.Body);
                        // 6.8.1 throws on nacks, timeout or channel loss. Its synchronous wait
                        // has no cancellation API: do not dispose the channel from another thread.
                        channel.WaitForConfirmsOrDie(options.ConfirmationTimeout);

                        // RabbitMQ sends mandatory returns before confirms on the same channel;
                        // the 6.8.1 client dispatches BasicReturn synchronously on that read path.
                        if (Volatile.Read(ref returned) != 0)
                        {
                            throw new IOException($"RabbitMQ returned message '{message.MessageId}' " +
                                $"from exchange '{destination.Exchange}' with routing key '{destination.RoutingKey}'.");
                        }
                        if (!channel.IsOpen || !connection.IsOpen)
                        {
                            throw new IOException("RabbitMQ transport closed during publication.");
                        }
                    }
                    finally
                    {
                        channel.BasicReturn -= onReturn;
                    }
                }
                catch (Exception exception)
                {
                    publicationFailure = exception;
                    throw;
                }
                finally
                {
                    try { channel.Dispose(); }
                    catch (Exception disposalFailure) when (publicationFailure is not null)
                    {
                        throw new AggregateException(publicationFailure, disposalFailure);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception failure)
            {
                // An ambiguous/failed session is never reused. A later application call may
                // create a fresh connection; it must keep the same identity for deduplication.
                try { disposeConnection(); }
                catch (Exception disposalFailure) { throw new AggregateException(failure, disposalFailure); }
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!disposed)
            {
                disposed = true;
                disposeConnection();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private void disposeConnection()
    {
        var previous = connection;
        connection = null;
        previous?.Dispose();
    }
}
