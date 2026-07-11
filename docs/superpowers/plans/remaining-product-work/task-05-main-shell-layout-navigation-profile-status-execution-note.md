# Task 05 Execution Note: Main Shell Layout, Navigation, Profile Menu, Status, Notifications, Minimized Strip, And Diagnostics Export

## Current Git Status

```text
 M src/Dobby.Host/Login/HostLoginRuntimeFactory.cs
 M src/Dobby.Host/Login/LockRuntimeWorkflow.cs
 M src/Dobby.Host/Login/LockWindow.xaml
 M src/Dobby.Host/Login/LockWindow.xaml.cs
 M src/Dobby.Host/Login/LoginAuditCoordinator.cs
 M src/Dobby.Host/RuntimeFolderLayout.cs
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
 M src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs
 M src/Dobby.UI/ThemeTokens.cs
 M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
 M tests/Dobby.IntegrationTests/Audit/DurableAuditTrailTests.cs
 M tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs
 M tests/Dobby.IntegrationTests/Auth/SelfServicePasswordChangeTests.cs
 M tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs
 M tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs
 M tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs
 M tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs
 M tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs
 M tests/Dobby.IntegrationTests/Runtime/RuntimeFolderLayoutTests.cs
 M tests/Dobby.UnitTests/Audit/AuditEventTests.cs
 M tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs
 M tests/Dobby.UnitTests/Diagnostics/LocalizationMissingKeyEventTests.cs
 M tests/Dobby.UnitTests/Host/LoginAuditCoordinatorTests.cs
 M tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs
 M tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs
 M tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs
 M tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs
 M tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs
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

## Relevant Dirty Files

Task 5 must consume completed Task 4 source/localization/runtime seams:

- `src/Dobby.Host/Settings/RenderedSettingsSections.cs`
- `src/Dobby.Host/Settings/UserManagementSettingsModel.cs`
- updated Settings/About models
- updated localization resource keys and `Host.*.resx`

Do not revert completed Tasks 1-4 changes. Pre-existing unrelated dirty files remain `src/Dobby.UI/ThemeTokens.cs` and `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`; avoid them unless a Task 5 test proves direct scope.

## Intended Touches

Will touch:

- `src/Dobby.Host/MainWindow.xaml`
- `src/Dobby.Host/MainWindow.xaml.cs`
- `src/Dobby.Shell/ShellViewModel.cs`
- `src/Dobby.Shell/ShellRouteHost.cs`
- `src/Dobby.Host/PostLoginShellPresenter.cs`
- `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`
- `src/Dobby.Host/ShellDiagnosticsExportCoordinator.cs`
- `src/Dobby.Navigation/NavigationComposer.cs`
- `src/Dobby.Localization/HostResourceKeys.cs`
- `src/Dobby.Localization/HostStringCatalog.cs`
- `src/Dobby.Localization/Resources/Host.az-AZ.resx`
- `src/Dobby.Localization/Resources/Host.en-US.resx`
- `src/Dobby.Localization/Resources/Host.tr-TR.resx`
- `tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs`
- `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`
- `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`
- `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- `tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs`
- `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`

Will not touch:

- login/auth KDF or lock workflow files except where tests reveal a direct shell-profile command integration issue
- plugin storage/persistence migration files
- installer or portable tooling

## Red Tests To Add

- `ShellSnapshotTests.Main_shell_renders_required_named_regions`
  - Expected initial failure: missing or incomplete named shell regions from the Task 5 required region list.
- `ShellViewModelNavigationTests.Navigation_uses_shell_route_host`
  - Expected initial failure: direct route activation bypasses `ShellRouteHost` or unknown route fallback is not pinned.
- `ShellDiagnosticsExportActionTests.Authenticated_shell_diagnostics_export_button_creates_package`
  - Expected initial failure: profile/menu diagnostics export action is not wired through `ShellDiagnosticsExportCoordinator`.
- `ShellKeyboardSmokeTests.Shell_regions_are_keyboard_reachable`
  - Expected initial failure: required profile/menu/status/notification/minimized-strip controls are not keyboard-reachable or named for automation.

## Production Seam

- `MainWindow.xaml` named WPF regions and bindings.
- `MainWindow.xaml.cs` shell command adapters only; no duplicated auth/business logic.
- `ShellViewModel` presentation state and route/status/feedback/minimized-strip data.
- `ShellRouteHost` as the route activation boundary.
- `PostLoginShellPresenter` as wiring from authenticated runtime to shell.
- `ShellDiagnosticsExportCoordinator` for explicit local diagnostics export.

## Focused Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ShellViewModel|FullyQualifiedName~Navigation"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShellSnapshotTests|FullyQualifiedName~ShellDiagnosticsExportActionTests|FullyQualifiedName~ShellUiStaticScanTests"
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter "FullyQualifiedName~ShellKeyboardSmokeTests"
git diff --check
```
