namespace Dobby.PluginFramework.Activation;

public sealed class PluginLifetimeManager : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, List<object>> pluginInstances = new(StringComparer.Ordinal);
    private bool disposed;

    public void TrackSingleton(string pluginId, object instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(instance);

        if (instance is not IDisposable and not IAsyncDisposable)
        {
            return;
        }

        lock (gate)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PluginLifetimeManager));
            }

            if (!pluginInstances.TryGetValue(pluginId, out var instances))
            {
                instances = [];
                pluginInstances[pluginId] = instances;
            }

            if (!ContainsReference(instances, instance))
            {
                instances.Add(instance);
            }
        }
    }

    public ValueTask DisposePluginAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        List<object>? instances;
        lock (gate)
        {
            if (!pluginInstances.Remove(pluginId, out instances))
            {
                return ValueTask.CompletedTask;
            }
        }

        return DisposeAllAsync(instances);
    }

    public ValueTask DisposeAsync()
    {
        List<object> instances;
        lock (gate)
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            instances = pluginInstances.Values.SelectMany(instancesForPlugin => instancesForPlugin).ToList();
            pluginInstances.Clear();
        }

        return DisposeAllAsync(instances);
    }

    private static async ValueTask DisposeAllAsync(IReadOnlyList<object> instances)
    {
        for (var index = instances.Count - 1; index >= 0; index--)
        {
            switch (instances[index])
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }

    private static bool ContainsReference<T>(IEnumerable<T> items, T candidate)
        where T : class
    {
        return items.Any(item => ReferenceEquals(item, candidate));
    }
}
