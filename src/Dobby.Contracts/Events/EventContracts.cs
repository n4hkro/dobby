using Dobby.Core;

namespace Dobby.Contracts.Events;

public abstract record PublicIntegrationEvent
{
    public CorrelationId CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : PublicIntegrationEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : PublicIntegrationEvent;
}
