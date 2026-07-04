# Shell Workstation Tabs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the agreed shell-first workstation tab experience: startup and every new tab open Home, tab titles track the active content, close behavior matches the approved rules, the main window close button asks for confirmation, and the shell visual structure uses one continuous light background.

**Architecture:** `ShellViewModel` owns workstation tab state and route state per tab. `MainWindow.xaml` renders a custom Chrome-like titlebar tab row, continuous-background sidebar, centered top navigation, and existing content surfaces; `MainWindow.xaml.cs` delegates tab and route actions to the view model and owns WPF-only confirmation dialogs. Existing work-session navigation warnings remain the lifecycle authority and continue to gate route changes.

**Tech Stack:** C#/.NET WPF, XAML, xUnit, Shouldly, existing Dobby shell/navigation/localization/theme tokens.

---

## Source Spec

Implement against `docs/superpowers/specs/2026-06-29-shell-workstation-tabs-design.md`.

The non-negotiable product rules from the approved spec:

- App startup shows exactly one active Home tab whose title is Home.
- `+` opens a new active Home tab every time.
- Navigation updates only the active tab route and title.
- Switching tabs restores that tab route.
- Closing the only tab replaces it with a fresh Home tab.
- Closing any tab when multiple tabs exist activates the immediate left tab, or the next tab when closing the leftmost tab.
- Main window `X` asks for confirmation and closes the whole app only when confirmed.
- Sidebar and top toolbar sit on one continuous shell background with no separate block background.
- Expanded sidebar shows brand mark plus app name and icon plus label items; collapsed sidebar shows only brand mark and icons.
- Top navigation is centered in the content area.

## File Structure

Create:

- `src/Dobby.Shell/WorkspaceTabs/ShellWorkspaceTab.cs`
  - Immutable view-model item for one workstation tab.
- `src/Dobby.Host/ShellWindowCloseConfirmation.cs`
  - WPF confirmation interface and default MessageBox implementation for whole-window close.
- `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`
  - Deterministic view-model tests for startup, new tab, title tracking, tab switching, close rules, and invalid ids.

Modify:

- `src/Dobby.Shell/ShellViewModel.cs`
  - Add workspace tab state, localized/glyph properties, tab lifecycle methods, active-tab route/title updates, and startup Home reset semantics.
- `src/Dobby.Localization/HostResourceKeys.cs`
  - Add shell tab/window-control labels and include them in `Sprint7VisibleKeys`.
- `src/Dobby.Localization/HostStringCatalog.cs`
  - Add fallback az-AZ strings for the new labels.
- `src/Dobby.Localization/Resources/Host.az-AZ.resx`
  - Add resource-backed az-AZ strings for the new labels.
- `src/Dobby.Host/MainWindow.xaml`
  - Replace the current normal-titlebar layout with a custom titlebar tab strip, continuous-background shell, expanded/collapsed sidebar rendering, centered top nav, and existing content panels moved into the new content host.
- `src/Dobby.Host/MainWindow.xaml.cs`
  - Add constructor injection for close confirmation, tab/new-tab/window-control handlers, and titlebar drag/maximize behavior.
- `tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs`
  - Update preference tests so persisted last route does not defeat the always-Home startup rule.
- `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`
  - Add rendered WPF coverage for tab creation, navigation-in-active-tab, tab close, and close confirmation.
- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
  - Require the new named regions and reject separate backgrounds on `ShellSidebar` and `TopToolbar`.

Do not change:

- `WorkSessionRegistry` or `WorkSessionCloseCoordinator` ownership semantics.
- Durable preference schema in `src/Dobby.Configuration/ShellViewPreference.cs`.
- Plugin activation timing in `src/Dobby.Host/App.xaml.cs` except as needed to keep startup Home already applied by `ShellViewModel`.

---

## Task 1: Add Failing View-Model Tests For Workspace Tabs

**Files:**
- Create: `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs` with this complete content:

```csharp
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
        var leftTabId = shell.ActiveWorkspaceTab.TabId;
        shell.OpenWorkspaceTab();
        shell.ActivateRoute(RouteRegistry.PluginsRouteKey).IsSuccess.ShouldBeTrue();
        var closingTabId = shell.ActiveWorkspaceTab.TabId;

        shell.CloseWorkspaceTab(closingTabId).IsSuccess.ShouldBeTrue();

        shell.WorkspaceTabs.Count.ShouldBe(1);
        shell.ActiveWorkspaceTab.TabId.ShouldBe(leftTabId);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
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

        shell.ActivateWorkspaceTab(Guid.CreateVersion7()).IsSuccess.ShouldBeFalse();
        shell.CloseWorkspaceTab(Guid.CreateVersion7()).IsSuccess.ShouldBeFalse();

        shell.WorkspaceTabs.ShouldBe(before);
        shell.ActiveWorkspaceTab.ShouldBe(activeBefore);
        shell.ActiveRouteKey.ShouldBe(routeBefore);
    }

    [Fact]
    public void Active_plugin_page_label_overrides_the_active_workspace_tab_title()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());

        shell.SetActivePluginPage(new DummyPluginPageModel("Live customer case"));

        shell.ActiveWorkspaceTab.Title.ShouldBe("Live customer case");
        shell.SetActivePluginPage(null);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
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
```

- [ ] **Step 2: Run the new tests and verify they fail for missing workspace-tab API**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~ShellViewModelWorkspaceTabTests
```

Expected: build fails with compiler errors such as `CS1061` for `WorkspaceTabs`, `OpenWorkspaceTab`, `ActivateWorkspaceTab`, `CloseWorkspaceTab`, or `ActiveWorkspaceTab`.

- [ ] **Step 3: Commit the failing tests**

Run:

```powershell
git add tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs
git commit -m "test: define workstation tab shell behavior"
```

Expected: commit succeeds and records only the new test file.

---

## Task 2: Implement Workspace Tab State In ShellViewModel

**Files:**
- Create: `src/Dobby.Shell/WorkspaceTabs/ShellWorkspaceTab.cs`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`
- Test: `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`

- [ ] **Step 1: Add the workspace tab record**

Create `src/Dobby.Shell/WorkspaceTabs/ShellWorkspaceTab.cs`:

