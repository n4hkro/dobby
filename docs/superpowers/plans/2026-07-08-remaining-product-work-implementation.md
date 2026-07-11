# Remaining Product Work Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every remaining product-preparation row in `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md` while preserving the sprint-plan authority and the repository's existing architecture boundaries.

**Architecture:** Implement the remaining work in dependency order: storage/auth/runtime first, rendered WPF shell and settings next, plugin/license/manager UI after shell surfaces exist, sample/Admin artifacts after the plugin runtime is visible, and installer/portable/release evidence last. Some audited rows are source-authority reconciliation work, not feature expansion: display-name policy, password policy, audit severity, root runtime folders, and Argon2id must follow the sprint plans instead of inventing stricter behavior.

**Tech Stack:** .NET 10, WPF, xUnit, Shouldly, SQLite3MultipleCiphers-backed local persistence, DPAPI/protected-storage wrappers, PowerShell release tooling, MSIX packaging tools approved by the existing Sprint 11 decisions, and existing Dobby localization/resource conventions.

## Global Constraints

- Treat `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md` as the row-level scope source. Do not remove or mark a row complete until implementation and verification evidence for that row exists.
- Treat `SPRINT_IMPLEMENTATION_PLAN.md` and `SPRINT_3_EXECUTION_PLAN.md` through `SPRINT_11_EXECUTION_PLAN.md` as behavior authority. If an audited row conflicts with those plans, close the row by adding tests/evidence for the approved sprint behavior, not by broadening scope silently.
- Keep login-first strict. Before successful authentication, the app may only perform the ADR-0041 allowed workload: configuration binding, diagnostics sink initialization, runtime folder/pre-login persistence preparation, login/recovery/security audit storage, and login UI first paint. No plugin discovery, activation, package install, license-heavy verification, domain import/export, report generation, background task execution, or post-login migrations may run before authentication.
- Do not add NuGet packages, package sources, installer toolchains, crypto packages, screenshot packages, or CI/upload clients unless the package/version/tool is already approved in the pin/evidence ledgers or a new human approval row is recorded first.
- Do not edit applied migration SQL files. Add append-only numbered migrations and register/embed them through `PostLoginPluginPersistenceMigrations`.
- Preserve project boundaries: `Dobby.PluginFramework` must not depend on `Dobby.Infrastructure`, plugin contracts must not expose WPF or host persistence details, host/UI adapters stay in `Dobby.Host`, and authorization policy stays in `Dobby.Security`.
- `ReadOnlyExpired` allows existing/read-only access and authorized `.dobbylicense` import. It blocks host/plugin mutation, plugin storage writes, local `.dobbyplugin` install/import, background task start, domain import/export, report/file output, virtual/file/unknown print output, print-to-file/PDF, and any equivalent write path.
- Production builds must not include or accept dev/QA trust material, sample `.dobbyplugin`/`.dobbylicense` artifacts, TEST-ONLY recovery fixtures, private keys, passphrases, raw signing material, raw recovery payloads, or unredacted Admin transcripts.
- WPF/STA/Dispatcher failures must be root-caused before production fixes. Check window close/dispatcher cleanup, pending action reset, shared/static state, ordering, and explicit completion signals before changing behavior.
- Each task starts with failing tests or static guardrails, then implementation, then focused verification. The final task runs the strongest no-restore verification local assets allow and refreshes the portable build.
- Before implementing any task, create a task-specific execution note under `docs/superpowers/plans/remaining-product-work/task-NN-<slug>-execution-note.md`. Create the note immediately before that task starts, from the current code state. The note must include `git status --short`, current dirty files relevant to the task, intended touched files, touched-file conflict/ownership notes split into `will touch` and `will not touch`, red test class and method names, expected initial failure, production seam, and focused no-restore verification command. Execution notes are not scope authority and must not replace this master plan, `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`, or sprint/spec authority.
- Build-scoped evidence uses `local-rc-<UTC>-<shortsha>` where `<UTC>` is UTC timestamp format `yyyyMMddTHHmmssZ` and `<shortsha>` is `git rev-parse --short HEAD`. Use `nogit` only if `git rev-parse --short HEAD` fails, and record the exact `git unavailable` reason in the audit/evidence file.
- Release evidence goes under `artifacts/release-evidence/local-rc-<UTC>-<shortsha>/...`. Dev/QA sample plugin package outputs go under `artifacts/sample-plugin/local-rc-<UTC>-<shortsha>/...`; release evidence may reference sample artifact hashes/reports, but sample packages/licenses must not be copied into production publish or installer output.
- Do not commit unless the human explicitly requests a commit.

## Source Documents

- Scope audit: `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`
- Master sequence: `SPRINT_IMPLEMENTATION_PLAN.md`
- Runtime/config/audit/storage authority: `SPRINT_3_EXECUTION_PLAN.md`, `SPRINT_4_EXECUTION_PLAN.md`, `docs/specs/storage-and-data-protection.md`, `docs/specs/audit-and-diagnostics.md`
- Auth/session authority: `SPRINT_5_EXECUTION_PLAN.md`, `docs/specs/session-lifecycle.md`, `docs/specs/user-management.md`, `docs/07_ACCEPTANCE_CRITERIA.md`
- Settings/shell authority: `SPRINT_6_EXECUTION_PLAN.md`, `SPRINT_7_EXECUTION_PLAN.md`, `docs/specs/settings-and-about.md`, `docs/specs/shell-ui.md`, `docs/specs/work-sessions.md`
- Plugin runtime authority: `SPRINT_8_EXECUTION_PLAN.md`, `docs/specs/plugins-manager.md`, `docs/specs/sample-plugin.md`
- Licensing/runtime-mode authority: `SPRINT_9_EXECUTION_PLAN.md`, `docs/specs/license-import.md`, `docs/adr/ADR-0053-capability-surface-runtime-mode.md`
- Manager/sample/Admin authority: `SPRINT_10_EXECUTION_PLAN.md`, `docs/specs/admin-tool.md`
- Release/installer authority: `SPRINT_11_EXECUTION_PLAN.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`

## Remaining Row Targeting

| Task | Rows targeted for closure |
|---|---|
| Task 1 | Preflight guardrails for all rows, especially authority-conflict rows 2, 3, 6, 7, 8 |
| Task 2 | 1, 2, 3, 4 |
| Task 3 | 5, 6, 7, 8, 9, 10, 11, 31 |
| Task 4 | 12, 13, 14, 15, 16, 17, 18, 29, 30 |
| Task 5 | 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 32 |
| Task 6 | 33, 34, 35, 36, 37 |
| Task 7 | 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48 |
| Task 8 | 49, 50, 51, 52, 53, 54, 55, 56 |
| Task 9 | 57, 58, 59 |
| Task 10 | 60, 61, 62, 63, 64 |
| Task 11 | 65, 66, 67, 68, 69, subject to release-gate semantics |
| Task 12 | Final status-file cleanup and whole-system verification for rows 1-69 |

Closure coverage is computed only from Task 2 through Task 11 rows in this table. Task 1 is a guardrail/preflight task and Task 12 is a meta-audit/status cleanup task; neither is a row-closure coverage source.

## Work Lanes

- Lane A: storage/auth/runtime closure, Tasks 1-3.
- Lane B: shared UI patch lane, sequential only for `src/Dobby.Host/MainWindow.xaml`, `src/Dobby.Host/MainWindow.xaml.cs`, `src/Dobby.Shell/ShellViewModel.cs`, and `src/Dobby.Host/PostLoginShellPresenter.cs`.
- Lane C: plugin/license/manager/sample/Admin analysis and non-UI backend work, Tasks 6-10.
- Lane D: installer/portable/release verification, Tasks 11-12.

Subagents may run in parallel for analysis, test inventory, current-behavior mapping, and non-overlapping backend patches. They must not patch the shared UI lane files in parallel. Shared UI file edits follow this order: Task 5 shell skeleton and route/status surfaces, Task 4 rendered Settings/About/User Management, Task 6 plugin page host, Task 7 license surface, then Task 8 Plugins Manager actions. Task 4 source-model and localization work may start before Task 5, but `MainWindow.xaml`, `MainWindow.xaml.cs`, `ShellViewModel`, and `PostLoginShellPresenter` edits wait for the shell skeleton. The main agent owns final edits, conflict resolution, test execution, and the status-file update.

## Minimum Red-Test Metadata

Each task-specific execution note must refine this table against current code before implementation. These entries are the minimum red-test metadata for the master plan, not a substitute for task-level TDD steps.

