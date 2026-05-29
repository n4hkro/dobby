using Dobby.Core;

namespace Dobby.PluginFramework.Activation;

public sealed record PluginDependency(string PluginId, string MinVersion);

public sealed record PluginDependencyDescriptor(
    string PluginId,
    IReadOnlyList<PluginDependency> Dependencies);

public static class PluginDependencySorter
{
    public static Result<IReadOnlyList<PluginDependencyDescriptor>> Sort(IReadOnlyList<PluginDependencyDescriptor> plugins)
    {
        var byId = plugins.ToDictionary(plugin => plugin.PluginId, StringComparer.Ordinal);
        var result = new List<PluginDependencyDescriptor>();
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var plugin in plugins.OrderBy(plugin => plugin.PluginId, StringComparer.Ordinal))
        {
            if (!Visit(plugin.PluginId, byId, visiting, visited, result))
            {
                return Result.Failure<IReadOnlyList<PluginDependencyDescriptor>>(ErrorCode.ValidationFailed);
            }
        }

        return Result.Success<IReadOnlyList<PluginDependencyDescriptor>>(result);
    }

    private static bool Visit(
        string pluginId,
        IReadOnlyDictionary<string, PluginDependencyDescriptor> byId,
        ISet<string> visiting,
        ISet<string> visited,
        ICollection<PluginDependencyDescriptor> result)
    {
        if (visited.Contains(pluginId))
        {
            return true;
        }

        if (!byId.TryGetValue(pluginId, out var plugin) || !visiting.Add(pluginId))
        {
            return false;
        }

        foreach (var dependency in plugin.Dependencies.OrderBy(dependency => dependency.PluginId, StringComparer.Ordinal))
        {
            if (!Visit(dependency.PluginId, byId, visiting, visited, result))
            {
                return false;
            }
        }

        visiting.Remove(pluginId);
        visited.Add(pluginId);
        result.Add(plugin);
        return true;
    }
}
