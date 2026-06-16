using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.Events;
using Dobby.Core;

namespace Dobby.Contracts.PluginFramework;

public interface IPluginModule
{
    void RegisterServices(IPluginServiceCollection services);

    void OnActivated(IPluginActivationContext context);
}

public interface IPluginServiceCollection
{
    IPluginServiceCollection AddInternal<TService, TImpl>(PluginLifetime lifetime)
        where TService : class
        where TImpl : class, TService;

    IPluginServiceCollection AddExtensionPoint<TContract, TImpl>()
        where TContract : class
        where TImpl : class, TContract;
}

public enum PluginLifetime
{
    Singleton,
    Scoped,
    Transient
}

public interface IPluginActivationContext
{
    string PluginId { get; }

    string PluginVersion { get; }

    CorrelationId CorrelationId { get; }

    IEventBus EventBus { get; }

    IBackgroundOperationReporter BackgroundOperations { get; }

    IBackgroundOperationCancellation BackgroundOperationCancellation { get; }
}

public sealed record PluginActivationContext(
    string PluginId,
    string PluginVersion,
    CorrelationId CorrelationId,
    IEventBus EventBus,
    IBackgroundOperationReporter BackgroundOperations,
    IBackgroundOperationCancellation BackgroundOperationCancellation) : IPluginActivationContext
{
    private static readonly IEventBus DefaultEventBus = new NoopEventBus();
    private static readonly IBackgroundOperationReporter DefaultBackgroundOperations = new NoopBackgroundOperationReporter();
    private static readonly IBackgroundOperationCancellation DefaultBackgroundOperationCancellation = new NoopBackgroundOperationCancellation();

    public PluginActivationContext(
        string pluginId,
        string pluginVersion,
        CorrelationId correlationId)
        : this(
            pluginId,
            pluginVersion,
            correlationId,
            DefaultEventBus,
            DefaultBackgroundOperations,
            DefaultBackgroundOperationCancellation)
    {
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
