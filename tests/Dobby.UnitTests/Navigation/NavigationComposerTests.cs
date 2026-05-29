using Dobby.Contracts.Ordering;
using Dobby.Navigation;
using Shouldly;

namespace Dobby.UnitTests.Navigation;

public sealed class NavigationComposerTests
{
    [Fact]
    public void Built_in_top_toolbar_routes_keep_fixed_home_first_and_settings_last_positions()
    {
        var plugin = NavigationContribution.Create(
            "sample.reports",
            "plugin.sample.reports",
            "Dobby.Plugin.Sample.Reports",
            NavigationTarget.TopToolbar,
            new UiOrderingKey("default", 50, "sample", 0));

        var composition = NavigationComposer.Compose([plugin]);

        composition.TopToolbar.Select(node => node.RouteKey).ShouldBe([
            RouteRegistry.HomeRouteKey,
            "plugin.sample.reports",
            RouteRegistry.PluginsRouteKey,
            RouteRegistry.SettingsRouteKey
        ]);
    }

    [Fact]
    public void Hierarchical_contributions_render_through_top_toolbar_sidebar_and_third_bar_without_activation()
    {
        var reports = NavigationContribution.Create(
            "sample.reports",
            "plugin.sample.reports",
            "Dobby.Plugin.Sample.Reports",
            NavigationTarget.TopToolbar,
            new UiOrderingKey("default", 50, "sample", 0));
        var daily = NavigationContribution.Create(
            "sample.reports.daily",
            "plugin.sample.reports.daily",
            "Dobby.Plugin.Sample.Reports.Daily",
            NavigationTarget.Sidebar,
            new UiOrderingKey("default", 10, "sample", 1),
            parentId: reports.Id);
        var filters = NavigationContribution.Create(
            "sample.reports.daily.filters",
            "plugin.sample.reports.daily.filters",
            "Dobby.Plugin.Sample.Reports.Daily.Filters",
            NavigationTarget.ThirdBar,
            new UiOrderingKey("default", 10, "sample", 2),
            parentId: daily.Id);

        var composition = NavigationComposer.Compose([reports, daily, filters]);

        var top = composition.TopToolbar.Single(node => node.Id == reports.Id);
        top.Target.ShouldBe(NavigationTarget.TopToolbar);
        var sidebar = top.Children.Single();
        sidebar.Id.ShouldBe(daily.Id);
        sidebar.Target.ShouldBe(NavigationTarget.Sidebar);
        sidebar.Children.Single().ShouldSatisfyAllConditions(
            node => node.Id.ShouldBe(filters.Id),
            node => node.Target.ShouldBe(NavigationTarget.ThirdBar));
    }

    [Fact]
    public void Reset_user_override_ignores_hidden_state_and_returns_default_order()
    {
        var plugin = NavigationContribution.Create(
            "sample.reports",
            "plugin.sample.reports",
            "Dobby.Plugin.Sample.Reports",
            NavigationTarget.TopToolbar,
            new UiOrderingKey("default", 50, "sample", 0));
        var overrides = new NavigationUserOverrides(
            TopToolbarOrder: ["plugin.sample.reports", RouteRegistry.HomeRouteKey],
            SidebarRouteOrder: [],
            ThirdBarRouteOrder: [],
            HiddenRouteKeys: ["plugin.sample.reports"],
            ResetToDefaults: true);

        var composition = NavigationComposer.Compose([plugin], overrides);

        composition.TopToolbar.Select(node => node.Id).ShouldContain("sample.reports");
        composition.TopToolbar.First().RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        composition.TopToolbar.Last().RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
    }

    [Fact]
    public void User_overrides_use_route_keys_for_ordering_and_hidden_state()
    {
        var parent = NavigationContribution.Create(
            "sample.analytics",
            "plugin.sample.analytics",
            "Dobby.Plugin.Sample.Analytics",
            NavigationTarget.TopToolbar,
            new UiOrderingKey("default", 50, "sample", 0));
        var child = NavigationContribution.Create(
            "sample.analytics.daily",
            "plugin.sample.analytics.daily",
            "Dobby.Plugin.Sample.Analytics.Daily",
            NavigationTarget.Sidebar,
            new UiOrderingKey("default", 10, "sample", 1),
            parent.Id);
        var leaf = NavigationContribution.Create(
            "sample.analytics.daily.filters",
            "plugin.sample.analytics.daily.filters",
            "Dobby.Plugin.Sample.Analytics.Daily.Filters",
            NavigationTarget.ThirdBar,
            new UiOrderingKey("default", 10, "sample", 2),
            child.Id);
        var reports = NavigationContribution.Create(
            "sample.reports",
            "plugin.sample.reports",
            "Dobby.Plugin.Sample.Reports",
            NavigationTarget.TopToolbar,
            new UiOrderingKey("default", 60, "sample", 3));
        var overrides = new NavigationUserOverrides(
            TopToolbarOrder: ["plugin.sample.analytics"],
            SidebarRouteOrder: ["plugin.sample.analytics.daily"],
            ThirdBarRouteOrder: ["plugin.sample.analytics.daily.filters"],
            HiddenRouteKeys: [RouteRegistry.PluginsRouteKey, "plugin.sample.reports"],
            ResetToDefaults: false);

        var composition = NavigationComposer.Compose([parent, child, leaf, reports], overrides);

        composition.TopToolbar.Select(node => node.RouteKey)
            .ShouldContain(RouteRegistry.PluginsRouteKey);
        composition.TopToolbar.Select(node => node.RouteKey)
            .ShouldNotContain("plugin.sample.reports");
        composition.TopToolbar.Single(node => node.RouteKey == "plugin.sample.analytics")
            .Children.Single().RouteKey.ShouldBe("plugin.sample.analytics.daily");
    }

    [Fact]
    public void Unknown_route_resolution_falls_back_to_home()
    {
        var composition = NavigationComposer.Compose([]);

        composition.ResolveOrHome("missing.route").RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }
}