| Task | Red test class and method names | Expected initial failure | Production seam | Focused verification |
|---|---|---|---|---|
| Task 1 | `RemainingProductWorkAuthorityTests.Canonical_runtime_storage_auth_and_audit_authority_is_preserved` | Fails if current or future code broadens P1 behavior beyond sprint/spec authority. | Existing validators/options/runtime layout/audit contracts only; no product behavior expansion. | `dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~RemainingProductWorkAuthorityTests"` |
| Task 2 | `HostPluginStorageTests.Plugin_service_bridge_injects_plugin_scoped_storage`; `HostPluginStorageTests.Shared_and_user_compartments_round_trip_and_isolate_values`; `RuntimeFolderLayoutTests.Logical_roles_stay_under_canonical_roots`; `HostPersistenceTests.Support_snapshot_truncates_wal_under_backup_root` | `IPluginStorage` cannot be constructor-injected through the production plugin bridge, plugin storage table/service is absent, logical role helpers are absent, or maintenance service is absent. | `PluginServiceRegistrationBridge` constructor injection of `IPluginStorage`, `PluginHostServices` host storage factory, `HostLoginRuntimeFactory`, `RuntimeFolderLayout`, `HostPersistenceMaintenanceService`. | Task 2 verify command block. |
| Task 3 | `PreLoginAllowedWorkloadTests.Login_visible_precedes_post_login_runtime_work`; `KdfPolicyResolverTests.Argon2id_fails_closed_without_approved_package`; `SelfServicePasswordChangeTests.Shell_account_action_changes_password_without_closing_session`; `WorkSessionShellIntegrationTests.Lock_preserves_work_sessions_and_logout_coordinates_close` | Post-login work can be prepared before auth, unsupported KDF is not fail-closed, password change is not reachable from UI, or lock/work-session lifecycle is incomplete. | `App.xaml.cs`, `HostLoginRuntimeFactory`, `PostLoginSessionRuntime`, `LockViewModel`, profile/account command wiring. | Task 3 verify command block. |
| Task 4 | `ShellSnapshotTests.Settings_tree_renders_required_categories`; `SettingsLocalizationStringCatalogTests.Settings_about_user_management_keys_have_resx_parity`; `ShellThemeAndSettingsIntegrationTests.Appearance_and_toolbar_theme_share_persisted_preference`; `SettingsAndAboutViewModelTests.User_management_is_permission_gated` | Settings/About/User Management surfaces or localization keys are missing, theme sync is incomplete, or saved session preference is not consumed. | `MainWindow.xaml`, settings source models, `ShellViewModel`, `PostLoginShellPresenter`, `HostResourceKeys`, `HostStringCatalog`, `Host.*.resx`. | Task 4 verify command block. |
| Task 5 | `ShellSnapshotTests.Main_shell_renders_required_named_regions`; `ShellViewModelNavigationTests.Navigation_uses_shell_route_host`; `ShellDiagnosticsExportActionTests.Authenticated_shell_diagnostics_export_button_creates_package`; `ShellKeyboardSmokeTests.Shell_regions_are_keyboard_reachable` | Named shell regions, route-host wiring, diagnostics export action, keyboard reachability, status/notification/minimized strip, or profile menu command wiring is missing. | `MainWindow.xaml`, `MainWindow.xaml.cs`, `ShellRouteHost`, `ShellViewModel`, `PostLoginShellPresenter`, diagnostics/status coordinators. | Task 5 verify command block. |
| Task 6 | `PluginPageRenderRegistryTests.Default_registry_renders_approved_page_models`; `PluginActivationRuntimeIntegrationTests.Plugin_route_renders_in_main_content`; `PluginBackgroundOperationReportingTests.Plugin_progress_updates_shell_status`; `ShellSnapshotTests.Plugin_fault_state_is_user_facing_and_non_disclosing` | Plugin page host/registry production wiring, plugin route flow, plugin progress surface, or fault UI is absent. | `PluginPageRenderRegistry`, `ShellRouteHost`, `HostPluginRuntimeCoordinator`, `PluginExceptionBoundary`, `ShellBackgroundOperationCoordinator`, `MainContent` host. | Task 6 verify command block. |
| Task 7 | `CustomerDeploymentContextIntegrationTests.Provisioned_context_is_single_security_owned_context`; `LicenseImportShellSurfaceTests.Plugins_license_surface_invokes_file_picker_and_coordinator`; `ReadOnlyExpiredEnforcementIntegrationTests.ReadOnlyExpired_blocks_all_mutation_surfaces`; `PrintOutputPolicyTests.ReadOnlyExpired_physical_print_requires_confirmation` | Non-default context provisioning, license UI/file picker wiring, complete runtime-mode gates, or physical-print confirmation is absent. | Security current-context service/store, license runtime-mode provider/service, `OperationAuthorizationPolicy`, host mode enforcement boundary, print confirmation seam. | Task 7 verify command block. |
| Task 8 | `ShellViewModelPluginsManagerTests.Manager_list_detail_states_are_structured`; `PluginIntegrationSuiteTests.Manager_install_enable_disable_uninstall_uses_real_coordinators`; `ShellSnapshotTests.Plugins_manager_renders_list_detail_and_safe_rejections`; `HostPluginLifecycleCoordinatorTests.Uninstall_coordinates_active_work_sessions` | Plugins Manager UI is absent or summary-only, lifecycle actions are not wired, rejection messages are unsafe/generic, or active work sessions are not coordinated. | `ShellViewModel`, `MainWindow.xaml`, `HostPluginPackageInstallCoordinator`, `HostPluginLifecycleCoordinator`, metadata cache/state store, localization resources. | Task 8 verify command block. |
| Task 9 | `PluginIntegrationSuiteTests.Sample_package_builds_signs_validates_and_installs_from_artifact_path`; `PluginActivationRuntimeIntegrationTests.Sample_page_renders_in_main_content`; `ShellSmokeTests.Sample_actions_cover_result_fault_progress_work_session`; `ProductionReleasePackagingStaticScanTests.Production_output_excludes_sample_plugin_artifacts` | Sample manifest/hash/signature/package evidence is placeholder or absent, sample page is not production-hosted, actions are not reachable, or production output includes sample artifacts. | Sample plugin source/manifest/assets, package signing path, plugin page host, background/work-session services, production static scan. | Task 9 verify command block. |
| Task 10 | `AdminCommandHostTests.Cli_routes_required_artifact_commands_without_wpf`; `AdminToolStaticScanTests.Verify_plugin_package_wrapper_exists_and_redacts_output`; `RecoveryResponseImportFixtureTests.Admin_recovery_response_imports_through_vendor_public_key`; `ProductionReleasePackagingStaticScanTests.Production_output_excludes_devqa_admin_material` | Required Admin commands/wrappers are missing, CLI opens WPF, verify tool is absent, recovery technical path is not host-import-compatible, or production scan misses dev/QA material. | `AdminCommandHost`, Admin command classes/wrappers, package/license/recovery validators, `VendorRecoveryPublicKey`, production static scans. | Task 10 verify command block. |
| Task 11 | `PortablePublishScriptTests.Portable_publish_enforces_support_dev_marker_and_profile`; `InstallerUpgradeSmokeTests.Rc_msix_upgrade_preserves_existing_data_root`; `ReleaseCandidateSmokeTests.Installed_artifact_launches_login_first_with_existing_data`; `ProductionReleasePackagingStaticScanTests.Portable_output_cannot_masquerade_as_production_installer` | Portable constraints are README-only, installer upgrade/preservation evidence is missing, installed launch proof is absent, or portable output can be mistaken for production installer output. | `build-rc-msix.ps1`, portable publish tooling, installer smoke/evidence scripts, production release static scan. | Task 11 verify command block. |
| Task 12 | `remaining-work-status-audit.md` one-shot audit generated from Task 2-11 closure evidence; no permanent test parses this plan. | Audit fails when row coverage table is incomplete/duplicated/out-of-range, a removed row lacks code/test/evidence, or release-gate status is unresolved. | `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md` and release evidence files only; Task 12 is not a row-closure coverage source. | Task 12 verify command blocks. |

---

## Task 1: Authority Reconciliation And Red Baseline

**Rows guarded:** all rows; special attention to rows 2, 3, 6, 7, 8.

**Files:**
- Create: `tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs`
- Modify after the new guardrail identifies the right existing test family: `tests/Dobby.UnitTests/Security/`, `tests/Dobby.IntegrationTests/Runtime/`, `tests/Dobby.UnitTests/Audit/`

**Purpose:** Prevent future implementation from "fixing" audited gaps by violating sprint authority. The tests in this task must fail against any behavior that broadens P1 beyond the plan files.

