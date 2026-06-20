using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.Licensing;
using Dobby.Contracts.PluginPages;
using Dobby.Contracts.Policies;
using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public sealed class PluginHostServices
{
    private readonly IReadOnlyDictionary<Type, object> services;
    private readonly Func<string, ICapabilityLicensing>? licensingFactory;

    public PluginHostServices(
        IEventBus eventBus,
        IBackgroundOperationReporter backgroundOperations,
        IBackgroundOperationCancellation backgroundOperationCancellation,
        Func<string, ICapabilityLicensing>? licensingFactory = null,
        ICapabilityPolicy<OperationPermissionCapability>? operationAuthorizationPolicy = null,
        IPluginWorkSessionService? pluginWorkSessions = null)
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        BackgroundOperations = backgroundOperations ?? throw new ArgumentNullException(nameof(backgroundOperations));
        BackgroundOperationCancellation = backgroundOperationCancellation ?? throw new ArgumentNullException(nameof(backgroundOperationCancellation));
        OperationAuthorizationPolicy = operationAuthorizationPolicy ?? new DenyAllOperationAuthorizationPolicy();
        PluginWorkSessions = pluginWorkSessions ?? new NoopPluginWorkSessionService();
        this.licensingFactory = licensingFactory;
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
        if (serviceType == typeof(ICapabilityLicensing) && licensingFactory is not null)
        {
            service = licensingFactory(pluginId);
            return true;
        }

        return services.TryGetValue(serviceType, out service!);
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
        public CancellationToken CancellationToken => System.Threading.CancellationToken.None;
    }

    private sealed class DenyAllOperationAuthorizationPolicy : ICapabilityPolicy<OperationPermissionCapability>
    {
        public Result<PolicyDecision> Evaluate(OperationPermissionCapability capability, PolicyContext context)
        {
            return Result.Success(PolicyDecision.Deny("permission-denied"));
        }
    }

    private sealed class NoopPluginWorkSessionService : IPluginWorkSessionService
    {
        public Result<PluginWorkSessionHandle> Open(string ownerId, string routeKey, string labelKey)
        {
            return Result.Success(new PluginWorkSessionHandle(Guid.Empty, ownerId, routeKey, labelKey));
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
