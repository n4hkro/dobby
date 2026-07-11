using Dobby.Contracts.Events;
using Dobby.Core;

namespace Dobby.Messaging;

public sealed class InMemoryEventBus(Action<Type, ErrorCode>? handlerFaultSink = null) : IEventBus
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

    private async Task PublishSequentiallyAsync(
        IEnumerable<Func<PublicIntegrationEvent, CancellationToken, Task>> handlers,
        PublicIntegrationEvent @event,
        CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (handlerFaultSink is null)
            {
                await handler(@event, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var result = await InvokeHandlerAsync(handler, @event, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                handlerFaultSink.Invoke(
                    @event.GetType(),
                    result.ErrorCode ?? ErrorCode.PluginInternalError);
            }
        }
    }

    private static async Task<Result> InvokeHandlerAsync(
        Func<PublicIntegrationEvent, CancellationToken, Task> handler,
        PublicIntegrationEvent @event,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler(@event, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Result.Failure(ErrorCode.Cancelled);
        }
        catch (ArgumentException)
        {
            return Result.Failure(ErrorCode.InvalidInput);
        }
        catch (NotSupportedException)
        {
            return Result.Failure(ErrorCode.UnsupportedOperation);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.PluginInternalError);
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
