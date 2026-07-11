# Task 04 Execution Note: Rendered Settings, User Management, About, Theme Sync, And Localization

## Current Git Status

```text
 M src/Dobby.Host/Login/HostLoginRuntimeFactory.cs
 M src/Dobby.Host/Login/LockRuntimeWorkflow.cs
 M src/Dobby.Host/Login/LockWindow.xaml
 M src/Dobby.Host/Login/LockWindow.xaml.cs
 M src/Dobby.Host/Login/LoginAuditCoordinator.cs
 M src/Dobby.Host/RuntimeFolderLayout.cs
 M src/Dobby.Infrastructure/Dobby.Infrastructure.csproj
 M src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs
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
 M tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs
 M tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs
 M tests/Dobby.IntegrationTests/Runtime/RuntimeFolderLayoutTests.cs
 M tests/Dobby.UnitTests/Audit/AuditEventTests.cs
 M tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs
 M tests/Dobby.UnitTests/Diagnostics/LocalizationMissingKeyEventTests.cs
 M tests/Dobby.UnitTests/Host/LoginAuditCoordinatorTests.cs
 M tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs
 M tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs
 M tests/Dobby.UnitTests/Shell/WorkSessionLifecycleTests.cs
?? PROGRAM_DEVELOPMENT_WORK_ITEMS.md
?? PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md
?? docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md
?? docs/superpowers/plans/remaining-product-work/
?? src/Dobby.Host/Login/LockViewModel.cs
?? src/Dobby.Host/Plugins/HostPluginStorage.cs
?? src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs
?? src/Dobby.Infrastructure/Persistence/HostPersistenceMaintenanceService.cs
?? src/Dobby.Infrastructure/Persistence/Migrations/0006_plugin_storage_values.sql
?? tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs
?? tests/Dobby.UnitTests/Host/HostPluginStorageTests.cs
?? tests/Dobby.UnitTests/Host/LockViewModelTests.cs
```

## Relevant Dirty Files

Will work around completed Tasks 1-3 changes in login, plugin storage, runtime layout, audit, persistence, and work-session files. Do not revert them. Pre-existing unrelated dirty files remain `src/Dobby.UI/ThemeTokens.cs` and `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`; do not edit them for Task 4 unless a test failure proves they are in-scope.

## Intended Touches

Will touch:

- `src/Dobby.Host/Settings/SettingsNavigationModel.cs`
- `src/Dobby.Host/Settings/AppearanceSettingsModel.cs`
- `src/Dobby.Host/Settings/AccountSettingsModel.cs`
- `src/Dobby.Host/Settings/SecuritySessionSettingsModel.cs`
- `src/Dobby.Host/Settings/AboutInfoProvider.cs`
- `src/Dobby.Localization/HostResourceKeys.cs`
- `src/Dobby.Localization/HostStringCatalog.cs`
- `src/Dobby.Localization/Resources/Host.az-AZ.resx`
- `src/Dobby.Localization/Resources/Host.en-US.resx`
- `src/Dobby.Localization/Resources/Host.tr-TR.resx`
- `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`
- `tests/Dobby.UnitTests/Host/ShellThemePreferenceTests.cs`
- `tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs`
- `tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs`
- `tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`

Will not touch in this Task 4 source/localization pass:

- `src/Dobby.Host/MainWindow.xaml`
- `src/Dobby.Host/MainWindow.xaml.cs`
- `src/Dobby.Shell/ShellViewModel.cs`
- `src/Dobby.Host/PostLoginShellPresenter.cs`

Reason: the master plan's Work Lanes say shared UI file edits wait for Task 5 shell skeleton. If Task 4 cannot fully close without these files, report `BLOCKED` or `DONE_WITH_CONCERNS` and identify the exact remaining UI dependency instead of editing the shared UI lane early.

## Red Tests To Add

- `SettingsAndAboutViewModelTests.Settings_source_exposes_required_rendered_sections_and_user_management_actions`
  - Expected initial failure: source models do not expose a structured render/action inventory for Appearance, Account, Security/Session, User Management, and About.
- `SettingsLocalizationStringCatalogTests.Settings_about_user_management_keys_have_resx_parity`
  - Expected initial failure: `Host.en-US.resx` and `Host.tr-TR.resx` do not exist, or required Settings/About/User Management/Profile action keys are missing from one or more cultures.
- `ShellThemeAndSettingsIntegrationTests.Authenticated_session_runtime_consumes_saved_auto_lock_preference`
  - Expected initial failure: post-login session runtime is created from static session options and does not consume authenticated saved Security/Session preferences.

## Production Seams

- Settings source models under `src/Dobby.Host/Settings`.
- Resource key list and catalog fallback under `src/Dobby.Localization`.
- Existing `HostConfigurationSettingsPreferenceSource` for durable authenticated preferences.
- Existing session lifecycle wiring in `HostLoginRuntimeFactory` only if needed to consume saved auto-lock preferences without touching shared UI lane files.

## Focused Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~SettingsAndAbout|FullyQualifiedName~ShellThemePreference|FullyQualifiedName~Localization"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShellThemeAndSettingsIntegrationTests|FullyQualifiedName~ShellUiStaticScanTests"
git diff --check
```

Rendered WPF `ShellSnapshotTests` remain part of Task 4 closure, but should be deferred until the Task 5 shell skeleton makes shared UI edits available.
