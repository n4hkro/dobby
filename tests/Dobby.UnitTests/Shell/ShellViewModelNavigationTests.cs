using Dobby.Contracts.Ordering;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Dobby.Shell.WorkSessions;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class ShellViewModelNavigationTests
{
    [Fact]
    public void Activating_nested_sidebar_route_exposes_sidebar_and_thirdbar_items()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();

        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics.daily");
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);
    }

    [Fact]
    public void Applying_runtime_navigation_preference_consumes_order_and_hidden_overrides()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var preference = ShellLayoutPreference.Default with
        {
            NavigationOverrides = new NavigationUserOverrides(
                TopToolbarOrder: ["plugin.sample.analytics"],
                SidebarRouteOrder: ["plugin.sample.analytics.daily"],
                ThirdBarRouteOrder: ["plugin.sample.analytics.daily.filters"],
                HiddenRouteKeys: ["plugin.sample.reports"],
                ResetToDefaults: false),
            LastActiveModuleRouteKey = "plugin.sample.analytics.daily"
        };

        shell.ApplyPreference(preference);

        shell.TopNavigation.Select(item => item.RouteKey)
            .ShouldContain(RouteRegistry.PluginsRouteKey);
        shell.TopNavigation.Select(item => item.RouteKey)
            .ShouldNotContain("plugin.sample.reports");
        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics.daily");
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);
    }

    [Fact]
    public void Applying_runtime_navigation_preference_preserves_composition_constructor_routes()
    {
        var shell = new ShellViewModel(
            new HostStringCatalog(),
            NavigationComposer.Compose(SnapshotContributions()));
        var preference = ShellLayoutPreference.Default with
        {
            LastActiveModuleRouteKey = "plugin.sample.analytics.daily"
        };

        shell.ApplyPreference(preference);

        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics.daily");
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);
    }

    [Fact]
    public void Pending_work_session_stay_closes_request_without_changing_route()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var propertyChanges = new List<string?>();
        shell.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);

        var pending = coordinator.RequestNavigation("plugin.sample.analytics");

        pending.ShouldNotBeNull();
        pending.RequestedRouteKey.ShouldBe("plugin.sample.analytics");
        pending.CloseRequest.SessionId.ShouldBe(session.SessionId);
        pending.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.Navigation);

        shell.SetPendingWorkSessionCloseRequest(pending);
        shell.HasPendingWorkSessionCloseRequest.ShouldBeTrue();

        var result = shell.ResolvePendingWorkSessionClose(WorkSessionCloseChoice.Cancel, coordinator);

        result.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.PendingWorkSessionCloseRequest.ShouldBeNull();
        shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
        registry.Get(session.SessionId).ShouldNotBeNull();
        registry.Get(session.SessionId)!.IsMinimized.ShouldBeFalse();
        propertyChanges.ShouldContain(nameof(ShellViewModel.PendingWorkSessionCloseRequest));
        propertyChanges.ShouldContain(nameof(ShellViewModel.HasPendingWorkSessionCloseRequest));

        registry.Minimize(session.SessionId);
        coordinator.RequestNavigation("plugin.sample.analytics").ShouldBeNull();
    }

    [Theory]
    [InlineData(WorkSessionCloseChoice.Minimize)]
    [InlineData(WorkSessionCloseChoice.Close)]
    public void Pending_work_session_minimize_or_close_continues_requested_navigation(WorkSessionCloseChoice choice)
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var pending = coordinator.RequestNavigation("plugin.sample.analytics");
        shell.SetPendingWorkSessionCloseRequest(pending);

        var result = shell.ResolvePendingWorkSessionClose(choice, coordinator);

        result.IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics");
        shell.PendingWorkSessionCloseRequest.ShouldBeNull();
        shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
        if (choice == WorkSessionCloseChoice.Minimize)
        {
            registry.Get(session.SessionId)!.IsMinimized.ShouldBeTrue();
        }
        else
        {
            registry.Get(session.SessionId).ShouldBeNull();
        }
    }

    private static IReadOnlyList<NavigationContribution> SnapshotContributions()
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

        return [parent, child, leaf, reports];
    }
}
