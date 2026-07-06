# Chrome-Scale Tabs Titlebar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Dobby shell titlebar tabs match the approved Chrome reference: larger compact tabs, one trailing `+`, tab-local close buttons, and native Windows caption controls.

**Architecture:** Keep workstation tab lifecycle in `ShellViewModel`; change only the titlebar rendering, sizing math, and tests. `WorkspaceTabSizingConverters` remains the layout math boundary. `WindowChrome` owns native caption buttons while WPF reserves right-side titlebar space for them.

**Tech Stack:** C#/.NET WPF, XAML, xUnit, Shouldly, existing WPF smoke harness.

---

## Source Spec

Implement against `docs/superpowers/specs/2026-07-06-chrome-scale-tabs-titlebar-design.md`.

## File Structure

Modify:

- `src/Dobby.Host/WorkspaceTabSizingConverters.cs`
  - Increase maximum tab width to Chrome scale and keep a stable minimum width for the tab close button.
- `src/Dobby.Host/MainWindow.xaml`
  - Enable native caption buttons, remove custom caption button visuals, keep the single `WorkspaceNewTabButton` after the tab strip.
- `src/Dobby.Host/MainWindow.xaml.cs`
  - Remove unused custom caption button click handlers while keeping `WndProc` close confirmation and double-click titlebar maximize/restore.
- `src/Dobby.UI/Themes/DesignTokens.xaml`
  - Update `Dobby.Shell.TabWidth` and remove custom caption button size tokens no longer used by XAML.
- `tests/Dobby.UnitTests/Host/WorkspaceTabSizingConvertersTests.cs`
  - Add converter tests for Chrome-scale max width, shrink behavior, minimum width, and native-caption reserve fallback.
- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
  - Update static expectations from custom caption buttons to native caption buttons.
- `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`
  - Update rendered smoke checks for native caption configuration, tab sizing, trailing `+`, and system close confirmation.

## Task 1: Converter Tests For Chrome-Scale Tab Math

**Files:**
- Create: `tests/Dobby.UnitTests/Host/WorkspaceTabSizingConvertersTests.cs`
- Modify: `src/Dobby.Host/WorkspaceTabSizingConverters.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Dobby.UnitTests/Host/WorkspaceTabSizingConvertersTests.cs`:

```csharp
using System.Globalization;
using Dobby.Host;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class WorkspaceTabSizingConvertersTests
{
    [Fact]
    public void Tab_strip_max_width_reserves_native_caption_space_without_caption_visual()
    {
        var converter = new WorkspaceTabStripMaxWidthConverter();

        var width = (double)converter.Convert([1024d, 42d], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(820d);
    }

    [Fact]
    public void Tab_width_uses_chrome_scale_maximum_when_space_allows()
    {
        var converter = new WorkspaceTabWidthConverter();

        var width = (double)converter.Convert([1600d, 4], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(292d);
    }

    [Fact]
    public void Tab_width_shrinks_evenly_when_many_tabs_share_the_strip()
    {
        var converter = new WorkspaceTabWidthConverter();

        var width = (double)converter.Convert([820d, 10], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(76d);
    }

    [Fact]
    public void Tab_width_keeps_a_close_button_readable_minimum()
    {
        var converter = new WorkspaceTabWidthConverter();

        var width = (double)converter.Convert([240d, 10], typeof(double), null!, CultureInfo.InvariantCulture);

        width.ShouldBe(48d);
    }
}
```

- [ ] **Step 2: Run the new converter tests and verify RED**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~WorkspaceTabSizingConvertersTests
```

Expected: tests compile and fail because `WorkspaceTabWidthConverter` still returns `240` for the max-width case and less than `48` for the minimum case.

- [ ] **Step 3: Implement the converter constants**

In `src/Dobby.Host/WorkspaceTabSizingConverters.cs`, set:

```csharp
private const double MaxTabWidth = 292;
private const double TabHorizontalMargin = 6;
private const double MinimumReadableTabWidth = 48;
```

Leave `WorkspaceTabStripMaxWidthConverter` behavior intact so the fallback native caption reserve remains `144`.

- [ ] **Step 4: Run converter tests and verify GREEN**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~WorkspaceTabSizingConvertersTests
```

Expected: all `WorkspaceTabSizingConvertersTests` pass.

## Task 2: Static Tests For Native Caption Titlebar Structure

**Files:**
- Modify: `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.UI/Themes/DesignTokens.xaml`

- [ ] **Step 1: Update static tests first**

In `Host_shell_xaml_declares_required_rendered_regions_and_action_surfaces`, remove these required names:

```csharp
"WindowCaptionButtons",
"WindowMinimizeButton",
"WindowMaximizeRestoreButton",
"WindowCloseButton",
```

Replace caption-button assertions with:

