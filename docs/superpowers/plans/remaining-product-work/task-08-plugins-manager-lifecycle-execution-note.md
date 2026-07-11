# Task 08 Execution Note: Plugins Manager And Lifecycle

Recovered at Task 8 resume from the current dirty working tree. The Task 8 brief and report placeholder had already been created, and provisional test/UI edits had begun before this controller resume; this note records that fact instead of presenting the post-dispatch tree as a pristine pre-implementation baseline.

## Authority Preflight

No unresolved authority conflict was found. The master plan's Task 8 behavior matches `docs/specs/plugins-manager.md` and the Sprint 10 ownership boundary: local package install, list/detail status, enable/disable, uninstall confirmation/data choice, lifecycle audit events, and safe non-disclosing rejection feedback are P1 scope. Marketplace, update, repair, downgrade, and eager hot activation remain out of scope.

Task 7's accepted runtime-mode boundary remains binding: `ReadOnlyExpired` package/lifecycle denial is a policy/license-mode outcome, not a package-validation failure.

## Current Git Status

Relevant dirty files when this resume was recovered:

```text
 M src/Dobby.Host/MainWindow.xaml
 M src/Dobby.Host/MainWindow.xaml.cs
 M src/Dobby.Host/Plugins/HostInstalledPluginMetadataCache.cs
 M src/Dobby.Host/Plugins/HostInstalledPluginStateStore.cs
 M src/Dobby.Host/Plugins/HostPluginLifecycleCoordinator.cs
 M src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs
 M src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs
 M src/Dobby.Host/PostLoginShellPresenter.cs
 M src/Dobby.Shell/ShellViewModel.cs
 M tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs
 M tests/Dobby.IntegrationTests/Plugins/PluginIntegrationSuiteTests.cs
 M tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs
 M tests/Dobby.UnitTests/Host/HostPluginLifecycleCoordinatorTests.cs
 M tests/Dobby.UnitTests/Host/HostPluginPackageInstallCoordinatorTests.cs
 M tests/Dobby.UnitTests/Host/PluginRuntimeAuditCoordinatorTests.cs
 M tests/Dobby.UnitTests/Shell/ShellViewModelPluginsManagerTests.cs
```

Ownership notes:

- `MainWindow.xaml`, `MainWindow.xaml.cs`, `ShellViewModel`, and `PostLoginShellPresenter` contain accepted Task 4-7 shared UI/runtime work. Task 8 is the next sequential shared-UI lane owner and must preserve those behaviors.
- Plugin install/state/cache/runtime coordinators contain accepted Task 2, Task 6, and Task 7 transaction, fault, diagnostics, and runtime-mode enforcement. Task 8 may add user-driven lifecycle orchestration without weakening those boundaries.
- The working tree is intentionally uncommitted under the master plan's no-commit rule. No unrelated dirty file may be reverted or normalized.

## Current Production Seams

- `ShellViewModel` already has a Plugins Manager list model, permission flags, selection, busy/feedback state, and a basic detail summary.
- `PostLoginShellPresenter` already builds the manager surface from durable metadata/state snapshots.
- `HostPluginPackageInstallCoordinator` owns permission, trust/validation, staging, durable install, audit, diagnostics, and Task 7 runtime-mode gating.
- `HostPluginLifecycleCoordinator` owns enable, disable, uninstall, pending-uninstall, cache/state mutation, data choice, and lifecycle audit behavior.
- `MainWindow` is the host-owned file-picker, confirmation, active-work-session coordination, and user-feedback boundary.
- Provisional Task 8 edits visible at resume already add structured detail rows and manager click-handler seams; their correctness and completeness still require implementer reporting, focused tests, and independent review.

## Intended Touches

### Will Touch

- Plugins Manager view model/list/detail/action state in `ShellViewModel` and its focused tests.
- Sequential shared UI wiring in `MainWindow.xaml`, `MainWindow.xaml.cs`, and `PostLoginShellPresenter`.
- Package install and lifecycle coordinators, installed state/cache ports/adapters, and audit mapping only where failing Task 8 tests prove a gap.
- Localization resource keys/catalogs and all existing host resource cultures for user-visible Task 8 text.
- Focused plugin integration, shell snapshot/static, and action-wiring tests.
- The Task 8 report and review artifacts under `.superpowers/sdd`.

### Will Not Touch

- Task 9 sample package behavior, Task 10 Admin tooling, or Task 11 installer/release work.
- Applied migration SQL, package wire schema, trust pins/material, private keys, or signing fixtures.
- Marketplace/update/repair/downgrade or eager hot-activation behavior.
- Task 7 customer-context policy, license-import recovery exception, diagnostics ZIP gate, or physical-print confirmation.
- Unrelated authentication, settings, theme, localization content, or line-ending normalization.

## RED Tests

Minimum deterministic RED coverage for this task:

- `ShellViewModelPluginsManagerTests.Plugins_manager_surface_shows_list_detail_and_permission_gated_actions`
- `ShellViewModelPluginsManagerTests.Plugins_manager_surface_handles_empty_busy_and_pending_uninstall_states`
- `ShellViewModelPluginsManagerTests.Plugins_manager_structured_detail_includes_safe_rejection_and_restart_state`
- `HostPluginPackageInstallCoordinatorTests.Install_package_requires_import_permission_before_audit_trust_or_staging`
- `HostPluginLifecycleCoordinatorTests.Disable_plugin_updates_state_cache_and_audits_existing_taxonomy`
- `HostPluginLifecycleCoordinatorTests.Uninstall_activated_plugin_schedules_pending_uninstall_without_runtime_deletion`
- Task 8 production-window action tests for picker cancellation, permission denial, trusted install, `ReadOnlyExpired`, enable/disable, uninstall cancellation, keep/delete data, and active work-session close/cancel.

Expected initial failures from the audited pre-Task-8 baseline:

- The manager detail was only a summary string and omitted structured trust, license, fault, rejection, and restart rows.
- Production buttons did not invoke the local package picker or install/lifecycle coordinators.
- Lifecycle action results were not refreshed into the durable manager surface and safely localized for the user.
- Uninstall confirmation/data choice and active work-session close/cancel were not reachable from the manager UI.

Because provisional Task 8 changes predate this recovery note, the implementer report must identify the actual RED commands/results already observed rather than reconstructing them after GREEN.

## Production Seams

- View state: `ShellViewModel` plus `ShellPluginManagerItem` and structured detail rows.
- Render/action boundary: `MainWindow.xaml` and `MainWindow.xaml.cs`.
- Snapshot refresh/composition: `PostLoginShellPresenter` and the post-login runtime coordinators exposed to the window.
- Install: `WindowsPluginPackageFilePicker` and `HostPluginPackageInstallCoordinator`.
- Lifecycle: `HostPluginLifecycleCoordinator`, `WorkSessionCloseCoordinator`, uninstall confirmation/data-choice adapters, installed state, metadata cache, runtime file cleanup, and plugin storage retention.
- User-safe feedback: `HostResourceKeys`, `HostStringCatalog`, and all `Host.*.resx` cultures; raw validator/exception details remain diagnostics-only.

## Focused Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ShellViewModelPluginsManager|FullyQualifiedName~HostPluginLifecycleCoordinator|FullyQualifiedName~HostPluginPackageInstallCoordinator|FullyQualifiedName~PluginRuntimeAuditCoordinator"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PluginIntegrationSuiteTests|FullyQualifiedName~PluginActivationRuntimeIntegrationTests|FullyQualifiedName~ShellSnapshotTests"
git diff --check
```