- [ ] **Step 1: Add source-authority guardrail tests**

Add tests asserting these approved facts:

- Runtime root folders remain the canonical storage-spec set: `data`, `config`, `logs`, `plugins`, `licenses`, `recovery`, `backup`, `temp`.
- Logical plugin install staging resolves under the plugin runtime area or existing staging table, not a new undocumented root folder.
- Technical diagnostics resolve under `logs` and diagnostics export packages stay explicit-call only.
- Audit minimum payload remains `timestamp`, `actor`, `action`, `target`, `result`, `correlationId`, and optional sanitized `details`; no audit `severity` column or payload field is introduced unless a later docs/ADR amendment approves it.
- Diagnostics event severity remains diagnostics-owned, for example `localization.missing-key` severity floor `Warning`.
- Display names are optional profile metadata: trim, store `null` when omitted/blank, preserve Azerbaijani/local Unicode, do not become login identifiers, do not get uniqueness checks.
- Password policy remains the documented P1 simple rule: non-empty/non-whitespace, length 1-128, no composition or rotation rule.
- Current approved KDF selection remains PBKDF2-HMAC-SHA256 with 600,000 iterations, 16-byte salt, 32-byte hash. Config validation may keep `Argon2id` in the allow-list, but resolver/login must fail closed through the `Login.KdfUnsupported` path unless a later approved Argon2 package/pin amendment exists.

- [ ] **Step 2: Record the source-authority preflight result**

Append a short evidence note to the implementation summary for this task. Do not edit `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md` yet. Do not add a permanent unit or integration test that parses this plan file; plan coverage and status-removal invariants are checked once in Task 12.

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~RemainingProductWorkAuthorityTests"
```

Expected before implementation: failures only on newly added assertions that the current code does not yet satisfy. Expected after Task 1 implementation: pass.

---

## Task 2: Plugin Storage, Runtime Layout, Audit Boundary, And Persistence Maintenance

**Rows targeted for closure:** 1, 2, 3, 4.

**Files:**
- Create: `src/Dobby.Host/Plugins/HostPluginStorage.cs`
- Create: `src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs`
- Create: `src/Dobby.Infrastructure/Persistence/Migrations/0006_plugin_storage_values.sql`
- Create: `src/Dobby.Infrastructure/Persistence/HostPersistenceMaintenanceService.cs`
- Modify: `src/Dobby.PluginFramework/Activation/PluginHostServices.cs`
- Modify: `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- Modify: `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs`
- Modify: `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`
- Modify: `src/Dobby.Host/RuntimeFolderLayout.cs`
- Modify: `tests/Dobby.UnitTests/Contracts/PluginStorageContractTests.cs`
- Create: `tests/Dobby.UnitTests/Host/HostPluginStorageTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Runtime/RuntimeFolderLayoutTests.cs`

**Behavior:**
- Plugins resolve `IPluginStorage` from `PluginHostServices`.
- `SharedDomainStorage` and `UserScopedStorage` are implemented by host-owned persistence, not by plugin-visible file paths.
- Storage key validation remains contract-owned and blocks absolute paths, `..`, `.`, empty keys, and `host:` keys.
- User-scoped storage uses an authenticated, stable, non-PII scope value. It must not persist raw username/display name as part of the key.
- Plugin storage writes are transactional and encrypted by the existing DB-at-rest provider.
- Root runtime folder list stays canonical. Add logical descriptors for staging and diagnostics only under existing roots.
- Audit severity is not added to audit payload/storage. Diagnostics severity remains diagnostics-owned.
- Add a host persistence maintenance service that can force WAL checkpoint and produce a support-safe consistent SQLite online snapshot into `backup/` without exposing a self-service backup/restore UI.

- [ ] **Step 1: Add failing storage backend tests**

Tests must prove:

- A test plugin whose constructor requires `IPluginStorage` is prepared and committed through `PluginServiceRegistrationBridge`, proving the production activation path can constructor-inject a plugin-specific host storage instance.
- The storage instance observed by the test plugin is scoped to the active plugin id. Do not test `PluginHostServices.TryResolve` directly and do not add `InternalsVisibleTo` only to reach internal implementation details.
- Shared and user-scoped compartments isolate keys by plugin id and scope.
- Invalid keys return `ErrorCode.InvalidInput`.
- `SetAsync`, `GetAsync`, `ExistsAsync`, `ListKeysAsync`, and `DeleteAsync` round-trip binary payload, content type, and schema version.
- User A and User B do not see each other's `UserScopedStorage` values for the same plugin/key.
- No plugin storage test can inspect provider names, DB paths, protected key material, or host table names through the plugin contract.

- [ ] **Step 2: Add migration and adapter**

Create migration `0006_plugin_storage_values.sql` with a single authoritative table:

```sql
CREATE TABLE IF NOT EXISTS PluginStorageValue (
    PluginId TEXT NOT NULL,
    Scope TEXT NOT NULL CHECK (Scope IN ('shared-domain','user-scoped')),
    UserScopeHash TEXT NOT NULL DEFAULT '',
    StorageKey TEXT NOT NULL,
    Payload BLOB NOT NULL,
    ContentType TEXT,
    SchemaVersion INTEGER,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY (PluginId, Scope, UserScopeHash, StorageKey)
);
CREATE INDEX IF NOT EXISTS IX_PluginStorageValue_PluginScope
    ON PluginStorageValue(PluginId, Scope, UserScopeHash);
```

Register and embed the migration without editing existing `0002` through `0005` files.

- [ ] **Step 3: Wire plugin storage post-login only**

Wire `HostPluginStorage` through `HostLoginRuntimeFactory` after successful authentication and post-login migrations. `PluginHostServices.CreateDefault()` must stay deny/noop for tests and must not accidentally expose writable storage.

- [ ] **Step 4: Reconcile runtime folder row 2**

Keep `RuntimeFolderLayout.RequiredFolderNames` at the storage-spec eight root names. Add these named logical role helpers:

- `PluginInstallStaging` resolves under `plugins`.
- `DiagnosticsLogs` resolves under `logs`.
- `TemporaryFiles` resolves under `temp`.

Tests must prove no undocumented root `staging` or `diagnostics` is required.

- [ ] **Step 5: Reconcile audit row 3**

Extend audit tests so `AuditEvent.ToCanonicalPayload()` and durable snapshot round-trip prove no audit severity field exists. Extend diagnostics tests so diagnostics-only severity remains covered. Do not add an `AuditLog.Severity` column.

- [ ] **Step 6: Add checkpoint and support snapshot behavior**

Implement `HostPersistenceMaintenanceService` with:

- `ForceWalCheckpoint()` using `PRAGMA wal_checkpoint(TRUNCATE);`
- `CreateSupportSnapshot(destinationPath)` using the provider-supported online backup API or existing SQLite connection backup support.
- Snapshot output under `backup/` only.
- No raw protected material, private keys, plugin runtime binaries, or unredacted diagnostics in the snapshot contract.

This is not a full backup/restore feature. Backup/restore remains P2. The task closes row 4 by adding real checkpoint/snapshot behavior and evidence, not by adding a user-facing restore workflow.

