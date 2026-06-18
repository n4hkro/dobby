using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Contracts.Licensing;

namespace Dobby.PluginFramework.Activation;

public sealed class PluginHostServices
{
    private readonly IReadOnlyDictionary<Type, object> services;
    private readonly Func<string, ICapabilityLicensing>? licensingFactory;

    public PluginHostServices(
        IEventBus eventBus,
        IBackgroundOperationReporter backgroundOperations,
        IBackgroundOperationCancellation backgroundOperationCancellation,
        Func<string, ICapabilityLicensing>? licensingFactory = null)
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        BackgroundOperations = backgroundOperations ?? throw new ArgumentNullException(nameof(backgroundOperations));
        BackgroundOperationCancellation = backgroundOperationCancellation ?? throw new ArgumentNullException(nameof(backgroundOperationCancellation));
        this.licensingFactory = licensingFactory;
        services = new Dictionary<Type, object>
        {
            [typeof(IEventBus)] = EventBus,
            [typeof(IBackgroundOperationReporter)] = BackgroundOperations,
            [typeof(IBackgroundOperationCancellation)] = BackgroundOperationCancellation
        };
    }

    public IEventBus EventBus { get; }

    public IBackgroundOperationReporter BackgroundOperations { get; }

    public IBackgroundOperationCancellation BackgroundOperationCancellation { get; }

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
}
