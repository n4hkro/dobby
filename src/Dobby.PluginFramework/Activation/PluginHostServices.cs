using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.PluginStorage;
using Dobby.Contracts.Policies;
using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public sealed class PluginHostServices
{
    private readonly object pluginSubscriptionGate = new();
    private readonly Dictionary<string, PluginSubscriptionLease> pluginSubscriptionLeases = new(StringComparer.Ordinal);
    private readonly HashSet<string> pluginWorkSessionOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Guid>> pluginWorkSessionGrants = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<Type, object> services;
    private readonly Func<string, ICapabilityLicensing>? licensingFactory;
    private readonly Func<string, IPluginStorage?>? pluginStorageFactory;
    private readonly Action<string, Type, ErrorCode>? pluginEventHandlerFaultSink;
    private readonly Func<string, Type, ErrorCode, PluginInvocationOutcomeKind, ValueTask>? pluginEventHandlerOutcomeSink;
    private long nextLeaseGeneration;

    public PluginHostServices(
        IEventBus eventBus,
        IBackgroundOperationReporter backgroundOperations,
        IBackgroundOperationCancellation backgroundOperationCancellation,
        Func<string, ICapabilityLicensing>? licensingFactory = null,
        ICapabilityPolicy<OperationPermissionCapability>? operationAuthorizationPolicy = null,
        IPluginWorkSessionService? pluginWorkSessions = null,
        Func<string, IPluginStorage?>? pluginStorageFactory = null,
        Action<string, Type, ErrorCode>? pluginEventHandlerFaultSink = null,
        Func<string, Type, ErrorCode, PluginInvocationOutcomeKind, ValueTask>? pluginEventHandlerOutcomeSink = null)
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        BackgroundOperations = backgroundOperations ?? throw new ArgumentNullException(nameof(backgroundOperations));
        BackgroundOperationCancellation = backgroundOperationCancellation ?? throw new ArgumentNullException(nameof(backgroundOperationCancellation));
        OperationAuthorizationPolicy = operationAuthorizationPolicy ?? new DenyAllOperationAuthorizationPolicy();
        PluginWorkSessions = pluginWorkSessions ?? new NoopPluginWorkSessionService();
        this.licensingFactory = licensingFactory;
        this.pluginStorageFactory = pluginStorageFactory;
        this.pluginEventHandlerFaultSink = pluginEventHandlerFaultSink;
        this.pluginEventHandlerOutcomeSink = pluginEventHandlerOutcomeSink;
        services = new Dictionary<Type, object>
        {
            [typeof(IEventBus)] = EventBus,
            [typeof(IBackgroundOperationReporter)] = BackgroundOperations,
            [typeof(IBackgroundOperationCancellation)] = BackgroundOperationCancellation,
            [typeof(ICapabilityPolicy<OperationPermissionCapability>)] = OperationAuthorizationPolicy,
            [typeof(IPluginWorkSessionService)] = PluginWorkSessions
        };
    }

    public IEventBus EventBus { get; }

    public IBackgroundOperationReporter BackgroundOperations { get; }

    public IBackgroundOperationCancellation BackgroundOperationCancellation { get; }

    public ICapabilityPolicy<OperationPermissionCapability> OperationAuthorizationPolicy { get; }

    public IPluginWorkSessionService PluginWorkSessions { get; }

    public static PluginHostServices CreateDefault()
    {
        return new PluginHostServices(
            new NoopEventBus(),
            new NoopBackgroundOperationReporter(),
            new NoopBackgroundOperationCancellation());
    }

    internal bool TryResolve(string pluginId, Type serviceType, out object service)
    {
        if (serviceType == typeof(IEventBus))
        {
            service = ResolveEventBus(pluginId);
            return true;
        }

        if (serviceType == typeof(IPluginWorkSessionService))
        {
            lock (pluginSubscriptionGate)
            {
                pluginWorkSessionOwners.Add(pluginId);
            }

            service = new PluginScopedWorkSessionService(pluginId, PluginWorkSessions, this);
            return true;
        }

        if (serviceType == typeof(ICapabilityLicensing) && licensingFactory is not null)
        {
            service = licensingFactory(pluginId);
            return true;
        }

        if (serviceType == typeof(IPluginStorage) && pluginStorageFactory is not null)
        {
            var pluginStorage = pluginStorageFactory(pluginId);
            if (pluginStorage is not null)
            {
                service = pluginStorage;
                return true;
            }
        }

        return services.TryGetValue(serviceType, out service!);
    }

    internal IEventBus ResolveEventBus(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        lock (pluginSubscriptionGate)
        {
            if (!pluginSubscriptionLeases.TryGetValue(pluginId, out var lease) || lease.IsClosed)
            {
                lease = new PluginSubscriptionLease(pluginId, ++nextLeaseGeneration);
                pluginSubscriptionLeases[pluginId] = lease;
            }

            return new PluginScopedEventBus(
                this,
                EventBus,
                lease,
                pluginEventHandlerFaultSink,
                pluginEventHandlerOutcomeSink);
        }
    }

    internal void CleanupPluginSubscriptions(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        IReadOnlyList<IDisposable> subscriptions = [];
        lock (pluginSubscriptionGate)
        {
            pluginWorkSessionOwners.Remove(pluginId);
            pluginWorkSessionGrants.Remove(pluginId);
            if (pluginSubscriptionLeases.Remove(pluginId, out var lease))
            {
                lease.IsClosed = true;
                subscriptions = lease.Subscriptions.ToArray();
                lease.Subscriptions.Clear();
            }
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        if (PluginWorkSessions is IPluginWorkSessionCleanup cleanup)
        {
            cleanup.CloseOwnedBy(pluginId);
        }
    }

    internal void CleanupAllPluginSubscriptions()
    {
        IReadOnlyList<IDisposable> subscriptions;
        string[] workSessionOwners;
        lock (pluginSubscriptionGate)
        {
            workSessionOwners = pluginWorkSessionOwners.ToArray();
            pluginWorkSessionOwners.Clear();
            pluginWorkSessionGrants.Clear();
            subscriptions = pluginSubscriptionLeases.Values
                .SelectMany(lease =>
                {
                    lease.IsClosed = true;
                    var current = lease.Subscriptions.ToArray();
                    lease.Subscriptions.Clear();
                    return current;
                })
                .ToList();
            pluginSubscriptionLeases.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        if (PluginWorkSessions is IPluginWorkSessionCleanup cleanup)
        {
            foreach (var ownerId in workSessionOwners)
            {
                cleanup.CloseOwnedBy(ownerId);
            }
        }
    }

    internal void GrantWorkSession(string pluginId, Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        lock (pluginSubscriptionGate)
        {
            if (!pluginWorkSessionGrants.TryGetValue(pluginId, out var granted))
            {
                granted = [];
                pluginWorkSessionGrants[pluginId] = granted;
            }

            granted.Add(sessionId);
        }
    }

    internal void RevokeWorkSession(string pluginId, Guid sessionId)
    {
        lock (pluginSubscriptionGate)
        {
            if (pluginWorkSessionGrants.TryGetValue(pluginId, out var granted) &&
                granted.Remove(sessionId) &&
                granted.Count == 0)
            {
                pluginWorkSessionGrants.Remove(pluginId);
            }
        }
    }

    internal bool OwnsWorkSession(string pluginId, Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return false;
        }

        lock (pluginSubscriptionGate)
        {
            return pluginWorkSessionGrants.TryGetValue(pluginId, out var granted) &&
                granted.Contains(sessionId);
        }
    }

    private IDisposable TryTrackPluginSubscription(
        PluginSubscriptionLease lease,
        Func<IDisposable> subscribe)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(subscribe);

        lock (pluginSubscriptionGate)
        {
            if (lease.IsClosed ||
                !pluginSubscriptionLeases.TryGetValue(lease.PluginId, out var currentLease) ||
                !ReferenceEquals(currentLease, lease))
            {
                return NoopSubscription.Instance;
            }

            var innerSubscription = subscribe();
            var tracked = new TrackedSubscription(innerSubscription, this, lease);
            lease.Subscriptions.Add(tracked);
            return tracked;
        }
    }

    private void UntrackPluginSubscription(PluginSubscriptionLease lease, IDisposable subscription)
    {
        lock (pluginSubscriptionGate)
        {
            lease.Subscriptions.Remove(subscription);
        }
    }

    private sealed class PluginScopedEventBus(
        PluginHostServices owner,
        IEventBus inner,
        PluginSubscriptionLease lease,
        Action<string, Type, ErrorCode>? handlerFaultSink,
        Func<string, Type, ErrorCode, PluginInvocationOutcomeKind, ValueTask>? handlerOutcomeSink) : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : PublicIntegrationEvent
        {
            return inner.PublishAsync(@event, cancellationToken);
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : PublicIntegrationEvent
        {
            ArgumentNullException.ThrowIfNull(handler);

            return owner.TryTrackPluginSubscription(
                lease,
                () => handlerFaultSink is null && handlerOutcomeSink is null
                    ? inner.Subscribe(handler)
                    : inner.Subscribe<TEvent>((@event, cancellationToken) =>
                        InvokePluginHandlerAsync(
                            handler,
                            @event,
                            cancellationToken,
                            handlerFaultSink,
                            handlerOutcomeSink)));
        }

        private async Task InvokePluginHandlerAsync<TEvent>(
            Func<TEvent, CancellationToken, Task> handler,
            TEvent @event,
            CancellationToken cancellationToken,
            Action<string, Type, ErrorCode>? handlerFaultSink,
            Func<string, Type, ErrorCode, PluginInvocationOutcomeKind, ValueTask>? handlerOutcomeSink)
            where TEvent : PublicIntegrationEvent
        {
            try
            {
                await handler(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                await ReportHandlerOutcomeAsync(
                    typeof(TEvent),
                    ErrorCode.Cancelled,
                    PluginInvocationOutcomeKind.Cancelled,
                    handlerFaultSink,
                    handlerOutcomeSink).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                await ReportHandlerOutcomeAsync(
                    typeof(TEvent),
                    ErrorCode.InvalidInput,
                    PluginInvocationOutcomeKind.PluginException,
                    handlerFaultSink,
                    handlerOutcomeSink).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                await ReportHandlerOutcomeAsync(
                    typeof(TEvent),
                    ErrorCode.UnsupportedOperation,
                    PluginInvocationOutcomeKind.PluginException,
                    handlerFaultSink,
                    handlerOutcomeSink).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                await ReportHandlerOutcomeAsync(
                    typeof(TEvent),
                    ErrorCode.PluginInternalError,
                    PluginInvocationOutcomeKind.PluginException,
                    handlerFaultSink,
                    handlerOutcomeSink).ConfigureAwait(false);
            }
        }

        private async ValueTask ReportHandlerOutcomeAsync(
            Type eventType,
            ErrorCode errorCode,
            PluginInvocationOutcomeKind outcomeKind,
            Action<string, Type, ErrorCode>? handlerFaultSink,
            Func<string, Type, ErrorCode, PluginInvocationOutcomeKind, ValueTask>? handlerOutcomeSink)
        {
            handlerFaultSink?.Invoke(lease.PluginId, eventType, errorCode);
            if (handlerOutcomeSink is not null)
            {
                await handlerOutcomeSink(
                    lease.PluginId,
                    eventType,
                    errorCode,
                    outcomeKind).ConfigureAwait(false);
            }
        }

    }

    private sealed class PluginSubscriptionLease(string pluginId, long generation)
    {
        public string PluginId { get; } = pluginId;

        public long Generation { get; } = generation;

        public List<IDisposable> Subscriptions { get; } = [];

        public bool IsClosed { get; set; }
    }

    private sealed class TrackedSubscription(
        IDisposable innerSubscription,
        PluginHostServices owner,
        PluginSubscriptionLease lease) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 1)
            {
                return;
            }

            innerSubscription.Dispose();
            owner.UntrackPluginSubscription(lease, this);
        }
    }

    private sealed class NoopSubscription : IDisposable
    {
        public static NoopSubscription Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class NoopEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : PublicIntegrationEvent
        {
            return Task.CompletedTask;
        }

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
            where TEvent : PublicIntegrationEvent
        {
            return new Subscription();
        }

        private sealed class Subscription : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class NoopBackgroundOperationReporter : IBackgroundOperationReporter
    {
        public IProgress<BackgroundOperationSnapshot> Progress { get; } = new NoopProgress();
    }

    private sealed class NoopProgress : IProgress<BackgroundOperationSnapshot>
    {
        public void Report(BackgroundOperationSnapshot value)
        {
        }
    }

    private sealed class NoopBackgroundOperationCancellation : IBackgroundOperationCancellation
    {
        private static readonly CancellationTokenSource HostLifetime = new();

        public CancellationToken CancellationToken => HostLifetime.Token;
    }

    private sealed class DenyAllOperationAuthorizationPolicy : ICapabilityPolicy<OperationPermissionCapability>
    {
        public Result<PolicyDecision> Evaluate(OperationPermissionCapability capability, PolicyContext context)
        {
            return Result.Success(PolicyDecision.Deny("permission-denied"));
        }
    }

    private sealed class PluginScopedWorkSessionService(
        string pluginId,
        IPluginWorkSessionService inner,
        PluginHostServices host) : IPluginWorkSessionService
    {
        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            if (!string.Equals(ownerId, pluginId, StringComparison.Ordinal))
            {
                return Result.Failure<PluginWorkSessionHandle>(ErrorCode.Forbidden);
            }

            var opened = inner.Open(ownerId, routeKey, labelKey);
            if (opened.IsSuccess)
            {
                host.GrantWorkSession(pluginId, opened.Value.SessionId);
            }

            return opened;
        }

        public Result UpdateProgress(Guid sessionId, int percent)
        {
            return Owns(sessionId)
                ? inner.UpdateProgress(sessionId, percent)
                : Result.Failure(ErrorCode.Forbidden);
        }

        public Result RecordResult(Guid sessionId, string resultSummaryKey)
        {
            return Owns(sessionId)
                ? inner.RecordResult(sessionId, resultSummaryKey)
                : Result.Failure(ErrorCode.Forbidden);
        }

        public Result RecordError(Guid sessionId, string errorSummaryKey)
        {
            return Owns(sessionId)
                ? inner.RecordError(sessionId, errorSummaryKey)
                : Result.Failure(ErrorCode.Forbidden);
        }

        public Result Close(Guid sessionId)
        {
            if (!Owns(sessionId))
            {
                return Result.Failure(ErrorCode.Forbidden);
            }

            var closed = inner.Close(sessionId);
            if (closed.IsSuccess)
            {
                host.RevokeWorkSession(pluginId, sessionId);
            }

            return closed;
        }

        private bool Owns(Guid sessionId)
        {
            return host.OwnsWorkSession(pluginId, sessionId);
        }
    }

    private sealed class NoopPluginWorkSessionService : IPluginWorkSessionService
    {
        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            return Result.Success(new PluginWorkSessionHandle(Guid.CreateVersion7(), ownerId, routeKey, labelKey));
        }

        public Result UpdateProgress(Guid sessionId, int percent)
        {
            return Result.Success();
        }

        public Result RecordResult(Guid sessionId, string resultSummaryKey)
        {
            return Result.Success();
        }

        public Result RecordError(Guid sessionId, string errorSummaryKey)
        {
            return Result.Success();
        }

        public Result Close(Guid sessionId)
        {
            return Result.Success();
        }
    }
}