```csharp
namespace Dobby.Shell.WorkspaceTabs;

public sealed record ShellWorkspaceTab(
    Guid TabId,
    string Title,
    string RouteKey,
    bool IsActive,
    bool CanClose);
```

- [ ] **Step 2: Add namespace import and fields to ShellViewModel**

In `src/Dobby.Shell/ShellViewModel.cs`, add this using with the existing shell usings:

```csharp
using Dobby.Shell.WorkspaceTabs;
```

Inside `ShellViewModel`, add this field after `private string? pluginsManagerFeedbackKey;`:

```csharp
    private readonly List<ShellWorkspaceTab> workspaceTabs = [];
```

- [ ] **Step 3: Initialize one Home workspace tab in both constructors**

In both `ShellViewModel` constructors, replace:

```csharp
        Layout = ShellLayoutState.CreateDefault(navigation);
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey);
```

with:

```csharp
        Layout = ShellLayoutState.CreateDefault(navigation);
        ResetWorkspaceTabsToHome();
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
```

- [ ] **Step 4: Add public tab properties and UI label/glyph properties**

Add these properties after `public string BrandName => strings.GetString(HostResourceKeys.ShellBrand);`:

```csharp
    public string BrandMark => "D";

    public IReadOnlyList<ShellWorkspaceTab> WorkspaceTabs => workspaceTabs;

    public ShellWorkspaceTab ActiveWorkspaceTab => workspaceTabs.First(tab => tab.IsActive);

    public string WorkspaceNewTabLabel => strings.GetString(HostResourceKeys.ShellWorkspaceNewTab);

    public string WorkspaceCloseTabLabel => strings.GetString(HostResourceKeys.ShellWorkspaceCloseTab);

    public string WindowMinimizeLabel => strings.GetString(HostResourceKeys.ShellWindowMinimize);

    public string WindowMaximizeLabel => strings.GetString(HostResourceKeys.ShellWindowMaximize);

    public string WindowRestoreLabel => strings.GetString(HostResourceKeys.ShellWindowRestore);

    public string WindowCloseLabel => strings.GetString(HostResourceKeys.ShellWindowClose);

    public string WorkspaceNewTabGlyph => "+";

    public string WorkspaceTabCloseGlyph => "x";

    public string WindowMinimizeGlyph => "-";

    public string WindowMaximizeGlyph => "[]";

    public string WindowCloseGlyph => "x";
```

- [ ] **Step 5: Add tab lifecycle methods**

Add these methods after `public event PropertyChangedEventHandler? PropertyChanged;`:

```csharp
    public ShellWorkspaceTab OpenWorkspaceTab()
    {
        DeactivateAllWorkspaceTabs();
        var tab = CreateWorkspaceTab(RouteRegistry.HomeRouteKey, isActive: true);
        workspaceTabs.Add(tab);
        RebuildNavigationLevels(tab.RouteKey, updateActiveWorkspaceTab: false);
        NotifyWorkspaceTabsChanged();
        return tab;
    }

    public Result ActivateWorkspaceTab(Guid tabId)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with
            {
                IsActive = workspaceTabs[index].TabId == tabId
            };
        }

        RebuildNavigationLevels(workspaceTabs[tabIndex].RouteKey, updateActiveWorkspaceTab: false);
        NotifyWorkspaceTabsChanged();
        return Result.Success();
    }

    public Result CloseWorkspaceTab(Guid tabId)
    {
        var tabIndex = IndexOfWorkspaceTab(tabId);
        if (tabIndex < 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        if (workspaceTabs.Count == 1)
        {
            ResetWorkspaceTabsToHome();
            RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
            NotifyWorkspaceTabsChanged();
            return Result.Success();
        }

        var nextActiveIndex = tabIndex == 0 ? 0 : tabIndex - 1;
        workspaceTabs.RemoveAt(tabIndex);
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with { IsActive = index == nextActiveIndex };
        }

        RebuildNavigationLevels(workspaceTabs[nextActiveIndex].RouteKey, updateActiveWorkspaceTab: false);
        NotifyWorkspaceTabsChanged();
        return Result.Success();
    }
```

- [ ] **Step 6: Update route activation to rewrite only the active workspace tab**

Replace `ActivateRoute` with:

```csharp
    public Result ActivateRoute(string? routeKey)
    {
        RebuildNavigationLevels(routeKey);
        return Result.Success();
    }
```

Then replace the existing private method signature:

```csharp
    private void RebuildNavigationLevels(string? routeKey)
```

with:

```csharp
    private void RebuildNavigationLevels(string? routeKey, bool updateActiveWorkspaceTab = true)
```

Inside `RebuildNavigationLevels`, after:

```csharp
        ActiveRouteKey = path[^1].RouteKey;
```

insert:

```csharp
        if (updateActiveWorkspaceTab)
        {
            UpdateActiveWorkspaceRoute(ActiveRouteKey);
        }
```

- [ ] **Step 7: Reset startup preference application to Home**

In `ApplyPreference`, replace:

```csharp
        RebuildNavigationLevels(preference.LastActiveModuleRouteKey ?? RouteRegistry.HomeRouteKey);
        OnLayoutChanged();
```

with:

```csharp
        ResetWorkspaceTabsToHome();
        RebuildNavigationLevels(RouteRegistry.HomeRouteKey, updateActiveWorkspaceTab: false);
        OnLayoutChanged();
        NotifyWorkspaceTabsChanged();
```

This preserves navigation ordering and window/sidebar preferences while enforcing the approved always-Home startup rule.

- [ ] **Step 8: Update active plugin page title behavior**

Replace `SetActivePluginPage` with:

```csharp
    public void SetActivePluginPage(IPluginPageModel? model)
    {
        activePluginPageModel = model;
        if (model is null)
        {
            UpdateActiveWorkspaceRoute(ActiveRouteKey);
        }
        else
        {
            UpdateActiveWorkspaceTitle(model.Label);
        }

        OnPropertyChanged(nameof(ActivePluginPageModel));
        OnPropertyChanged(nameof(HasActivePluginPage));
        NotifyRouteSurfaceChanged();
    }
```

- [ ] **Step 9: Add private workspace helpers**

Add these helpers before `NotificationLabelKey`:

