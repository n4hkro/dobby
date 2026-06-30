# Approved CRM Shell Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement only the approved CRM-reference shell design from the frozen spec.

**Architecture:** `ShellViewModel` keeps workstation behavior and adds small presentation models for polished navigation, pipeline cards, and metric panels. `MainWindow.xaml` owns the WPF visual composition and vector icon templates. `Dobby.UI` theme dictionaries own colors, spacing, focus, and surface tokens so the shell does not regress into raw WPF controls or one-off styling.

**Tech Stack:** .NET WPF, XAML resource dictionaries, C#, xUnit, Shouldly, existing Dobby localization and shell tests.

---

## Source Spec

Implement against `docs/superpowers/specs/2026-06-30-crm-reference-shell-polish-design.md`.

Approved constraints:

- Workstations stay Chrome-like tabs in the top titlebar row.
- Logo and navigation controls use vector icons, not placeholder text glyphs.
- The shell uses one soft light CRM-style background.
- The Home route renders a dashboard/pipeline surface, not an empty white panel.
- Secondary controls such as `Pipelines`, `Davam edir`, `Tamamlandı`, and `Vaxtı keçib` sit directly on the app background with no island background, no extra shadow, and no extra container color.
- Existing workstation behavior and work-session warning behavior remain unchanged.

## File Structure

Modify:

- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
  - Add static guardrails for vector icon usage, polished shell regions, and the no-island secondary subnav rule.
- `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
  - Extend rendered WPF checks to require dashboard header, pipeline board, metric panels, and content cards.
- `tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs`
  - Require new dashboard-visible labels to be registered in fallback and resource-backed localization.
- `src/Dobby.Localization/HostResourceKeys.cs`
  - Add dashboard and pipeline presentation keys.
- `src/Dobby.Localization/HostStringCatalog.cs`
  - Add az-AZ fallback strings for dashboard and pipeline presentation.
- `src/Dobby.Localization/Resources/Host.az-AZ.resx`
  - Add resource-backed az-AZ strings for dashboard and pipeline presentation.
- `src/Dobby.Shell/ShellViewModel.cs`
  - Add navigation icon/active state metadata and static shell presentation lists for dashboard columns, cards, filters, and metrics.
- `src/Dobby.UI/Themes/DesignTokens.xaml`
  - Add reusable radii, spacing, font, and icon dimensions needed by the polished shell.
- `src/Dobby.UI/Themes/LightTheme.xaml`
  - Add light CRM shell brushes.
- `src/Dobby.UI/Themes/DarkTheme.xaml`
  - Add corresponding dark brushes so existing dark-theme tests still render.
- `src/Dobby.UI/ThemeTokens.cs`
  - Expose any new brush token constants needed by static coverage.
- `src/Dobby.Host/MainWindow.xaml`
  - Replace raw shell layout/control visuals with the approved CRM-style composition.

No changes:

- `WorkSessionRegistry`
- `WorkSessionCloseCoordinator`
- workstation tab lifecycle rules
- plugin activation/runtime coordination

---

## Task 1: Add Failing Visual Guardrail Tests

**Files:**
- Modify: `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`

- [ ] **Step 1: Add static scan guardrails**

Add tests requiring `MainWindow.xaml` to contain these named regions:

```csharp
var polishedNames = new[]
{
    "ShellChrome",
    "ShellHeader",
    "HomeDashboardHeader",
    "HomePipelineFilters",
    "HomePipelineBoard",
    "HomeMetricPanels",
    "PipelineSubnav"
};
```

Add assertions that `MainWindow.xaml` does not bind window/workspace controls to placeholder glyph properties:

```csharp
shell.ShouldNotContain("WorkspaceNewTabGlyph");
shell.ShouldNotContain("WorkspaceTabCloseGlyph");
shell.ShouldNotContain("WindowMinimizeGlyph");
shell.ShouldNotContain("WindowMaximizeGlyph");
shell.ShouldNotContain("WindowCloseGlyph");
shell.ShouldContain("<Path");
```

Add assertions that `PipelineSubnav` and `ThirdBar` have no `Background=`, `Effect=`, or `DropShadowEffect` in their named element scopes.

- [ ] **Step 2: Extend rendered snapshot coverage**

In `ShellSnapshotTests`, require rendered surface names to include:

```csharp
"HomeDashboardHeader",
"HomePipelineBoard",
"HomeMetricPanels",
"PipelineSubnav"
```

Require rendered text to contain:

```csharp
"Cases Overview",
"Pipelines",
"Davam edir",
"Tamamlandı",
"Vaxtı keçib"
```

- [ ] **Step 3: Run focused red tests**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShellUiStaticScanTests|FullyQualifiedName~ShellSnapshotTests"
```

