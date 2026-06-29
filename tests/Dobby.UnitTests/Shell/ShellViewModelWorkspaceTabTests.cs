using Dobby.Contracts.Ordering;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Shouldly;

namespace Dobby.UnitTests.Shell;

public sealed class ShellViewModelWorkspaceTabTests
{
    [Fact]
    public void Startup_creates_one_active_home_workspace_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        shell.WorkspaceTabs.Count.ShouldBe(1);
        var tab = shell.WorkspaceTabs.Single();
        tab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        tab.Title.ShouldBe("Ana səhifə");
        tab.IsActive.ShouldBeTrue();
        tab.CanClose.ShouldBeTrue();
        shell.ActiveWorkspaceTab.ShouldBe(tab);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Opening_workspace_tab_adds_active_home_tab_and_keeps_previous_tab_route()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var firstTabId = shell.ActiveWorkspaceTab.TabId;

        var secondTab = shell.OpenWorkspaceTab();

        shell.WorkspaceTabs.Count.ShouldBe(2);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(secondTab.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId).RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId).Title.ShouldBe("Tənzimləmələr");
        shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId).IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Navigation_updates_only_the_active_workspace_tab_route_and_title()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var firstTabId = shell.ActiveWorkspaceTab.TabId;
        var secondTab = shell.OpenWorkspaceTab();

        shell.ActivateRoute(RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();

        var firstTab = shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId);
        var activeTab = shell.WorkspaceTabs.Single(tab => tab.TabId == secondTab.TabId);
        firstTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        firstTab.Title.ShouldBe("Tənzimləmələr");
        firstTab.IsActive.ShouldBeFalse();
        activeTab.RouteKey.ShouldBe(RouteRegistry.PluginsRouteKey);
        activeTab.Title.ShouldBe("Pluginlər");
        activeTab.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activating_workspace_tab_restores_that_tabs_route_and_surface_state()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var settingsTabId = shell.ActiveWorkspaceTab.TabId;
        var homeTab = shell.OpenWorkspaceTab();

        shell.ActivateWorkspaceTab(settingsTabId).IsSuccess.ShouldBeTrue();

        shell.ActiveWorkspaceTab.TabId.ShouldBe(settingsTabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.SettingsSurfaceVisibility.ShouldBe(System.Windows.Visibility.Visible);
        shell.WorkspaceTabs.Single(tab => tab.TabId == homeTab.TabId).RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Closing_only_workspace_tab_replaces_it_with_a_fresh_active_home_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var originalTabId = shell.ActiveWorkspaceTab.TabId;

        shell.CloseWorkspaceTab(originalTabId).IsSuccess.ShouldBeTrue();

        shell.WorkspaceTabs.Count.ShouldBe(1);
        shell.ActiveWorkspaceTab.TabId.ShouldNotBe(originalTabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Closing_active_tab_when_a_left_tab_exists_activates_the_immediate_left_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var firstTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        shell.ActivateRoute(RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        var middleTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        var closingTabId = shell.ActiveWorkspaceTab.TabId;

        shell.CloseWorkspaceTab(closingTabId).IsSuccess.ShouldBeTrue();

        shell.WorkspaceTabs.Count.ShouldBe(2);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(middleTabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.PluginsRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Pluginlər");
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.PluginsRouteKey);
        var firstTab = shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId);
        firstTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        firstTab.Title.ShouldBe("Tənzimləmələr");
        firstTab.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Closing_leftmost_tab_when_multiple_tabs_exist_activates_the_next_right_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var leftTabId = shell.ActiveWorkspaceTab.TabId;
        var rightTab = shell.OpenWorkspaceTab();

        shell.CloseWorkspaceTab(leftTabId).IsSuccess.ShouldBeTrue();

        shell.WorkspaceTabs.Count.ShouldBe(1);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(rightTab.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Invalid_workspace_tab_ids_fail_without_mutating_state()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var before = shell.WorkspaceTabs.ToArray();
        var activeBefore = shell.ActiveWorkspaceTab;
        var routeBefore = shell.ActiveRouteKey;

        shell.ActivateWorkspaceTab(Guid.Parse("11111111-1111-1111-1111-111111111111")).IsSuccess.ShouldBeFalse();
        shell.CloseWorkspaceTab(Guid.Parse("22222222-2222-2222-2222-222222222222")).IsSuccess.ShouldBeFalse();

        shell.WorkspaceTabs.ShouldBe(before);
        shell.ActiveWorkspaceTab.ShouldBe(activeBefore);
        shell.ActiveRouteKey.ShouldBe(routeBefore);
    }

    [Fact]
    public void Active_plugin_page_label_overrides_the_active_workspace_tab_title()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var firstTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();

        shell.SetActivePluginPage(new DummyPluginPageModel("Live customer case"));

        shell.ActiveWorkspaceTab.Title.ShouldBe("Live customer case");
        var firstTab = shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId);
        firstTab.Title.ShouldBe("Tənzimləmələr");
        firstTab.IsActive.ShouldBeFalse();

        shell.SetActivePluginPage(null);

        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        firstTab = shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId);
        firstTab.Title.ShouldBe("Tənzimləmələr");
        firstTab.IsActive.ShouldBeFalse();
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

        return [parent, child, leaf];
    }

    private sealed class DummyPluginPageModel(string label) : Dobby.Contracts.PluginPages.IPluginPageModel
    {
        public string Label { get; } = label;
    }
}