```csharp
    private void ResetWorkspaceTabsToHome()
    {
        workspaceTabs.Clear();
        workspaceTabs.Add(CreateWorkspaceTab(RouteRegistry.HomeRouteKey, isActive: true));
    }

    private ShellWorkspaceTab CreateWorkspaceTab(string routeKey, bool isActive)
    {
        return new ShellWorkspaceTab(
            Guid.CreateVersion7(),
            ResolveWorkspaceTitle(routeKey),
            routeKey,
            isActive,
            CanClose: true);
    }

    private string ResolveWorkspaceTitle(string routeKey)
    {
        var path = navigation.ResolvePathOrHome(routeKey);
        return strings.GetString(path[^1].LabelKey);
    }

    private int IndexOfWorkspaceTab(Guid tabId)
    {
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            if (workspaceTabs[index].TabId == tabId)
            {
                return index;
            }
        }

        return -1;
    }

    private void DeactivateAllWorkspaceTabs()
    {
        for (var index = 0; index < workspaceTabs.Count; index++)
        {
            workspaceTabs[index] = workspaceTabs[index] with { IsActive = false };
        }
    }

    private void UpdateActiveWorkspaceRoute(string routeKey)
    {
        var activeIndex = workspaceTabs.FindIndex(tab => tab.IsActive);
        if (activeIndex < 0)
        {
            return;
        }

        workspaceTabs[activeIndex] = workspaceTabs[activeIndex] with
        {
            RouteKey = routeKey,
            Title = ResolveWorkspaceTitle(routeKey)
        };
        NotifyWorkspaceTabsChanged();
    }

    private void UpdateActiveWorkspaceTitle(string title)
    {
        var activeIndex = workspaceTabs.FindIndex(tab => tab.IsActive);
        if (activeIndex < 0)
        {
            return;
        }

        workspaceTabs[activeIndex] = workspaceTabs[activeIndex] with
        {
            Title = title
        };
        NotifyWorkspaceTabsChanged();
    }

    private void NotifyWorkspaceTabsChanged()
    {
        OnPropertyChanged(nameof(WorkspaceTabs));
        OnPropertyChanged(nameof(ActiveWorkspaceTab));
    }
```

- [ ] **Step 10: Run the workspace tab tests**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~ShellViewModelWorkspaceTabTests
```

Expected: all tests in `ShellViewModelWorkspaceTabTests` pass.

- [ ] **Step 11: Commit the view-model implementation**

Run:

```powershell
git add src/Dobby.Shell/WorkspaceTabs/ShellWorkspaceTab.cs src/Dobby.Shell/ShellViewModel.cs tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs
git commit -m "feat: add workstation tab shell state"
```

Expected: commit succeeds with the model and tests.

---

## Task 3: Update Preference Tests For Always-Home Startup

**Files:**
- Modify: `tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs`
- Test: `tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs`

- [ ] **Step 1: Change the preference expectations**

In `Applying_runtime_navigation_preference_consumes_order_and_hidden_overrides`, replace:

```csharp
        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics.daily");
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);
```

with:

```csharp
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.SidebarNavigation.ShouldBeEmpty();
        shell.ThirdBarNavigation.ShouldBeEmpty();
```

In `Applying_runtime_navigation_preference_preserves_composition_constructor_routes`, replace:

```csharp
        shell.ActiveRouteKey.ShouldBe("plugin.sample.analytics.daily");
        shell.SidebarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily"]);
        shell.ThirdBarNavigation.Select(item => item.RouteKey)
            .ShouldBe(["plugin.sample.analytics.daily.filters"]);
```

with:

```csharp
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
        shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");
        shell.SidebarNavigation.ShouldBeEmpty();
        shell.ThirdBarNavigation.ShouldBeEmpty();
```

- [ ] **Step 2: Run the adjusted navigation tests**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~ShellViewModelNavigationTests
```

Expected: all `ShellViewModelNavigationTests` pass.

- [ ] **Step 3: Commit preference test updates**

Run:

```powershell
git add tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs
git commit -m "test: keep shell startup anchored on home"
```

Expected: commit succeeds with only the navigation test update.

---

## Task 4: Add Localized Shell Tab And Window-Control Labels

**Files:**
- Modify: `src/Dobby.Localization/HostResourceKeys.cs`
- Modify: `src/Dobby.Localization/HostStringCatalog.cs`
- Modify: `src/Dobby.Localization/Resources/Host.az-AZ.resx`
- Test: `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`

- [ ] **Step 1: Add resource keys**

In `HostResourceKeys`, after `public const string ShellThirdBar = "Dobby.Shell.ThirdBar";`, add:

```csharp
    public const string ShellWorkspaceNewTab = "Dobby.Shell.Workspace.NewTab";
    public const string ShellWorkspaceCloseTab = "Dobby.Shell.Workspace.CloseTab";
    public const string ShellWindowMinimize = "Dobby.Shell.Window.Minimize";
    public const string ShellWindowMaximize = "Dobby.Shell.Window.Maximize";
    public const string ShellWindowRestore = "Dobby.Shell.Window.Restore";
    public const string ShellWindowClose = "Dobby.Shell.Window.Close";
    public const string ShellWindowCloseConfirmationTitle = "Dobby.Shell.Window.CloseConfirmation.Title";
    public const string ShellWindowCloseConfirmationMessage = "Dobby.Shell.Window.CloseConfirmation.Message";
```

In `Sprint7VisibleKeys`, after `ShellThirdBar,`, add:

```csharp
        ShellWorkspaceNewTab,
        ShellWorkspaceCloseTab,
        ShellWindowMinimize,
        ShellWindowMaximize,
        ShellWindowRestore,
        ShellWindowClose,
        ShellWindowCloseConfirmationTitle,
        ShellWindowCloseConfirmationMessage,
```

- [ ] **Step 2: Add HostStringCatalog fallback values**

In `HostStringCatalog`, after:

```csharp
        [HostResourceKeys.ShellThirdBar] = "Üçüncü panel",
```

add:

```csharp
        [HostResourceKeys.ShellWorkspaceNewTab] = "Yeni Home tab",
        [HostResourceKeys.ShellWorkspaceCloseTab] = "Tabı bağla",
        [HostResourceKeys.ShellWindowMinimize] = "Pəncərəni yığ",
        [HostResourceKeys.ShellWindowMaximize] = "Pəncərəni böyüt",
        [HostResourceKeys.ShellWindowRestore] = "Pəncərəni bərpa et",
        [HostResourceKeys.ShellWindowClose] = "Tətbiqi bağla",
        [HostResourceKeys.ShellWindowCloseConfirmationTitle] = "Tətbiqi bağla",
        [HostResourceKeys.ShellWindowCloseConfirmationMessage] = "Dobby tətbiqini bağlamaq istəyirsiniz?",
```