Expected: fail because the current shell still uses placeholder glyph bindings, lacks polished dashboard regions, and renders the third bar as a raw side panel.

---

## Task 2: Add Failing Localization Coverage

**Files:**
- Modify: `tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs`

- [ ] **Step 1: Add dashboard localization coverage**

Add a test that checks these expected labels through fallback and resource-backed catalogs:

```csharp
var expected = new Dictionary<string, string>(StringComparer.Ordinal)
{
    [HostResourceKeys.ShellDashboardCasesOverview] = "Cases Overview",
    [HostResourceKeys.ShellDashboardPipelines] = "Pipelines",
    [HostResourceKeys.ShellDashboardInProgress] = "Davam edir",
    [HostResourceKeys.ShellDashboardCompleted] = "Tamamlandı",
    [HostResourceKeys.ShellDashboardExpired] = "Vaxtı keçib",
    [HostResourceKeys.ShellDashboardNewCases] = "Yeni işlər",
    [HostResourceKeys.ShellDashboardPendingInfo] = "Məlumat gözləyir",
    [HostResourceKeys.ShellDashboardClosed] = "Bağlı",
    [HostResourceKeys.ShellDashboardTopDrivers] = "Top iş səbəbləri",
    [HostResourceKeys.ShellDashboardCaseMetrics] = "İş metrikaları"
};
```

- [ ] **Step 2: Run focused red test**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter FullyQualifiedName~Dashboard_shell_keys
```

Expected: fail because the new resource keys do not exist yet.

---

## Task 3: Add Shell Presentation Data

**Files:**
- Modify: `src/Dobby.Localization/HostResourceKeys.cs`
- Modify: `src/Dobby.Localization/HostStringCatalog.cs`
- Modify: `src/Dobby.Localization/Resources/Host.az-AZ.resx`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`

- [ ] **Step 1: Add dashboard resource keys**

Add the keys from Task 2 to `HostResourceKeys` and `Sprint7VisibleKeys`, then add matching fallback and resx values.

- [ ] **Step 2: Extend navigation presentation**

Change:

```csharp
public sealed record ShellNavigationItem(string RouteKey, string Label, int Order);
```

to include icon and active-state metadata:

```csharp
public sealed record ShellNavigationItem(
    string RouteKey,
    string Label,
    int Order,
    string IconPathData,
    bool IsActive);
```

Build items with a route-based icon path and active state derived from the current navigation path.

- [ ] **Step 3: Add dashboard records and properties**

Add records for `ShellDashboardFilterItem`, `ShellDashboardColumn`, `ShellDashboardCard`, and `ShellDashboardMetric`. Expose localized properties for heading, filters, pipeline columns/cards, and metric panels from `ShellViewModel`.

