namespace Dobby.PluginFramework.Activation;

public sealed class PluginExtensionPointRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<Type, List<PluginExtensionPointRegistration>> registrations = [];

    public IReadOnlyList<TContract> GetSnapshot<TContract>()
        where TContract : class
    {
        lock (gate)
        {
            return registrations.TryGetValue(typeof(TContract), out var values)
                ? values.Select(value => (TContract)value.Instance).ToArray()
                : [];
        }
    }

    public IReadOnlyList<TContract> Get<TContract>()
        where TContract : class
    {
        return GetSnapshot<TContract>();
    }

    public IReadOnlyList<PluginExtensionPointRegistration<TContract>> GetRegistrationsSnapshot<TContract>()
        where TContract : class
    {
        lock (gate)
        {
            return registrations.TryGetValue(typeof(TContract), out var values)
                ? values
                    .Select(value => new PluginExtensionPointRegistration<TContract>(
                        value.PluginId,
                        (TContract)value.Instance))
                    .ToArray()
                : [];
        }
    }

    public IReadOnlyList<PluginExtensionPointRegistration<TContract>> GetRegistrations<TContract>()
        where TContract : class
    {
        return GetRegistrationsSnapshot<TContract>();
    }

    public void RemovePlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        lock (gate)
        {
            foreach (var values in registrations.Values)
            {
                values.RemoveAll(value => value.PluginId.Equals(pluginId, StringComparison.Ordinal));
            }
        }
    }

    internal void Publish(
        string pluginId,
        IReadOnlyList<PluginPreparedExtensionPoint> extensionPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(extensionPoints);

        lock (gate)
        {
            foreach (var extensionPoint in extensionPoints)
            {
                if (!registrations.TryGetValue(extensionPoint.ContractType, out var values))
                {
                    values = [];
                    registrations[extensionPoint.ContractType] = values;
                }

                values.Add(new PluginExtensionPointRegistration(pluginId, extensionPoint.Instance));
            }
        }
    }

    private sealed record PluginExtensionPointRegistration(string PluginId, object Instance);
}

public sealed record PluginExtensionPointRegistration<TContract>(
    string PluginId,
    TContract Instance)
    where TContract : class;
