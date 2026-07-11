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
    public void Activating_workspace_tab_uses_the_route_resolved_by_shell_route_host()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        var staleRouteTabId = shell.ActiveWorkspaceTab.TabId;
        var activeTab = shell.OpenWorkspaceTab();
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();

        var result = shell.ActivateWorkspaceTab(staleRouteTabId, RouteRegistry.HomeRouteKey);

        result.IsSuccess.ShouldBeTrue();
        shell.ActiveWorkspaceTab.TabId.ShouldBe(staleRouteTabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.WorkspaceTabs.Single(tab => tab.TabId == activeTab.TabId).RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
    }

    [Fact]
    public void Closing_only_workspace_tab_replaces_it_with_a_fresh_active_home_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var originalTabId = shell.ActiveWorkspaceTab.TabId;
        var onlyTabBefore = shell.ActiveWorkspaceTab;

        var result = shell.CloseWorkspaceTab(originalTabId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(RouteRegistry.HomeRouteKey);

        shell.WorkspaceTabs.Count.ShouldBe(1);
        shell.ActiveWorkspaceTab.ShouldBe(onlyTabBefore);
    }

    [Fact]
    public void Closing_active_tab_when_a_left_tab_exists_returns_the_immediate_left_tab_route_for_host_activation()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var firstTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        shell.ActivateRoute(RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        var middleTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        var closingTabId = shell.ActiveWorkspaceTab.TabId;
        var before = shell.WorkspaceTabs.ToArray();
        var activeBefore = shell.ActiveWorkspaceTab;

        var result = shell.CloseWorkspaceTab(closingTabId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(RouteRegistry.PluginsRouteKey);

        shell.WorkspaceTabs.ShouldBe(before);
        shell.ActiveWorkspaceTab.ShouldBe(activeBefore);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Closing_active_leftmost_tab_returns_the_next_right_tab_route_for_host_activation()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var closingTabId = shell.ActiveWorkspaceTab.TabId;
        var rightTab = shell.OpenWorkspaceTab();
        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        shell.ActivateWorkspaceTab(closingTabId).IsSuccess.ShouldBeTrue();
        var before = shell.WorkspaceTabs.ToArray();

        var result = shell.CloseWorkspaceTab(closingTabId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("plugin.sample.analytics.daily");
        shell.WorkspaceTabs.ShouldBe(before);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(closingTabId);
        shell.WorkspaceTabs.Single(tab => tab.TabId == rightTab.TabId).RouteKey.ShouldBe("plugin.sample.analytics.daily");
    }

    [Fact]
    public void Committing_active_tab_close_uses_the_route_resolved_by_shell_route_host()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var closingTabId = shell.ActiveWorkspaceTab.TabId;
        var survivingTab = shell.OpenWorkspaceTab();
        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        shell.ActivateWorkspaceTab(closingTabId).IsSuccess.ShouldBeTrue();

        var result = shell.CommitCloseWorkspaceTab(closingTabId, RouteRegistry.HomeRouteKey);

        result.IsSuccess.ShouldBeTrue();
        shell.WorkspaceTabs.Count.ShouldBe(1);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(survivingTab.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Closing_leftmost_tab_when_multiple_tabs_exist_activates_the_next_right_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var leftTabId = shell.ActiveWorkspaceTab.TabId;
        var rightTab = shell.OpenWorkspaceTab();

        var result = shell.CloseWorkspaceTab(leftTabId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();

        shell.WorkspaceTabs.Count.ShouldBe(1);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(rightTab.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
    }

    [Fact]
    public void Closing_inactive_tab_before_active_tab_preserves_the_current_active_tab()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var leftTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        shell.ActivateRoute(RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        var middleTabId = shell.ActiveWorkspaceTab.TabId;
        var activeTab = shell.OpenWorkspaceTab();
        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();

        var result = shell.CloseWorkspaceTab(middleTabId);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        shell.WorkspaceTabs.Select(tab => tab.TabId).ShouldBe([leftTabId, activeTab.TabId]);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(activeTab.TabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe("plugin.sample.analytics.daily");
        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics.daily");
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
    public void Active_plugin_page_model_keeps_host_owned_workspace_route_title()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var firstTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();

        shell.SetActivePluginPage(new DummyPluginPageModel("Live customer case"));

        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        var firstTab = shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId);
        firstTab.Title.ShouldBe("Tənzimləmələr");
        firstTab.IsActive.ShouldBeFalse();

        shell.SetActivePluginPage(null);

        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        firstTab = shell.WorkspaceTabs.Single(tab => tab.TabId == firstTabId);
        firstTab.Title.ShouldBe("Tənzimləmələr");
        firstTab.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Unapproved_plugin_page_model_label_getter_is_not_reflected_for_workspace_title()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();

        shell.SetActivePluginPage(new ThrowingLabelPluginPageModel());

        shell.ActiveWorkspaceTab.Title.ShouldBe("Tənzimləmələr");
        shell.HasActivePluginPage.ShouldBeTrue();
    }

    [Fact]
    public void Cancelled_pending_work_session_navigation_leaves_workspace_tabs_unchanged()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var registry = new Dobby.Shell.WorkSessions.WorkSessionRegistry();
        var coordinator = new Dobby.Shell.WorkSessions.WorkSessionCloseCoordinator(registry);
        registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var pending = coordinator.RequestNavigation("plugin.sample.work", RouteRegistry.PluginsRouteKey);
        pending.ShouldNotBeNull();
        var before = shell.WorkspaceTabs.ToArray();

        shell.SetPendingWorkSessionCloseRequest(pending);
        shell.ResolvePendingWorkSessionClose(Dobby.Shell.WorkSessions.WorkSessionCloseChoice.Cancel, coordinator).IsSuccess.ShouldBeTrue();

        shell.WorkspaceTabs.ShouldBe(before);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
    }

    [Fact]
    public void Exported_preference_tracks_last_route_and_sidebar_state_without_restoring_route_on_apply()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        shell.ToggleSidebarCollapsed();

        var exported = shell.ExportPreference(1440, 900, 24, 36);
        var restored = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        restored.ApplyPreference(exported);

        exported.LastActiveModuleRouteKey.ShouldBe("plugin.sample.analytics.daily");
        exported.SidebarCollapsed.ShouldBeTrue();
        exported.SidebarOpenNodeIds.ShouldContain("sample.analytics.daily");
        restored.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        restored.Layout.Preference.SidebarCollapsed.ShouldBeTrue();
        restored.Layout.Preference.SidebarOpenNodeIds.ShouldContain("sample.analytics.daily");
    }

    [Fact]
    public void Active_workspace_tab_close_is_expected_to_reenter_shell_route_host()
    {
        var mainWindowCode = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var workspaceTabClose = MethodBody(mainWindowCode, "private async void WorkspaceTabClose_Click", "private void ProfileMenuButton_Click");

        workspaceTabClose.ShouldContain("var continuationRoute = shellViewModel.CloseWorkspaceTab(tabId);");
        workspaceTabClose.ShouldContain("shellRouteHost!.ActivateOrHomeAsync(continuationRoute.Value)");
        workspaceTabClose.ShouldContain("shellViewModel.CommitCloseWorkspaceTab(tabId, activation.Value);");
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

    private sealed class ThrowingLabelPluginPageModel : Dobby.Contracts.PluginPages.IPluginPageModel
    {
        public string Label => throw new InvalidOperationException("raw label detail");
    }

    private static string MethodBody(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);
        return source[start..end];
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
