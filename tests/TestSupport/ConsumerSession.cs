using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMq.Consumers;

namespace TestSupport;

public sealed class ConsumerSession<THandler> : IAsyncDisposable where THandler : class, IMessageHandler
{
    public IConnectionFactory Factory { get; } = Substitute.For<IConnectionFactory>();
    public IConnection Connection { get; } = Substitute.For<IConnection>();
    public IModel Channel { get; } = Substitute.For<IModel>();
    public CancellationTokenSource Cancellation { get; } = new();
    public AsyncEventingBasicConsumer Subscription { get; private set; } = null!;
    public ServiceProvider Services { get; }
    public RabbitMqConsumer<THandler> Consumer { get; }
    public Task Running { get; private set; } = Task.CompletedTask;
    public TaskCompletionSource Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ConsumerSession(RabbitMqConsumerOptions options, Action<IServiceCollection> configure)
    {
        Factory.CreateConnection().Returns(Connection);
        Connection.CreateModel().Returns(Channel);
        Connection.IsOpen.Returns(true);
        Channel.IsOpen.Returns(true);
        Channel.BasicConsume(Arg.Any<string>(), false, Arg.Any<string>(), false, false,
                Arg.Any<IDictionary<string, object>>(), Arg.Any<IBasicConsumer>())
            .Returns(call =>
            {
                Subscription = (AsyncEventingBasicConsumer)call[6];
                Subscribed.TrySetResult();
                return "consumer-tag";
            });
        ServiceCollection services = new();
        configure(services);
        Services = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        Consumer = new(Factory, options, Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RabbitMqConsumer<THandler>>.Instance);
    }

    public void Start() => Running = Consumer.RunAsync(Cancellation.Token);

    public Task DeliverAsync(ReadOnlyMemory<byte> body = default, ulong tag = 42,
        string exchange = "exchange", string routingKey = "key", IBasicProperties? properties = null,
        bool redelivered = false) => Subscription.HandleBasicDeliver(
            "consumer-tag", tag, redelivered, exchange, routingKey, properties, body);

    public async ValueTask DisposeAsync()
    {
        await Cancellation.CancelAsync();
        try { await Running.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (IOException) { }
        await Services.DisposeAsync();
        Cancellation.Dispose();
    }
}
