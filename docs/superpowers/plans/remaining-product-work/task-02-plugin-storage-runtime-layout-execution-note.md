# Task 02 Execution Note: Plugin Storage, Runtime Layout, Audit Boundary, And Persistence Maintenance

## Current Status

Branch: `codex/remaining-product-work`

`git status --short` before task start:

```text
 M src/Dobby.Host/Login/HostLoginRuntimeFactory.cs
 M src/Dobby.Host/Login/LoginAuditCoordinator.cs
 M src/Dobby.Security/Authentication/LoginAttemptResult.cs
 M src/Dobby.Security/Authentication/LoginService.cs
 M src/Dobby.UI/ThemeTokens.cs
 M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
 M tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs
 M tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs
 M tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs
 M tests/Dobby.UnitTests/Host/LoginAuditCoordinatorTests.cs
?? PROGRAM_DEVELOPMENT_WORK_ITEMS.md
?? PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md
?? docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md
?? docs/superpowers/plans/remaining-product-work/
?? tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs
```

Task 1 is complete and review-approved. Preserve its auth changes, especially the `KdfPolicyResolver` constructor dependency and `LoginAttemptKind.KdfUnsupported` path in `HostLoginRuntimeFactory`, `LoginService`, and related tests.

## Dirty Files Relevant To Task

Will touch:

- `src/Dobby.Host/Plugins/HostPluginStorage.cs`
- `src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs`
- `src/Dobby.Infrastructure/Persistence/Migrations/0006_plugin_storage_values.sql`
- `src/Dobby.Infrastructure/Persistence/HostPersistenceMaintenanceService.cs`
- `src/Dobby.PluginFramework/Activation/PluginHostServices.cs`
- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs`
- `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`
- `src/Dobby.Host/RuntimeFolderLayout.cs`
- `tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs`
- `tests/Dobby.UnitTests/Host/HostPluginStorageTests.cs`
- `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs`
- `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`
- `tests/Dobby.IntegrationTests/Runtime/RuntimeFolderLayoutTests.cs`

Will not touch:

- `src/Dobby.UI/ThemeTokens.cs`
- `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`
- `PROGRAM_DEVELOPMENT_WORK_ITEMS.md`
- `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`
- `docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md`

Conflict/ownership notes:

- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` already has Task 1 auth edits and is also a Task 2 production seam. Preserve the KDF resolver wiring while adding plugin storage wiring.
- Do not edit applied migration SQL files `0002` through `0005`; add `0006_plugin_storage_values.sql` and register/embed it.
- Do not expose provider names, DB paths, protected key material, host table names, or plugin-visible file paths through `IPluginStorage`.

## Red Test Metadata

Red test classes and methods:

- `HostPluginStorageTests.Plugin_service_bridge_injects_plugin_scoped_storage`
- `HostPluginStorageTests.Shared_and_user_compartments_round_trip_and_isolate_values`
- `RuntimeFolderLayoutTests.Logical_roles_stay_under_canonical_roots`
- `HostPersistenceTests.Support_snapshot_truncates_wal_under_backup_root`

Expected initial failure:

- `IPluginStorage` cannot yet be constructor-injected through `PluginServiceRegistrationBridge`.
- No host plugin storage adapter/table is available for shared-domain and user-scoped round trips.
- Runtime folder layout has no logical role helpers for plugin staging, diagnostics logs, and temp files.
- No host persistence maintenance service currently forces WAL checkpoint or creates a support-safe online snapshot under `backup/`.

Production seam:

- `PluginServiceRegistrationBridge` constructor injection of `IPluginStorage`
- `PluginHostServices` host storage factory
- `HostLoginRuntimeFactory` post-login runtime composition
- `RuntimeFolderLayout` logical role helpers without adding root folders
- `PostLoginPluginPersistenceMigrations` append-only migration registration
- `HostPersistenceMaintenanceService`

Focused no-restore verification:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~PluginStorage|FullyQualifiedName~HostPluginStorage|FullyQualifiedName~AuditEvent"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~RuntimeFolderLayoutTests|FullyQualifiedName~HostPersistenceTests|FullyQualifiedName~PluginInstallStatePersistenceTests"
git diff --check
```
