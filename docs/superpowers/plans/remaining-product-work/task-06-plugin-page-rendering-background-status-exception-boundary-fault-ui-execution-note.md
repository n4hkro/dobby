# Task 06 Execution Note: Plugin Page Rendering, Background Status, Exception Boundary, And Fault UI

Created: 2026-07-09

Scope authority:
- Requirements source: `.superpowers/sdd/task-6-brief.md`
- Row source: `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md` rows 33-37
- This note is execution context only; it does not replace the task brief, status file, or sprint/spec authority.

Current row status snapshot:
- Row 33: `QİSMƏN` - `MainWindow` already uses `PluginPageRenderRegistry.CreateDefault()` and `PluginPageHost`; approved model coverage, exception boundary, and fault UI/persisted state remain.
- Row 34: `QİSMƏN` - route clicks go through `ShellRouteHost`/`PluginPageRouteCoordinator`; page activation/load exception boundary, rejected/faulted user state, and sample proof remain.
- Row 35: `QİSMƏN` - plugin-owned background operation snapshots have shell reporter path; production plugin callback/status integration proof remains.
- Row 36: `QİSMƏN` - `PluginExceptionBoundary` exists; systematic use in production activation/page paths remains.
- Row 37: `QİSMƏN` - activation outcome/audit/diagnostics exist; user-facing fault state persist/render remains.

Current `git status --short` at task start:
```text
 M src/Dobby.Host/Login/HostLoginRuntimeFactory.cs
 M src/Dobby.Host/Login/LockRuntimeWorkflow.cs
 M src/Dobby.Host/Login/LockWindow.xaml
 M src/Dobby.Host/Login/LockWindow.xaml.cs
 M src/Dobby.Host/Login/LoginAuditCoordinator.cs
 M src/Dobby.Host/MainWindow.xaml
 M src/Dobby.Host/MainWindow.xaml.cs
 M src/Dobby.Host/Plugins/PluginPageRouteCoordinator.cs
 M src/Dobby.Host/PostLoginShellPresenter.cs
 M src/Dobby.Host/RuntimeFolderLayout.cs
 M src/Dobby.Host/Settings/AboutInfoProvider.cs
 M src/Dobby.Host/Settings/AboutSettingsModel.cs
 M src/Dobby.Host/Settings/AccountSettingsModel.cs
 M src/Dobby.Host/Settings/AppearanceSettingsModel.cs
 M src/Dobby.Host/Settings/SecuritySessionSettingsModel.cs
 M src/Dobby.Infrastructure/Dobby.Infrastructure.csproj
 M src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs
 M src/Dobby.Localization/HostResourceKeys.cs
 M src/Dobby.Localization/HostStringCatalog.cs
 M src/Dobby.Localization/Resources/Host.az-AZ.resx
 M src/Dobby.PluginFramework/Activation/PluginHostServices.cs
 M src/Dobby.Security/Authentication/LoginAttemptResult.cs
 M src/Dobby.Security/Authentication/LoginService.cs
 M src/Dobby.Shell/Layout/ShellLayoutState.cs
 M src/Dobby.Shell/ShellRouteHost.cs
 M src/Dobby.Shell/ShellViewModel.cs
 M src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs
 M src/Dobby.UI/ThemeTokens.cs
 M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
 M tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs
 M tests/Dobby.IntegrationTests/Audit/DurableAuditTrailTests.cs
 M tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs
 M tests/Dobby.IntegrationTests/Auth/SelfServicePasswordChangeTests.cs
 M tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs
 M tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs
 M tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs
 M tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs
 M tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs
 M tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs
 M tests/Dobby.IntegrationTests/Runtime/RuntimeFolderLayoutTests.cs
 M tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs
 M tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs
 M tests/Dobby.UnitTests/Audit/AuditEventTests.cs
 M tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs
 M tests/Dobby.UnitTests/Diagnostics/LocalizationMissingKeyEventTests.cs
 M tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs
 M tests/Dobby.UnitTests/Host/LoginAuditCoordinatorTests.cs
 M tests/Dobby.UnitTests/Host/PluginPageRouteCoordinatorTests.cs
 M tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs
 M tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs
 M tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs
 M tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs
 M tests/Dobby.UnitTests/Navigation/NavigationTests.cs
 M tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs
 M tests/Dobby.UnitTests/Shell/ShellLayoutStateTests.cs
 M tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs
 M tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs
 M tests/Dobby.UnitTests/Shell/WorkSessionLifecycleTests.cs
?? PROGRAM_DEVELOPMENT_WORK_ITEMS.md
?? PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md
?? docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md
?? docs/superpowers/plans/remaining-product-work/
?? src/Dobby.Host/Login/LockViewModel.cs
?? src/Dobby.Host/Plugins/HostPluginStorage.cs
?? src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs
?? src/Dobby.Host/Settings/RenderedSettingsSections.cs
?? src/Dobby.Host/Settings/UserManagementSettingsModel.cs
?? src/Dobby.Infrastructure/Persistence/HostPersistenceMaintenanceService.cs
?? src/Dobby.Infrastructure/Persistence/Migrations/0006_plugin_storage_values.sql
?? src/Dobby.Localization/Resources/Host.en-US.resx
?? src/Dobby.Localization/Resources/Host.tr-TR.resx
?? tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs
?? tests/Dobby.UnitTests/Host/HostPluginStorageTests.cs
?? tests/Dobby.UnitTests/Host/LockViewModelTests.cs
```

