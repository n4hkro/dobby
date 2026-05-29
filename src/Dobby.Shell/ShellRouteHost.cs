using Dobby.Navigation;

namespace Dobby.Shell;

public sealed class ShellRouteHost(RouteRegistry routeRegistry)
{
    public NavigationRoute ActivateOrHome(string? routeKey)
    {
        return routeRegistry.ResolveOrHome(routeKey);
    }
}