- [ ] **Step 3: Add resx values**

In `Host.az-AZ.resx`, after the `Dobby.Shell.ThirdBar` data node, add:

```xml
  <data name="Dobby.Shell.Workspace.NewTab" xml:space="preserve">
    <value>Yeni Home tab</value>
  </data>
  <data name="Dobby.Shell.Workspace.CloseTab" xml:space="preserve">
    <value>Tabı bağla</value>
  </data>
  <data name="Dobby.Shell.Window.Minimize" xml:space="preserve">
    <value>Pəncərəni yığ</value>
  </data>
  <data name="Dobby.Shell.Window.Maximize" xml:space="preserve">
    <value>Pəncərəni böyüt</value>
  </data>
  <data name="Dobby.Shell.Window.Restore" xml:space="preserve">
    <value>Pəncərəni bərpa et</value>
  </data>
  <data name="Dobby.Shell.Window.Close" xml:space="preserve">
    <value>Tətbiqi bağla</value>
  </data>
  <data name="Dobby.Shell.Window.CloseConfirmation.Title" xml:space="preserve">
    <value>Tətbiqi bağla</value>
  </data>
  <data name="Dobby.Shell.Window.CloseConfirmation.Message" xml:space="preserve">
    <value>Dobby tətbiqini bağlamaq istəyirsiniz?</value>
  </data>
```

- [ ] **Step 4: Run localization/static architecture test**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ShellUiStaticScanTests.Host_shell_xaml_does_not_hardcode_visible_strings_or_shell_level_tokens
```

Expected: pass. This test should still pass before the XAML rewrite because no XAML visible strings have been added.

- [ ] **Step 5: Commit localization labels**

Run:

```powershell
git add src/Dobby.Localization/HostResourceKeys.cs src/Dobby.Localization/HostStringCatalog.cs src/Dobby.Localization/Resources/Host.az-AZ.resx
git commit -m "feat: add shell tab localization labels"
```

Expected: commit succeeds with the localization files.

---

## Task 5: Add WPF Close Confirmation Abstraction

**Files:**
- Create: `src/Dobby.Host/ShellWindowCloseConfirmation.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Test: `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`

- [ ] **Step 1: Create the confirmation service**

Create `src/Dobby.Host/ShellWindowCloseConfirmation.cs`:

```csharp
using System.Windows;
using Dobby.Localization;

namespace Dobby.Host;

public interface IShellWindowCloseConfirmation
{
    bool ConfirmClose();
}

public sealed class WpfShellWindowCloseConfirmation(
    Window owner,
    HostStringCatalog? strings = null) : IShellWindowCloseConfirmation
{
    private readonly HostStringCatalog stringCatalog = strings ?? HostStringCatalog.CreateResourceBacked();

    public bool ConfirmClose()
    {
        var response = MessageBox.Show(
            owner,
            stringCatalog.GetString(HostResourceKeys.ShellWindowCloseConfirmationMessage),
            stringCatalog.GetString(HostResourceKeys.ShellWindowCloseConfirmationTitle),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);

        return response == MessageBoxResult.OK;
    }
}
```

- [ ] **Step 2: Inject the confirmation service into MainWindow**

In `MainWindow.xaml.cs`, add this field after `private readonly IPluginUninstallConfirmation pluginUninstallConfirmation;`:

```csharp
    private readonly IShellWindowCloseConfirmation shellWindowCloseConfirmation;
```

In the parameterless constructor, after:

```csharp
        pluginUninstallConfirmation = new WpfPluginUninstallConfirmation(this);
```

add:

```csharp
        shellWindowCloseConfirmation = new WpfShellWindowCloseConfirmation(this);
```

In the long constructor signature, after:

```csharp
        IPluginUninstallConfirmation? pluginUninstallConfirmation = null,
```

add:

```csharp
        IShellWindowCloseConfirmation? shellWindowCloseConfirmation = null,
```

In the long constructor body, after:

```csharp
        this.pluginUninstallConfirmation = pluginUninstallConfirmation ?? new WpfPluginUninstallConfirmation(this);
```

add:

```csharp
        this.shellWindowCloseConfirmation = shellWindowCloseConfirmation ?? new WpfShellWindowCloseConfirmation(this);
```

- [ ] **Step 3: Add the window close handler**

In `MainWindow.xaml.cs`, add this method near the other click handlers:

```csharp
    private void WindowClose_Click(object sender, RoutedEventArgs e)
    {
        if (shellWindowCloseConfirmation.ConfirmClose())
        {
            Close();
        }
    }
```

- [ ] **Step 4: Add a smoke test fake for the confirmation service**

In `ShellKeyboardSmokeTests.cs`, add this nested class near `CountingPreferenceSource`:

```csharp
    private sealed class RecordingShellWindowCloseConfirmation(bool result) : IShellWindowCloseConfirmation
    {
        public int CallCount { get; private set; }

        public bool ConfirmClose()
        {
            CallCount++;
            return result;
        }
    }
```

Add this test before helper methods:

```csharp
    [Fact]
    public void Rendered_shell_window_close_button_requests_confirmation_and_respects_cancel()
    {
        RunOnSta(() =>
        {
            var confirmation = new RecordingShellWindowCloseConfirmation(result: false);
            var window = new MainWindow(
                null!,
                null,
                null!,
                ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked()),
                null,
                null,
                null,
                null,
                null,
                null,
                shellWindowCloseConfirmation: confirmation)
            {
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                FindNamed<Button>(window, "WindowCloseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                confirmation.CallCount.ShouldBe(1);
                window.IsVisible.ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        });
    }
```