- [ ] **Step 7: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~PluginStorage|FullyQualifiedName~HostPluginStorage|FullyQualifiedName~AuditEvent"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~RuntimeFolderLayoutTests|FullyQualifiedName~HostPersistenceTests|FullyQualifiedName~PluginInstallStatePersistenceTests"
git diff --check
```

Expected: focused tests pass, architecture boundaries remain unchanged, no applied migration file is edited.

---

## Task 3: Login-First, Auth Policy, Lock, Password Change, Instrumentation, And Work-Session Lifecycle

**Rows targeted for closure:** 5, 6, 7, 8, 9, 10, 11, 31.

**Files:**
- Modify: `src/Dobby.Host/App.xaml.cs`
- Modify: `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- Modify: `src/Dobby.Host/Login/PostLoginSessionRuntime.cs`
- Create: `src/Dobby.Host/Login/LockViewModel.cs`
- Modify: `src/Dobby.Host/Login/LockWindow.xaml`
- Modify: `src/Dobby.Host/Login/LockWindow.xaml.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Modify: `src/Dobby.Security/Authentication/DisplayNameValidator.cs`
- Modify: `src/Dobby.Security/Authentication/PasswordPolicyValidator.cs`
- Modify: `src/Dobby.Security/Authentication/KdfPolicyResolver.cs`
- Modify: `src/Dobby.Configuration/DobbyOptionsValidator.cs`
- Modify: `tests/Dobby.UnitTests/Security/DisplayNameValidatorTests.cs`
- Modify: `tests/Dobby.UnitTests/Security/PasswordPolicyValidatorTests.cs`
- Modify: `tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Auth/SelfServicePasswordChangeTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`

**Behavior:**
- Pre-login stays closed to plugin/runtime/background work.
- Display name and password policy match the documented P1 simple policy, with tests proving the simplicity is intentional.
- PBKDF2 remains the selected approved KDF. `Argon2id` may remain accepted by config validation, but resolver/login behavior must fail closed through the `Login.KdfUnsupported` path without a later approved Argon2 package/pin amendment; no silent fallback from `Argon2id` to PBKDF2.
- Lock screen has a production ViewModel and preserves the active session until unlock/logout.
- Profile/account password change is reachable from shell/settings UI and preserves current session.
- Pre-login instrumentation covers real WPF startup milestones without starting forbidden workloads.
- Lock/logout/plugin lifecycle work-session behavior follows `docs/specs/work-sessions.md`: lock does not close work sessions; logout/disable/uninstall use controlled close/cancel behavior.

- [ ] **Step 1: Add login-first regression tests**

Extend startup tests to prove:

- `LoginVisible` happens before any post-login migration, plugin package scan, plugin activation, license-heavy verification, domain import/export, diagnostics export, or background task start.
- `LoginRuntimePreparer` does not prepare post-login plugin runtime before authentication.
- Pre-login instrumentation records allowed startup milestones only.

- [ ] **Step 2: Close display-name policy by tests and typed result**

Keep the approved behavior:

- `null`, empty, and whitespace normalize to `null`.
- Non-empty values are trimmed.
- Azerbaijani and local Unicode are preserved.
- Display name is not username, is not unique, and is not used for login.

If adding a `ValidateAndNormalize` API, it must return a typed `Result<string?>` without adding charset/complexity rules.

- [ ] **Step 3: Close password policy by tests and UI wiring**

Keep the approved P1 policy:

- Reject `null`, empty, whitespace-only, and length greater than `128`.
- Accept length `1` through `128`.
- No uppercase/lowercase/digit/symbol composition rule.
- No password rotation rule.

Ensure login, Owner bootstrap, user reset, recovery reset, and self-service password change all call the same validator path.

- [ ] **Step 4: Align KDF validator and resolver**

Choose the current sprint-authorized path:

- `Pbkdf2Sha256` resolves to PBKDF2-HMAC-SHA256 with 600,000 iterations, 16-byte salt, and 32-byte hash.
- `DobbyOptionsValidator` may continue to accept `Auth.PasswordKdfMode=Argon2id` because Sprint 5 explicitly lists it in the allow-list.
- Without a later human-approved Argon2 package/pin amendment, `KdfPolicyResolver.Resolve()` for `Argon2id` returns a non-success `Result` and the login path reports the support-safe `Login.KdfUnsupported` reason.
- No code path silently rewrites unsupported `Argon2id` to PBKDF2, and no implementation adds an Argon2 package or branch without the approved amendment and evidence.
- Update KDF evidence text only after tests prove the selected behavior.

- [ ] **Step 5: Implement LockViewModel and bind LockWindow**

`LockViewModel` must expose:

- Current user display label.
- Password input command path through unlock coordinator.
- Logout command path back to login.
- Typed error/countdown text without raw credential detail.
- Busy state so duplicate unlock/logout clicks do not race.

`LockWindow` code-behind should only adapt WPF password/control events into the ViewModel.

- [ ] **Step 6: Wire password change UI action**

Wire profile/account and Settings Account action to `SelfServicePasswordChangeCoordinator`. Tests must prove:

- No current-password prompt.
- Active session remains open.
- Old password fails next login.
- New password succeeds next login.
- Remember-me/session version rebind behavior remains intact.
- `user.password.change` audit is emitted.

- [ ] **Step 7: Wire work-session lifecycle**

Implement non-empty `HandleLock` behavior:

- Manual lock and auto-lock preserve work sessions.
- Unlock restores minimized/active work-session strip state.
- Logout asks controlled close/cancel when work sessions are active.
- Plugin disable/uninstall asks controlled close/cancel and does not lose user work silently.

- [ ] **Step 8: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~DisplayNameValidator|FullyQualifiedName~PasswordPolicyValidator|FullyQualifiedName~KdfPolicyResolver|FullyQualifiedName~PostLoginSessionRuntime"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PreLoginAllowedWorkloadTests|FullyQualifiedName~StartupBootstrapWiringTests|FullyQualifiedName~SelfServicePasswordChangeTests|FullyQualifiedName~WorkSessionShellIntegrationTests"
git diff --check
```

Expected: focused tests pass and no pre-login workload regression exists.

---

## Task 4: Rendered Settings, User Management, About, Theme Sync, And Localization

**Rows targeted for closure:** 12, 13, 14, 15, 16, 17, 18, 29, 30.

**Files:**
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`
- Modify: `src/Dobby.Host/PostLoginShellPresenter.cs`
- Modify: `src/Dobby.Host/Settings/SettingsNavigationModel.cs`
- Modify: `src/Dobby.Host/Settings/AppearanceSettingsModel.cs`
- Modify: `src/Dobby.Host/Settings/AccountSettingsModel.cs`
- Modify: `src/Dobby.Host/Settings/SecuritySessionSettingsModel.cs`
- Modify: `src/Dobby.Host/Settings/AboutInfoProvider.cs`
- Modify: `src/Dobby.Localization/HostResourceKeys.cs`
- Modify: `src/Dobby.Localization/HostStringCatalog.cs`
- Modify: `src/Dobby.Localization/Resources/Host.az-AZ.resx`
- Create: `src/Dobby.Localization/Resources/Host.en-US.resx`
- Create: `src/Dobby.Localization/Resources/Host.tr-TR.resx`
- Modify: `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/ShellThemePreferenceTests.cs`
- Modify: `tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs`
- Modify: `tests/Dobby.UnitTests/Localization/ShellLocalizationCoverageTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Host/ShellThemeAndSettingsIntegrationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`

**Behavior:**
- Settings tree renders `Appearance`, `Account`, `Security / Session`, `User Management`, and `About`.
- User Management entry is permission-gated by `users.management`.
- Appearance screen uses the same preference source as the TopToolbar theme switch.
- Account screen shows profile/account details and self-service password change.
- Security/Session screen controls auto-lock enablement and minute value, and saved preference is consumed by session runtime.
- About screen renders app version, build number/date, host edition/channel, redacted device/support code, plugin versions/status or empty state, support contact, legal line, and neutral Owner status for managed users. About must not show license import/status management.
- `az-AZ` remains default. `en-US` and `tr-TR` skeleton resources exist for the same key set. Missing-key diagnostics remain diagnostics-only.

- [ ] **Step 1: Add rendered Settings snapshot tests**

Extend `ShellSnapshotTests` to assert visible WPF regions/text for all required Settings categories and About fields. Include permission-gated User Management visibility and absence from unauthorized users.

- [ ] **Step 2: Add localization parity tests**

Tests must assert:

- Every visible Settings/About/User Management/Profile action key exists in `HostResourceKeys`.
- `HostStringCatalog` has fallback values.
- `Host.az-AZ.resx`, `Host.en-US.resx`, and `Host.tr-TR.resx` contain the same required keys.
- `MainWindow.xaml` does not contain hardcoded visible Settings/About/User Management strings.

- [ ] **Step 3: Implement User Management screen**

Render list/detail/action UI for:

- View users.
- Create managed user.
- Update display name/active state.
- Deactivate/reactivate.
- Reset password.
- Grant/revoke permissions.

Owner protections must remain service-backed, not UI-only. Managed users without permission see non-disclosing disabled/denied state.

- [ ] **Step 4: Implement Settings screens**

Render:

- Appearance theme selection.
- Account information and password change action.
- Security/Session auto-lock toggle and minute input.
- About read-only fields.

Use existing source models and preference source. Do not reimplement Sprint 6 source logic.

- [ ] **Step 5: Wire theme and auto-lock runtime**

Tests must prove:

- TopToolbar theme switch and Settings Appearance change the same persisted value.
- Auto-lock saved preference is read by the authenticated session runtime.
- Auto-lock default is passive.
- Minute input is validated and persisted through existing settings preference storage.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~SettingsAndAbout|FullyQualifiedName~ShellThemePreference|FullyQualifiedName~Localization"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShellSnapshotTests|FullyQualifiedName~ShellThemeAndSettingsIntegrationTests|FullyQualifiedName~ShellUiStaticScanTests"
git diff --check
```

Expected: Settings/About/User Management render evidence passes and visible strings are localized.

