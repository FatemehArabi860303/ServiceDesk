using System.Collections.Frozen;

namespace ServiceDesk.Messaging;

public sealed class RabbitMqOutboxRouting
{
    private readonly FrozenDictionary<string, RabbitMqOutboxRoute> routes;

    public RabbitMqOutboxRouting(IEnumerable<RabbitMqOutboxRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        this.routes = routes.ToFrozenDictionary(
            route => route.EventType,
            StringComparer.Ordinal);
    }

    public RabbitMqOutboxRoute Resolve(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        if (!routes.TryGetValue(eventType, out var route))
        {
            throw new NotSupportedException(
                $"Outbox event type '{eventType}' is not supported.");
        }

        return route;
    }
}