- [ ] **Step 5: Run the new smoke test and verify it fails before XAML adds the button**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter FullyQualifiedName~Rendered_shell_window_close_button_requests_confirmation
```

Expected: test fails because `WindowCloseButton` does not exist in `MainWindow.xaml`.

- [ ] **Step 6: Commit the confirmation service and failing smoke test**

Run:

```powershell
git add src/Dobby.Host/ShellWindowCloseConfirmation.cs src/Dobby.Host/MainWindow.xaml.cs tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs
git commit -m "test: require shell window close confirmation"
```

Expected: commit succeeds with the service, handler, and failing UI expectation.

---

## Task 6: Rewrite Shell XAML Around Custom Titlebar, Tabs, Sidebar, And Centered Top Nav

**Files:**
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Test: `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
- Test: `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`

- [ ] **Step 1: Add static scan requirements for the new shell regions**

In `ShellUiStaticScanTests.Host_shell_xaml_declares_required_rendered_regions_and_action_surfaces`, add these strings to `requiredNames`:

```csharp
            "WindowTitleBar",
            "WorkspaceTabStrip",
            "WorkspaceNewTabButton",
            "WindowMinimizeButton",
            "WindowMaximizeRestoreButton",
            "WindowCloseButton",
            "ShellSidebar",
            "ShellBrandMark",
            "CenteredTopToolbar",
            "ShellContentHost",
```

After the existing shell event-handler assertions, add:

```csharp
        shell.ShouldContain("WorkspaceNewTab_Click");
        shell.ShouldContain("WorkspaceTab_Click");
        shell.ShouldContain("WorkspaceTabClose_Click");
        shell.ShouldContain("WindowMinimize_Click");
        shell.ShouldContain("WindowMaximizeRestore_Click");
        shell.ShouldContain("WindowClose_Click");
        shell.ShouldContain("ItemsSource=\"{Binding WorkspaceTabs}\"");
        shell.ShouldContain("AutomationProperties.Name=\"{Binding WorkspaceNewTabLabel}\"");
        shell.ShouldContain("x:Name=\"CenteredTopToolbar\"");
```

Add this new test to `ShellUiStaticScanTests`:

```csharp
    [Fact]
    public void Host_shell_sidebar_and_top_toolbar_share_continuous_background()
    {
        var root = RepoRoot();
        var shell = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml"));

        NamedElement(shell, "ShellSidebar").ShouldNotContain("Background=");
        NamedElement(shell, "TopToolbar").ShouldNotContain("Background=");
        NamedElement(shell, "CenteredTopToolbar").ShouldNotContain("Background=");
    }

    private static string NamedElement(string xaml, string name)
    {
        var nameIndex = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        nameIndex.ShouldBeGreaterThanOrEqualTo(0, $"Expected x:Name=\"{name}\" in MainWindow.xaml.");
        var start = xaml.LastIndexOf('<', nameIndex);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextName = xaml.IndexOf("x:Name=\"", nameIndex + name.Length + 8, StringComparison.Ordinal);
        return nextName < 0
            ? xaml[start..]
            : xaml[start..nextName];
    }
```

- [ ] **Step 2: Run static scan and verify it fails for missing regions**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ShellUiStaticScanTests
```

Expected: fails because the new named regions and event handlers are not yet in XAML.

- [ ] **Step 3: Add custom titlebar attributes and namespace**

In `MainWindow.xaml`, add this namespace on `<Window>`:

```xml
    xmlns:shellChrome="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
```

Add these attributes to `<Window>`:

```xml
    WindowStyle="None"
    ResizeMode="CanResize"
```

Add this `WindowChrome` block directly inside `<Window>` before the root `<Grid>`:

```xml
  <shellChrome:WindowChrome.WindowChrome>
    <shellChrome:WindowChrome
        CaptionHeight="38"
        CornerRadius="0"
        GlassFrameThickness="0"
        ResizeBorderThickness="6"
        UseAeroCaptionButtons="False" />
  </shellChrome:WindowChrome.WindowChrome>
```

- [ ] **Step 4: Replace the root grid shape**

Replace the opening root grid and its row/column definitions:

```xml
  <Grid Margin="{DynamicResource Dobby.Spacing.Medium}">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="240" />
      <ColumnDefinition Width="*" />
      <ColumnDefinition Width="220" />
    </Grid.ColumnDefinitions>
```

with:

```xml
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
```

- [ ] **Step 5: Replace the old `TopToolbar` DockPanel with a custom titlebar plus centered top toolbar**

Replace the old block that starts with `<DockPanel` and contains `x:Name="TopToolbar"` and ends at that DockPanel's closing `</DockPanel>` with:

```xml
    <Grid
        x:Name="WindowTitleBar"
        Grid.Row="0"
        MinHeight="44"
        MouseLeftButtonDown="WindowTitleBar_MouseLeftButtonDown">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <ItemsControl
          x:Name="WorkspaceTabStrip"
          Grid.Column="0"
          MinWidth="280"
          ItemsSource="{Binding WorkspaceTabs}"
          shellChrome:WindowChrome.IsHitTestVisibleInChrome="True">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal" />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Grid Margin="{DynamicResource Dobby.Spacing.Small}">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="Auto" />
              </Grid.ColumnDefinitions>
              <Button
                  Grid.Column="0"
                  MinWidth="132"
                  Click="WorkspaceTab_Click"
                  Content="{Binding Title}"
                  Tag="{Binding TabId}"
                  AutomationProperties.Name="{Binding Title}"
                  shellChrome:WindowChrome.IsHitTestVisibleInChrome="True" />
              <Button
                  Grid.Column="1"
                  MinWidth="28"
                  Click="WorkspaceTabClose_Click"
                  Content="{Binding DataContext.WorkspaceTabCloseGlyph, RelativeSource={RelativeSource AncestorType=Window}}"
                  Tag="{Binding TabId}"
                  AutomationProperties.Name="{Binding DataContext.WorkspaceCloseTabLabel, RelativeSource={RelativeSource AncestorType=Window}}"
                  shellChrome:WindowChrome.IsHitTestVisibleInChrome="True" />
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
      <Button
          x:Name="WorkspaceNewTabButton"
          Grid.Column="0"
          MinWidth="36"
          Margin="{DynamicResource Dobby.Spacing.Small}"
          HorizontalAlignment="Right"
          Click="WorkspaceNewTab_Click"
          Content="{Binding WorkspaceNewTabGlyph}"
          AutomationProperties.Name="{Binding WorkspaceNewTabLabel}"
          shellChrome:WindowChrome.IsHitTestVisibleInChrome="True" />
      <StackPanel
          Grid.Column="2"
          Orientation="Horizontal"
          shellChrome:WindowChrome.IsHitTestVisibleInChrome="True">
        <Button
            x:Name="WindowMinimizeButton"
            MinWidth="44"
            Click="WindowMinimize_Click"
            Content="{Binding WindowMinimizeGlyph}"
            AutomationProperties.Name="{Binding WindowMinimizeLabel}" />
        <Button
            x:Name="WindowMaximizeRestoreButton"
            MinWidth="44"
            Click="WindowMaximizeRestore_Click"
            Content="{Binding WindowMaximizeGlyph}"
            AutomationProperties.Name="{Binding WindowMaximizeLabel}" />
        <Button
            x:Name="WindowCloseButton"
            MinWidth="44"
            Click="WindowClose_Click"
            Content="{Binding WindowCloseGlyph}"
            AutomationProperties.Name="{Binding WindowCloseLabel}" />
      </StackPanel>
    </Grid>

    <Grid
        x:Name="TopToolbar"
        Grid.Row="1"
        Margin="{DynamicResource Dobby.Spacing.Medium}">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <ItemsControl
          x:Name="CenteredTopToolbar"
          Grid.Column="1"
          HorizontalAlignment="Center"
          ItemsSource="{Binding TopNavigation}">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal" />
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Button
                Margin="{DynamicResource Dobby.Spacing.Small}"
                Click="Navigation_Click"
                Content="{Binding Label}"
                Tag="{Binding RouteKey}" />
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
      <StackPanel
          Grid.Column="2"
          Orientation="Horizontal">
        <Button
            Margin="{DynamicResource Dobby.Spacing.Small}"
            Click="ThemeToggle_Click"
            Content="{Binding ThemeToggleLabel}" />
        <Button
            x:Name="ProfileMenuButton"
            Margin="{DynamicResource Dobby.Spacing.Small}"
            Click="ProfileMenu_Click"
            Content="{Binding ProfileAccountInfoLabel}" />
      </StackPanel>
    </Grid>
