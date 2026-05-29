using Dobby.Contracts.Events;

namespace Dobby.Messaging;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, List<Func<PublicIntegrationEvent, CancellationToken, Task>>> _handlers = [];

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : PublicIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        Func<PublicIntegrationEvent, CancellationToken, Task>[] handlers;
        lock (_gate)
        {
            handlers = _handlers.TryGetValue(typeof(TEvent), out var registered)
                ? registered.ToArray()
                : [];
        }

        return PublishSequentiallyAsync(handlers, @event, cancellationToken);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : PublicIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        Task Adapter(PublicIntegrationEvent evt, CancellationToken token) => handler((TEvent)evt, token);

        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = [];
                _handlers.Add(typeof(TEvent), handlers);
            }

            handlers.Add(Adapter);
        }

        return new Subscription(() => Unsubscribe<TEvent>(Adapter));
    }

    private static async Task PublishSequentiallyAsync(
        IEnumerable<Func<PublicIntegrationEvent, CancellationToken, Task>> handlers,
        PublicIntegrationEvent @event,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler(@event, cancellationToken);
        }
    }

    private void Unsubscribe<TEvent>(Func<PublicIntegrationEvent, CancellationToken, Task> handler)
        where TEvent : PublicIntegrationEvent
    {
        lock (_gate)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
