# Sprint 11 Execution Plan — Release Gates, Installer, Evidence, and Smoke

## 1. Sprint Summary

- Sprint number and title: Sprint 11, "Release Gates, Installer, Evidence, and Smoke" (`SPRINT_IMPLEMENTATION_PLAN.md`, Sprint 11 section).
- Main goal: "Prove P1 release readiness against documented acceptance criteria and release procedures" (`SPRINT_IMPLEMENTATION_PLAN.md`, Sprint 11 section).
- Expected deliverable: a release-candidate evidence package under `artifacts/release-evidence/<build-id>/`, Release build/test/format/static-scan results, installer/upgrade verification, release smoke logs, UI snapshot/performance evidence, and explicit disposition of every open release gate (`SPRINT_IMPLEMENTATION_PLAN.md`, Sprint 11; `docs/09_BUILD_RUN_AND_RELEASE.md`).
- Why this sprint exists: Sprints 1-10 produced the P1 host/runtime/security/plugin/licensing/admin foundations, and Sprint 11 is the consolidation sprint that proves them against AC-008, AC-014, AC-024, AC-041, AC-046, and AC-047 without taking ownership away from the earlier primary AC-owning sprints (`SPRINT_IMPLEMENTATION_PLAN.md`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md`).
- Readiness: **BLOCKED PENDING HUMAN DECISION for full release-candidate closure**. Planning is safe, but implementation must not claim release readiness until open release/key/performance/installer/CI gates in Section 13 are closed (`artifacts/release-evidence/skeleton-local/decision-log.md`, `D-31` and `D-33`; `src/Dobby.Host/Trust/README.md`; `docs/_perf/README.md`; `installer/README.md`; `tools/verify-flake-sensitive-tests/README.md`).
- Strategy:
  1. Preserve the current source/docs/spec/ADR boundary and add only release-gate implementation or evidence work mapped to Sprint 11 scope (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/08_CODING_STANDARDS.md`).
  2. Convert deferred Sprint 10 release evidence into concrete Sprint 11 release-candidate evidence only after production signing/key handoff authority is present (`artifacts/release-evidence/skeleton-local/decision-log.md`, `D-31`, `D-33`; `docs/adr/ADR-0033-signing-key-ownership.md`).
  3. Build evidence from real Release/publish/installer artifacts from the same run and keep unit, integration, smoke, static, UI, performance, and installer evidence separate (`docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-041).
  4. Prefer tests and deterministic evidence over manual claims; use manual/out-of-band evidence only where ADRs or release docs require external hardware, installer signing, or production key custody (`docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`; `docs/adr/ADR-0033-signing-key-ownership.md`).
- Dependency summary: Sprint 11 consumes Sprints 1-10, ADR-0042, ADR-0047, `docs/07_ACCEPTANCE_CRITERIA.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, `docs/_perf/README.md`, the current solution layout in `Dobby.sln`, the release-evidence skeleton, and the Sprint 10 admin/sample-plugin tooling. It unblocks P1 release-gate review, not a later feature sprint (`SPRINT_IMPLEMENTATION_PLAN.md`; `Dobby.sln`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md`).

## 2. Sources Read

Inspection depths: "deep" means the file's sprint-relevant content was read directly; "targeted" means relevant sections, symbols, tests, or decision markers were inspected; "inventory" means repository layout was inspected to prove presence or absence.

| Group | Sources | Depth | How it affected the plan |
| --- | --- | --- | --- |
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` | Deep | Defines Sprint 11 goal, scope, dependencies, AC ownership, exit conditions, and risks. |
| Previous sprint execution plans | `SPRINT_1_EXECUTION_PLAN.md`; `SPRINT_2_EXECUTION_PLAN.md`; `SPRINT_3_EXECUTION_PLAN.md`; `SPRINT_4_EXECUTION_PLAN.md`; `SPRINT_5_EXECUTION_PLAN.md`; `SPRINT_6_EXECUTION_PLAN.md`; `SPRINT_7_EXECUTION_PLAN.md`; `SPRINT_8_EXECUTION_PLAN.md`; `SPRINT_9_EXECUTION_PLAN.md`; `SPRINT_10_EXECUTION_PLAN.md` | Targeted continuity plus readiness/decision extraction | Identifies predecessor gates, implementation assumptions, and the Sprint 10 production/Admin recovery deferral that Sprint 11 must close or explicitly keep blocked. |
| Later sprint references | None discovered in repository root | Inventory | Sprint 11 is the final master-plan sprint; no later execution plan creates downstream scope. |
| Repo-root files | `AGENTS.md`; `Dobby.sln`; `global.json`; `Directory.Build.props`; `Directory.Packages.props`; `.editorconfig`; `.gitattributes`; `.gitignore`; repository root inventory | Deep/targeted/inventory | Establishes quality rules, project membership, SDK/package pins, warning/format constraints, and target output-file absence. |
| Product / architecture / requirements docs | `docs/02_ARCHITECTURE_MAP.md`; `docs/03_TECH_STACK_AND_VERSIONS.md`; `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md`; `docs/07_ACCEPTANCE_CRITERIA.md`; `docs/08_CODING_STANDARDS.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/_perf/README.md`; `docs/superpowers/plans/2026-06-23-flake-prevention.md` | Deep/targeted | Grounds release gates, AC mapping, package policy, UTF-8/format rules, UI responsiveness, no telemetry, no UI blocking, performance-baseline status, and flake-sensitive verification. |
| ADRs | `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md`; `docs/adr/ADR-0023-admin-host-boundary.md`; `docs/adr/ADR-0033-signing-key-ownership.md`; `docs/adr/ADR-0039-owner-password-recovery.md`; `docs/adr/ADR-0041-login-first-startup-policy.md`; `docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`; `docs/adr/ADR-0051-license-lifecycle-v2.md`; `docs/adr/ADR-0052-tenant-customer-context-v2.md`; `docs/adr/ADR-0053-capability-surface-runtime-mode.md`; `docs/adr/ADR-0054-db-at-rest-protection-provider.md`; `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` | Deep/targeted | Grounds signing custody, recovery, startup, performance, installer, license, runtime mode, and storage-provider evidence. |
| Specs | `docs/specs/admin-tool.md`; `docs/specs/audit-and-diagnostics.md`; `docs/specs/license-import.md`; `docs/specs/plugins-manager.md`; `docs/specs/sample-plugin.md`; `docs/specs/shell-ui.md`; `docs/specs/storage-and-data-protection.md`; `docs/specs/work-sessions.md` | Deep | Grounds release smoke workflows, admin artifact tools, diagnostics export, plugin manager, sample plugin, UI snapshots/accessibility, storage proof, and work-session behavior. |
| Schemas | `docs/schemas/dobby-plugin-manifest.schema.json`; `docs/schemas/dobby-plugin-license.schema.json` | Deep | Confirms plugin/package/license artifact contract and key-version constraints for release smoke and static scans. |
| Source code | `src/Dobby.Admin/Commands/AdminCommandHost.cs`; `src/Dobby.Host/Startup/PreLoginInstrumentation.cs`; `src/Dobby.Host/Startup/HostBootstrapper.cs`; `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`; `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`; `src/Dobby.Host/Plugins/HostPluginLifecycleCoordinator.cs`; `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`; `src/Dobby.Messaging/UiDispatch/IUiDispatcher.cs`; `src/Dobby.Shell/UiDispatch/WpfUiDispatcher.cs`; `src/Dobby.Infrastructure/Persistence/SqliteProviderInitializer.cs`; `src/Dobby.Infrastructure/Persistence/EncryptedSqliteConnectionFactory.cs`; `src/Dobby.Infrastructure/Persistence/HostPersistenceMigrator.cs`; `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs`; `src/Dobby.Infrastructure/ProtectedStorage/DpapiCurrentUserProtector.cs`; `src/Dobby.Infrastructure/ProtectedStorage/DpapiLocalMachineProtector.cs`; `src/Dobby.Infrastructure/ProtectedStorage/ProtectedMaterialAvailabilityProbe.cs`; `src/Dobby.Infrastructure/ProtectedStorage/ProtectedMaterialPaths.cs`; `src/Dobby.Security/Trust/PluginTrustPublicKeyProvider.cs`; `src/Dobby.Security/Trust/PluginTrustVerifier.cs`; `src/Dobby.Security/Trust/DobbyPluginTrustProfile.cs`; `src/Dobby.Security/Authentication/VendorRecoveryPublicKey.cs`; `src/Dobby.Security/Authentication/RecoveryRequestExporter.cs`; `src/Dobby.Security/Authentication/RecoveryResponseImporter.cs`; `src/Dobby.Host/Login/RecoveryResponseImportCoordinator.cs`; `src/Dobby.Host/Login/RecoveryRequestExportCoordinator.cs`; `src/Dobby.Core/Result.cs`; `src/Dobby.Audit/AuditEventName.cs`; `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`; `src/Dobby.Application/BackgroundOperationReporter.cs`; `src/Dobby.Host/Trust/README.md`; `src/Dobby.Host/Trust/vendor-master-public-key.pem`; `src/Dobby.Host/Trust/dev-qa-public-key.pem` | Deep/targeted | Confirms current implementation surfaces for admin tooling, login-first startup, plugin install/lifecycle/license import, UI dispatcher/background operations, encrypted storage, protected material, trust resources, recovery, Result/ErrorCode, audit events, and background operation contracts. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs`; `tests/Dobby.IntegrationTests/Architecture/Sprint10ReleaseEvidenceTests.cs`; `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`; `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`; `tests/Dobby.IntegrationTests/Architecture/AdminToolStaticScanTests.cs`; `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`; `tests/Dobby.SmokeTests/CrashRecoverySmokeTests.cs`; `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`; `tests/Dobby.SmokeTests/ShellSmokeTests.cs`; `tests/Dobby.IntegrationTests/Shell/LicenseImportShellSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`; `tests/Dobby.IntegrationTests/Shell/WpfIntegrationTestCollection.cs`; `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`; `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`; `tests/Dobby.IntegrationTests/Plugins/PluginIntegrationSuiteTests.cs`; `tests/Dobby.IntegrationTests/Licensing/LicenseImportIntegrationTests.cs`; `tests/Dobby.IntegrationTests/Licensing/LicenseRuntimeModeIntegrationTests.cs`; `tests/Dobby.IntegrationTests/Licensing/ReadOnlyExpiredEnforcementIntegrationTests.cs`; `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`; `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs`; `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`; `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs`; `tests/Dobby.IntegrationTests/Infrastructure/ProtectedMaterialDpapiTests.cs`; `tests/Dobby.IntegrationTests/Security/NoPlaintextSecretContentScanTests.cs`; `tests/Dobby.IntegrationTests/Recovery/RecoveryRequestExportTests.cs`; `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportConfirmTests.cs`; `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs`; `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs`; `tests/Dobby.UnitTests/Host/LicenseImportCoordinatorTests.cs`; `tests/Dobby.UnitTests/Host/HostPluginLifecycleCoordinatorTests.cs`; `tests/Dobby.UnitTests/Admin/AdminCommandHostTests.cs`; `tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs`; `tests/Dobby.UnitTests/Security/PluginTrustPublicKeyProviderTests.cs`; `tests/Dobby.UnitTests/Security/PluginTrustVerifierTests.cs` | Deep/targeted | Establishes current automated coverage and exact places where Sprint 11 should extend release evidence, smoke, static scans, startup/performance, installer, recovery, and UI responsiveness checks. |
| Project / config / build files | `src/Dobby.Host/Dobby.Host.csproj`; `src/Dobby.Admin/Dobby.Admin.csproj`; `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`; `src/Dobby.Security/Dobby.Security.csproj`; `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`; `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj`; `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`; `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` | Deep | Confirms TFM, WPF, native SQLite asset copying, trust-profile embedding, test project references, and sample-plugin package-candidate generation. |
| Tools / installer | `installer/README.md`; `tools/sign-plugin-package/sign-plugin-package.ps1`; `tools/create-plugin-license/create-plugin-license.ps1`; `tools/create-recovery-response/create-recovery-response.ps1`; `tools/verify-flake-sensitive-tests/README.md`; `tools/verify-flake-sensitive-tests/verify-flake-sensitive-tests.ps1` | Deep | Confirms installer work is currently only an ownership path, admin wrappers are canonical command entry points, and release/handover flake-sensitive verification already exists. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/architecture-baseline.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`; `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md` | Deep | Confirms predecessor implementation evidence, closed decisions, deferred production/Admin recovery gate, storage/KDF/toolchain proof, test-only recovery fixture boundary, and Sprint 10 packaging scan. |

Total inspected sources used by this plan: **139**. Total implementation assumptions in Section 7a: **6**.

## 3. Sprint Scope From Master Sprint Plan

- Master-plan goal: "Prove P1 release readiness against documented acceptance criteria and release procedures" (`SPRINT_IMPLEMENTATION_PLAN.md`, Sprint 11).
- Master-plan task groups:
  - Build and CI Gate: run documented restore/build/test/format/architecture graph/UTF-8/plugin-boundary/devQA trust checks, with evidence covering core build/static blockers (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`).
  - Installer and Upgrade: verify MSIX primary installer, MSI fallback only if required, data preservation on upgrade, and portable artifact support/dev-only behavior (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-024).
  - Smoke and Acceptance: execute release smoke for login-first, Owner bootstrap, user management, shell, plugin install/activation, license import/mode, work session, logout/lock, diagnostics/export, sample plugin, and AC-046 heartbeat during long-running plugin install/license import/storage operations (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-008, AC-041, AC-046).
  - Performance and UI Evidence: measure startup on the ADR-0042 baseline hardware profile, collect P95 evidence, capture shell snapshots, and verify responsiveness/accessibility/DPI/resolution (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/specs/shell-ui.md`).
  - Release Evidence Package: gather release evidence under documented artifact paths, including storage proof, KDF evidence, Sprint 5 TEST-ONLY recovery fixture closure, production vendor key handover/recovery-evidence replacement, smoke logs, screenshots, static scan results, and installer verification (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`).
- Acceptance / exit expectations:
  - Primary AC ownership for Sprint 11: AC-008, AC-014, AC-024, AC-041, AC-046, AC-047 (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/07_ACCEPTANCE_CRITERIA.md`).
  - AC-051 contribution: release-candidate `auth-kdf-selection.md` must justify PBKDF2 fallback by approved package status or ADR-0042 latency evidence (`SPRINT_IMPLEMENTATION_PLAN.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-051; `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`).
  - Exit condition: evidence is complete enough for P1 release-gate review, or gaps are explicitly blocked by documented human decisions (`SPRINT_IMPLEMENTATION_PLAN.md`, Sprint 11).
- Dependencies:
  - Sprints 1-10 are prerequisites (`SPRINT_IMPLEMENTATION_PLAN.md`).
  - ADR-0042, ADR-0047, `docs/07_ACCEPTANCE_CRITERIA.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, and `docs/_perf/README.md` are explicit Sprint 11 dependencies (`SPRINT_IMPLEMENTATION_PLAN.md`).
- Later sprints depending on this sprint: no later sprint execution plans were discovered, and the master plan lists Sprint 11 as the release-gate closeout sprint (`SPRINT_IMPLEMENTATION_PLAN.md`; repository root inventory).
- Master-plan explicit out-of-scope boundaries:
  - Sprint 11 consolidates evidence and must not reassign primary AC ownership from earlier implementation sprints (`SPRINT_IMPLEMENTATION_PLAN.md`).
  - MSI fallback is not required unless a release decision requires it; portable support remains support/dev-only and non-blocking if absent (`docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`).

## 4. Prior Sprint Continuity Check

| Sprint | Verified predecessor output | Missing / carried forward to Sprint 11 |
| --- | --- | --- |
| Sprint 1 | Found `SPRINT_1_EXECUTION_PLAN.md` and skeleton release evidence baseline under `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. Current solution still carries architecture/static guardrail tests in `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` and `ReleaseEvidenceTests.cs`. | No Sprint 11 redo. Use current architecture evidence as baseline only (`SPRINT_IMPLEMENTATION_PLAN.md`). |
| Sprint 2 | Core contracts and shared primitives are present in `src/Dobby.Core/Result.cs`, `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`, and solution membership in `Dobby.sln`. | No new `ErrorCode` or contract widening without authority (`tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `docs/08_CODING_STANDARDS.md`). |
| Sprint 3 | Diagnostics/audit/runtime folders are present through `src/Dobby.Audit/AuditEventName.cs`, `src/Dobby.Application/BackgroundOperationReporter.cs`, runtime tests in `tests/Dobby.IntegrationTests/Runtime/`, and diagnostics export tests in `tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs`. | Sprint 11 uses diagnostics/export in smoke and evidence; it must not add telemetry (`docs/specs/audit-and-diagnostics.md`; `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md`, NFR-020). |
| Sprint 4 | Protected storage and encrypted persistence are present in `src/Dobby.Infrastructure/Persistence/EncryptedSqliteConnectionFactory.cs`, `SqliteProviderInitializer.cs`, DPAPI protectors, and `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`. Storage materialization evidence records provider/package/hash approval (`artifacts/release-evidence/skeleton-local/storage-materialization.md`). | Sprint 11 must regenerate release-candidate storage proof under `artifacts/release-evidence/<build-id>/storage-provider-proof.md` (`docs/09_BUILD_RUN_AND_RELEASE.md`). |
| Sprint 5 | Login-first auth, sessions, and owner recovery exist in recovery source/tests and KDF evidence (`src/Dobby.Security/Authentication/RecoveryResponseImporter.cs`; `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs`; `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`). | Sprint 5 TEST-ONLY recovery fixtures remain regression-only; Sprint 11 must close production/Admin recovery evidence via vendor handover or keep release blocked (`artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`, `D-33`). |
| Sprint 6 | Operation authorization, users, and settings are present in security tests and host login runtime tests (`tests/Dobby.UnitTests/Security/OperationAuthorizationPolicyTests.cs`; `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs`). | Sprint 11 smoke must cover Owner bootstrap/user management without changing operation-target closed sets (`docs/specs/shell-ui.md`; `src/Dobby.Core/Result.cs`). |
| Sprint 7 | Shell/navigation/theme/work-session surfaces are present in `src/Dobby.Shell/`, `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`, and `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`. | Sprint 11 adds release UI snapshots/responsiveness evidence and uses flake-sensitive guardrails for WPF/STA tests (`docs/specs/shell-ui.md`; `tools/verify-flake-sensitive-tests/README.md`). |
| Sprint 8 | Plugin package trust and activation runtime are present through trust resources, `src/Dobby.Security/Trust/`, plugin tests, and sample plugin package candidate generation (`samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`; `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`). | Sprint 8 bootstrap keys must rotate before production release, and dev/QA trust must be excluded from production publish (`src/Dobby.Host/Trust/README.md`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md`). |
| Sprint 9 | Licensing/runtime mode state exists in license schemas, source/tests, and Sprint 9 release evidence (`docs/schemas/dobby-plugin-license.schema.json`; `tests/Dobby.IntegrationTests/Licensing/LicenseImportIntegrationTests.cs`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md`). | Sprint 11 release smoke must cover license import/mode and ReadOnlyExpired policy without inventing new license states (`docs/specs/license-import.md`; `docs/adr/ADR-0051-license-lifecycle-v2.md`). |
| Sprint 10 | Plugin manager, sample plugin, and admin artifact tooling exist in source/tests/tools and Sprint 10 evidence (`src/Dobby.Admin/Commands/AdminCommandHost.cs`; `tools/sign-plugin-package/sign-plugin-package.ps1`; `tests/Dobby.SmokeTests/ShellSmokeTests.cs`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md`). | Production/Admin `.dobbyrecovery` release evidence is explicitly deferred to Sprint 11/vendor handover and must not be claimed from Sprint 10 technical-path evidence (`SPRINT_10_EXECUTION_PLAN.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`, `D-31`, `D-33`). |

Readiness conclusion: predecessor code/tests/evidence are present enough to plan Sprint 11, but the release-candidate claim remains blocked by open human authority gates in Section 13.

## 5. Current Codebase State

| Target-sprint area | Current files / surfaces | Sprint 11 implication |
| --- | --- | --- |
| Solution and project graph | `Dobby.sln` contains `src`, `tests`, and `samples` projects including `Dobby.Host`, `Dobby.Admin`, `Dobby.IntegrationTests`, `Dobby.SmokeTests`, and `Dobby.Sample.Plugin`. | Release commands and evidence must target the current solution/project graph, not a new release-only solution (`Dobby.sln`; `docs/09_BUILD_RUN_AND_RELEASE.md`). |
| Host startup / login-first | `src/Dobby.Host/Startup/HostBootstrapper.cs` and `PreLoginInstrumentation.cs` record login-first milestones and forbidden pre-login workload checks; tests exist in `tests/Dobby.IntegrationTests/Startup/`. | AC-008 and AC-014 evidence must use these surfaces and avoid moving plugin/storage-heavy work before login visible (`docs/adr/ADR-0041-login-first-startup-policy.md`; `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`). |
| Admin CLI/artifact tools | `src/Dobby.Admin/Commands/AdminCommandHost.cs` implements `sign-plugin-package`, `create-plugin-license`, and `create-recovery-response`; wrappers live under `tools/`. | Sprint 11 can generate RC admin artifacts through existing commands, but production key custody and output evidence must come from an approved handover (`docs/specs/admin-tool.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`, `D-30`, `D-33`). |
| Recovery/trust | Host recovery verification uses `VendorRecoveryPublicKey` backed by `PluginTrustPublicKeyProvider`; production/devQA public PEMs are embedded through `src/Dobby.Security/Dobby.Security.csproj`. | Release recovery proof must use production/vendor authority, not test-only fixtures or devQA private material (`src/Dobby.Host/Trust/README.md`; `docs/adr/ADR-0039-owner-password-recovery.md`; `docs/adr/ADR-0033-signing-key-ownership.md`). |
| Plugin manager/install/runtime | Plugin install, lifecycle, license import, activation, and sample plugin tests are present in `src/Dobby.Host/Plugins/`, `tests/Dobby.IntegrationTests/Plugins/`, and `tests/Dobby.SmokeTests/ShellSmokeTests.cs`. | Release smoke should extend the current flow instead of inventing another plugin host path (`docs/specs/plugins-manager.md`; `docs/specs/sample-plugin.md`). |
| License import/mode | `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`, `docs/schemas/dobby-plugin-license.schema.json`, and licensing integration tests cover signed license import and runtime mode. | Sprint 11 must smoke the import/mode path and preserve ReadOnlyExpired install/import denial policy (`docs/specs/license-import.md`; `docs/adr/ADR-0053-capability-surface-runtime-mode.md`). |
| UI responsiveness / background operations | `ShellBackgroundOperationCoordinator` posts reporter updates through `IUiDispatcher.Post`; WPF dispatcher uses `BeginInvoke`; unit tests verify posted background operation updates. | AC-046 release smoke must prove plugin install, license import, and storage operations keep a heartbeat visible and do not block the UI dispatcher (`src/Dobby.Host/ShellBackgroundOperationCoordinator.cs`; `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-046). |
| Persistence / protected storage | Encrypted SQLite provider, provider initializer, DPAPI protected material, and migrations exist in `src/Dobby.Infrastructure/`; persistence tests cover cipher guardrails, protected material, transaction rollback, and crash recovery. | Sprint 11 must regenerate storage proof and installer-upgrade preservation evidence rather than changing storage policy (`docs/specs/storage-and-data-protection.md`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`). |
| UI snapshots/accessibility | `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`, `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`, and `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs` cover shell layout, DPI/resolution, keyboard, labels, and theme tokens. | Sprint 11 should attach release-candidate screenshots/snapshot descriptors and accessibility evidence to the evidence package (`docs/specs/shell-ui.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-014, AC-046). |
| Installer | `installer/README.md` is the only installer ownership artifact; no `.wapproj`, `.appxmanifest`, `.wixproj`, `.wxs`, `.msix`, or installer project file was found in the inspected tree. | Exact MSIX package identity/signing/tooling is an open release authority gate before installer implementation can safely create packaging files (`installer/README.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`). |
| CI | No `.github`, Azure Pipelines, or other CI workflow file was found by inventory; flake-sensitive tooling warns not to create a CI platform without ownership. | The "Build and CI Gate" can be planned, but CI platform/workflow creation requires human authority if repository CI ownership is absent (`tools/verify-flake-sensitive-tests/README.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`). |

Mismatch summary:

- The release docs require `artifacts/release-evidence/<build-id>/` RC artifacts, while current evidence is skeleton/local and Sprint 10 local evidence (`docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/skeleton-local/`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md`).
- The master plan requires installer verification, but the repo currently only has an installer ownership README, not concrete MSIX packaging (`SPRINT_IMPLEMENTATION_PLAN.md`; `installer/README.md`).
- ADR-0042 requires accepted baseline measurement artifacts, while `docs/_perf/README.md` states the perf area is future scope until the Phase 1 measurement process exists and is accepted (`docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/_perf/README.md`).

## 6. Documentation and ADR Alignment

### Documents Fully Aligned For Sprint 11

- `docs/07_ACCEPTANCE_CRITERIA.md`: defines the ACs Sprint 11 must prove, including AC-008, AC-014, AC-024, AC-041, AC-046, AC-047, and AC-051.
- `docs/09_BUILD_RUN_AND_RELEASE.md`: defines canonical build/test/format/publish/release evidence paths and minimum evidence artifacts.
- `docs/specs/admin-tool.md`: aligns with current Admin CLI commands and wrappers.
- `docs/specs/audit-and-diagnostics.md`: aligns with no-telemetry diagnostics/export boundaries for release smoke.
- `docs/specs/plugins-manager.md`, `docs/specs/sample-plugin.md`, and `docs/specs/license-import.md`: align with current plugin/admin/sample/license smoke surfaces.
- `docs/specs/storage-and-data-protection.md`: aligns with current encrypted SQLite/provider/protected material evidence.
- `docs/specs/shell-ui.md`: aligns with snapshot/DPI/accessibility/UI responsiveness release evidence.

### Documents Requiring Only Implementation-Time Evidence Appends

- `artifacts/release-evidence/<build-id>/auth-kdf-selection.md`, `storage-provider-proof.md`, `packaging-static-scan.md`, and `startup-performance.md` must be generated per `docs/09_BUILD_RUN_AND_RELEASE.md`; current skeleton evidence must not be edited as a substitute.
- Sprint 11 evidence must append or create release-candidate evidence, not change the master sprint plan, predecessor plans, docs, ADRs, specs, or schemas during planning (`SPRINT_IMPLEMENTATION_PLAN.md`; attached user instructions).

### Documents That May Need Update Notes After Implementation

- `installer/README.md` may need replacement or supplement only when MSIX packaging ownership and exact package identity/tooling are approved (`installer/README.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`).
- `docs/_perf/README.md` may need a baseline artifact note only after the Phase 1 measurement process is accepted (`docs/_perf/README.md`; `docs/adr/ADR-0042-release-gate-hardware-baseline.md`).

### ADR Alignment Classification

| ADR | Classification | Sprint 11 handling |
| --- | --- | --- |
| `ADR-0019` | No action | Preserve audit/diagnostics separation and no telemetry in diagnostics export evidence (`docs/specs/audit-and-diagnostics.md`). |
| `ADR-0023` | No action | Keep admin tooling in `Dobby.Admin`; do not route admin artifact creation through Host UI (`docs/specs/admin-tool.md`; `tests/Dobby.IntegrationTests/Architecture/AdminToolStaticScanTests.cs`). |
| `ADR-0033` | Human decision required | Production cryptographic key custody and vendor handover remain external authority gates (`artifacts/release-evidence/skeleton-local/decision-log.md`, `D-31`, `D-33`). |
| `ADR-0039` | Aligned, release evidence pending | Current recovery code/tests exist, but production/Admin `.dobbyrecovery` RC proof must replace TEST-ONLY fixture evidence (`src/Dobby.Security/Authentication/RecoveryResponseImporter.cs`; `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`). |
| `ADR-0041` | No action | Use current startup instrumentation and tests; do not move heavy work pre-login (`src/Dobby.Host/Startup/PreLoginInstrumentation.cs`; `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`). |
| `ADR-0042` | Human decision required | Baseline hardware/run process must be accepted before AC-014 release performance can pass (`docs/_perf/README.md`). |
| `ADR-0045` | No action | Preserve `.dobbyplugin`/`.dobbylicense` separation and schema/key-version constraints in release smoke (`docs/schemas/dobby-plugin-manifest.schema.json`; `docs/schemas/dobby-plugin-license.schema.json`). |
| `ADR-0047` | Human decision required for concrete packaging | MSIX primary is accepted; exact package identity/signing/tooling is not present in current repo (`installer/README.md`). |
| `ADR-0051` | No action | License lifecycle v2 is current; release smoke must prove current mode handling and avoid new license states. |
| `ADR-0052` | No action | Customer/deployment binding remains license evidence scope; no new binding storage surface. |
| `ADR-0053` | No action | Runtime mode gates already exist; Sprint 11 smokes them. |
| `ADR-0054` | No action | Native encrypted SQLite provider proof must be regenerated for RC evidence. |
| `ADR-0055` | No action | Protected material scope/key wrapping remains unchanged; installer upgrade must preserve it. |

### Specs and Schemas Alignment

- Specs are implementation-aligned enough for Sprint 11; no spec/schema edits are planned (`docs/specs/*`; `docs/schemas/*`).
- The schema key-version const values remain `1`; changing them would require a future signing-key ADR amendment and is out of scope (`docs/schemas/dobby-plugin-manifest.schema.json`; `docs/schemas/dobby-plugin-license.schema.json`; `docs/adr/ADR-0033-signing-key-ownership.md`).

Net documentation footprint summary: Sprint 11 should create release-candidate evidence artifacts and, only after approved implementation, possibly update installer/perf ownership notes. It must not rewrite docs/ADRs/specs/schemas as part of planning.

## 7. Files to Create

Naming rule: every new file must map to a documented role in the master plan, an ADR, a spec, current evidence, or current code. If implementation discovers an unavoidable new name without such support, stop and record it as an Implementation assumption with source, risk, and fallback.

| File / placement | Purpose | Source basis | Associated tests / checks | Boundary restrictions |
| --- | --- | --- | --- | --- |
| `artifacts/release-evidence/<build-id>/auth-kdf-selection.md` | Release-candidate KDF selection and AC-051 evidence replacing skeleton-local timing for the RC build. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-051; `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md` | `tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs`; release performance evidence from ADR-0042 run | No Argon2/new package unless approved in pin gate. |
| `artifacts/release-evidence/<build-id>/storage-provider-proof.md` | RC proof for encrypted SQLite provider, native DLL path/hash, cipher guardrails, protected material, and no plaintext storage. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/specs/storage-and-data-protection.md`; `artifacts/release-evidence/skeleton-local/storage-materialization.md` | `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`; `tests/Dobby.IntegrationTests/Infrastructure/ProtectedMaterialDpapiTests.cs`; `tests/Dobby.SmokeTests/CrashRecoverySmokeTests.cs` | No storage-provider switch or unprotected DB fallback. |
| `artifacts/release-evidence/<build-id>/packaging-static-scan.md` | RC static scan for production publish/installer/sample/devQA trust exclusion and forbidden artifacts. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md`; `src/Dobby.Host/Trust/README.md` | `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`; `PluginTrustResourcePackagingTests.cs` | No devQA private material or sample plugin in production output. |
| `artifacts/release-evidence/<build-id>/startup-performance.md` | ADR-0042 P95 startup baseline evidence for cold/warm/login-to-shell and regression boundary. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/_perf/README.md` | New/updated performance evidence harness per IA-S11-005; startup tests in `tests/Dobby.IntegrationTests/Startup/` | Human-approved hardware/process required. |
| `artifacts/release-evidence/<build-id>/installer-verification.md` | MSIX install/uninstall/upgrade/data-preservation evidence; MSI fallback only if required. | `SPRINT_IMPLEMENTATION_PLAN.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md` | New installer smoke/manual evidence per IA-S11-004 | Exact installer files require HD-S11-003. |
| `artifacts/release-evidence/<build-id>/unit-integration-smoke-results.md` | Separate Release unit/integration/smoke result summary for AC-041. | `docs/07_ACCEPTANCE_CRITERIA.md`, AC-041; `docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | `dotnet test` evidence from Unit, Integration, Smoke projects | Do not merge isolated profile evidence with unit results. |
| `artifacts/release-evidence/<build-id>/release-smoke.md` | Human-readable release smoke transcript for login-first, Owner bootstrap, user management, shell, plugin, license, work sessions, logout/lock, diagnostics/export, sample plugin. | `SPRINT_IMPLEMENTATION_PLAN.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/specs/sample-plugin.md` | `tests/Dobby.SmokeTests/ShellSmokeTests.cs`; `ShellKeyboardSmokeTests.cs`; new release smoke per IA-S11-003 | Smoke must run against publish/installer artifact from same run. |
| `artifacts/release-evidence/<build-id>/ui-snapshots/` | Screenshot/snapshot outputs for shell layouts, DPI/resolution, accessibility, and responsiveness evidence. | `SPRINT_IMPLEMENTATION_PLAN.md`; `docs/specs/shell-ui.md`; `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs` | `ShellSnapshotTests.cs`; `ShellUiStaticScanTests.cs`; `ShellKeyboardSmokeTests.cs` | Do not add visual claims without captured artifact or test descriptor. |
| `artifacts/release-evidence/<build-id>/production-recovery-handover.md` | Production/Admin AC-007 recovery handover proof replacing Sprint 5 TEST-ONLY fixture closure. | `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`, `D-33`; `docs/adr/ADR-0033-signing-key-ownership.md` | `tests/Dobby.UnitTests/Admin/AdminCommandHostTests.cs`; `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportConfirmTests.cs`; release smoke using production-approved material | Blocked by HD-S11-001; no production private key in repo/source/test/evidence. |
| `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs` | Assert Sprint 11 RC evidence files and release-gate markers exist and no deferred Sprint 10 claim remains. | Existing `Sprint5`-`Sprint10` release-evidence test pattern; `tests/Dobby.IntegrationTests/Architecture/Sprint10ReleaseEvidenceTests.cs`; `docs/09_BUILD_RUN_AND_RELEASE.md` | Architecture/integration tests | See IA-S11-002; if rejected, extend `ReleaseEvidenceTests.cs` instead. |
| `tests/Dobby.SmokeTests/ReleaseCandidateSmokeTests.cs` | End-to-end release smoke against the publish/installer artifact from the same run. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `tests/Dobby.SmokeTests/ShellSmokeTests.cs`; `SPRINT_IMPLEMENTATION_PLAN.md` | Smoke tests | See IA-S11-003; do not duplicate existing shell smoke without adding RC artifact binding. |
| `tests/Dobby.SmokeTests/InstallerUpgradeSmokeTests.cs` | Automated installer/upgrade preservation smoke when MSIX tooling and package identity are approved. | `docs/07_ACCEPTANCE_CRITERIA.md`, AC-024; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`; `installer/README.md` | Smoke/manual installer verification | See IA-S11-004; do not invent package identity. |

## 7a. Implementation Assumptions for New Files

### IA-S11-001: Build ID Source For `artifacts/release-evidence/<build-id>/`

- Assumption: the implementation agent may use a deterministic CI build ID, release run ID, or timestamped local RC ID for `<build-id>` when no CI build identifier exists.
- Cited source: `docs/09_BUILD_RUN_AND_RELEASE.md` defines the path shape but not the ID source; no CI workflow file was found in repository inventory.
- Risk if wrong: evidence may be hard to trace to the exact publish/installer artifact.
- Fallback: stop and obtain a release-owner decision for the canonical build ID and evidence-root naming.

### IA-S11-002: Sprint 11 Evidence Test File Name

- Assumption: create `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs` following the existing `Sprint5ReleaseEvidenceTests.cs` through `Sprint10ReleaseEvidenceTests.cs` pattern.
- Cited source: `tests/Dobby.IntegrationTests/Architecture/Sprint10ReleaseEvidenceTests.cs`; `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs`.
- Risk if wrong: architecture tests may fragment release-evidence assertions.
- Fallback: extend `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs` instead and record the filename decision in release evidence.

### IA-S11-003: Release-Candidate Smoke Test Placement

- Assumption: create `tests/Dobby.SmokeTests/ReleaseCandidateSmokeTests.cs` only for smoke flows that bind to the same publish/installer artifact; keep existing shell/admin/sample smoke in `ShellSmokeTests.cs` where no artifact binding is needed.
- Cited source: `docs/09_BUILD_RUN_AND_RELEASE.md` requires smoke against the publish/installer artifact from the same CI run; current smoke coverage lives in `tests/Dobby.SmokeTests/ShellSmokeTests.cs`.
- Risk if wrong: duplicate smoke tests may increase maintenance cost without stronger release evidence.
- Fallback: add artifact-binding cases to `ShellSmokeTests.cs` and document the coverage in `release-smoke.md`.

### IA-S11-004: Installer Upgrade Smoke File

- Assumption: create `tests/Dobby.SmokeTests/InstallerUpgradeSmokeTests.cs` only after the MSIX package identity/signing/tooling decision is closed.
- Cited source: `installer/README.md` says concrete packaging is deferred; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md` makes MSIX primary.
- Risk if wrong: tests could encode an unauthorized installer identity or toolchain.
- Fallback: keep AC-024 as manual/out-of-band evidence in `installer-verification.md` until HD-S11-003 closes.

### IA-S11-005: ADR-0042 Measurement Harness Location

- Assumption: the implementation may add a small smoke/performance harness or test wrapper in the existing test/tooling surface to collect N=100 startup samples, but exact placement must be chosen after HD-S11-002 accepts the measurement process.
- Cited source: `docs/adr/ADR-0042-release-gate-hardware-baseline.md` defines measurement requirements; `docs/_perf/README.md` says the perf artifact location is future scope until accepted.
- Risk if wrong: performance evidence may not be reviewable or comparable to baseline.
- Fallback: stop and obtain release-performance owner approval for harness placement and artifact format.

### IA-S11-006: Additional Evidence File Names Beyond The Minimum Four

- Assumption: `installer-verification.md`, `unit-integration-smoke-results.md`, `release-smoke.md`, `ui-snapshots/`, and `production-recovery-handover.md` are acceptable release-evidence names because the master plan requires those evidence classes but only `docs/09_BUILD_RUN_AND_RELEASE.md` names the minimum four files.
- Cited source: `SPRINT_IMPLEMENTATION_PLAN.md` release evidence package task group; `docs/09_BUILD_RUN_AND_RELEASE.md` minimum evidence list.
- Risk if wrong: release review may reject the artifact layout.
- Fallback: obtain release-owner naming approval and update Section 7 entries before implementation.

## 8. Files to Modify

| File | Exact change scope | Reason | Boundary impact | Required tests |
| --- | --- | --- | --- | --- |
| `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs` | Update the deferred Sprint 10 recovery assertion once HD-S11-001 closes; add production publish/installer/devQA exclusion assertions for the RC evidence root. | Sprint 10 currently asserts production/Admin recovery evidence remains deferred; Sprint 11 must either close or explicitly block it. | Architecture/static scan only; no source behavior. | Integration architecture tests; `packaging-static-scan.md`. |
| `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs` or `Sprint11ReleaseEvidenceTests.cs` | Assert required RC evidence files/markers exist and link to AC-008/014/024/041/046/047/051. | `docs/09_BUILD_RUN_AND_RELEASE.md` and Sprint 11 require named evidence. | Test/evidence contract only. | Release evidence tests. |
| `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` | Include newly created release evidence and installer files in UTF-8 no-BOM checks if current glob excludes them. | AC-047 applies to docs/source/resource text. | No production behavior. | Encoding boundary tests. |
| `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | Add guard only if implementation tries to add packages; otherwise leave unchanged. | New packages require approved pin decisions. | Protects package governance. | Package governance tests. |
| `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs` | Add RC production trust/devQA exclusion evidence checks if packaging outputs are inspectable. | Production publish must include only the selected trust profile and no devQA anchor. | Security packaging boundary. | Plugin trust resource packaging tests; `packaging-static-scan.md`. |
| `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs` | Add AC-046/static UI-operation evidence checks only where static structure is relevant. | Shell already reports background operations; release gate must prove visibility/heartbeat. | UI static scan only. | Shell UI static scan tests. |
| `tests/Dobby.SmokeTests/ShellSmokeTests.cs` | Extend existing smoke only for missing release smoke steps that do not require a separate RC artifact-bound file. | Current smoke already covers admin CLI and sample plugin; Sprint 11 needs broader release acceptance flow. | Smoke-only. | Smoke tests; `release-smoke.md`. |
| `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs` | Extend only if release accessibility/keyboard smoke gaps remain after shell snapshot evidence. | Shell UI spec requires keyboard/accessibility evidence. | Smoke-only. | Smoke tests; UI evidence. |
| `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs` | Add or wire release snapshot descriptors/artifact capture if current descriptors do not emit evidence files. | Sprint 11 requires screenshots/shell snapshots. | WPF test harness only; preserve `WpfIntegrationTestCollection`. | Shell snapshot tests; flake-sensitive release profile. |
| `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs` | Add heartbeat/progress assertions if current posted-operation tests do not prove AC-046 flow. | AC-046 needs long-running operation heartbeat. | No production surface widening. | Unit tests plus release smoke. |
| `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs` | Modify only if tests prove heartbeat/progress is not surfaced through current dispatcher/reporting path. | Avoid speculative UI-thread fixes. | Host shell only; no plugin contract changes. | Unit tests and AC-046 smoke. |
| `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs` | Modify only if AC-046 smoke proves plugin install blocks UI or lacks background operation reporting/cancellation. | Long-running plugin install is in Sprint 11 AC-046 scope. | No new audit events/error codes without authority. | Unit/integration/smoke tests. |
| `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs` | Modify only if AC-046 smoke proves license import blocks UI or lacks reporting/cancellation. | Long-running license import is in Sprint 11 AC-046 scope. | Preserve license import policy and audit taxonomy. | `LicenseImportCoordinatorTests.cs`; release smoke. |
| `src/Dobby.Infrastructure/Persistence/EncryptedSqliteConnectionFactory.cs` and related persistence tests | Modify only if storage-operation smoke proves UI blocking or RC storage proof exposes a real provider defect. | AC-046 includes storage operations; storage proof is a release gate. | No provider/policy change without ADR/pin authority. | Persistence tests; storage proof; crash recovery smoke. |
| `installer/` files | Create/modify only after HD-S11-003 closes exact MSIX package identity/signing/tooling. | Current installer scope is deferred ownership path. | Release packaging boundary. | Installer upgrade smoke/manual evidence. |
| `tools/verify-flake-sensitive-tests/README.md` or script | Modify only if release closure requires a documented command profile change and ownership exists. | Existing release profile already exists. | Tooling only; no CI creation without authority. | Script smoke plus release profile output. |

## 9. Files Not Touched

- `SPRINT_IMPLEMENTATION_PLAN.md`: master plan authority; Sprint 11 implementation must not rewrite scope (`attached prompt`; `SPRINT_IMPLEMENTATION_PLAN.md`).
- `SPRINT_1_EXECUTION_PLAN.md` through `SPRINT_10_EXECUTION_PLAN.md`: predecessor plans are evidence/context only and must not be edited (`attached prompt`).
- `docs/adr/*.md`: ADR outcomes, signing custody, KDF, storage, installer, and performance baseline decisions require human authority, not sprint implementation edits (`docs/adr/ADR-0033-signing-key-ownership.md`; `docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`).
- `docs/specs/*.md` and `docs/schemas/*.json`: specs/schemas are current intended behavior and should not be changed to make release evidence pass (`docs/08_CODING_STANDARDS.md`; `docs/specs/*`; `docs/schemas/*`).
- `src/Dobby.Core/Result.cs` closed `ErrorCode` set and `src/Dobby.Audit/AuditEventName.cs` audit event taxonomy: no new closed-set entries unless an existing decision-log entry authorizes them (`tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `docs/specs/audit-and-diagnostics.md`).
- `src/Dobby.Contracts/`, `src/Dobby.SDK/`, and plugin-facing contracts: release-gate work should not widen plugin runtime or SDK surfaces (`docs/02_ARCHITECTURE_MAP.md`; `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`).
- Production private keys and signing material: never commit or generate in repo, tests, release evidence, publish output, or installer output (`docs/adr/ADR-0033-signing-key-ownership.md`; `src/Dobby.Host/Trust/README.md`).
- `samples/Dobby.Sample.Plugin/` production bundling: sample plugin remains a dev/QA release verification artifact, not bundled production feature (`docs/specs/sample-plugin.md`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md`).
- Marketplace/update/repair/downgrade/remote install flows: not in P1 Sprint 11 scope (`docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`).
- CI workflow/platform files: do not create `.github`, Azure Pipelines, or another CI platform without repository CI ownership or human approval (`tools/verify-flake-sensitive-tests/README.md`).

## 10. Error / Result / Policy Mapping

Sprint 11 should not add new `ErrorCode` values, audit event names, operation verbs, license states, plugin schema fields, or admin CLI commands. It is a release-gate/evidence sprint using existing policy surfaces.

| Sprint 11 surface | Existing policy/result/audit source | Required mapping |
| --- | --- | --- |
| Admin artifact failures | `src/Dobby.Admin/Commands/AdminCommandHost.cs`; `docs/specs/admin-tool.md` | Preserve exit codes `64`, `65`, `66`, `70`; stdout/stderr remain redacted. |
| Plugin install/reject | `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`; `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`; `src/Dobby.Audit/AuditEventName.cs` | Use existing plugin install attempted/verified/rejected/signature/trust audit events and existing `Result` errors. |
| License import/mode | `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`; `docs/specs/license-import.md`; `docs/adr/ADR-0051-license-lifecycle-v2.md` | Use existing license import attempted/succeeded/rejected and license enforcement events; preserve ReadOnlyExpired denial semantics. |
| Recovery import/export | `src/Dobby.Host/Login/RecoveryRequestExportCoordinator.cs`; `RecoveryResponseImportCoordinator.cs`; `docs/adr/ADR-0039-owner-password-recovery.md` | Use `recovery.export` and `recovery.import`; UI gets non-disclosing outcome while audit/diagnostics keep approved details. |
| Diagnostics/export | `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Audit/AuditEventName.cs` | Use explicit local diagnostics export only; no telemetry/upload client. |
| UI heartbeat/background operations | `src/Dobby.Contracts/BackgroundOperations/BackgroundOperationContracts.cs`; `src/Dobby.Application/BackgroundOperationReporter.cs`; `src/Dobby.Host/ShellBackgroundOperationCoordinator.cs` | Use existing `BackgroundOperationSnapshot` statuses/progress; do not add plugin contract surface unless a decision authorizes it. |

Local sub-code semantics:

- Installer/static/performance evidence may use release-evidence headings and support codes inside Markdown artifacts, but must not widen `Dobby.Core.ErrorCode` or audit event closed sets (`src/Dobby.Core/Result.cs`; `src/Dobby.Audit/AuditEventName.cs`).
- UI messages remain non-disclosing; full file paths, key details, stack traces, and raw secrets belong only in redacted diagnostics/evidence where allowed (`docs/specs/audit-and-diagnostics.md`; `docs/specs/admin-tool.md`).

Plugin-host boundary statement: release smoke may install/activate the sample plugin and inspect host manager behavior, but must not add plugin references from production projects or host internals into `Dobby.Contracts`/`Dobby.SDK` (`tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`; `artifacts/release-evidence/sprint10-local-2026-06-20/packaging-static-scan.md`).

Result-vs-exception split: domain failures remain `Result`/existing error values; IO/SQLite/dispatcher/test harness failures may throw inside implementation or tests only where existing code already does so (`src/Dobby.Core/Result.cs`; `src/Dobby.Infrastructure/Persistence/EncryptedSqliteConnectionFactory.cs`).

## 11. Technology Stack and Versions

- SDK: `.NET SDK 10.0.202` with `latestPatch` roll-forward (`global.json`).
- Language/build: C# `14.0`, nullable enabled, implicit usings enabled, `TreatWarningsAsErrors`, latest analysis, code style enforced in build, deterministic builds (`Directory.Build.props`).
- Target frameworks:
  - Host/Admin/Smoke/Integration: `net10.0-windows`, WPF where applicable (`src/Dobby.Host/Dobby.Host.csproj`; `src/Dobby.Admin/Dobby.Admin.csproj`; `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj`; `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`).
  - Infrastructure: `net10.0` and `net10.0-windows` with Windows-only protected storage compiled conditionally (`src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`).
  - Security: `net10.0` with `DobbyPluginTrustProfile` defaulting to `Production` and optional `DevQa` build profile (`src/Dobby.Security/Dobby.Security.csproj`).
- Central package pins consumed from `Directory.Packages.props`: `Microsoft.Extensions.* 10.0.7`, `Dapper 2.1.72`, `Microsoft.Data.Sqlite.Core 10.0.8`, `SQLitePCLRaw.core 3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl 3.0.3`, `System.Management 10.0.8`, `JsonSchema.Net 9.2.0`, `WPF-UI 4.3.0`, `CommunityToolkit.Mvvm 8.4.2`, `xunit 2.9.3`, `Shouldly 4.3.0`, `NSubstitute 5.3.0`, `Verify.Xunit 31.12.5`, and test infrastructure pins.
- Package pin impact:
  - Allowed without new decision: use existing packages and BCL/WPF/MSBuild functionality already pinned or in the SDK (`Directory.Packages.props`; `docs/03_TECH_STACK_AND_VERSIONS.md`).
  - Human decision required: any new installer packaging package/tool, Argon2 package, CI/upload client, telemetry client, or test package not already pinned (`artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).
- Native assets: `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll` is copied by `Dobby.Infrastructure.csproj`; existing evidence records SHA-256 `3744689bf8427b40b222c1ec046ebc5e4f08c5c9898d56573112006ff2c4def8` for the extracted native provider (`artifacts/release-evidence/skeleton-local/storage-materialization.md`).
- Trust resources: production and devQA public PEMs are embedded by selected `DobbyPluginTrustProfile`; private keys stay outside the repo and current bootstrap keys must rotate before production release (`src/Dobby.Security/Dobby.Security.csproj`; `src/Dobby.Host/Trust/README.md`).
- Zero-warning/format/encoding: Release build must be warning-clean, `dotnet format --verify-no-changes --no-restore` must pass, and text files must be UTF-8 no BOM with final newline/no trailing whitespace (`Directory.Build.props`; `docs/08_CODING_STANDARDS.md`; `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`).

## 12. Code Work Plan

### Pre-Flight

1. Confirm open authority gates before writing release-candidate evidence.
   - Action: confirm HD-S11-001 through HD-S11-005 are either closed or explicitly marked blocking in release evidence.
   - Success criteria: every open gate has an owner, decision state, and evidence anchor in Section 13 and `artifacts/release-evidence/<build-id>/`.
   - Out-of-scope guardrail: do not generate production private keys or invent signing/installer/performance decisions (`docs/adr/ADR-0033-signing-key-ownership.md`; `docs/_perf/README.md`; `installer/README.md`).

2. Select the release evidence root.
   - Action: choose `artifacts/release-evidence/<build-id>/` per IA-S11-001 and create only Sprint 11 release-candidate evidence under that root.
   - Success criteria: evidence root names the build/run and separates RC evidence from `skeleton-local` and `sprint10-local-2026-06-20`.
   - Out-of-scope guardrail: do not edit skeleton evidence as a substitute for RC proof (`docs/09_BUILD_RUN_AND_RELEASE.md`).

3. Add release-evidence contract tests first.
   - Action: add or extend architecture tests to fail until required Sprint 11 evidence files/markers exist.
   - Success criteria: tests identify missing `auth-kdf-selection.md`, `storage-provider-proof.md`, `packaging-static-scan.md`, `startup-performance.md`, installer/smoke/UI/recovery evidence, and no unresolved Sprint 10 deferred claim.
   - Out-of-scope guardrail: tests must not require unsupported decisions to appear closed; they should accept explicit blocked evidence only where Section 13 permits it (`tests/Dobby.IntegrationTests/Architecture/Sprint10ReleaseEvidenceTests.cs`; `docs/09_BUILD_RUN_AND_RELEASE.md`).

### Implementation

4. Wire Build and CI Gate evidence.
   - Action: run the documented no-restore Release sequence after restore/publish preparation: SDK info, restore, Release build with warnings as errors, Release tests, format verify, architecture/static scans, UTF-8, plugin boundary, devQA trust exclusion, and release profile flake-sensitive verification.
   - Success criteria: `unit-integration-smoke-results.md`, `packaging-static-scan.md`, and flake-sensitive summary paths record pass/fail outcomes with command lines and counts.
   - Out-of-scope guardrail: do not create a CI workflow without HD-S11-005 (`docs/09_BUILD_RUN_AND_RELEASE.md`; `tools/verify-flake-sensitive-tests/README.md`).

5. Close or explicitly block production/Admin recovery evidence.
   - Action: after HD-S11-001 closes, use approved production vendor signing material through existing `Dobby.Admin` command paths to generate/verify a `.dobbyrecovery` response against Host import code; record key custody/public hash/proof without private material.
   - Success criteria: `production-recovery-handover.md` proves production-approved public key path/hash, Admin-generated response, Host import verification, no TEST-ONLY fixture reliance, and absence of private key material in repo/publish artifacts.
   - Out-of-scope guardrail: if HD-S11-001 is not closed, leave a blocked evidence entry and do not claim AC-007 production/Admin release evidence (`artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `src/Dobby.Admin/Commands/AdminCommandHost.cs`).

6. Implement installer/publish evidence after packaging authority closes.
   - Action: create or update MSIX packaging only after HD-S11-003 approves package identity, signing certificate, versioning, and tooling; verify install/uninstall/upgrade and data preservation.
   - Success criteria: `installer-verification.md` records package identity, artifact name/hash, install/uninstall, vN-to-vN+1 upgrade, app data/audit/license/users/permissions/preferences preservation, and MSI fallback disposition if applicable.
   - Out-of-scope guardrail: no MSI fallback unless required/approved; no installer writing to `%LocalAppData%/Dobby` as package target (`docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`).

7. Expand release smoke.
   - Action: bind smoke tests or smoke scripts to the same publish/installer artifact and cover login-first, Owner bootstrap, user management, shell, plugin install/activation, license import/mode, work session, logout/lock, diagnostics export, sample plugin actions, and crash recovery.
   - Success criteria: `release-smoke.md` contains automated test names, artifact ID, isolated profile path policy, and pass/fail logs; smoke tests remain isolated from unit profile state.
   - Out-of-scope guardrail: no remote marketplace/catalog install and no sample plugin production bundling (`docs/09_BUILD_RUN_AND_RELEASE.md`; `docs/specs/sample-plugin.md`; `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`).

8. Prove AC-046 UI heartbeat.
   - Action: add deterministic smoke/unit/integration checks for long-running plugin install, license import, and storage operations reporting background progress through the shell dispatcher without blocking UI.
   - Success criteria: tests prove a heartbeat/progress snapshot while operation work is pending and no dispatcher deadlock; evidence names the tests.
   - Out-of-scope guardrail: do not patch production behavior for flakes unless a current failure reproduces; preserve WPF collection/dispatcher cleanup discipline (`AGENTS.md`; `docs/superpowers/plans/2026-06-23-flake-prevention.md`).

9. Generate performance evidence.
   - Action: after HD-S11-002 closes, collect ADR-0042 N=100 cold/warm/login-to-shell samples on the accepted baseline hardware/profile and write P95/regression evidence.
   - Success criteria: `startup-performance.md` records hardware profile, OS, network state, methodology, sample counts, P95 values, and AC-014 pass/fail/regression result.
   - Out-of-scope guardrail: do not invent a baseline artifact format or claim AC-014 pass from skeleton/local data (`docs/adr/ADR-0042-release-gate-hardware-baseline.md`; `docs/_perf/README.md`).

10. Capture UI/accessibility evidence.
    - Action: capture or record snapshot descriptors/screenshots for required shell states, DPI/resolution matrix, keyboard/accessibility smoke, focus tokens, and background operation surfaces.
    - Success criteria: `ui-snapshots/` and `release-smoke.md` cite snapshot files/test names for 1366x768, 1920x1080, 2560x1440, 3840x2160, 100% DPI automated baseline, and 125/150% manual smoke where required.
    - Out-of-scope guardrail: do not change UI text/resources solely to make screenshots pass unless a real test failure identifies a product defect (`docs/specs/shell-ui.md`).

### Evidence / Closure

11. Generate RC storage/KDF/static evidence.
    - Action: regenerate `storage-provider-proof.md`, `auth-kdf-selection.md`, and `packaging-static-scan.md` from the current Release artifact and approved evidence sources.
    - Success criteria: evidence cites current package pins/native hash, PBKDF2 fallback justification, no unprotected DB, no devQA/sample production bundling, and no forbidden references.
    - Out-of-scope guardrail: no new package or storage/KDF floor changes without approved decision (`artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`; `docs/07_ACCEPTANCE_CRITERIA.md`, AC-051).

12. Run final verification matrix.
    - Action: run the strongest relevant no-restore verification the local assets allow after restore/build/publish prerequisites are satisfied: Release build, Release unit/integration/smoke, format verify, architecture/static scans, flake-sensitive Release profile, installer/upgrade smoke/manual evidence, performance evidence, and evidence contract tests.
    - Success criteria: all non-blocked gates pass; blocked gates are explicitly marked as human decisions and prevent a "ready" release claim.
    - Out-of-scope guardrail: do not mark complete with flaky/known defects or unverified assumptions (`AGENTS.md`).

13. Final evidence review.
    - Action: review every AC row in Section 16, every reviewer-focus item in Section 17, and every open decision in Section 13 before declaring readiness.
    - Success criteria: release reviewer can trace each pass/block to a file, test, command, or decision.
    - Out-of-scope guardrail: do not replace missing evidence with prose claims.

## 13. Decisions and Risks

### Closed Authority Gates

| Gate | Authority / evidence | Disposition for Sprint 11 |
| --- | --- | --- |
| `D-30` / `HD-S10-001` | `artifacts/release-evidence/skeleton-local/decision-log.md` | Admin CLI public option contract is closed; Sprint 11 uses existing commands and wrappers. |
| `D-31` / `HD-S10-002` | `artifacts/release-evidence/skeleton-local/decision-log.md` | Production/Admin recovery evidence was deferred to Sprint 11; not closed. |
| `D-33` / `HD-S10-004` | `artifacts/release-evidence/skeleton-local/decision-log.md` | Sprint 10 technical path is not production/Admin AC-007 release evidence; Sprint 11 must close or block. |
| `D-34` / `HD-S10-005` | `artifacts/release-evidence/skeleton-local/decision-log.md` | ReadOnlyExpired package install/import policy is closed; Sprint 11 smoke preserves it. |
| Sprint 8 trust resource approval | `src/Dobby.Host/Trust/README.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`, `D-23` | Public bootstrap keys approved as repo inputs, but rotation required before production release. |
| Storage/KDF/package approvals | `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `auth-kdf-selection.md`; `toolchain-pin-gate.md` | Existing provider/KDF decisions stand; Sprint 11 regenerates RC evidence, not policy. |

### Approved Implementation Choices

- Use existing Admin CLI commands instead of creating new tool surfaces (`docs/specs/admin-tool.md`; `src/Dobby.Admin/Commands/AdminCommandHost.cs`).
- Use existing release-evidence convention under `artifacts/release-evidence/<build-id>/` (`docs/09_BUILD_RUN_AND_RELEASE.md`).
- Use existing smoke/integration/unit test projects and solution layout (`Dobby.sln`; `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj`; `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj`).
- Use existing WPF flake-sensitive release profile for handover confidence (`tools/verify-flake-sensitive-tests/README.md`).

### Human Decision Required

| ID | Decision required | Why it blocks | Evidence anchor needed before implementation/closure |
| --- | --- | --- | --- |
| HD-S11-001 | Production vendor key handover, custody proof, public key/hash, rotation disposition, and approved use of signing material for production/Admin `.dobbyrecovery` RC evidence. | Sprint 11 must replace Sprint 5 TEST-ONLY recovery fixture closure and Sprint 10 technical-path proof; production private material must not enter repo. | `production-recovery-handover.md` plus decision-log entry; source basis `docs/adr/ADR-0033-signing-key-ownership.md`, `src/Dobby.Host/Trust/README.md`, `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`, `D-31`, `D-33`. |
| HD-S11-002 | ADR-0042 baseline hardware availability, measurement process, artifact format/location, and acceptance criteria owner. | AC-014 and AC-051 release evidence depend on accepted baseline timing; `docs/_perf/README.md` says process/artifact are future scope. | `startup-performance.md` plus accepted baseline artifact; source basis `docs/adr/ADR-0042-release-gate-hardware-baseline.md`, `docs/_perf/README.md`. |
| HD-S11-003 | MSIX package identity, signing certificate, versioning, packaging tooling, and MSI fallback disposition. | Installer verification is Sprint 11 scope, but current repo has only `installer/README.md`; creating packaging files would otherwise invent release identity/tooling. | `installer-verification.md` plus approved installer files; source basis `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, `installer/README.md`. |
| HD-S11-004 | Release build ID / evidence-root naming if no CI build identifier exists. | Release evidence must trace to one publish/installer artifact from the same run. | Decision entry or release-owner note naming `<build-id>`; source basis `docs/09_BUILD_RUN_AND_RELEASE.md`. |
| HD-S11-005 | CI platform/workflow ownership if the Build and CI Gate requires repository workflow creation. | No CI workflow exists; tool docs forbid creating a new CI platform without ownership. | CI owner decision or explicit local-only release-gate acceptance; source basis `tools/verify-flake-sensitive-tests/README.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`. |

### Risks And Mitigations

| Risk | Mitigation | Evidence |
| --- | --- | --- |
| Sprint 11 claims release readiness while production recovery remains test-only. | Keep HD-S11-001 blocking and require `production-recovery-handover.md`. | `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `D-33`. |
| Performance evidence is collected on non-baseline hardware or too few samples. | Require HD-S11-002 and ADR-0042 N=100 P95 evidence. | `docs/adr/ADR-0042-release-gate-hardware-baseline.md`. |
| Installer files encode unauthorized identity or signing. | Block packaging changes until HD-S11-003 closes. | `installer/README.md`; `docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`. |
| DevQA trust/sample artifacts leak into production output. | Extend static scans and packaging evidence. | `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`; `src/Dobby.Host/Trust/README.md`. |
| WPF/STA smoke flakes are misdiagnosed as production defects. | Use existing flake-sensitive tooling and root-cause rules before behavior fixes. | `AGENTS.md`; `tools/verify-flake-sensitive-tests/README.md`. |
| New packages are added for installer/perf/screenshot convenience. | Package governance tests and human pin gate. | `Directory.Packages.props`; `PackageGovernanceTests.cs`; `toolchain-pin-gate.md`. |

## 14. Test Plan

### Unit Tests

| File | Scenarios | AC / gate coverage |
| --- | --- | --- |
| `tests/Dobby.UnitTests/Admin/AdminCommandHostTests.cs` | Production-approved recovery response generation path, redacted reports, existing exit code behavior, no private path disclosure. | Production/Admin recovery gate; AC-041 evidence separation. |
| `tests/Dobby.UnitTests/Host/ShellBackgroundOperationCoordinatorTests.cs` | Background operation heartbeat/progress posts through dispatcher while operation remains pending. | AC-046. |
| `tests/Dobby.UnitTests/Host/LicenseImportCoordinatorTests.cs` | Async license import cancellation/permission/file-read ordering remains non-blocking and policy-preserving. | AC-046; license import smoke. |
| `tests/Dobby.UnitTests/Host/HostPluginLifecycleCoordinatorTests.cs` | Disable/uninstall/install lifecycle policy remains stable under release smoke and active work-session checks. | AC-046; plugin manager smoke. |
| `tests/Dobby.UnitTests/Security/KdfPolicyResolverTests.cs` | PBKDF2 floor and Argon2 deferral remain enforced for RC KDF evidence. | AC-051 contribution. |
| `tests/Dobby.UnitTests/Security/PluginTrustPublicKeyProviderTests.cs` and `PluginTrustVerifierTests.cs` | Production/devQA resource separation and malformed/mixed resource rejection. | DevQA trust exclusion; packaging static scan. |

### Integration Tests

| File | Scenarios | AC / gate coverage |
| --- | --- | --- |
| `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs` or `ReleaseEvidenceTests.cs` | Required RC evidence files/markers, blocked human decisions, no stale Sprint 10 deferred claim after closure. | AC-041; release evidence package. |
| `ProductionReleasePackagingStaticScanTests.cs` | Production output excludes sample plugin, generated smoke artifacts, devQA trust anchor, private keys, `.dobbyplugin`, `.dobbylicense`, `.dobbyrecovery` unless approved evidence. | Static scans; devQA trust exclusion. |
| `EncodingBoundaryTests.cs` | New evidence/installer text files are UTF-8 no BOM. | AC-047. |
| `PackageGovernanceTests.cs` | No new unapproved package pins or rejected/deferred package drift. | Package gate. |
| `PluginTrustResourcePackagingTests.cs` | Selected production trust resource only; devQA absent from production security assembly. | Security/static gate. |
| `ShellUiStaticScanTests.cs` and `ShellSnapshotTests.cs` | Shell regions, first-paint marker, DPI/resolution matrix, accessibility/focus/motion tokens, background-operation surface. | AC-014; AC-046. |
| `PreLoginAllowedWorkloadTests.cs` and `StartupBootstrapWiringTests.cs` | Login-first, forbidden pre-login workload, startup milestones. | AC-008; AC-014. |
| `HostPersistenceTests.cs`, `PluginInstallStatePersistenceTests.cs`, `ProtectedMaterialDpapiTests.cs` | Encrypted provider, native hash/guardrails, protected material, transactions, plugin state persistence. | Storage proof; AC-024 upgrade preservation support. |
| `RecoveryRequestExportTests.cs`, `RecoveryResponseImportConfirmTests.cs`, `RecoveryResponseImportFixtureTests.cs` | Owner recovery export/import/confirm/replay/expiration/nonce behavior. | Production recovery proof once HD-S11-001 closes. |
| `LicenseImportIntegrationTests.cs`, `LicenseRuntimeModeIntegrationTests.cs`, `ReadOnlyExpiredEnforcementIntegrationTests.cs` | License import, customer/deployment binding, runtime mode, ReadOnlyExpired policy. | Release smoke; AC-041. |
| `WorkSessionShellIntegrationTests.cs` | Work-session lock/logout/disable/uninstall lifecycle and deterministic WPF/STA coverage. | Release smoke; flake-sensitive gate. |

### Smoke Tests

| File / evidence | Scenarios | AC / gate coverage |
| --- | --- | --- |
| `tests/Dobby.SmokeTests/ReleaseCandidateSmokeTests.cs` or extended `ShellSmokeTests.cs` | Smoke against same publish/installer artifact: login-first, Owner bootstrap, user management, shell, plugin install/activation, license import/mode, work session, logout/lock, diagnostics export, sample plugin actions. | AC-008; AC-041. |
| `tests/Dobby.SmokeTests/InstallerUpgradeSmokeTests.cs` or `installer-verification.md` | MSIX install/uninstall/upgrade and data preservation; MSI fallback only if approved. | AC-024. |
| `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs` | Keyboard traversal, focusable actions, accessible labels. | Shell UI/accessibility evidence. |
| `tests/Dobby.SmokeTests/CrashRecoverySmokeTests.cs` | Transaction/crash recovery smoke after storage operations. | Storage proof; AC-046 storage operation boundary. |
| `tools/verify-flake-sensitive-tests/verify-flake-sensitive-tests.ps1 -Profile Release` | 20 focused WorkSession iterations, 10 integration iterations, 5 solution iterations after Release build unless intentionally `-NoBuild`. | Handover confidence for WPF/STA-sensitive release smoke. |

### Manual / Out-Of-Band Evidence

- Production key custody/handover proof and production signing material use: manual/release-owner evidence only, no private key material in repo (`docs/adr/ADR-0033-signing-key-ownership.md`).
- ADR-0042 performance baseline: manual/hardware-controlled evidence for accepted hardware profile, OS, no-network state, and N=100 P95 samples (`docs/adr/ADR-0042-release-gate-hardware-baseline.md`).
- Installer signing and upgrade: manual/out-of-band evidence may be required for MSIX certificate identity and installed-app upgrade behavior (`docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`).
- DPI 125/150% manual smoke baseline may supplement automated 100% DPI snapshots (`docs/specs/shell-ui.md`).

### Test Count Expectation

- Sprint 10 evidence recorded Release verification with Unit `617`, Integration `241`, and Smoke `11` after boundary reverify (`artifacts/release-evidence/skeleton-local/architecture-baseline.md`).
- Sprint 11 should not reduce these counts. New evidence/static/smoke/performance tests should increase counts, but exact final count is implementation-time evidence in `unit-integration-smoke-results.md`.

## 15. Out of Scope

- Redoing Sprints 1-10 implementation work; Sprint 11 consumes and verifies it (`SPRINT_IMPLEMENTATION_PLAN.md`).
- New product features outside release gates, installer verification, evidence, smoke, performance, UI snapshots, static scans, and explicit handover closure (`SPRINT_IMPLEMENTATION_PLAN.md`).
- New marketplace/catalog/remote plugin install, update/repair/downgrade flows, online calls, telemetry, or cloud diagnostics (`docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`; `docs/specs/audit-and-diagnostics.md`; `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md`, NFR-020).
- New package pins, package sources, installer toolchains, Argon2 packages, CI upload clients, or screenshot/performance packages without human approval (`Directory.Packages.props`; `PackageGovernanceTests.cs`).
- New `ErrorCode`, audit event names, operation verbs, license states, plugin schema fields, or public SDK/Contracts surfaces without documented authority (`src/Dobby.Core/Result.cs`; `src/Dobby.Audit/AuditEventName.cs`; `docs/schemas/*`).
- Production private key storage, generation, or disclosure in repo/source/tests/evidence/publish output (`docs/adr/ADR-0033-signing-key-ownership.md`; `src/Dobby.Host/Trust/README.md`).
- Editing ADRs/specs/schemas/master plan/predecessor execution plans during Sprint 11 implementation unless a separate human-authorized documentation task exists (`attached prompt`; `docs/08_CODING_STANDARDS.md`).
- MSI fallback unless a release decision requires it; portable artifact is support/dev/offline-test scope and not a release blocker if absent (`docs/adr/ADR-0047-host-installer-and-portable-artifact-policy.md`).

## 16. Definition of Done

Build / test / format expectations:

- Restore/publish prerequisites are complete, then documented Release build/test/format sequence passes without warnings or formatting drift (`docs/09_BUILD_RUN_AND_RELEASE.md`; `Directory.Build.props`).
- Unit, integration, and smoke evidence are separate and tied to isolated profiles where required (`docs/07_ACCEPTANCE_CRITERIA.md`, AC-041).
- Architecture/static scans pass: dependency graph, package governance, UTF-8, plugin boundary, production/devQA trust exclusion, no telemetry/upload clients, no plaintext secrets (`tests/Dobby.IntegrationTests/Architecture/*`; `tests/Dobby.IntegrationTests/Security/NoPlaintextSecretContentScanTests.cs`).
- Flake-sensitive Release profile completes or records failure evidence without speculative behavior fixes (`tools/verify-flake-sensitive-tests/README.md`; `AGENTS.md`).

Per-AC expectations:

| AC | Required named evidence |
| --- | --- |
| AC-008 | Release smoke covering no-mandatory-internet login/shell/local plugin-host foundation; startup tests in `PreLoginAllowedWorkloadTests.cs` and `StartupBootstrapWiringTests.cs`; `release-smoke.md`. |
| AC-014 | `startup-performance.md` with ADR-0042 P95 evidence; shell snapshot/UI evidence from `ShellSnapshotTests.cs` and `ui-snapshots/`. |
| AC-024 | `installer-verification.md` with MSIX install/uninstall/upgrade and data preservation; installer smoke/manual evidence. |
| AC-041 | `unit-integration-smoke-results.md` with separate unit/integration/smoke runs and artifact binding. |
| AC-046 | AC-046 heartbeat tests/evidence for plugin install, license import, and storage operations; background operation UI evidence. |
| AC-047 | `EncodingBoundaryTests.cs` passes for docs/source/resources/evidence/installer files. |
| AC-051 contribution | `auth-kdf-selection.md` justifies PBKDF2 fallback by approved package status and/or ADR-0042 latency evidence. |

Evidence expectations:

- `artifacts/release-evidence/<build-id>/auth-kdf-selection.md`
- `artifacts/release-evidence/<build-id>/storage-provider-proof.md`
- `artifacts/release-evidence/<build-id>/packaging-static-scan.md`
- `artifacts/release-evidence/<build-id>/startup-performance.md`
- `artifacts/release-evidence/<build-id>/installer-verification.md`
- `artifacts/release-evidence/<build-id>/unit-integration-smoke-results.md`
- `artifacts/release-evidence/<build-id>/release-smoke.md`
- `artifacts/release-evidence/<build-id>/ui-snapshots/`
- `artifacts/release-evidence/<build-id>/production-recovery-handover.md` or explicit HD-S11-001 blocked entry

Boundary preservation expectations:

- No production private keys, devQA private keys, sample plugin production bundling, or unapproved generated artifacts in production publish/installer output (`src/Dobby.Host/Trust/README.md`; `packaging-static-scan.md`).
- No unapproved package pins or new closed-set entries (`PackageGovernanceTests.cs`; `ArchitectureDependencyBoundaryTests.cs`).
- No telemetry/network upload clients (`NoPlaintextSecretContentScanTests.cs`; `docs/specs/audit-and-diagnostics.md`).

Open items allowed at close:

- None for a "READY FOR RELEASE REVIEW" claim.
- If any HD-S11 item remains open, Sprint 11 may close only as "BLOCKED PENDING HUMAN DECISION" with evidence, not as release-ready.

## 17. Reviewer Focus

1. Verify Section 13 open decisions are closed before accepting any release-ready claim: inspect `production-recovery-handover.md`, `startup-performance.md`, `installer-verification.md`, and decision-log entries (`D-31`, `D-33`, HD-S11-001 through HD-S11-005).
2. Verify release evidence root integrity: every file under `artifacts/release-evidence/<build-id>/` maps to one RC run and not to `skeleton-local` or Sprint 10 local evidence (`docs/09_BUILD_RUN_AND_RELEASE.md`).
3. Verify Build/Test/Format evidence: command lines, configurations, no-restore/no-build sequencing, unit/integration/smoke separation, and final counts in `unit-integration-smoke-results.md`.
4. Verify production recovery: Admin-generated `.dobbyrecovery` proof uses approved production/vendor material, Host verifies it, TEST-ONLY fixtures remain regression-only, and no private material appears in repo/output (`test-fixture-authority.md`; `src/Dobby.Host/Trust/README.md`).
5. Verify installer: MSIX identity/signing/versioning, install/uninstall/upgrade, data preservation, MSI fallback disposition, and portable scope in `installer-verification.md`.
6. Verify performance: ADR-0042 hardware profile, N=100 sample counts, cold/warm/login-to-shell P95, and regression boundary in `startup-performance.md`.
7. Verify AC-046: named tests/evidence show heartbeat/progress during plugin install, license import, and storage operations without UI dispatcher blocking.
8. Verify static scans: production/devQA trust exclusion, sample plugin exclusion, package governance, architecture boundaries, no telemetry, no plaintext secrets, and UTF-8 evidence.
9. Verify smoke: release smoke binds to the same publish/installer artifact and covers all Sprint 11 smoke workflows.
10. Verify WPF stability evidence: flake-sensitive Release profile summary exists and failures, if any, have root-cause evidence before behavior changes.

## 18. Final Execution Checklist

### Before Starting

- [ ] Confirm `SPRINT_11_EXECUTION_PLAN.md` is the only planning artifact created for this task.
- [ ] Confirm HD-S11-001 through HD-S11-005 status and owners.
- [ ] Confirm current `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `Dobby.sln` are the build authority.
- [ ] Confirm no new package/tool/CI/installer identity is introduced without an approved decision.
- [ ] Confirm evidence root naming per IA-S11-001 or release-owner decision.

### During Implementation

- [ ] Write tests/evidence contract before source changes where behavior must change.
- [ ] Keep release-gate changes scoped to tests, evidence, installer/publish wiring, and minimal source fixes proven by failing tests.
- [ ] Preserve `Result`/`ErrorCode`/audit/operation/plugin schema closed sets.
- [ ] Preserve plugin boundary and no production references to sample plugin/devQA/generated artifacts.
- [ ] Use background operation reporter/dispatcher surfaces for AC-046; do not block WPF UI thread.
- [ ] For WPF/STA failures, first rule out harness state leakage, cleanup, shared state, test ordering, and missing completion signals.
- [ ] Keep production private keys outside repo/source/tests/evidence/output.
- [ ] Keep docs/ADRs/specs/schemas/master/predecessor plans untouched unless a separate human-authorized documentation task exists.

### Before Finishing

- [ ] Release build/test/format/static/encoding/package-governance/plugin-boundary checks are green or explicitly blocked.
- [ ] Unit/integration/smoke results are separated and recorded.
- [ ] Installer, performance, UI snapshot, storage, KDF, packaging, and production recovery evidence files exist or are blocked by named human decisions.
- [ ] Flake-sensitive Release profile summary is present for handover confidence.
- [ ] No unresolved authority gate is hidden behind a "pass" claim.
- [ ] Reviewer Focus checklist has traceable evidence paths/test names for every item.

## 19. Planning Coverage and Confidence

| Topic | Coverage | Justification |
| --- | --- | --- |
| Master sprint scope | High | Sprint 11 section, dependencies, task groups, AC ownership, risks, and exit condition were inspected in `SPRINT_IMPLEMENTATION_PLAN.md`. |
| Prior sprint continuity | High | All prior execution plans were discovered and inspected for readiness/decision continuity, then verified against current code/tests/evidence. |
| Current code/test surface | High | Admin, host startup, plugins, licensing, shell, recovery, storage, security, smoke, integration, architecture, and unit surfaces were inspected. |
| Release evidence requirements | High | `docs/09_BUILD_RUN_AND_RELEASE.md`, ACs, and existing evidence artifacts define the required files and gates. |
| Production recovery/key handover | Medium | Code/tests/tooling are present, but production custody and rotation authority are external and blocked by HD-S11-001. |
| Installer implementation | Medium | MSIX/MSI policy is clear, but concrete packaging files are absent and blocked by HD-S11-003. |
| Performance baseline | Medium | ADR-0042 is explicit, but `docs/_perf/README.md` marks measurement artifacts/process as future until accepted; blocked by HD-S11-002. |
| CI gate | Medium | Local commands and flake-sensitive tooling are clear; repository CI ownership is absent and blocked by HD-S11-005 if workflow creation is required. |
| Package/tooling governance | High | Package pins, package governance tests, and toolchain evidence were inspected. |
| UI responsiveness/WPF flake risk | High | Shell dispatcher/background operation surfaces, WPF collection, flake-prevention plan, and release-profile tool were inspected. |

Confidence limits:

- This plan is evidence-grounded enough for implementation planning, but not enough to claim release readiness.
- Production cryptographic custody, ADR-0042 baseline process, installer package identity/signing/tooling, build ID/CI evidence ownership, and CI workflow ownership remain human decisions.
- Implementation may start only on non-authority-gated tests/evidence scaffolding and current-surface verification. Full Sprint 11/release-candidate closure is **BLOCKED PENDING HUMAN DECISION** until HD-S11-001 through HD-S11-005 are closed or explicitly accepted as release blockers.
