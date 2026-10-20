using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMq.Consumers;

/// <summary>
/// One manually acknowledged session. All application-initiated channel operations and handlers
/// are serialized. Failure never nacks/requeues a delivery or automatically restarts a session.
/// The host must await RunAsync before disposing its service provider.
/// </summary>
public sealed class RabbitMqConsumer<THandler>(
    IConnectionFactory connectionFactory,
    RabbitMqConsumerOptions options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqConsumer<THandler>> logger) where THandler : class, IMessageHandler
{
    private int started;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            throw new InvalidOperationException("A consumer instance can run only one session.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Exchange);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Queue);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RoutingKey);

        // Recovery would create another session outside our lifecycle and fail-stop policy.
        if (connectionFactory is ConnectionFactory { AutomaticRecoveryEnabled: true })
        {
            throw new InvalidOperationException("Automatic RabbitMQ recovery must be disabled.");
        }
        if (connectionFactory is ConnectionFactory { DispatchConsumersAsync: false })
        {
            throw new InvalidOperationException("Asynchronous RabbitMQ consumer dispatch must be enabled.");
        }

        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = processing.Token;
        // Do not dispose the gate: broker callbacks already queued may still enter after shutdown.
        var gate = new SemaphoreSlim(1, 1);
        IConnection? connection = null;
        IModel? channel = null;
        AsyncEventingBasicConsumer? consumer = null;
        using var registration = cancellationToken.Register(() => stopped.TrySetResult(null));
        EventHandler<ShutdownEventArgs> shutdown = (_, args) =>
            stopped.TrySetResult(new IOException($"RabbitMQ session shut down: {args.ReplyCode} {args.ReplyText}"));

        async Task onDelivery(object sender, BasicDeliverEventArgs args)
        {
            try
            {
                await gate.WaitAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                if (token.IsCancellationRequested || stopped.Task.IsCompleted)
                {
                    return;
                }

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                    var message = new MessageContext(args.Body, args.Exchange, args.RoutingKey,
                        args.BasicProperties?.MessageId, args.BasicProperties?.Type,
                        args.BasicProperties?.ContentType, args.Redelivered);
                    await handler.HandleAsync(message, token);
                    token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Message processing failed; delivery remains unacknowledged.");
                    if (options.StopOnHandlerFailure)
                    {
                        stopped.TrySetResult(exception);
                    }
                    return;
                }

                // Scope disposal is part of successful processing. Shutdown prevents queued work
                // and acknowledgements even if a handler ignores cancellation and returns normally.
                if (!token.IsCancellationRequested && !stopped.Task.IsCompleted)
                {
                    try
                    {
                        channel!.BasicAck(args.DeliveryTag, multiple: false);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Acknowledgement failed; stopping the RabbitMQ session.");
                        stopped.TrySetResult(exception);
                    }
                }
            }
            finally
            {
                gate.Release();
            }
        }

        Task onConsumerCancelled(object sender, ConsumerEventArgs args)
        {
            stopped.TrySetResult(new IOException("RabbitMQ cancelled the consumer subscription."));
            return Task.CompletedTask;
        }

        try
        {
            await gate.WaitAsync(CancellationToken.None);
            try
            {
                token.ThrowIfCancellationRequested();
                connection = connectionFactory.CreateConnection();
                connection.ConnectionShutdown += shutdown;
                channel = connection.CreateModel();
                channel.ModelShutdown += shutdown;
                channel.ExchangeDeclare(options.Exchange, options.ExchangeType, options.Durable);
                channel.QueueDeclare(options.Queue, options.Durable, options.Exclusive, options.AutoDelete);
                channel.QueueBind(options.Queue, options.Exchange, options.RoutingKey);
                channel.BasicQos(0, options.PrefetchCount, global: false);
                consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += onDelivery;
                consumer.ConsumerCancelled += onConsumerCancelled;
                channel.BasicConsume(options.Queue, autoAck: false, consumer);
                if (!connection.IsOpen || !channel.IsOpen)
                {
                    stopped.TrySetResult(new IOException("RabbitMQ closed while starting the consumer."));
                }
            }
            finally
            {
                gate.Release();
            }

            var failure = await stopped.Task;
            if (failure is not null && !cancellationToken.IsCancellationRequested)
            {
                throw new IOException("RabbitMQ consumer session failed; manual restart is required.", failure);
            }
        }
        finally
        {
            try
            {
                await processing.CancelAsync();
            }
            finally
            {
                // Cancellation reaches the handler before waiting; no resource is disposed until
                // the active callback releases the gate. Later callbacks observe cancellation.
                await gate.WaitAsync(CancellationToken.None);
                try
                {
                    if (consumer is not null)
                    {
                        consumer.Received -= onDelivery;
                        consumer.ConsumerCancelled -= onConsumerCancelled;
                    }
                    if (channel is not null)
                    {
                        channel.ModelShutdown -= shutdown;
                        try { channel.Dispose(); }
                        catch (Exception exception) { logger.LogError(exception, "RabbitMQ channel disposal failed."); }
                    }
                    if (connection is not null)
                    {
                        connection.ConnectionShutdown -= shutdown;
                        try { connection.Dispose(); }
                        catch (Exception exception) { logger.LogError(exception, "RabbitMQ connection disposal failed."); }
                    }
                }
                finally
                {
                    gate.Release();
                }
            }
        }
    }
}