---

## Task 5: Main Shell Layout, Navigation, Profile Menu, Status, Notifications, Minimized Strip, And Diagnostics Export

**Rows targeted for closure:** 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 32.

**Files:**
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`
- Modify: `src/Dobby.Shell/ShellRouteHost.cs`
- Modify: `src/Dobby.Host/PostLoginShellPresenter.cs`
- Modify: `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`
- Modify: `src/Dobby.Host/ShellDiagnosticsExportCoordinator.cs`
- Modify: `src/Dobby.Navigation/NavigationComposer.cs`
- Modify: `src/Dobby.Localization/HostResourceKeys.cs`
- Modify: `src/Dobby.Localization/HostStringCatalog.cs`
- Modify: `src/Dobby.Localization/Resources/Host.*.resx`
- Modify: `tests/Dobby.UnitTests/Shell/ShellViewModelNavigationTests.cs`
- Modify: `tests/Dobby.UnitTests/Shell/ShellViewModelWorkspaceTabTests.cs`
- Modify: `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs`
- Modify: `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`

**Behavior:**
- `MainWindow.xaml` contains real named shell surfaces: `TopToolbar`, `Sidebar`, `ThirdBar`, `MainContent`, bottom status/progress, notification/feedback region, and minimized work-session strip.
- `ShellRouteHost` is the production route activation boundary.
- Sidebar collapse/open-node state is visible and persisted.
- Third Bar is rendered and ordered.
- TopToolbar/Homebar renders built-in and plugin ordering/hidden state.
- Main Content hosts built-in route content and plugin page host content.
- Background operation status/progress is visible.
- Notifications and feedback states are visible, non-overlapping, localized, and keyboard reachable.
- Minimized work-session strip renders session metadata and restore controls.
- Profile/session menu opens and wires profile/account info, password change, manual lock, logout, and diagnostics export.
- Diagnostics export action invokes `ShellDiagnosticsExportCoordinator`, uses the existing diagnostics package builder, emits `diagnostics.export.created`, and never uploads data.

- [ ] **Step 1: Add shell structure tests**

Static and rendered tests must require named WPF regions:

```csharp
var requiredRegions = new[]
{
    "TopToolbar",
    "Sidebar",
    "ThirdBar",
    "MainContent",
    "ShellStatusBar",
    "ShellNotificationHost",
    "ShellFeedbackHost",
    "MinimizedWorkSessionStrip",
    "ProfileMenu"
};
```

- [ ] **Step 2: Route all navigation through ShellRouteHost**

Remove direct route bypass from click handlers. Built-in and plugin routes must pass through the coordinator/route host path and fall back to Home for unknown route.

- [ ] **Step 3: Render and persist navigation state**

Implement visible controls for:

- Sidebar collapsed state.
- Sidebar open node ids.
- Third Bar order.
- TopToolbar plugin ordering and hidden state.
- Last active route storage without startup auto-restore of work sessions.

- [ ] **Step 4: Wire profile menu commands**

Profile menu handlers must call the same backend paths used by session/auth tasks:

- Password change.
- Manual lock.
- Logout.
- Diagnostics export.

All actions must have busy/denied/error feedback and localized labels.

- [ ] **Step 5: Wire status, notifications, feedback, and minimized strip**

Use existing ViewModel fields or add small presentation models. Avoid nested floating-card layouts that clip on 1024x680. Snapshot tests must cover 1024x680 and 1920x1080.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ShellViewModel|FullyQualifiedName~Navigation"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~ShellSnapshotTests|FullyQualifiedName~ShellDiagnosticsExportActionTests|FullyQualifiedName~ShellUiStaticScanTests"
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter "FullyQualifiedName~ShellKeyboardSmokeTests"
git diff --check
```

Expected: shell surfaces render, keyboard smoke passes, diagnostics export is authenticated and explicit.

---

## Task 6: Plugin Page Rendering, Plugin Background Status, Exception Boundary, And Fault UI

**Rows targeted for closure:** 33, 34, 35, 36, 37.

**Files:**
- Modify: `src/Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs`
- Modify: `src/Dobby.Shell/ShellRouteHost.cs`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs`
- Modify: `src/Dobby.PluginFramework/Activation/PluginExceptionBoundary.cs`
- Modify: `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`
- Modify: `tests/Dobby.UnitTests/Shell/PluginPageRenderRegistryTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`
- Create: `tests/Dobby.IntegrationTests/Plugins/PluginBackgroundOperationReportingTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`

**Behavior:**
- Production `MainWindow` uses `PluginPageRenderRegistry.CreateDefault()` and a concrete `PluginPageHost`/`ContentControl`.
- Plugin pages render only approved host-owned view models. Plugin-supplied WPF controls/types never enter the host surface.
- Route clicks call plugin page route coordinator and active page state, not a direct temporary bypass path.
- Plugin background snapshots visibly update status/progress through the same shell status surface as host operations.
- Plugin exceptions during activation/page/event/action flow pass through `PluginExceptionBoundary` or equivalent host-owned boundary.
- Faulted/rejected/skipped states are user-facing, non-disclosing, persisted or derivable from durable state/cache where required, and mapped to audit/diagnostics with redacted details.

- [ ] **Step 1: Add MainWindow plugin page host tests**

Static and snapshot tests must require `PluginPageHost` and prove an approved test `IPluginPageModel` renders through `PluginPageRenderRegistry`.

- [ ] **Step 2: Wire production registry and route coordinator**

Production activation and navigation must set shell active plugin page state through the coordinator. `Navigation_Click` must not bypass the coordinator.

- [ ] **Step 3: Wire background operation UI**

Tests must prove `BackgroundOperationOwnerKind.Plugin` snapshots show visible status/progress in `ShellStatusBar` and do not use a custom progress channel.

- [ ] **Step 4: Apply exception boundary to production plugin paths**

Wrap:

- Activation hook execution.
- Plugin page model creation.
- Plugin action/event handlers.
- Background operation callbacks.

Map failures to non-disclosing user state and detailed redacted diagnostics.

- [ ] **Step 5: Persist or derive fault states**

Manager/list/detail UI must be able to show `Faulted`, `Rejected`, `Skipped`, or safe reason text after the operation completes. Do not show stack traces or raw exception messages.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~PluginPageRenderRegistry|FullyQualifiedName~ShellBackgroundOperationCoordinator"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PluginActivationRuntimeIntegrationTests|FullyQualifiedName~PluginBackgroundOperationReportingTests|FullyQualifiedName~ShellSnapshotTests"
git diff --check
```

Expected: plugin page rendering and fault/status surfaces are production-wired.

---

## Task 7: Customer Deployment Context, License UI, And Complete ReadOnlyExpired Enforcement

**Rows targeted for closure:** 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48.