```

- [ ] **Step 6: Add a shell body grid under the top toolbar**

Wrap the existing sidebar, main content, third bar, and profile menu panel inside this new grid by placing the following grid immediately after the new `TopToolbar` block:

```xml
    <Grid
        x:Name="ShellBody"
        Grid.Row="1"
        Margin="{DynamicResource Dobby.Spacing.Medium}">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
      </Grid.RowDefinitions>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="220" />
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="220" />
      </Grid.ColumnDefinitions>
```

Move `ProfileMenuPanel` from the old toolbar into `ShellBody` at `Grid.Row="0" Grid.Column="1" Grid.ColumnSpan="2"`, keeping all existing child controls and names unchanged.

- [ ] **Step 7: Replace the old sidebar opening tag and add brand/sidebar item labels**

Replace the old opening sidebar:

```xml
    <StackPanel
        x:Name="Sidebar"
        Grid.Row="1"
        Grid.Column="0"
        Margin="{DynamicResource Dobby.Spacing.Medium}">
```

with:

```xml
      <StackPanel
          x:Name="ShellSidebar"
          Grid.Row="1"
          Grid.Column="0"
          Margin="{DynamicResource Dobby.Spacing.Medium}">
        <StackPanel
            x:Name="Sidebar"
            Orientation="Vertical">
          <StackPanel Orientation="Horizontal">
            <TextBlock
                x:Name="ShellBrandMark"
                Margin="{DynamicResource Dobby.Spacing.Small}"
                FontSize="{DynamicResource Dobby.FontSize.Title}"
                Foreground="{DynamicResource Dobby.Brush.Text}"
                Text="{Binding BrandMark}" />
            <TextBlock
                Margin="{DynamicResource Dobby.Spacing.Small}"
                FontSize="{DynamicResource Dobby.FontSize.Title}"
                Foreground="{DynamicResource Dobby.Brush.Text}"
                Text="{Binding BrandName}">
              <TextBlock.Style>
                <Style TargetType="TextBlock">
                  <Setter Property="Visibility" Value="Visible" />
                  <Style.Triggers>
                    <DataTrigger Binding="{Binding Layout.Preference.SidebarCollapsed}" Value="True">
                      <Setter Property="Visibility" Value="Collapsed" />
                    </DataTrigger>
                  </Style.Triggers>
                </Style>
              </TextBlock.Style>
            </TextBlock>
          </StackPanel>
```

Replace the `SidebarNavigationList` item template button with this template:

```xml
          <ItemsControl
              x:Name="SidebarNavigationList"
              Margin="{DynamicResource Dobby.Spacing.Medium}"
              ItemsSource="{Binding SidebarNavigation}">
            <ItemsControl.ItemTemplate>
              <DataTemplate>
                <Button
                    Margin="{DynamicResource Dobby.Spacing.Small}"
                    Click="Navigation_Click"
                    Tag="{Binding RouteKey}">
                  <StackPanel Orientation="Horizontal">
                    <TextBlock
                        Margin="{DynamicResource Dobby.Spacing.Small}"
                        Foreground="{DynamicResource Dobby.Brush.Text}"
                        Text="{Binding Order}" />
                    <TextBlock
                        Margin="{DynamicResource Dobby.Spacing.Small}"
                        Foreground="{DynamicResource Dobby.Brush.Text}"
                        Text="{Binding Label}">
                      <TextBlock.Style>
                        <Style TargetType="TextBlock">
                          <Setter Property="Visibility" Value="Visible" />
                          <Style.Triggers>
                            <DataTrigger Binding="{Binding DataContext.Layout.Preference.SidebarCollapsed, RelativeSource={RelativeSource AncestorType=Window}}" Value="True">
                              <Setter Property="Visibility" Value="Collapsed" />
                            </DataTrigger>
                          </Style.Triggers>
                        </Style>
                      </TextBlock.Style>
                    </TextBlock>
                  </StackPanel>
                </Button>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
```

Close the extra inner `StackPanel` before closing `ShellSidebar`.

- [ ] **Step 8: Move the existing content surfaces into `ShellContentHost`**

Change the existing `MainContent` border placement from `Grid.Row="1" Grid.Column="1"` to:

```xml
          x:Name="MainContent"
          Grid.Row="1"
          Grid.Column="1"
