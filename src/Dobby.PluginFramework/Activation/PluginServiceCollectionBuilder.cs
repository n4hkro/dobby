using Dobby.Contracts.PluginFramework;

namespace Dobby.PluginFramework.Activation;

public sealed record PluginServiceDescriptor(
    Type ServiceType,
    Type ImplementationType,
    PluginLifetime Lifetime,
    bool IsExtensionPoint);

public sealed class PluginServiceCollectionBuilder : IPluginServiceCollection
{
    private readonly List<PluginServiceDescriptor> descriptors = [];

    public IReadOnlyList<PluginServiceDescriptor> Descriptors => descriptors;

    public IPluginServiceCollection AddInternal<TService, TImpl>(PluginLifetime lifetime)
        where TService : class
        where TImpl : class, TService
    {
        if (!Enum.IsDefined(lifetime))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        if (descriptors.Any(descriptor => !descriptor.IsExtensionPoint && descriptor.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException($"Internal service '{typeof(TService).FullName}' is already registered.");
        }

        descriptors.Add(new PluginServiceDescriptor(typeof(TService), typeof(TImpl), lifetime, IsExtensionPoint: false));
        return this;
    }

    public IPluginServiceCollection AddExtensionPoint<TContract, TImpl>()
        where TContract : class
        where TImpl : class, TContract
    {
        if (typeof(TContract).Assembly != typeof(IPluginModule).Assembly)
        {
            throw new InvalidOperationException("Extension point contracts must come from Dobby.Contracts.");
        }

        descriptors.Add(new PluginServiceDescriptor(
            typeof(TContract),
            typeof(TImpl),
            PluginLifetime.Singleton,
            IsExtensionPoint: true));
        return this;
    }
}