- [ ] **Step 4: Run focused green tests**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ShellLocalizationCoverageTests|FullyQualifiedName~ShellViewModelNavigationTests|FullyQualifiedName~ShellViewModelWorkspaceTabTests"
```

Expected: all selected tests pass.

---

## Task 4: Add Theme Tokens For The Approved Shell

**Files:**
- Modify: `src/Dobby.UI/Themes/DesignTokens.xaml`
- Modify: `src/Dobby.UI/Themes/LightTheme.xaml`
- Modify: `src/Dobby.UI/Themes/DarkTheme.xaml`
- Modify: `src/Dobby.UI/ThemeTokens.cs`

- [ ] **Step 1: Add reusable shell tokens**

Add tokens for medium/large radius, compact font size, body font size, icon size, shell border, muted text, active control, inactive control, card surface, panel surface, and chart accent brushes.

- [ ] **Step 2: Preserve contrast**

Keep `Dobby.Brush.Background`, `Dobby.Brush.Surface`, and `Dobby.Brush.Focus` contrast at the thresholds enforced by `ShellUiStaticScanTests`.

- [ ] **Step 3: Run theme static tests**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Theme_resources_include_accessibility_motion_and_focus_tokens|FullyQualifiedName~Theme_contrast_tokens_meet_minimum_accessibility_ratio"
```

Expected: pass.

---

## Task 5: Rework MainWindow XAML To The Approved Design

**Files:**
- Modify: `src/Dobby.Host/MainWindow.xaml`

- [ ] **Step 1: Replace text glyph controls with vector icon buttons**

Render workspace tab icon, close icon, new-tab icon, and window controls with WPF `Path` geometry. Keep localized `AutomationProperties.Name` bindings.

- [ ] **Step 2: Render Chrome-like workstation tabs**

Keep `WorkspaceTabStrip`, `WorkspaceNewTabButton`, and existing click handlers. Style active tabs as rounded top browser tabs and inactive tabs as softer tabs behind them.

- [ ] **Step 3: Build the CRM shell header and icon rail**

Render `ShellHeader`, centered top navigation pills, right-side icon actions, and `ShellSidebar` as a slim icon rail with vector icons. Preserve collapsed-label behavior through the existing sidebar preference.

- [ ] **Step 4: Move secondary navigation into the central third zone**

Render `ThirdBarNavigationList` inside `PipelineSubnav`, directly on the shell background. Do not add a `Border`, background brush, `Effect`, or shadow to the subnav itself.

- [ ] **Step 5: Replace HomePanel content with dashboard layout**

Render `HomeDashboardHeader`, `HomePipelineFilters`, `HomePipelineBoard`, card columns, and `HomeMetricPanels` from `ShellViewModel` presentation lists. Keep `WorkSessionWarningPanel`, `PluginsSurface`, `SettingsSurface`, and `PluginPageHost` names and bindings intact.

- [ ] **Step 6: Run focused visual tests**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShellUiStaticScanTests|FullyQualifiedName~ShellSnapshotTests"
```

Expected: pass.

---

## Task 6: Full Verification

**Files:**
- No planned edits unless verification exposes a real defect.

- [ ] **Step 1: Run unit tests**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore
```

Expected: pass.

- [ ] **Step 2: Run integration tests**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore
```

Expected: pass.

- [ ] **Step 3: Run smoke tests**

Run:

```powershell
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore
```

Expected: pass. For any WPF/STA failure, inspect cleanup, dispatcher drain, pending state, shared/static state, and ordering before changing production code.

- [ ] **Step 4: Run solution build**

Run:

```powershell
dotnet build .\Dobby.sln --no-restore
```

Expected: pass.

- [ ] **Step 5: Launch manual visual check**

Run:

```powershell
dotnet run --no-restore --project .\src\Dobby.Host\Dobby.Host.csproj
```

Expected manual checks:

- Chrome-like workstation tabs render at the top.
- Icons are vector shapes, not placeholder text.
- The shell background is continuous and soft/light.
- `Pipelines`, `Davam edir`, `Tamamlandı`, and `Vaxtı keçib` sit directly on the app background with no island background or shadow.
- Home dashboard has pipeline cards and metric panels.
- Existing workstation tab behavior still works.

---

## Self-Review Notes

- The plan implements the approved spec only; it does not add another design direction.
- Static tests cover icon placeholder regression and no-island subnav regression.
- Rendered WPF tests cover the main dashboard regions.
- Localization tests cover visible new labels.
- Work-session and workstation behavior stays under existing tests.