```

Inside `MainContent`, rename its direct child stack panel:

```xml
      <StackPanel
          x:Name="ShellContentHost"
          Margin="{DynamicResource Dobby.Spacing.Medium}">
```

Keep these existing named content blocks inside `ShellContentHost` with their current bindings and click handlers:

- `WorkSessionWarningPanel`
- `HomePanel`
- `NotificationPresentationList`
- `FeedbackStatePresentationList`
- `PluginsSurface`
- `PluginsManagerPanel`
- `LicensePanel`
- `PluginPageHost`
- `SettingsSurface`
- `SettingsCategoryList`
- `AboutFieldList`

- [ ] **Step 9: Move the existing third bar and bottom status into the new grid rows**

For `ThirdBar`, use:

```xml
      <StackPanel
          x:Name="ThirdBar"
          Grid.Row="1"
          Grid.Column="2"
          Margin="{DynamicResource Dobby.Spacing.Medium}">
```

For `BottomStatus`, keep it outside `ShellBody` at root `Grid.Row="2"` and remove the old `Grid.ColumnSpan="3"` because the root grid no longer has columns:

```xml
    <StackPanel
        x:Name="BottomStatus"
        Grid.Row="2"
        Margin="{DynamicResource Dobby.Spacing.Medium}">
```

Ensure the `ShellBody` grid is closed before `BottomStatus`.

- [ ] **Step 10: Add tab and window-control handlers**

In `MainWindow.xaml.cs`, add these methods near `Navigation_Click`:

```csharp
    private void WorkspaceNewTab_Click(object sender, RoutedEventArgs e)
    {
        shellViewModel.OpenWorkspaceTab();
        UpdatePluginPageHost();
        PersistShellPreference();
    }

    private void WorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid tabId })
        {
            return;
        }

        var result = shellViewModel.ActivateWorkspaceTab(tabId);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.NotFound.ToString());
            return;
        }

        UpdatePluginPageHost();
        PersistShellPreference();
    }

    private void WorkspaceTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid tabId })
        {
            return;
        }

        var result = shellViewModel.CloseWorkspaceTab(tabId);
        if (!result.IsSuccess)
        {
            ShowAccountStatus(result.ErrorCode?.ToString() ?? ErrorCode.NotFound.ToString());
            return;
        }

        UpdatePluginPageHost();
        PersistShellPreference();
    }

    private void WindowMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void WindowMaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void WindowTitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowMaximizeRestore_Click(sender, e);
            return;
        }

        DragMove();
    }
```

- [ ] **Step 11: Run static scan**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ShellUiStaticScanTests
```

Expected: all `ShellUiStaticScanTests` pass.

- [ ] **Step 12: Run the close confirmation smoke test**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter FullyQualifiedName~Rendered_shell_window_close_button_requests_confirmation
```

Expected: pass.

- [ ] **Step 13: Commit the XAML shell rewrite**

Run:

```powershell
git add src/Dobby.Host/MainWindow.xaml src/Dobby.Host/MainWindow.xaml.cs tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs
git commit -m "feat: render shell workstation tabs"
```

Expected: commit succeeds with the shell rendering work.

---

## Task 7: Add Rendered WPF Smoke Coverage For Tab Creation, Navigation, And Close Rules

**Files:**
- Modify: `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`

- [ ] **Step 1: Add helpers for visible tab buttons**

Add these helpers near `ClickRoute`:

```csharp
    private static IReadOnlyList<Button> WorkspaceTabButtons(Window window)
    {
        return FindVisuals<Button>((DependencyObject)window.Content)
            .Where(button => button.Tag is Guid && button.Content is string text && text.Length > 1)
            .ToArray();
    }

    private static IReadOnlyList<Button> WorkspaceTabCloseButtons(Window window)
    {
        return FindVisuals<Button>((DependencyObject)window.Content)
            .Where(button => button.Tag is Guid && string.Equals(button.Content as string, "x", StringComparison.Ordinal))
            .ToArray();
    }
```

- [ ] **Step 2: Add rendered tab lifecycle smoke test**

Add this test before helper methods:

```csharp
    [Fact]
    public void Rendered_shell_new_tab_opens_home_and_navigation_updates_active_tab()
    {
        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
            var window = new MainWindow
            {
                DataContext = shell,
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                FindNamed<ItemsControl>(window, "WorkspaceTabStrip").Items.Count.ShouldBe(1);
                shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");

                FindNamed<Button>(window, "WorkspaceNewTabButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                FindNamed<ItemsControl>(window, "WorkspaceTabStrip").Items.Count.ShouldBe(2);
                shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.HomeRouteKey);
                shell.ActiveWorkspaceTab.Title.ShouldBe("Ana səhifə");

                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);

                shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
                shell.ActiveWorkspaceTab.Title.ShouldBe("Tənzimləmələr");
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Visible);
            }
            finally
            {
                window.Close();
            }
        });
    }