**Files:**
- Modify: `src/Dobby.Licensing/ILicenseRuntimeModeProvider.cs`
- Modify: `src/Dobby.Licensing/ILicenseStateStore.cs`
- Modify: `src/Dobby.Licensing/LicenseDeviceFingerprintV1.cs`
- Modify: `src/Dobby.Licensing/LicenseImportResult.cs`
- Modify: `src/Dobby.Licensing/LicenseImportService.cs`
- Modify: `src/Dobby.Licensing/LicenseLifecycleState.cs`
- Modify: `src/Dobby.Licensing/LicensePayloadValidator.cs`
- Modify: `src/Dobby.Licensing/LicenseRejectReasons.cs`
- Modify: `src/Dobby.Licensing/LicenseRuntimeModeProvider.cs`
- Modify: `src/Dobby.Licensing/LicenseRuntimeModeService.cs`
- Modify: `src/Dobby.Licensing/LicenseStateRecord.cs`
- Modify: `src/Dobby.Licensing/PluginCapabilityLicensing.cs`
- Modify: `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`
- Modify: `src/Dobby.Security/CurrentContext/CurrentCustomerDeploymentContext.cs`
- Modify: `src/Dobby.Security/CurrentContext/CurrentCustomerDeploymentContextResult.cs`
- Modify: `src/Dobby.Security/CurrentContext/CurrentCustomerDeploymentContextService.cs`
- Modify: `src/Dobby.Security/CurrentContext/ICurrentCustomerDeploymentContextStore.cs`
- Modify: `src/Dobby.Host/HostCustomerDeploymentContextStore.cs`
- Modify: `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginStorage.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs`
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`
- Modify: `src/Dobby.Application/Printing/PrintTargetClassifier.cs`
- Modify: `src/Dobby.Infrastructure/Printing/WindowsPrintTargetClassifier.cs`
- Modify: `tests/Dobby.UnitTests/Host/LicenseImportCoordinatorTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/HostPluginPackageInstallCoordinatorTests.cs`
- Modify: `tests/Dobby.UnitTests/Application/PrintOutputPolicyTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Licensing/LicenseImportIntegrationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Licensing/LicenseRuntimeModeIntegrationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Licensing/ReadOnlyExpiredEnforcementIntegrationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/LicenseImportShellSurfaceTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`

**Behavior:**
- Clean install has exactly one active `(customerId, deploymentId) = (default, default)` context.
- A real provisioning path exists for non-default customer/deployment context without license import opportunistically creating or mutating context.
- `Plugins > License` renders status/list/detail and import action with permission gates.
- Production UI calls `WindowsLicenseFilePicker.PickLicenseFile()` through the coordinator.
- License rejection/status messages are localized, safe, and non-disclosing.
- `ReadOnlyExpired` enforcement covers every write/mutation row in the Sprint 9 matrix, not only package install.
- Physical printer output in `ReadOnlyExpired` requires explicit confirmation; virtual/file/unknown print output and print-to-file/PDF are denied.

- [ ] **Step 1: Add current-context provisioning tests**

Tests must prove:

- Clean install materializes one default/default context.
- Provisioned non-default context is read through the Security-owned current-context service.
- Missing/duplicate context fails closed before license state mutation.
- License import and plugin activation consume the same context.
- Raw context values are not disclosed in UI/plugin surfaces.

- [ ] **Step 2: Implement license shell surface**

Render `Plugins > License` with:

- View gated by `plugins.license.view`.
- Import gated by `plugins.license.import`.
- Import available in `ReadOnlyExpired` when permission and binding checks pass.
- Busy/duplicate-click state.
- Safe success/reject/permission feedback.
- About screen remains free of license status/import management.

- [ ] **Step 3: Invoke file picker from production UI**

Wire import button to the existing file picker seam and coordinator. Tests must use a fake picker; production path uses `WindowsLicenseFilePicker`.

- [ ] **Step 4: Add mode-aware operation gates**

Introduce or extend a single host-owned runtime-mode enforcement boundary so these return typed denials in `ReadOnlyExpired`:

- General mutation command pipeline.
- Plugin storage `SetAsync` and `DeleteAsync`.
- Background task start.
- `.dobbyplugin` package install/import through `HostPluginPackageInstallCoordinator`.
- Domain import/export.
- Report/file output.
- Virtual/file/unknown printing and print-to-file/PDF.

Allowed paths:

- Read-only view/list/detail.
- Existing data read.
- Authorized `.dobbylicense` import.
- Physical printer only after explicit confirmation.

- [ ] **Step 5: Add print confirmation path**

Physical print in `ReadOnlyExpired` must route through a confirmation seam. Tests must cover allow-after-confirm, deny-on-cancel, deny virtual/file/unknown, and no print-to-file/PDF bypass.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~LicenseImportCoordinator|FullyQualifiedName~HostPluginPackageInstallCoordinator|FullyQualifiedName~PrintOutputPolicy"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~LicenseImportIntegrationTests|FullyQualifiedName~LicenseRuntimeModeIntegrationTests|FullyQualifiedName~ReadOnlyExpiredEnforcementIntegrationTests|FullyQualifiedName~LicenseImportShellSurfaceTests|FullyQualifiedName~ShellSnapshotTests"
git diff --check
```

Expected: every ReadOnlyExpired row has a concrete gate proof or explicit absent-surface proof.

---

## Task 8: Plugins Manager UI And Lifecycle Actions

**Rows targeted for closure:** 49, 50, 51, 52, 53, 54, 55, 56.

**Files:**
- Modify: `src/Dobby.Host/MainWindow.xaml`
- Modify: `src/Dobby.Host/MainWindow.xaml.cs`
- Modify: `src/Dobby.Shell/ShellViewModel.cs`
- Modify: `src/Dobby.Host/PostLoginShellPresenter.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`
- Modify: `src/Dobby.Host/Plugins/HostPluginLifecycleCoordinator.cs`
- Modify: `src/Dobby.Host/Plugins/PluginRuntimeAuditCoordinator.cs`
- Modify: `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs`
- Modify: `src/Dobby.PluginFramework/Packaging/InstalledPluginMetadataCache.cs`
- Modify: `src/Dobby.Localization/HostResourceKeys.cs`
- Modify: `src/Dobby.Localization/HostStringCatalog.cs`
- Modify: `src/Dobby.Localization/Resources/Host.*.resx`
- Modify: `tests/Dobby.UnitTests/Shell/ShellViewModelPluginsManagerTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/HostPluginLifecycleCoordinatorTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/HostPluginPackageInstallCoordinatorTests.cs`
- Modify: `tests/Dobby.UnitTests/Host/PluginRuntimeAuditCoordinatorTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Plugins/PluginIntegrationSuiteTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`

**Behavior:**
- Plugins Manager is a visible shell screen under the Plugins route.
- Manager list shows display name, plugin id, version, enablement, status, trust/license/fault summary, pending uninstall/restart required state, and safe rejection text.
- Detail view is structured UI, not only a summary string.
- Install action opens file picker, calls package install coordinator, respects `plugins.management.import`, uses background operation status, and maps rejection reasons safely.
- Enable/disable actions respect `plugins.management.configure`.
- Uninstall action respects `plugins.management.delete`, shows confirmation, supports keep/delete data choice, and coordinates active work-session close/cancel.
- Lifecycle audit events are emitted for install/enable/disable/uninstall.
- `LicenseReadOnly` under `ReadOnlyExpired` is shown as policy/license-mode denial, not package-invalid.

- [ ] **Step 1: Add manager UI tests**

Add list/detail/action state tests and snapshots for:

- Empty state.
- Loading/busy.
- Enabled.
- Disabled.
- Rejected.
- Faulted.
- ReadOnlyExpired denial.
- Pending uninstall/restart required.

- [ ] **Step 2: Render manager list and detail**

Bind list selection to structured detail sections: identity, package/trust, license/runtime mode, status/fault, operation availability, and last action feedback.

- [ ] **Step 3: Wire install action**

Production button calls file picker and coordinator. Tests must cover permission denial, cancelled picker, invalid package, trusted install, ReadOnlyExpired denial, and background operation progress.

- [ ] **Step 4: Wire enable/disable/uninstall**

Actions must update durable state/cache and avoid activation for disabled or pending uninstall plugins. Uninstall must handle active work sessions and data retention choice.

- [ ] **Step 5: Map user-facing rejection messages**

All install/lifecycle rejection reasons must map to localized, non-disclosing UI messages. Raw validator/exception details stay diagnostics-only.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ShellViewModelPluginsManager|FullyQualifiedName~HostPluginLifecycleCoordinator|FullyQualifiedName~HostPluginPackageInstallCoordinator|FullyQualifiedName~PluginRuntimeAuditCoordinator"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PluginIntegrationSuiteTests|FullyQualifiedName~PluginActivationRuntimeIntegrationTests|FullyQualifiedName~ShellSnapshotTests"
git diff --check
```

Expected: manager UI performs real lifecycle actions and reports safe states.

---

## Task 9: Final Sample Plugin Package And Reachable Behavior

**Rows targeted for closure:** 57, 58, 59.

**Files:**
- Modify: `samples/Dobby.Sample.Plugin/SamplePlugin.cs`
- Modify: `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json`
- Modify: `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`
- Modify: `samples/Dobby.Sample.Plugin/assets/sample-plugin.png`
- Create/update: generated dev/QA sample package and report under `artifacts/sample-plugin/local-rc-<UTC>-<shortsha>/`, not production publish output
- Modify: `tests/Dobby.IntegrationTests/Plugins/PluginIntegrationSuiteTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`
- Modify: `tests/Dobby.SmokeTests/ShellSmokeTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`

**Behavior:**
- Sample plugin remains dev/QA artifact, `net10.0`, Contracts/SDK-only.
- Manifest has real file hashes/signature metadata and passes schema/procedural validation.
- Final `.dobbyplugin` is signed with approved dev/QA material and installable in dev/QA profile.
- Sample package evidence is written under `artifacts/sample-plugin/local-rc-<UTC>-<shortsha>/`; release evidence records hash/report references only.
- Production output excludes the sample package and rejects dev/QA trust.
- Sample plugin page renders in Main Content through the production plugin page host.
- Sample plugin actions cover permission denial, `Result` success/invalid, fault isolation, progress reporting, and work-session behavior.
- Work-session action closes the session when the sample operation completes or user cancels through the documented path.

- [ ] **Step 1: Add sample package fixture tests**

Tests must build/sign the sample package from source, validate manifest hashes/signature, and install it through the same manager/coordinator path as user packages.

- [ ] **Step 2: Finalize manifest and assets**

Replace the current manifest dummy hash/signature values. Keep private signing material out of repo. Record only public hash/evidence allowed by Sprint 10 rules.

- [ ] **Step 3: Wire sample Main Content page**

Sample page must render through `PluginPageRenderRegistry` and `MainContent`, not a sample-only host path.

- [ ] **Step 4: Implement sample action coverage**

Expose sample actions reachable from UI:

- Permission denied action.
- Successful `Result`.
- Invalid input `Result`.
- Faulting action isolated by plugin boundary.
- Progress action using official background operation reporting.
- Work-session action that opens and closes or cancels through documented session behavior.

- [ ] **Step 5: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PluginIntegrationSuiteTests|FullyQualifiedName~PluginActivationRuntimeIntegrationTests|FullyQualifiedName~PluginAndSdkBoundaryTests|FullyQualifiedName~ProductionReleasePackagingStaticScanTests"
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter "FullyQualifiedName~ShellSmokeTests"
git diff --check
```