```csharp
shell.ShouldContain("UseAeroCaptionButtons=\"True\"");
shell.ShouldNotContain("UseAeroCaptionButtons=\"False\"");
shell.ShouldNotContain("WindowMinimize_Click");
shell.ShouldNotContain("WindowMaximizeRestore_Click");
shell.ShouldNotContain("WindowClose_Click");
shell.ShouldNotContain("x:Name=\"WindowCaptionButtons\"");
shell.ShouldNotContain("x:Name=\"WindowMinimizeButton\"");
shell.ShouldNotContain("x:Name=\"WindowMaximizeRestoreButton\"");
shell.ShouldNotContain("x:Name=\"WindowCloseButton\"");
```

In `Host_shell_uses_visible_windows_style_caption_buttons_and_chrome_sized_tabs`, update expectations:

```csharp
shell.ShouldContain("UseAeroCaptionButtons=\"True\"");
shell.ShouldContain("GlassFrameThickness=\"0\"");
shell.ShouldContain("CaptionHeight=\"{DynamicResource Dobby.Shell.ChromeHeight}\"");
shell.ShouldContain("<ColumnDefinition Width=\"*\" MinWidth=\"0\" />");
shell.ShouldContain("<ColumnDefinition Width=\"{DynamicResource Dobby.Shell.CaptionButtonReserveGridLength}\" />");
shell.ShouldNotContain("WindowCaptionButtons");
shell.ShouldNotContain("WindowChromeButtonStyle");
shell.ShouldNotContain("WindowCloseButtonStyle");
shell.ShouldContain("WorkspaceTabStripMaxWidthConverter");
shell.ShouldContain("WorkspaceTabWidthConverter");
shell.ShouldContain("ClipToBounds=\"True\"");
shell.ShouldContain("MaxWidth=\"{DynamicResource Dobby.Shell.TabWidth}\"");
shell.ShouldContain("Height=\"{DynamicResource Dobby.Shell.TabHeight}\"");
designTokens.ShouldContain("<sys:Double x:Key=\"Dobby.Shell.TabWidth\">292</sys:Double>");
designTokens.ShouldContain("<sys:Double x:Key=\"Dobby.Shell.TabHeight\">42</sys:Double>");
designTokens.ShouldContain("<GridLength x:Key=\"Dobby.Shell.CaptionButtonReserveGridLength\">144</GridLength>");
designTokens.ShouldNotContain("Dobby.Shell.CaptionButtonWidth");
designTokens.ShouldNotContain("Dobby.Shell.CaptionButtonHeight");
```

- [ ] **Step 2: Run static tests and verify RED**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ShellUiStaticScanTests
```

Expected: static scan fails because XAML still has custom caption buttons, `UseAeroCaptionButtons="False"`, and `TabWidth` is still `240`.

- [ ] **Step 3: Implement the XAML/token structure**

In `src/Dobby.Host/MainWindow.xaml`:

- Remove `WindowChromeButtonStyle`.
- Remove `WindowCloseButtonStyle`.
- Set `UseAeroCaptionButtons="True"`.
- Change `WindowTitleBar` columns to:

```xml
<Grid.ColumnDefinitions>
  <ColumnDefinition Width="*" MinWidth="0" />
  <ColumnDefinition Width="Auto" />
  <ColumnDefinition Width="{DynamicResource Dobby.Shell.CaptionButtonReserveGridLength}" />
</Grid.ColumnDefinitions>
```

- Remove the `WindowCaptionButtons` `StackPanel`.
- Remove the third `WorkspaceTabStripMaxWidthConverter` binding to `WindowCaptionButtons.ActualWidth`.
- Keep `WorkspaceNewTabButton` in `Grid.Column="1"` so it appears once after the tab strip.

In `src/Dobby.UI/Themes/DesignTokens.xaml`:

```xml
<sys:Double x:Key="Dobby.Shell.TabWidth">292</sys:Double>
```

Remove:

```xml
<sys:Double x:Key="Dobby.Shell.CaptionButtonWidth">46</sys:Double>
<sys:Double x:Key="Dobby.Shell.CaptionButtonHeight">32</sys:Double>
```

- [ ] **Step 4: Run static tests and verify GREEN**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ShellUiStaticScanTests
```

Expected: all `ShellUiStaticScanTests` pass.

## Task 3: Rendered Smoke Tests For Native Caption And Trailing Plus

**Files:**
- Modify: `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`

- [ ] **Step 1: Update rendered tests before production cleanup**

Change `Rendered_shell_window_close_button_requests_confirmation_and_respects_cancel` to request system close instead of clicking a custom button:

```csharp
SystemCommands.CloseWindow(window);
Pump(window);

confirmation.CallCount.ShouldBe(1);
window.IsVisible.ShouldBeTrue();
```

Change `Rendered_shell_uses_chrome_sized_workspace_tabs_and_visible_caption_buttons` to:

```csharp
public void Rendered_shell_uses_chrome_sized_workspace_tabs_and_native_caption_buttons()
{
    RunOnSta(() =>
    {
        var window = new MainWindow
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

            var chrome = WindowChrome.GetWindowChrome(window);
            chrome.ShouldNotBeNull();
            chrome.UseAeroCaptionButtons.ShouldBeTrue();
            chrome.CaptionHeight.ShouldBe(48);
            chrome.GlassFrameThickness.ShouldBe(new Thickness(0));

            var tabFrame = FindVisuals<Border>((DependencyObject)window.Content)
                .First(border => border.Name.Equals("WorkspaceTabFrame", StringComparison.Ordinal));
            tabFrame.ActualWidth.ShouldBe(292);
            tabFrame.ActualHeight.ShouldBe(42);

            FindNamedOrDefault<StackPanel>(window, "WindowCaptionButtons").ShouldBeNull();
            FindNamedOrDefault<Button>(window, "WindowMinimizeButton").ShouldBeNull();
            FindNamedOrDefault<Button>(window, "WindowMaximizeRestoreButton").ShouldBeNull();
            FindNamedOrDefault<Button>(window, "WindowCloseButton").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    });
}
```

In tab shrink tests, replace `captionButtons` bounds checks with `WorkspaceNewTabButton` reserve checks:

```csharp
var titleBar = FindNamed<Grid>(window, "WindowTitleBar");
var newTabButton = FindNamed<Button>(window, "WorkspaceNewTabButton");
var newTabBounds = newTabButton
    .TransformToAncestor(titleBar)
    .TransformBounds(new Rect(0, 0, newTabButton.ActualWidth, newTabButton.ActualHeight));

newTabButton.IsVisible.ShouldBeTrue();
newTabBounds.Right.ShouldBeLessThanOrEqualTo(titleBar.ActualWidth - 144 + 0.5);
```

Add helper:

```csharp
private static T? FindNamedOrDefault<T>(Window window, string name)
    where T : FrameworkElement
{
    return FindVisuals<T>((DependencyObject)window.Content)
        .SingleOrDefault(element => element.Name.Equals(name, StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run smoke tests and verify RED if code-behind/XAML is not fully updated**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter FullyQualifiedName~ShellKeyboardSmokeTests
```

Expected: failures identify any remaining custom-caption expectations or system-close confirmation gap.

- [ ] **Step 3: Remove unused custom caption handlers**

In `src/Dobby.Host/MainWindow.xaml.cs`, remove these private methods:

```csharp
private void WindowMinimize_Click(object sender, RoutedEventArgs e) { ... }
private void WindowMaximizeRestore_Click(object sender, RoutedEventArgs e) { ... }
private void WindowClose_Click(object sender, RoutedEventArgs e) { ... }
```

Keep `WindowTitleBar_MouseLeftButtonDown`, `ToggleWindowMaximizeRestore`, `SetShellMaximized`, `SetShellNormal`, and `WndProc`.

- [ ] **Step 4: Run smoke tests and verify GREEN**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter FullyQualifiedName~ShellKeyboardSmokeTests
```

Expected: all `ShellKeyboardSmokeTests` pass.

## Task 4: Focused And Full Verification

**Files:**
- No expected edits unless verification reveals a concrete defect.

- [ ] **Step 1: Run focused unit tests**

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~WorkspaceTabSizingConvertersTests
```

- [ ] **Step 2: Run shell unit tests**

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~ShellViewModelWorkspaceTabTests
```

- [ ] **Step 3: Run static scan tests**

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ShellUiStaticScanTests
```

- [ ] **Step 4: Run WPF shell smoke tests**

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter FullyQualifiedName~ShellKeyboardSmokeTests
```

- [ ] **Step 5: Run solution build**

```powershell
dotnet build .\Dobby.sln --no-restore
```

- [ ] **Step 6: Refresh portable build**

```powershell
.\tools\publish-portable\publish-portable.ps1 -NoRestore
```

Expected: command exits 0 and prints the portable executable path. Report that path in the final response.

- [ ] **Step 7: Inspect diff**

```powershell
git status --short
git diff --stat
```

Expected: changed files are limited to the titlebar plan, XAML, converter, tokens, and tests for this task plus pre-existing user changes.

## Self-Review Notes

Spec coverage:

- Chrome-scale length: Task 1 converter max and Task 3 rendered tab width.
- Shrink behavior: Task 1 shrink test and Task 3 rendered shrink checks.
- Single trailing `+`: Task 2 keeps one `WorkspaceNewTabButton`; Task 3 bounds check verifies it stays before native caption reserve.
- Tab-local close buttons: existing rendered tab-close smoke remains and Task 2 keeps `WorkspaceTabClose_Click`.
- Native caption buttons: Task 2 static scan and Task 3 `WindowChrome.UseAeroCaptionButtons` smoke.
- Window close confirmation: Task 3 system close smoke keeps `WndProc` confirmation.
- Portable build refresh: Task 4.

Placeholder scan: no placeholder markers or undefined steps are left in this plan.

Type consistency: converter tests use existing converter types; XAML names use existing `WorkspaceTabStrip` and `WorkspaceNewTabButton`; smoke tests use existing `SystemCommands`, `WindowChrome`, and visual-tree helpers.
