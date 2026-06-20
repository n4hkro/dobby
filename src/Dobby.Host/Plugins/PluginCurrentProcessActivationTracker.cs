namespace Dobby.Host.Plugins;

public interface IPluginCurrentProcessActivationTracker
{
    bool IsActivatedInCurrentProcess(string pluginId);

    void MarkActivatedInCurrentProcess(IReadOnlyCollection<string> pluginIds);
}

public sealed class PluginCurrentProcessActivationTracker : IPluginCurrentProcessActivationTracker
{
    private readonly object gate = new();
    private readonly HashSet<string> activatedPluginIds = new(StringComparer.Ordinal);

    public bool IsActivatedInCurrentProcess(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        lock (gate)
        {
            return activatedPluginIds.Contains(pluginId);
        }
    }

    public void MarkActivatedInCurrentProcess(IReadOnlyCollection<string> pluginIds)
    {
        ArgumentNullException.ThrowIfNull(pluginIds);

        lock (gate)
        {
            foreach (var pluginId in pluginIds)
            {
                if (!string.IsNullOrWhiteSpace(pluginId))
                {
                    activatedPluginIds.Add(pluginId);
                }
            }
        }
    }
}