Expected: sample plugin is a real dev/QA package and is not present or trusted in production output.

---

## Task 10: Admin Artifact Tools, Verify Tool, Recovery Technical Path, And Production Trust Exclusion

**Rows targeted for closure:** 60, 61, 62, 63, 64.

**Files:**
- Modify: `src/Dobby.Admin/Commands/AdminCommandHost.cs`
- Modify: `src/Dobby.Admin/App.xaml`
- Modify: `src/Dobby.Admin/App.xaml.cs`
- Create: `src/Dobby.Admin/Commands/SignPluginPackageCommand.cs`
- Create: `src/Dobby.Admin/Commands/CreatePluginLicenseCommand.cs`
- Create: `src/Dobby.Admin/Commands/VerifyPluginPackageCommand.cs`
- Create: `src/Dobby.Admin/Commands/CreateRecoveryResponseCommand.cs`
- Modify: `tools/sign-plugin-package/sign-plugin-package.ps1`
- Modify: `tools/create-plugin-license/create-plugin-license.ps1`
- Create: `tools/verify-plugin-package/verify-plugin-package.ps1`
- Create: `tools/verify-plugin-package/README.md`
- Modify: `tools/create-recovery-response/create-recovery-response.ps1`
- Modify: `src/Dobby.Security/Authentication/VendorRecoveryPublicKey.cs`
- Modify: `tests/Dobby.UnitTests/Admin/AdminCommandHostTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Licensing/LicenseImportIntegrationTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/AdminToolStaticScanTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`
- Modify: `tests/Dobby.SmokeTests/ShellSmokeTests.cs`

**Behavior:**
- `Dobby.Admin.exe --cli <command>` is a real process path and does not open WPF UI in CLI mode.
- Admin options match `docs/specs/admin-tool.md`; do not infer option names from illustrative release docs.
- `sign-plugin-package` signs packages and produces deterministic, redacted report output.
- `create-plugin-license` can generate at least active, pending, readonly/expired, denied, binding-mismatch testable variants using approved non-production/Admin material. It must not reuse Sprint 9 test-only fixture material as product/Admin evidence.
- `verify-plugin-package` is implemented even though Sprint 10 marked it optional, because remaining row 62 requires closure. It validates package shape/signature/trust/fingerprint and exits with the approved code vocabulary.
- `create-recovery-response` produces host-import-compatible `.dobbyrecovery` through the real `VendorRecoveryPublicKey` verifier path for technical-path proof. Production/Admin release evidence remains Sprint 11/vendor handover.
- Production build/static scan rejects dev/QA artifacts and bootstrap/test fixtures.

- [ ] **Step 1: Add Admin CLI process tests**

Tests must cover:

- Valid `--cli` routing.
- Unknown command/invalid option exit `64`.
- Validation conflict exit `65`.
- Signing material unavailable exit `66`.
- Redacted stdout/stderr.
- WPF window not opened in CLI mode.

- [ ] **Step 2: Implement sign-plugin-package**

Use existing package reader/validator/fingerprint shapes. Do not create a second package-validity model.

- [ ] **Step 3: Implement create-plugin-license variants**

Generate detached `.dobbylicense` v2 artifacts compatible with existing import verifier. Tests must cover active, pending, readonly/expired, denied, wrong binding, and package-fingerprint binding.

- [ ] **Step 4: Implement verify-plugin-package**

Add Admin command and wrapper. The command must verify package extension/zip/manifest/schema/hashes/signature/trust/fingerprint and print only safe summary/reason codes.

- [ ] **Step 5: Implement create-recovery-response technical path**

Wire `VendorRecoveryPublicKey` to approved non-production or otherwise available non-release public key material for host-import compatibility tests. Do not claim production/Admin AC-007 release evidence in this task.

- [ ] **Step 6: Add production exclusion scans**

Static scans must fail production publish/installer output if it includes:

- Dev/QA public/private material.
- Dev/QA trust profile metadata.
- Sample `.dobbyplugin` or sample `.dobbylicense`.
- TEST-ONLY recovery fixtures.
- Bootstrap/non-production recovery material.
- Private keys or signing transport details.

- [ ] **Step 7: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AdminCommandHost"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~RecoveryResponseImportFixtureTests|FullyQualifiedName~LicenseImportIntegrationTests|FullyQualifiedName~AdminToolStaticScanTests|FullyQualifiedName~ProductionReleasePackagingStaticScanTests"
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter "FullyQualifiedName~ShellSmokeTests"
git diff --check
```

Expected: required Admin tools work, verify tool exists, and production static scans reject non-production artifacts.

---

## Task 11: MSIX Installer, Upgrade/Data Preservation, Portable Constraint, And Installed Launch Proof

**Rows targeted for closure:** 65, 66, 67, 68, 69. These rows are not removed from the status file while release-gate semantics mark them GA/customer-blocked.

**Files:**
- Modify: `installer/msix/build-rc-msix.ps1`
- Modify: `installer/README.md`
- Modify: `tools/publish-portable/publish-portable.ps1`
- Modify: `tools/publish-portable/README.md`
- Review: `src/Dobby.Host/appsettings.json`; modify only when portable/release profile separation uses a committed profile flag, otherwise record `no source config change required` in `remaining-work-status-audit.md`
- Modify: `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`
- Create: `tests/Dobby.SmokeTests/InstallerUpgradeSmokeTests.cs`
- Create: `tests/Dobby.SmokeTests/ReleaseCandidateSmokeTests.cs`
- Create/update release evidence files under `artifacts/release-evidence/local-rc-<UTC>-<shortsha>/`

**Behavior:**
- MSIX remains primary installer. RC self-signed mechanics are evidence only and must not be called GA/customer signing readiness unless release authority accepts it.
- Installer identity, versioning, signing certificate disposition, artifact hash, and upgrade path are recorded.
- Install, uninstall, upgrade, rollback where supported, and app data preservation are verified.
- Existing `%LocalAppData%/Dobby` data survives upgrade: users, credentials, permissions, license state, plugin state/cache/storage, settings, shell preferences, audit, protected identity.
- Installed artifact launch proof uses existing app data and confirms login-first startup.
- Portable artifact remains support/dev/offline-test scope and cannot masquerade as production installer output.
- Portable publish script enforces dev/support constraints through script/runtime marker/static scan, not only README text.

**Release-Gate Semantics:**

| Row | RC mechanics/evidence can establish | GA/customer readiness remains blocked when | Status-file removal rule |
|---:|---|---|---|
| 65 | MSIX-primary RC package mechanics, identity, versioning, certificate disposition, artifact hash, and `installer-verification.md` evidence exist for the same build run. | Customer/GA signing certificate, production package identity/profile, or release authority acceptance is absent. | Remove only when RC evidence passes and GA/customer signing readiness is not blocked; otherwise keep row 65 with the exact signing/identity blocker. |
| 66 | Install, upgrade from N to N+1, uninstall, and rollback/disposition evidence exists against the approved RC installer mechanics. | Evidence is only script construction or package staging without executed install/upgrade proof. | Remove only after executed installer upgrade evidence passes; otherwise keep row 66 with the missing execution proof. |
| 67 | Installer-specific data preservation evidence proves users, credentials, permissions, license state, plugin state/cache/storage, settings, shell preferences, audit, and protected identity survive upgrade. | Preservation is proven only against portable/publish output or non-installed runtime. | Remove only after installer-specific preservation proof passes; otherwise keep row 67 with the missing preservation scope. |
| 68 | Portable tooling enforces support/dev/offline-test constraints through script/runtime marker/static scan and cannot be mistaken for the production installer. | Portable output can still look like production installer output or uses production release profile without an explicit support/dev marker. | Remove when portable constraints are enforced and scanned; this row does not require GA installer signing. |
| 69 | Installed artifact launches with existing app data, shows login first, performs no forbidden pre-auth plugin/background/domain work, and reads existing data after auth. | Proof uses only `dotnet run`, portable output, or staged files without installed-artifact launch. | Remove only after installed-artifact launch proof passes; otherwise keep row 69 with the missing installed-launch evidence. |

Cross-release gates still affect final release claims even when a row's RC mechanics pass: production recovery key custody/handover, ADR-0042 performance baseline, production signing/cert/profile acceptance, and redaction/static-scan evidence. Evidence path exists is not the same as release-ready.

- [ ] **Step 1: Confirm installer authority gate**

Before changing installer files, confirm `HD-S11-003` or later decision records exact MSIX identity/tooling/signing mechanics. If only RC mechanics are approved, every evidence line must say RC mechanics and must not claim GA/customer signing readiness.

- [ ] **Step 2: Add installer smoke tests or manual-evidence parser**

Tests/evidence must cover:

- Fresh install.
- Upgrade from version N to N+1 with same identity.
- Uninstall behavior.
- Rollback behavior if supported by the chosen mechanics.
- Launch after install with an existing data root.
- Login-first first window.
- Data preservation assertions.

- [ ] **Step 3: Implement or complete MSIX package mechanics**

Use the approved identity, versioning, certificate, and artifact naming. Do not introduce WiX/MSI fallback unless a release decision requires it.

- [ ] **Step 4: Enforce portable dev/support constraints**

Update portable publish tooling so:

- Portable output contains an explicit support/dev marker.
- Production release static scan cannot treat portable output as primary installer.
- Script refuses production-signing/release flags that would imply installer equivalence.
- Output config/profile is explicit and documented.

- [ ] **Step 5: Add installed launch proof**

Run installed artifact with pre-existing app data in an isolated profile. Evidence must show:

- Login window appears first.
- No plugin activation/background/domain work runs before auth.
- Existing Owner can log in.
- Existing plugin/license/settings data remain readable after auth.

- [ ] **Step 6: Verify**

Run:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~PortablePublishScriptTests|FullyQualifiedName~ProductionReleasePackagingStaticScanTests"
dotnet test .\tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj --no-restore --filter "FullyQualifiedName~InstallerUpgradeSmokeTests|FullyQualifiedName~ReleaseCandidateSmokeTests"
.\tools\publish-portable\publish-portable.ps1 -NoRestore
git diff --check
```

