using NuGet.Versioning;

namespace Dobby.PluginFramework.Activation;

public sealed record PluginDependencyCandidate(
    string PluginId,
    string Version,
    bool IsEnabled,
    IReadOnlyList<PluginDependency> Dependencies);

public sealed record PluginDependencyResolution(
    IReadOnlyList<PluginDependencyCandidate> ActivationOrder,
    IReadOnlyList<PluginActivationOutcome> Outcomes);

public static class PluginDependencyGraph
{
    public static PluginDependencyResolution Resolve(IReadOnlyList<PluginDependencyCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var byId = candidates.ToDictionary(candidate => candidate.PluginId, StringComparer.Ordinal);
        var skips = new Dictionary<string, PluginActivationOutcome>(StringComparer.Ordinal);

        foreach (var candidate in candidates.OrderBy(candidate => candidate.PluginId, StringComparer.Ordinal))
        {
            if (!candidate.IsEnabled)
            {
                skips[candidate.PluginId] = Skipped(candidate.PluginId, "disabled");
                continue;
            }

            foreach (var dependency in candidate.Dependencies.OrderBy(dependency => dependency.PluginId, StringComparer.Ordinal))
            {
                if (!byId.TryGetValue(dependency.PluginId, out var installed) || !installed.IsEnabled)
                {
                    skips[candidate.PluginId] = Skipped(candidate.PluginId, $"missing-dependency: {dependency.PluginId}");
                    break;
                }

                if (!MeetsMinimumVersion(installed.Version, dependency.MinVersion))
                {
                    skips[candidate.PluginId] = Skipped(
                        candidate.PluginId,
                        $"dependency-version-below-min: {dependency.PluginId}:{installed.Version}:{dependency.MinVersion}");
                    break;
                }
            }
        }

        var cycles = FindCycleMembers(byId, skips.Keys);
        foreach (var cycle in cycles)
        {
            var reason = $"circular-dependency: {string.Join(",", cycle.Order(StringComparer.Ordinal))}";
            foreach (var pluginId in cycle)
            {
                skips[pluginId] = Skipped(pluginId, reason);
            }
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var candidate in candidates.Where(candidate => !skips.ContainsKey(candidate.PluginId)))
            {
                var skippedDependency = candidate.Dependencies
                    .OrderBy(dependency => dependency.PluginId, StringComparer.Ordinal)
                    .FirstOrDefault(dependency => skips.ContainsKey(dependency.PluginId));
                if (skippedDependency is null)
                {
                    continue;
                }

                skips[candidate.PluginId] = Skipped(candidate.PluginId, $"missing-dependency: {skippedDependency.PluginId}");
                changed = true;
            }
        }

        var active = byId
            .Where(pair => !skips.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var order = TopologicalOrder(active);

        return new PluginDependencyResolution(
            order,
            skips.Values.OrderBy(outcome => outcome.PluginId, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<HashSet<string>> FindCycleMembers(
        IReadOnlyDictionary<string, PluginDependencyCandidate> byId,
        IEnumerable<string> excluded)
    {
        var excludedSet = excluded.ToHashSet(StringComparer.Ordinal);
        var visiting = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var cycles = new List<HashSet<string>>();

        void Visit(string pluginId)
        {
            var stackIndex = visiting.FindIndex(item => item.Equals(pluginId, StringComparison.Ordinal));
            if (stackIndex >= 0)
            {
                cycles.Add(visiting.Skip(stackIndex).ToHashSet(StringComparer.Ordinal));
                return;
            }

            if (excludedSet.Contains(pluginId) || !visited.Add(pluginId) || !byId.TryGetValue(pluginId, out var candidate))
            {
                return;
            }

            visiting.Add(pluginId);
            foreach (var dependency in candidate.Dependencies.OrderBy(dependency => dependency.PluginId, StringComparer.Ordinal))
            {
                Visit(dependency.PluginId);
            }

            visiting.RemoveAt(visiting.Count - 1);
        }

        foreach (var pluginId in byId.Keys.Order(StringComparer.Ordinal))
        {
            Visit(pluginId);
        }

        return cycles;
    }

    private static IReadOnlyList<PluginDependencyCandidate> TopologicalOrder(
        IReadOnlyDictionary<string, PluginDependencyCandidate> active)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<PluginDependencyCandidate>();

        void Visit(string pluginId)
        {
            if (!visited.Add(pluginId) || !active.TryGetValue(pluginId, out var candidate))
            {
                return;
            }

            foreach (var dependency in candidate.Dependencies.OrderBy(dependency => dependency.PluginId, StringComparer.Ordinal))
            {
                Visit(dependency.PluginId);
            }

            result.Add(candidate);
        }

        foreach (var pluginId in active.Keys.Order(StringComparer.Ordinal))
        {
            Visit(pluginId);
        }

        return result;
    }

    private static bool MeetsMinimumVersion(string installed, string minimum)
    {
        return NuGetVersion.TryParse(installed, out var installedVersion) &&
            NuGetVersion.TryParse(minimum, out var minimumVersion) &&
            installedVersion >= minimumVersion;
    }

    private static PluginActivationOutcome Skipped(string pluginId, string reason)
    {
        return new PluginActivationOutcome(pluginId, PluginActivationStatus.Skipped, reason);
    }
}
