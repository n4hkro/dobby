namespace Dobby.PluginFramework.Activation;

public sealed class PluginLifetimeManager : IAsyncDisposable
{
    public void TrackSingleton(string pluginId, object instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(instance);
    }

    public ValueTask DisposePluginAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