Expected: installer evidence exists, portable constraints are enforced, and portable publish succeeds with a reported executable path.

---

## Task 12: Final Status File Cleanup, Full Verification, And Portable Refresh

**Rows targeted for closure:** final verification and status-removal gate for rows 1-69.

**Files:**
- Modify: `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`
- Create/update: `artifacts/release-evidence/local-rc-<UTC>-<shortsha>/remaining-work-status-audit.md`
- Modify: release evidence files touched by implementation tasks

**Behavior:**
- Completed rows are removed from `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`.
- If every row is verified complete, the file should state that there are no remaining incomplete product-preparation rows and include the verification commands/evidence paths.
- If any row remains blocked by a real external decision, keep only that row with a concrete blocker and exact decision/evidence dependency.
- A row may be removed only when code/test/evidence exists, required verification passed, and release-gate semantics do not mark it as GA/customer-blocked. Otherwise keep the row with exact blocker.

- [ ] **Step 1: One-shot coverage and status-removal audit**

Create or update `artifacts/release-evidence/local-rc-<UTC>-<shortsha>/remaining-work-status-audit.md`. This is a one-shot completion-control audit, not a permanent unit/integration test.

First, verify this plan still targets rows `1` through `69` exactly once by parsing only Task 2 through Task 11 rows in the "Remaining Row Targeting" table. Task 1 and Task 12 are not coverage sources. The parser must fail on missing rows, duplicate rows, or out-of-range row numbers:

```powershell
$planPath = "docs\superpowers\plans\2026-07-08-remaining-product-work-implementation.md"
$lines = Get-Content $planPath
$inTable = $false
$coverage = @{}
$outOfRange = New-Object System.Collections.Generic.List[int]

foreach ($line in $lines) {
    if ($line -eq "## Remaining Row Targeting") {
        $inTable = $true
        continue
    }

    if ($inTable -and $line -eq "## Work Lanes") {
        break
    }

    if (-not $inTable) {
        continue
    }

    if ($line -notmatch '^\| Task (?<task>\d+) \|(?<rows>.+)\|$') {
        continue
    }

    $task = [int]$Matches.task
    if ($task -lt 2 -or $task -gt 11) {
        continue
    }

    foreach ($match in [regex]::Matches($Matches.rows, '\b\d+\b')) {
        $row = [int]$match.Value
        if ($row -lt 1 -or $row -gt 69) {
            $outOfRange.Add($row)
            continue
        }

        $rowKey = [string]$row
        if (-not $coverage.ContainsKey($rowKey)) {
            $coverage[$rowKey] = New-Object System.Collections.Generic.List[int]
        }

        $coverage[$rowKey].Add($task)
    }
}

$missing = 1..69 | Where-Object { -not $coverage.ContainsKey([string]$_) }
$duplicates = $coverage.GetEnumerator() |
    Where-Object { $_.Value.Count -gt 1 } |
    Sort-Object { [int]$_.Key } |
    ForEach-Object { "row $($_.Key) in tasks $($_.Value -join ',')" }

if ($missing) {
    throw "Remaining Row Targeting Task 2-11 coverage is missing rows: $($missing -join ', ')"
}
if ($duplicates) {
    throw "Remaining Row Targeting Task 2-11 coverage has duplicates: $($duplicates -join '; ')"
}
if ($outOfRange.Count -gt 0) {
    throw "Remaining Row Targeting Task 2-11 coverage has out-of-range rows: $($outOfRange -join ', ')"
}
"Remaining Row Targeting Task 2-11 coverage targets rows 1-69 exactly once."
```

Then audit status-file removals. For every removed row, the audit must record:

1. code/test/evidence reference
2. verification command and result
3. release-gate status: not GA/customer-blocked, or exact blocker if blocked

A row may be removed only when code/test/evidence exists, required verification passed, and release-gate semantics do not mark it as GA/customer-blocked. Otherwise keep the row with exact blocker.

For each row `1` through `69`, record:

- Task that targeted and implemented it.
- Code/test/evidence files proving closure.
- Focused verification command and result.
- Release-gate status: `not GA/customer-blocked`, or the exact blocker and decision/evidence dependency.

- [ ] **Step 2: Update status file**

Remove only rows that pass Step 1's removal rule and renumber remaining rows from `1`. If no rows remain, replace the table with a short "all rows verified closed" summary and keep the verification evidence references. If any row is GA/customer-blocked, keep it in the table even when an RC mechanics path exists.

- [ ] **Step 3: Run full no-restore verification**

Run the strongest local no-restore verification that available assets allow:

```powershell
dotnet test .\Dobby.sln --no-restore
dotnet test .\Dobby.sln -c Release --no-restore
dotnet format .\Dobby.sln --verify-no-changes --no-restore
git diff --check
```

If Release no-restore cannot run because restore/build assets are missing, record the exact error and run the strongest passing no-restore subset.

- [ ] **Step 4: Run focused WPF/STA flake-sensitive verification**

Run the existing flake-sensitive tooling when local prerequisites allow:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 -Profile Release
```

If the tool cannot run locally, record why and run focused WPF shell/work-session tests at least once.

- [ ] **Step 5: Refresh portable build**

Run:

```powershell
.\tools\publish-portable\publish-portable.ps1 -NoRestore
```

Record the produced executable path in the implementation summary.

- [ ] **Step 6: Final static and artifact scan**

Run or verify existing scans for:

- No dev/QA trust material in production output.
- No sample packages/licenses in production output.
- No private keys or signing transport detail in repo/evidence.
- UTF-8/no-BOM for new docs/resources/evidence.
- Architecture/package-governance boundaries.
- Hardcoded visible shell strings.

- [ ] **Step 7: Final summary**

Final implementation summary must include:

- Remaining-row count after cleanup.
- Full verification commands and results.
- Portable executable path.
- Installer artifact path if produced.
- Any rows left due to external approval/evidence gates, with exact blocker.
