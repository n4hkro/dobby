namespace Dobby.Navigation;

public sealed class RouteRegistry
{
    public const string HomeRouteKey = "Dobby.Shell.Navigation.Home";
    public const string PluginsRouteKey = "Dobby.Shell.Navigation.Plugins";
    public const string SettingsRouteKey = "Dobby.Shell.Navigation.Settings";

    private readonly Dictionary<string, NavigationRoute> _routes;

    private RouteRegistry(IEnumerable<NavigationRoute> routes)
    {
        _routes = routes.ToDictionary(route => route.RouteKey, StringComparer.Ordinal);
    }

    public static RouteRegistry CreateWithBuiltIns()
    {
        return new RouteRegistry(
        [
            new NavigationRoute(HomeRouteKey, "Dobby.Shell.Home.Title", 0),
            new NavigationRoute(PluginsRouteKey, "Dobby.Shell.Plugins.Title", 100),
            new NavigationRoute(SettingsRouteKey, "Dobby.Shell.Settings.Title", int.MaxValue)
        ]);
    }

    public NavigationRoute ResolveOrHome(string? routeKey)
    {
        if (routeKey is not null && _routes.TryGetValue(routeKey, out var route))
        {
            return route;
        }

        return _routes[HomeRouteKey];
    }

    public IReadOnlyList<NavigationRoute> Snapshot()
    {
        return _routes.Values.OrderBy(route => route.Order).ToArray();
    }
}

public sealed record NavigationRoute(string RouteKey, string LabelKey, int Order);
