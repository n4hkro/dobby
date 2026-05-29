using Dobby.Contracts.Ordering;

namespace Dobby.Navigation;

public static class NavigationComposer
{
    public static NavigationComposition Compose(
        IEnumerable<NavigationContribution> contributions,
        NavigationUserOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var all = BuiltIns().Concat(contributions).ToArray();
        var hidden = overrides is { ResetToDefaults: false }
            ? overrides.HiddenRouteKeys.ToHashSet(StringComparer.Ordinal)
            : [];
        var visible = all
            .Where(contribution => !hidden.Contains(contribution.RouteKey) || !IsHideableRoute(contribution.RouteKey))
            .GroupBy(contribution => contribution.RouteKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var childrenByParent = visible
            .Where(contribution => contribution.ParentId is not null)
            .GroupBy(contribution => contribution.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray().AsEnumerable(), StringComparer.Ordinal);

        IReadOnlyList<string> PreferredOrderForChildren(NavigationContribution parent)
        {
            if (overrides is not { ResetToDefaults: false })
            {
                return [];
            }

            return parent.Target switch
            {
                NavigationTarget.TopToolbar => overrides.SidebarRouteOrder,
                NavigationTarget.Sidebar => overrides.ThirdBarRouteOrder,
                _ => []
            };
        }

        NavigationNode BuildNode(NavigationContribution contribution)
        {
            var children = childrenByParent.TryGetValue(contribution.Id, out var childContributions)
                ? Order(childContributions, PreferredOrderForChildren(contribution)).Select(BuildNode).ToArray()
                : [];
            return new NavigationNode(
                contribution.Id,
                contribution.RouteKey,
                contribution.LabelKey,
                contribution.Target,
                contribution.Ordering,
                children);
        }

        var topToolbar = Order(visible.Where(contribution =>
                contribution.Target == NavigationTarget.TopToolbar &&
                contribution.ParentId is null),
                overrides is { ResetToDefaults: false } ? overrides.TopToolbarOrder : [])
            .Select(BuildNode)
            .ToArray();

        return new NavigationComposition(topToolbar);
    }

    private static bool IsHideableRoute(string routeKey)
    {
        return !routeKey.Equals(RouteRegistry.HomeRouteKey, StringComparison.Ordinal) &&
            !routeKey.Equals(RouteRegistry.PluginsRouteKey, StringComparison.Ordinal) &&
            !routeKey.Equals(RouteRegistry.SettingsRouteKey, StringComparison.Ordinal);
    }

    private static IOrderedEnumerable<NavigationContribution> Order(
        IEnumerable<NavigationContribution> contributions,
        IReadOnlyList<string> preferredOrder)
    {
        var preferred = preferredOrder
            .Select((routeKey, index) => new { routeKey, index })
            .ToDictionary(item => item.routeKey, item => item.index, StringComparer.Ordinal);

        return contributions
            .OrderBy(contribution => FixedBucket(contribution.FixedPosition))
            .ThenBy(contribution => preferred.TryGetValue(contribution.RouteKey, out var index) ? index : int.MaxValue)
            .ThenBy(contribution => contribution.Ordering, new DefaultUiOrderingComparer());
    }

    private static int FixedBucket(NavigationFixedPosition fixedPosition)
    {
        return fixedPosition switch
        {
            NavigationFixedPosition.First => 0,
            NavigationFixedPosition.None => 1,
            NavigationFixedPosition.Last => 2,
            _ => 1
        };
    }

    private static IReadOnlyList<NavigationContribution> BuiltIns()
    {
        return
        [
            NavigationContribution.Create(
                RouteRegistry.HomeRouteKey,
                RouteRegistry.HomeRouteKey,
                "Dobby.Shell.Home.Title",
                NavigationTarget.TopToolbar,
                new UiOrderingKey("built-in", 0, "host", 0),
                fixedPosition: NavigationFixedPosition.First),
            NavigationContribution.Create(
                RouteRegistry.PluginsRouteKey,
                RouteRegistry.PluginsRouteKey,
                "Dobby.Shell.Plugins.Title",
                NavigationTarget.TopToolbar,
                new UiOrderingKey("default", 100, "host", 1)),
            NavigationContribution.Create(
                RouteRegistry.SettingsRouteKey,
                RouteRegistry.SettingsRouteKey,
                "Dobby.Shell.Settings.Title",
                NavigationTarget.TopToolbar,
                new UiOrderingKey("built-in", int.MaxValue, "host", 2),
                fixedPosition: NavigationFixedPosition.Last)
        ];
    }
}

public sealed record NavigationNode(
    string Id,
    string RouteKey,
    string LabelKey,
    NavigationTarget Target,
    UiOrderingKey Ordering,
    IReadOnlyList<NavigationNode> Children)
{
    internal int FixedBucket => RouteKey switch
    {
        RouteRegistry.HomeRouteKey => 0,
        RouteRegistry.SettingsRouteKey => 2,
        _ => 1
    };
}

public sealed class NavigationComposition
{
    private readonly IReadOnlyDictionary<string, NavigationNode> routeIndex;

    public NavigationComposition(IReadOnlyList<NavigationNode> topToolbar)
    {
        TopToolbar = topToolbar;
        routeIndex = Flatten(topToolbar).ToDictionary(node => node.RouteKey, StringComparer.Ordinal);
    }

    public IReadOnlyList<NavigationNode> TopToolbar { get; }

    public NavigationNode ResolveOrHome(string? routeKey)
    {
        if (routeKey is not null && routeIndex.TryGetValue(routeKey, out var node))
        {
            return node;
        }

        return routeIndex[RouteRegistry.HomeRouteKey];
    }

    public IReadOnlyList<NavigationNode> ResolvePathOrHome(string? routeKey)
    {
        var target = ResolveOrHome(routeKey);
        foreach (var top in TopToolbar)
        {
            var path = FindPath(top, target.RouteKey);
            if (path.Count > 0)
            {
                return path;
            }
        }

        return [ResolveOrHome(RouteRegistry.HomeRouteKey)];
    }

    private static IReadOnlyList<NavigationNode> FindPath(NavigationNode node, string routeKey)
    {
        if (node.RouteKey.Equals(routeKey, StringComparison.Ordinal))
        {
            return [node];
        }

        foreach (var child in node.Children)
        {
            var childPath = FindPath(child, routeKey);
            if (childPath.Count > 0)
            {
                return [node, .. childPath];
            }
        }

        return [];
    }

    private static IEnumerable<NavigationNode> Flatten(IEnumerable<NavigationNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
