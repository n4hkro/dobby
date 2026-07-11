using Dobby.Contracts.Ordering;
using Dobby.Core;
using Dobby.Navigation;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Navigation;

public sealed class NavigationTests
{
    [Fact]
    public void Ordering_comparer_uses_group_weight_plugin_and_declaration_index()
    {
        var comparer = new DefaultUiOrderingComparer();
        var sorted = new[]
        {
            new UiOrderingKey("default", 100, "plugin-b", 0),
            new UiOrderingKey("default", 50, "plugin-b", 0),
            new UiOrderingKey("alpha", 100, "plugin-a", 1),
            new UiOrderingKey("alpha", 100, "plugin-a", 0)
        }.Order(comparer).ToArray();

        sorted.Select(x => $"{x.Group}:{x.Weight}:{x.PluginId}:{x.DeclarationIndex}")
            .ShouldBe([
                "alpha:100:plugin-a:0",
                "alpha:100:plugin-a:1",
                "default:50:plugin-b:0",
                "default:100:plugin-b:0"
            ]);
    }

    [Fact]
    public void Route_registry_falls_back_to_home_for_unknown_route()
    {
        var registry = RouteRegistry.CreateWithBuiltIns();

        registry.ResolveOrHome("missing.route").RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Route_registry_resolves_known_built_in_route_identities()
    {
        var registry = RouteRegistry.CreateWithBuiltIns();

        registry.ResolveOrHome(RouteRegistry.HomeRouteKey).RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        registry.ResolveOrHome(RouteRegistry.PluginsRouteKey).RouteKey.ShouldBe(RouteRegistry.PluginsRouteKey);
        registry.ResolveOrHome(RouteRegistry.SettingsRouteKey).RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
    }

    [Fact]
    public async Task Shell_route_host_resolves_unknown_route_to_home_before_activation()
    {
        var activatedRoutes = new List<string>();
        var host = new ShellRouteHost(
            routeKey => routeKey == "missing.route" ? RouteRegistry.HomeRouteKey : routeKey ?? RouteRegistry.HomeRouteKey,
            routeKey =>
            {
                activatedRoutes.Add(routeKey);
                return ValueTask.FromResult(Result.Success());
            });

        var result = await host.ActivateOrHomeAsync("missing.route");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(RouteRegistry.HomeRouteKey);
        activatedRoutes.ShouldBe([RouteRegistry.HomeRouteKey]);
    }
}
