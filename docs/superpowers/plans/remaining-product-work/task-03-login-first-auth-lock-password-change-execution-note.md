# Task 03 Execution Note: Login-First, Auth Policy, Lock, Password Change, Instrumentation, And Work-Session Lifecycle

## Current Status

Branch: `codex/remaining-product-work`

`git status --short` before task start:

```text
 M src/Dobby.Host/Login/HostLoginRuntimeFactory.cs
 M src/Dobby.Host/Login/LoginAuditCoordinator.cs
 M src/Dobby.Host/RuntimeFolderLayout.cs
 M src/Dobby.Infrastructure/Dobby.Infrastructure.csproj
 M src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs
 M src/Dobby.PluginFramework/Activation/PluginHostServices.cs
 M src/Dobby.Security/Authentication/LoginAttemptResult.cs
 M src/Dobby.Security/Authentication/LoginService.cs
 M src/Dobby.UI/ThemeTokens.cs
 M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
 M tests/Dobby.IntegrationTests/Audit/DurableAuditTrailTests.cs
 M tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs
 M tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs
 M tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs
 M tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs
 M tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs
 M tests/Dobby.IntegrationTests/Runtime/RuntimeFolderLayoutTests.cs
 M tests/Dobby.UnitTests/Audit/AuditEventTests.cs
 M tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs
 M tests/Dobby.UnitTests/Diagnostics/LocalizationMissingKeyEventTests.cs
 M tests/Dobby.UnitTests/Host/LoginAuditCoordinatorTests.cs
?? PROGRAM_DEVELOPMENT_WORK_ITEMS.md
?? PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md
?? docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md
?? docs/superpowers/plans/remaining-product-work/
?? src/Dobby.Host/Plugins/HostPluginStorage.cs
?? src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs
?? src/Dobby.Infrastructure/Persistence/HostPersistenceMaintenanceService.cs
?? src/Dobby.Infrastructure/Persistence/Migrations/0006_plugin_storage_values.sql
?? tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs
?? tests/Dobby.UnitTests/Host/HostPluginStorageTests.cs
```

Task 1 is complete. Task 2 implementation is verification-green and under review; preserve its plugin storage/migration/runtime-layout changes.

## Dirty Files Relevant To Task

Will touch:

- `src/Dobby.Host/App.xaml.cs`
- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- `src/Dobby.Host/Login/PostLoginSessionRuntime.cs`
- `src/Dobby.Host/Login/LockViewModel.cs`
- `src/Dobby.Host/Login/LockWindow.xaml`
- `src/Dobby.Host/Login/LockWindow.xaml.cs`
- `src/Dobby.Host/MainWindow.xaml.cs`
- `src/Dobby.Security/Authentication/DisplayNameValidator.cs`
- `src/Dobby.Security/Authentication/PasswordPolicyValidator.cs`
- `src/Dobby.Security/Authentication/KdfPolicyResolver.cs`
- `src/Dobby.Configuration/DobbyOptionsValidator.cs`
- `tests/Dobby.UnitTests/Security/DisplayNameValidatorTests.cs`
- `tests/Dobby.UnitTests/Security/PasswordPolicyValidatorTests.cs`
- `tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs`
- `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`
- `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs`
- `tests/Dobby.IntegrationTests/Auth/SelfServicePasswordChangeTests.cs`
- `tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs`
- `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`

Will not touch:

- Task 2 persistence migration and plugin storage files except if a compile integration issue requires preserving API compatibility.
- `src/Dobby.UI/ThemeTokens.cs`
- `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`
- `PROGRAM_DEVELOPMENT_WORK_ITEMS.md`
- `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`

Conflict/ownership notes:

- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` already contains Task 1 KDF wiring and Task 2 plugin storage wiring. Task 3 edits must preserve both.
- Shared UI lane files are sequential-only; Task 3 may touch only the lock/account entry points specified here and must not begin Task 4/5 shell redesign work.
- No Argon2 package, package source, or fallback branch may be added.

## Red Test Metadata

Red test classes and methods:

- `PreLoginAllowedWorkloadTests.Login_visible_precedes_post_login_runtime_work`
- `KdfPolicyResolverTests.Argon2id_fails_closed_without_approved_package`
- `SelfServicePasswordChangeTests.Shell_account_action_changes_password_without_closing_session`
- `WorkSessionShellIntegrationTests.Lock_preserves_work_sessions_and_logout_coordinates_close`
- `PostLoginSessionRuntimeTests.Handle_lock_preserves_active_work_sessions`

Expected initial failure:

- Startup/static tests may show post-login runtime preparation before auth or missing milestone guardrails.
- Lock screen may not have a production `LockViewModel`.
- Shell/account password change action may not be wired through the self-service coordinator.
- Work-session lifecycle may not coordinate lock/logout/disable/uninstall with controlled close/cancel behavior.

Production seam:

- Login startup sequencing and instrumentation in `App.xaml.cs`/`LoginRuntimePreparer`/`HostLoginRuntimeFactory`.
- Auth policy validators and `KdfPolicyResolver`.
- `PostLoginSessionRuntime`, `LockViewModel`, and `LockWindow` adapter code.
- Shell account/profile password-change command wiring.
- Work-session registry/close coordinator adapters for lock/logout/plugin lifecycle.

Focused no-restore verification:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~DisplayNameValidator|FullyQualifiedName~PasswordPolicyValidator|FullyQualifiedName~KdfPolicyResolver|FullyQualifiedName~PostLoginSessionRuntime"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PreLoginAllowedWorkloadTests|FullyQualifiedName~StartupBootstrapWiringTests|FullyQualifiedName~SelfServicePasswordChangeTests|FullyQualifiedName~WorkSessionShellIntegrationTests"
git diff --check
```
