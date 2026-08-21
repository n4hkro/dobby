# Shell, Accessibility and Responsiveness Execution Plan

**Dependencies:** plugin/licensing command contracts must be stable before final UI wiring  
**Completion blockers addressed:** F-019, F-020, F-024; legacy rows 19–37 and 48–56

## UI-01 — Extract shell orchestration without changing behavior

**Priority:** P1-Medium  
**Depends on:** ID-04, PL-05, LC-04

### Files to inspect/modify/create

- `src/Dobby.Host/MainWindow.xaml`
- `src/Dobby.Host/MainWindow.xaml.cs`
- `src/Dobby.Shell/ShellViewModel.cs`
- `src/Dobby.Host/PostLoginShellPresenter.cs`
- host coordinators under `src/Dobby.Host/Plugins`, `Settings`, `Printing`
- shell/view-model/WPF integration and smoke tests

### Steps

1. Characterize current behavior with command-level and WPF tests for navigation,
   profile/session, settings, plugin manager/license, work sessions, progress and
   close flows. Preserve these before moving code.
2. Split `MainWindow.xaml.cs` into thin view event/adaptor code plus injected
   command/presenter objects. Split `ShellViewModel` into bounded state owners:
   navigation/layout, settings, plugin manager/license, notifications/progress
   and workspace sessions.
3. Use one observable state/command path per action; remove duplicate UI and
   fallback paths that bypass authorization/lifecycle gates.
4. Make subscriptions and dispatcher callbacks lifetime-owned by the
   authenticated scope. Dispose/unsubscribe during logout/window close before a
   new scope is created.
5. Preserve XAML automation IDs and snapshot contracts unless a deliberate
   accessible replacement is added in the same change.
6. Refactor in vertical slices with focused tests after each slice. Do not combine
   broad visual redesign with orchestration extraction.

### Acceptance

- Main window code-behind contains view adaptation, not archive/database/crypto/
  authorization orchestration.
- A second login/window instance has no state/events from the first.
- Existing shell behavior tests remain green without source-text-only substitutes.

## UI-02 — Make heavy actions truly asynchronous and cancellable

**Priority:** P0-High for plugin install; P1 for remaining actions  
**Depends on:** PL-02, PL-04, PL-05, LC-03

### Files to inspect/modify

- `src/Dobby.Host/MainWindow.xaml.cs`
- `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`
- `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`
- `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`
- `src/Dobby.Application/BackgroundOperationReporter.cs`
- picker/coordinator/view-model and WPF tests

### Steps

1. Add a dispatcher heartbeat/probe test that starts a maximum valid package
   install and proves input/dispatcher work continues while verification,
   extraction, hashing and persistence run. Use explicit started/progress/
   completion signals, not sleeps.
2. Give install/import/lifecycle/page actions asynchronous application APIs with
   linked cancellation and real progress based on bounded bytes/steps. Do not
   perform synchronous `InstallPackage` from an `async void` handler.
3. Perform picker/window interaction on the dispatcher, perform file/crypto/DB
   work through the application operation, and marshal only immutable state
   updates back to WPF.
4. Disable only conflicting actions while busy. Expose cancel where safe; define
   non-cancellable atomic-commit section and show a finishing state rather than
   abandoning it.
5. On cancellation/failure, await cleanup and restore controls/focus exactly once.
   Prevent double-click/reentrancy with command state, not timing throttles.
6. Verify close/logout cancels and awaits operations under the authenticated
   scope; no completion callback targets a disposed window/view model.

### Acceptance

- Dispatcher probe stays responsive during maximum valid package install and
  license import.
- Progress is monotonic/meaningful and cancellation leaves no partial promoted
  package or half-committed state.
- Reentrancy and window-close tests are deterministic.

## UI-03 — Complete localization, accessibility and support-safe UX

**Priority:** P1-Medium  
**Depends on:** ID-06, PL-06, LC-04

### Files to inspect/modify

- `src/Dobby.Localization/HostResourceKeys.cs`
- all three `src/Dobby.Localization/Resources/Host.*.resx`
- `src/Dobby.Localization/HostStringCatalog.cs`
- `src/Dobby.Host/Settings/AboutInfoProvider.cs`
- `src/Dobby.Host/Settings/FixedSupportDeviceCodeProvider.cs`
- `src/Dobby.Host/MainWindow.xaml`
- Login/Lock/Recovery XAML and code-behind
- localization/settings/shell keyboard tests

### Steps

1. Move all remaining visible literals, including login runtime failure and safe
   plugin/license reason text, into the resource catalogs. Enforce three-culture
   key parity and non-empty values.
2. Replace `support@example.invalid` with validated owner-provided build/runtime
   configuration. Keep customer release blocked if the value is absent or still
   a placeholder; tests may use an explicit TEST-ONLY address.
3. Render the ID-06 support-safe device code with copy affordance and explanatory
   text; never render raw machine identifiers or paths.
4. Add accessible names/help text, logical tab order, keyboard activation, focus
   restoration, screen-reader announcements for validation/progress and high-
   contrast/theme checks to login, recovery, settings, plugin manager/license,
   notifications and minimized work sessions.
5. Replace summary-string-only plugin detail with structured labels/values and
   stable safe rejection codes. Avoid exposing raw exception/artifact fields.
6. Test long localized strings, 200% scaling, high contrast, minimum supported
   screen and keyboard-only flows. Use screenshots as supplementary evidence,
   not the only assertion.

### Acceptance

- No customer-visible placeholder or hard-coded English remains.
- All interactive controls have accessible identity, focus and keyboard behavior.
- Long strings/scaling do not overlap or hide the primary recovery/cancel action.

## UI-04 — Behavior-first end-to-end shell scenarios

**Priority:** P1  
**Depends on:** UI-01 through UI-03, PL-07, LC-04

### Files to inspect/modify/create

- WPF integration collections under `tests/Dobby.IntegrationTests/Shell`
- `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`
- `tests/Dobby.SmokeTests/ShellSmokeTests.cs`
- unit tests that currently assert source strings for product behavior
- new deterministic WPF test harness helpers

### Steps

1. Build a per-test WPF/STA/Dispatcher harness with explicit window ownership,
   completion signals, `Window.Close`, dispatcher queue drain, subscription/
   pending-action reset and static/shared-state cleanup.
2. Add end-to-end scenarios: login → shell → settings/password; plugin install →
   detail → enable → page/action/progress → disable/uninstall; license import and
   every lifecycle state; lock/unlock/logout; diagnostics in denied state; work-
   session minimize/restore/close.
3. Drive real commands/coordinators with deterministic fakes for picker/clock/
   filesystem where necessary. Assert rendered state plus backend outcome.
4. Replace behavior claims based only on XAML/C# token searches with these tests.
   Retain static scans for resource parity, forbidden dependencies, placeholders
   and automation IDs.
5. Run scenarios repeatedly and in varied order. If a failure is flaky, first
   check window/dispatcher cleanup, pending actions, static state, subscriptions
   and test ordering. Do not add production timing workarounds unless the defect
   reproduces in production behavior.

### Acceptance

- The complete shell scenarios pass repeatedly and in isolation without sleeps
  as synchronization.
- Every opened window and authenticated runtime is deterministically disposed.
- Static scans no longer serve as sole evidence for user-visible behavior.

## Workstream Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Shell|FullyQualifiedName~Host|FullyQualifiedName~Localization"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Shell|FullyQualifiedName~Host"
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj -c Release --no-restore
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 -NoRestore
```

Record accessibility/manual inspection items that cannot be automated, with
Windows build, DPI, theme, culture and exact candidate artifact hash.
