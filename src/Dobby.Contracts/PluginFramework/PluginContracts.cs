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
}

public sealed record PluginActivationContext(
    string PluginId,
    string PluginVersion,
    CorrelationId CorrelationId) : IPluginActivationContext;