Touched-file ownership notes:
- Will touch Task 6 files from the brief: `src/Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs`, `src/Dobby.Shell/ShellRouteHost.cs`, `src/Dobby.Shell/ShellViewModel.cs`, `src/Dobby.Host/MainWindow.xaml`, `src/Dobby.Host/MainWindow.xaml.cs`, `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs`, `src/Dobby.PluginFramework/Activation/PluginExceptionBoundary.cs`, `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`, `tests/Dobby.UnitTests/Shell/PluginPageRenderRegistryTests.cs`, `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs`, `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`, `tests/Dobby.IntegrationTests/Plugins/PluginBackgroundOperationReportingTests.cs`, and `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`.
- May touch existing Task 5 shell tests only where Task 6 plugin rendering/status/fault behavior requires it.
- Will not touch unrelated pre-existing dirty files `src/Dobby.UI/ThemeTokens.cs` and `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`.
- Will not revert Task 1-5 changes in auth, storage, runtime layout, settings/about/localization, route host, or shell surfaces.
- Will not add packages, toolchains, screenshot libraries, or new migrations unless Task 6 evidence proves an approved existing seam requires it.

Red test plan:
- `PluginPageRenderRegistryTests.Default_registry_rejects_unapproved_plugin_supplied_framework_elements`
  - Expected initial failure: registry may not explicitly prove only approved host-owned model renderers can enter the host surface.
- `ShellSnapshotTests.Plugin_page_host_renders_approved_plugin_page_model`
  - Expected initial failure: snapshot may only assert `PluginPageHost` presence, not actual approved model rendering through the registry.
- `ShellBackgroundOperationCoordinatorTests.Plugin_owned_background_operation_reaches_shell_status_surface`
  - Expected initial failure: current coverage may not prove plugin callbacks/status integration through the same shell surface.
- `PluginBackgroundOperationReportingTests.Plugin_reporter_updates_shell_status_and_progress`
  - Expected initial failure: integration proof for plugin-owned background snapshots is absent.
- `PluginActivationRuntimeIntegrationTests.Plugin_exceptions_are_mapped_to_non_disclosing_fault_state`
  - Expected initial failure: production activation/page exception paths may not consistently use `PluginExceptionBoundary` or expose safe fault UI state.

Production seams to use:
- `PluginPageRenderRegistry.CreateDefault()` and `MainWindow.RenderPluginPageHost()`
- `PluginPageRouteCoordinator.ActivateRouteAsync(...)`
- `ShellViewModel.SetActivePluginPage(...)` and status/feedback/fault presentation state
- `HostPluginRuntimeCoordinator` activation result/fault path
- `PluginExceptionBoundary.InvokeAsync(...)`
- `ShellBackgroundOperationCoordinator` and `BackgroundOperationOwnerKind.Plugin`

Focused verification command:
```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~PluginPageRenderRegistry|FullyQualifiedName~ShellBackgroundOperationCoordinator"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PluginActivationRuntimeIntegrationTests|FullyQualifiedName~PluginBackgroundOperationReportingTests|FullyQualifiedName~ShellSnapshotTests"
git diff --check
```
