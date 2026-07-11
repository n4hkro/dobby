using Dobby.Contracts.Ordering;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.Localization;
using Dobby.Navigation;
using Dobby.Shell;
using Dobby.Shell.Layout;
using Dobby.Shell.WorkSessions;
using Shouldly;
using System.Windows;

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
            SidebarCollapsed = true,
            SidebarOpenNodeIds = ["sample.analytics.daily"],
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
        shell.Layout.Preference.SidebarCollapsed.ShouldBeTrue();
        shell.Layout.Preference.SidebarOpenNodeIds.ShouldBe(["sample.analytics.daily"]);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.SidebarNavigation.ShouldBeEmpty();
        shell.ThirdBarNavigation.ShouldBeEmpty();
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

        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.SidebarNavigation.ShouldBeEmpty();
        shell.ThirdBarNavigation.ShouldBeEmpty();
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

        var pending = coordinator.RequestNavigation("plugin.sample.work", "plugin.sample.analytics");

        pending.ShouldNotBeNull();
        pending.RequestedRouteKey.ShouldBe("plugin.sample.analytics");
        pending.CloseRequest.SessionId.ShouldBe(session.SessionId);
        pending.CloseRequest.Reason.ShouldBe(WorkSessionCloseReason.Navigation);

        shell.SetPendingWorkSessionCloseRequest(pending);
        shell.HasPendingWorkSessionCloseRequest.ShouldBeTrue();

        var result = shell.ResolvePendingWorkSessionClose(WorkSessionCloseChoice.Cancel, coordinator);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.PendingWorkSessionCloseRequest.ShouldBeNull();
        shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
        registry.Get(session.SessionId).ShouldNotBeNull();
        registry.Get(session.SessionId)!.IsMinimized.ShouldBeFalse();
        propertyChanges.ShouldContain(nameof(ShellViewModel.PendingWorkSessionCloseRequest));
        propertyChanges.ShouldContain(nameof(ShellViewModel.HasPendingWorkSessionCloseRequest));

        registry.Minimize(session.SessionId);
        coordinator.RequestNavigation("plugin.sample.work", "plugin.sample.analytics").ShouldBeNull();
    }

    [Theory]
    [InlineData(WorkSessionCloseChoice.Minimize)]
    [InlineData(WorkSessionCloseChoice.Close)]
    public void Pending_work_session_minimize_or_close_returns_requested_navigation_without_applying_choice_until_activation_commits(WorkSessionCloseChoice choice)
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var pending = coordinator.RequestNavigation("plugin.sample.work", "plugin.sample.analytics");
        shell.SetPendingWorkSessionCloseRequest(pending);

        var result = shell.ResolvePendingWorkSessionClose(choice, coordinator);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("plugin.sample.analytics");
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.PendingWorkSessionCloseRequest.ShouldBe(pending);
        shell.HasPendingWorkSessionCloseRequest.ShouldBeTrue();
        if (choice == WorkSessionCloseChoice.Minimize)
        {
            registry.Get(session.SessionId)!.IsMinimized.ShouldBeFalse();
        }
        else
        {
            registry.Get(session.SessionId).ShouldNotBeNull();
        }
    }

    [Fact]
    public void Pending_logout_close_applies_immediately_without_route_activation()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var pending = coordinator.HandleLogout();
        pending.ShouldNotBeNull();
        shell.SetPendingWorkSessionCloseRequest(pending);

        var result = shell.ResolvePendingWorkSessionClose(WorkSessionCloseChoice.Close, coordinator);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        shell.PendingWorkSessionCloseRequest.ShouldBeNull();
        shell.HasPendingWorkSessionCloseRequest.ShouldBeFalse();
        registry.Get(session.SessionId).ShouldBeNull();
    }

    [Fact]
    public void Pending_logout_rejects_minimize_and_preserves_the_request_and_session()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var pending = coordinator.HandleLogout();
        pending.ShouldNotBeNull();
        shell.SetPendingWorkSessionCloseRequest(pending);

        var result = shell.ResolvePendingWorkSessionClose(WorkSessionCloseChoice.Minimize, coordinator);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        shell.PendingWorkSessionCloseRequest.ShouldBe(pending);
        registry.Get(session.SessionId).ShouldNotBeNull();
        registry.Get(session.SessionId)!.IsMinimized.ShouldBeFalse();
    }

    [Fact]
    public void Pending_work_session_request_cannot_be_replaced_before_resolution_commits()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        registry.Open("plugin.sample", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var navigation = coordinator.RequestNavigation("plugin.sample.work", "plugin.sample.analytics");
        var logout = coordinator.HandleLogout();
        navigation.ShouldNotBeNull();
        logout.ShouldNotBeNull();

        shell.SetPendingWorkSessionCloseRequest(navigation).ShouldBeTrue();
        shell.SetPendingWorkSessionCloseRequest(logout).ShouldBeFalse();

        shell.PendingWorkSessionCloseRequest.ShouldBe(navigation);
        shell.PendingWorkSessionCloseRequest!.RequestId.ShouldBe(navigation.RequestId);
    }

    [Fact]
    public void Stale_navigation_commit_cannot_apply_its_choice_to_a_newer_pending_request()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var registry = new WorkSessionRegistry();
        var coordinator = new WorkSessionCloseCoordinator(registry);
        var session = registry.Open("plugin.sample", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var navigation = coordinator.RequestNavigation("plugin.sample.work", "plugin.sample.analytics");
        var logout = coordinator.HandleLogout();
        navigation.ShouldNotBeNull();
        logout.ShouldNotBeNull();
        shell.SetPendingWorkSessionCloseRequest(navigation).ShouldBeTrue();
        shell.SetPendingWorkSessionCloseRequest(null).ShouldBeTrue();
        shell.SetPendingWorkSessionCloseRequest(logout).ShouldBeTrue();

        var result = shell.CommitPendingWorkSessionClose(
            WorkSessionCloseChoice.Close,
            coordinator,
            navigation);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        shell.PendingWorkSessionCloseRequest.ShouldBe(logout);
        registry.Get(session.SessionId).ShouldNotBeNull();
    }

    [Fact]
    public void Active_plugin_page_model_is_exposed_and_notifies_shell_state()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        var propertyChanges = new List<string?>();
        shell.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);

        shell.SetActivePluginPage(new DummyPluginPageModel("plugin.page"));

        shell.HasActivePluginPage.ShouldBeTrue();
        shell.ActivePluginPageModel.ShouldNotBeNull();
        propertyChanges.ShouldContain(nameof(ShellViewModel.ActivePluginPageModel));
        propertyChanges.ShouldContain(nameof(ShellViewModel.HasActivePluginPage));
    }

    [Fact]
    public void Workspace_tab_route_restore_returns_route_without_selecting_tab_until_activation_commits()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();
        var analyticsTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        shell.ActivateRoute("plugin.sample.reports").IsSuccess.ShouldBeTrue();
        var activeBefore = shell.ActiveWorkspaceTab;

        var restoredRoute = shell.SelectWorkspaceTabForRouteActivation(analyticsTabId);

        restoredRoute.IsSuccess.ShouldBeTrue();
        restoredRoute.Value.ShouldBe("plugin.sample.analytics.daily");
        shell.ActiveWorkspaceTab.ShouldBe(activeBefore);
    }

    [Fact]
    public void Workspace_tab_click_uses_shell_route_host_instead_of_direct_route_activation()
    {
        var mainWindowCode = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var workspaceTabClick = MethodBody(mainWindowCode, "private async void WorkspaceTab_Click", "private async void WorkspaceTabClose_Click");
        var select = workspaceTabClick.IndexOf("SelectWorkspaceTabForRouteActivation", StringComparison.Ordinal);
        var activateTab = workspaceTabClick.IndexOf("shellViewModel.ActivateWorkspaceTab(tabId, restoredRouteKey)", StringComparison.Ordinal);
        var routeHost = workspaceTabClick.IndexOf("shellRouteHost!.ActivateOrHomeAsync(restoredRouteKey)", StringComparison.Ordinal);

        workspaceTabClick.ShouldContain("var previousActiveTab = shellViewModel.ActiveWorkspaceTab;");
        workspaceTabClick.ShouldContain("SelectWorkspaceTabForRouteActivation");
        workspaceTabClick.ShouldContain("shellViewModel.ActivateWorkspaceTab(tabId, restoredRouteKey)");
        workspaceTabClick.ShouldContain("shellRouteHost!.ActivateOrHomeAsync(restoredRouteKey)");
        workspaceTabClick.ShouldContain("shellViewModel.ActivateWorkspaceTab(previousActiveTab.TabId, previousActiveTab.RouteKey);");
        select.ShouldBeGreaterThanOrEqualTo(0);
        activateTab.ShouldBeGreaterThan(select);
        routeHost.ShouldBeGreaterThan(activateTab);
    }

    [Fact]
    public void Pending_work_session_choice_uses_shell_route_host_for_navigation_continuation()
    {
        var mainWindowCode = File.ReadAllText(Path.Combine(RepoRoot().FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var pendingChoice = MethodBody(mainWindowCode, "private async void WorkSessionMinimize_Click", "private void DiagnosticsExport_Click");

        pendingChoice.ShouldContain("ResolvePendingWorkSessionChoiceAsync");
        pendingChoice.ShouldContain("shellRouteHost!.ActivateOrHomeAsync(continuationRoute.Value)");
        pendingChoice.ShouldContain("shellViewModel.CommitPendingWorkSessionClose(");
        pendingChoice.ShouldContain("pending);");
    }

    [Fact]
    public void Built_in_route_surfaces_are_visible_only_for_the_active_route()
    {
        var shell = ShellViewModel.CreateDefault(new HostStringCatalog());
        shell.SetPluginsManagerSurface(
            canView: true,
            canImport: true,
            canConfigure: true,
            canDelete: true,
            plugins: []);
        shell.SetLicenseSurface(
            canViewStatus: true,
            canImport: true,
            Dobby.Contracts.Licensing.PluginRuntimeMode.FullAccess);
        shell.SetSettingsAndAbout(
            [new ShellSettingsCategoryItem("appearance", "Gorunus", IsAvailable: true)],
            [new ShellAboutField("version", "Versiya", "1.0.0")]);

        shell.HomePanelVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsSurfaceVisibility.ShouldBe(Visibility.Collapsed);
        shell.SettingsSurfaceVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicensePanelVisibility.ShouldBe(Visibility.Collapsed);

        shell.ActivateRoute(RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();

        shell.HomePanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsSurfaceVisibility.ShouldBe(Visibility.Visible);
        shell.SettingsSurfaceVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Visible);
        shell.LicensePanelVisibility.ShouldBe(Visibility.Visible);

        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();

        shell.HomePanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.PluginsSurfaceVisibility.ShouldBe(Visibility.Collapsed);
        shell.SettingsSurfaceVisibility.ShouldBe(Visibility.Visible);
        shell.PluginsManagerPanelVisibility.ShouldBe(Visibility.Collapsed);
        shell.LicensePanelVisibility.ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Applying_plugin_navigation_contributions_rebuilds_shell_navigation_without_losing_layout_state()
    {
        var shell = ShellViewModel.CreateDefault(new HostStringCatalog());
        shell.ShowBackgroundOperation(new Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot(
            Guid.NewGuid(),
            Dobby.Contracts.BackgroundOperations.BackgroundOperationOwnerKind.Host,
            null,
            "Dobby.Plugin.Activation",
            null,
            Dobby.Contracts.BackgroundOperations.BackgroundOperationStatus.Running,
            Dobby.Contracts.BackgroundOperations.BackgroundOperationProgress.Determinate(25),
            false,
            Guid.NewGuid(),
            null));
        var contributions = PluginNavigationContributionMapper.Map(
        [
            new PluginNavigationContribution(
                "com.dobby.sample",
                "sample.home",
                "sample.home",
                "sample.home.label",
                "top-toolbar",
                null,
                "default",
                100,
                0)
        ]);

        shell.ApplyPluginNavigationContributions(contributions);

        shell.TopNavigation.Select(item => item.RouteKey).ShouldContain("plugin/com.dobby.sample/sample.home");
        shell.BackgroundOperationPercent.ShouldBe(25);
        shell.ActivateRoute("plugin/com.dobby.sample/sample.home").IsSuccess.ShouldBeTrue();
        shell.ActiveRouteKey.ShouldBe("plugin/com.dobby.sample/sample.home");
    }

    [Fact]
    public void Applied_sidebar_open_node_state_is_visible_and_persisted()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ApplyPreference(ShellLayoutPreference.Default with
        {
            SidebarOpenNodeIds = ["sample.analytics.daily"]
        });

        shell.ActivateRoute("plugin.sample.analytics.daily").IsSuccess.ShouldBeTrue();

        shell.SidebarNavigation.Single().HasChildren.ShouldBeTrue();
        shell.SidebarNavigation.Single().IsOpen.ShouldBeTrue();
        shell.ExportPreference(1024, 680, null, null).SidebarOpenNodeIds.ShouldContain("sample.analytics.daily");
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

    private sealed class DummyPluginPageModel(string label) : IPluginPageModel
    {
        public string Label { get; } = label;
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