```

- [ ] **Step 3: Add rendered active tab close smoke test**

Add this test after the new-tab smoke test:

```csharp
    [Fact]
    public void Rendered_shell_closing_active_tab_activates_left_tab()
    {
        RunOnSta(() =>
        {
            var shell = ShellViewModel.CreateDefault(HostStringCatalog.CreateResourceBacked());
            var window = new MainWindow
            {
                DataContext = shell,
                Left = -32000,
                Top = -32000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize
            };

            try
            {
                AddThemeResources(window);
                window.Show();
                Pump(window);

                ClickRoute(window, RouteRegistry.SettingsRouteKey);
                Pump(window);
                var leftTabId = shell.ActiveWorkspaceTab.TabId;

                FindNamed<Button>(window, "WorkspaceNewTabButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);
                ClickRoute(window, RouteRegistry.PluginsRouteKey);
                Pump(window);

                WorkspaceTabCloseButtons(window)
                    .Single(button => (Guid)button.Tag == shell.ActiveWorkspaceTab.TabId)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump(window);

                shell.WorkspaceTabs.Count.ShouldBe(1);
                shell.ActiveWorkspaceTab.TabId.ShouldBe(leftTabId);
                shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
                FindNamed<FrameworkElement>(window, "SettingsSurface").Visibility.ShouldBe(Visibility.Visible);
            }
            finally
            {
                window.Close();
            }
        });
    }
```

- [ ] **Step 4: Run shell smoke tests**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter FullyQualifiedName~ShellKeyboardSmokeTests
```

Expected: all `ShellKeyboardSmokeTests` pass.

- [ ] **Step 5: Commit rendered tab smoke tests**

Run:

```powershell
git add tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs
git commit -m "test: cover rendered workstation tabs"
```

Expected: commit succeeds with smoke-test coverage.

---

## Task 8: Protect Work-Session Warning Behavior During Tab Navigation

**Files:**
- Modify: `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Test: `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`

- [ ] **Step 1: Add unit coverage for cancelled pending navigation preserving tabs**

Add this test to `ShellViewModelWorkspaceTabTests`:

```csharp
    [Fact]
    public void Cancelled_pending_work_session_navigation_leaves_workspace_tabs_unchanged()
    {
        var shell = new ShellViewModel(new HostStringCatalog(), SnapshotContributions());
        shell.ActivateRoute(RouteRegistry.SettingsRouteKey).IsSuccess.ShouldBeTrue();
        var registry = new Dobby.Shell.WorkSessions.WorkSessionRegistry();
        var coordinator = new Dobby.Shell.WorkSessions.WorkSessionCloseCoordinator(registry);
        registry.Open("owner-1", "plugin.sample.work", "Dobby.Plugin.Sample.Work");
        var pending = coordinator.RequestNavigation(RouteRegistry.PluginsRouteKey);
        pending.ShouldNotBeNull();
        var before = shell.WorkspaceTabs.ToArray();

        shell.SetPendingWorkSessionCloseRequest(pending);
        shell.ResolvePendingWorkSessionClose(Dobby.Shell.WorkSessions.WorkSessionCloseChoice.Cancel, coordinator).IsSuccess.ShouldBeTrue();

        shell.WorkspaceTabs.ShouldBe(before);
        shell.ActiveWorkspaceTab.RouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
        shell.ActiveRouteKey.ShouldBe(RouteRegistry.SettingsRouteKey);
    }
```

- [ ] **Step 2: Run the work-session-specific unit test**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~Cancelled_pending_work_session_navigation_leaves_workspace_tabs_unchanged
```

Expected: pass without production changes because Task 2 routes through `ActivateRoute` only after the coordinator applies a non-cancel choice.

- [ ] **Step 3: Keep tab close work-session integration intentionally narrow**

Do not add speculative tab-close-to-work-session ownership logic in this task. The current codebase does not expose a tab-to-work-session id mapping. The acceptance requirement for this phase is that route navigation inside a tab continues to use the existing warning flow, and this task verifies that. Add explicit tab-owned work-session close prompts only when a concrete tab/session ownership model exists.

- [ ] **Step 4: Commit the guardrail test**

Run:

```powershell
git add tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs
git commit -m "test: preserve work-session warning tab state"
```

Expected: commit succeeds with the guardrail test.

---

## Task 9: Full Verification And Local Visual Check

**Files:**
- No code changes expected unless verification exposes a real defect.

- [ ] **Step 1: Run unit tests**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore
```

Expected: all tests pass.

- [ ] **Step 2: Run integration architecture tests**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore
```

Expected: all tests pass.

- [ ] **Step 3: Run WPF smoke tests**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore
```

Expected: all tests pass. If a WPF/STA test flakes, first inspect cleanup, dispatcher drain, pending action reset, shared/static state, test ordering, and explicit completion signals before changing production logic.

- [ ] **Step 4: Run solution build**

Run:

```powershell
dotnet build .\Dobby.sln --no-restore
```

Expected: build succeeds with zero errors.

- [ ] **Step 5: Launch the host for manual visual validation**

Run:

```powershell
dotnet run --no-restore --project .\src\Dobby.Host\Dobby.Host.csproj
```

Expected manual checks:

- Login remains reachable.
- After login, the first visible shell tab is titled Home.
- The `+` button creates a new active Home tab.
- Settings and Plugins click into the active tab and update only that tab title.
- Closing the only tab creates a fresh Home tab.
- Closing the active right tab activates the tab to its left.
- Main window `X` shows the close confirmation.
- Sidebar and top toolbar visually share the page background with no separate block fill.
- Expanded sidebar shows brand mark plus Dobby and icon/index plus label items.
- Collapsed sidebar hides labels and keeps the same item order.

- [ ] **Step 6: Inspect changed files and confirm no unrelated edits**

Run:

```powershell
git status --short
git diff --stat
```

Expected: only files listed in this plan are changed after the last commit; `git diff --stat` is empty if all task commits were created.

- [ ] **Step 7: Final commit if any verification fixes were needed**

If verification exposed a real defect and the fix is not already committed, run:

```powershell
git add src/Dobby.Shell src/Dobby.Host src/Dobby.Localization tests/Dobby.UnitTests tests/Dobby.SmokeTests tests/Dobby.IntegrationTests
git commit -m "fix: stabilize workstation tab shell behavior"
```

Expected: commit succeeds only when there are uncommitted verification fixes.

---

## Self-Review Notes

Spec coverage:

- Startup Home tab: Task 1 and Task 2.
- New `+` Home tab: Task 1, Task 2, Task 7.
- Active content title tracking: Task 1 and Task 2.
- Per-tab route restore: Task 1 and Task 2.
- Close-only and multi-tab close rules: Task 1, Task 2, Task 7.
- Main window close confirmation: Task 5 and Task 6.
- Continuous shell background: Task 6 static scan and manual verification.
- Expanded/collapsed sidebar rendering: Task 6 and manual verification.
- Centered top navigation: Task 6 static scan and manual verification.
- Work-session warning guardrail: Task 8.

Type consistency:

- `ShellWorkspaceTab.TabId` is a `Guid`; XAML `Tag` carries the `Guid`; code-behind pattern matching uses `Tag: Guid tabId`.
- `OpenWorkspaceTab` returns `ShellWorkspaceTab`; `ActivateWorkspaceTab` and `CloseWorkspaceTab` return `Result`.
- `WorkspaceTabs` and `ActiveWorkspaceTab` are exposed on `ShellViewModel`.
- New localization keys are referenced through `HostResourceKeys`.

Verification gates:

- No completion claim until Task 9 passes or any failing command is reported with the exact failure.
