# Sprint 10 Execution Plan — Plugins Manager, Sample Plugin, and Admin Artifact Tools

## 1. Sprint Summary

| Field | Plan |
|---|---|
| Sprint | Sprint 10 |
| Title | Plugins Manager, Sample Plugin, and Admin Artifact Tools |
| Main goal | Complete P1 plugin management workflows and the artifacts used to prove package, license, recovery, and plugin behavior, per `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 10. |
| Expected deliverable | Permission-gated Plugins Manager lifecycle UI, finalized dev/QA sample plugin verification artifact, scripted Admin artifact flows, and release evidence that production rejects dev/QA trust artifacts. |
| Why this sprint exists | Sprint 5 supplies host-side recovery request/import behavior and TEST-ONLY fixture boundary; Sprint 8 supplies plugin package trust, install, activation, sample scaffold, and dev/QA/production trust profile foundation; Sprint 9 supplies license import/runtime mode enforcement. Sprint 10 consumes those outputs and provides the Admin recovery technical path plus plugin/sample validation artifacts for Sprint 11 release gates, while production/Admin `.dobbyrecovery` release evidence is deferred by `HD-S10-002` and the D-33 AC-007 evidence split, per `SPRINT_IMPLEMENTATION_PLAN.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md`, and `artifacts/release-evidence/skeleton-local/decision-log.md`. |
| Readiness | READY FOR SPRINT 10 IMPLEMENTATION WITH PRODUCTION EVIDENCE DEFERRAL. `HD-S10-001`, `HD-S10-002`, `HD-S10-003`, and `HD-S10-004` are decided in `artifacts/release-evidence/skeleton-local/decision-log.md`; the canonical Admin CLI contract is now `docs/specs/admin-tool.md`. Production/Admin `.dobbyrecovery` release evidence remains deferred to Sprint 11/vendor handover and must not be claimed by Sprint 10 technical-path or host-import compatibility proof. |
| Strategy | 1. Implement Admin public CLI behavior from the approved `docs/specs/admin-tool.md` contract. 2. Extend current installed plugin state/cache/runtime coordinator surfaces for manager lifecycle. 3. Wire manager UI through existing post-login shell and operation authorization policy. 4. Finalize sample plugin as a dev/QA artifact using current Contracts/SDK-only boundary. 5. Add Admin artifact services and wrappers with fail-closed production signing-material handling. 6. Add release/static scans and evidence appends for dev/QA trust exclusion and AC coverage. |
| Dependency summary | Consumes Sprints 5, 8, and 9; unblocks Sprint 11 release evidence for AC-007, AC-018, AC-034, AC-036, production package hygiene, and P1 smoke consolidation. Evidence: `SPRINT_IMPLEMENTATION_PLAN.md`, `SPRINT_5_EXECUTION_PLAN.md`, `SPRINT_8_EXECUTION_PLAN.md`, `SPRINT_9_EXECUTION_PLAN.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. |

## 2. Sources Read

Inspection depth legend: **deep** = full file or sprint section read and used directly; **targeted** = relevant sections/search hits read and verified against current code; **boundary/context** = read only to protect scope.

| Group | Source | Depth | Why it matters / plan effect |
|---|---|---:|---|
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` | deep | Sprint 10 scope, dependencies, AC ownership, Admin option decision dependency, Sprint 11 dependency. |
| Previous sprint execution plans | `SPRINT_1_EXECUTION_PLAN.md` | targeted | Baseline architecture/test/evidence conventions and prior plan format discovery. |
| Previous sprint execution plans | `SPRINT_2_EXECUTION_PLAN.md` | targeted | Core contracts/shared primitives continuity and earlier planning style. |
| Previous sprint execution plans | `SPRINT_3_EXECUTION_PLAN.md` | targeted | Config/runtime/diagnostics/audit continuity. |
| Previous sprint execution plans | `SPRINT_4_EXECUTION_PLAN.md` | targeted | Persistence/materialization continuity and package governance context. |
| Previous sprint execution plans | `SPRINT_5_EXECUTION_PLAN.md` | targeted | Owner recovery host-side boundary and TEST-ONLY fixture authority dependency. |
| Previous sprint execution plans | `SPRINT_6_EXECUTION_PLAN.md` | targeted | Operation authorization and settings continuity. |
| Previous sprint execution plans | `SPRINT_7_EXECUTION_PLAN.md` | targeted | Shell/navigation/localization/work-session continuity and current 19-section plan structure. |
| Previous sprint execution plans | `SPRINT_8_EXECUTION_PLAN.md` | targeted | Plugin package trust, install, runtime, dev/QA trust, sample scaffold dependency. |
| Previous sprint execution plans | `SPRINT_9_EXECUTION_PLAN.md` | deep | Immediate predecessor; licensing/runtime mode outputs and Admin option boundary. |
| Later sprint references | `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 11 section | boundary/context | Confirms release gates/evidence consolidation stay Sprint 11 while Sprint 10 supplies inputs. |
| Repo-root files | `Dobby.sln` | targeted | Confirms `Dobby.Admin`, tests, and sample plugin are current solution projects. |
| Repo-root files | `global.json` | deep | SDK `10.0.202`, `latestPatch` roll-forward. |
| Repo-root files | `Directory.Build.props` | deep | Nullable, C# 14, warnings-as-errors, deterministic build constraints. |
| Repo-root files | `Directory.Packages.props` | deep | Central package versions and no-new-pin constraint. |
| Product / architecture / requirements docs | `docs/02_ARCHITECTURE_MAP.md` | deep | Project ownership, Admin rows 84-86, PluginFramework/Host/Shell boundaries, future-scope exclusions. |
| Product / architecture / requirements docs | `docs/03_TECH_STACK_AND_VERSIONS.md` | deep | TFM split, package selection, plugin packaging, Admin target, no new package basis. |
| Product / architecture / requirements docs | `docs/04_RESULT_VS_EXCEPTION_POLICY.md` | deep | Result vs exception discipline, plugin exception sealing, ErrorCode baseline. |
| Product / architecture / requirements docs | `docs/05_FUNCTIONAL_REQUIREMENTS.md` | targeted | FR-007/008/015/016/017/018/025/026/028/030 scope anchors. |
| Product / architecture / requirements docs | `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` | targeted | NFR-006/008/009/014/015/016/017/020/021 boundaries. |
| Product / architecture / requirements docs | `docs/07_ACCEPTANCE_CRITERIA.md` | deep | AC-007, AC-018, AC-031, AC-034, AC-036, AC-043, AC-046 mapping. |
| Product / architecture / requirements docs | `docs/08_CODING_STANDARDS.md` | deep | Namespace/file naming, MVVM, UI/OS adapter, no invented namespace rule. |
| Product / architecture / requirements docs | `docs/09_BUILD_RUN_AND_RELEASE.md` | deep | Admin wrapper conventions, sample/devQA production exclusion scan, release evidence paths. |
| ADRs | `docs/adr/ADR-0009-plugin-dependency-graph.md` | targeted | Missing dependency skipped at activation, not install rejection. |
| ADRs | `docs/adr/ADR-0011-plugin-sandbox-enforcement-principle.md` | targeted | Host-controlled sandbox/permission/fail-closed boundary. |
| ADRs | `docs/adr/ADR-0023-admin-host-boundary.md` | deep | Admin/Host split, `Dobby.Admin` standalone, P1-core recovery generator, no host business duplication. |
| ADRs | `docs/adr/ADR-0032-plugin-signature-trust-model.md` | targeted | Offline ECDSA trust verification. |
| ADRs | `docs/adr/ADR-0033-signing-key-ownership.md` | deep | Production vs dev/QA key custody, `dobby.dev-qa`, no dev/QA trust in production. |
| ADRs | `docs/adr/ADR-0036-plugin-manifest-schema.md` | targeted | Manifest field and extension/operation target semantics. |
| ADRs | `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` | targeted | SemVer/minHost/prerelease compatibility. |
| ADRs | `docs/adr/ADR-0039-owner-password-recovery.md` | deep | `.dobbyrecoveryrequest`/`.dobbyrecovery`, no user picker, P1363/base64 recovery signature. |
| ADRs | `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` | deep | `.dobbyplugin`/`.dobbylicense` separation and package reject reasons. |
| ADRs | `docs/adr/ADR-0048-operation-based-authorization.md` | targeted | `plugins.management` and plugin operation target authorization model. |
| ADRs | `docs/adr/ADR-0051-license-lifecycle-v2.md` | targeted | License v2 lifecycle and import constraints. |
| ADRs | `docs/adr/ADR-0053-capability-surface-runtime-mode.md` | targeted | FullAccess/ReadOnlyExpired/Denied runtime modes. |
| Specs | `docs/specs/admin-tool.md` | deep | Command inventory, wrapper contract, option table blocker. |
| Specs | `docs/specs/plugins-manager.md` | deep | Manager UI/lifecycle/status/audit/permission behavior. |
| Specs | `docs/specs/sample-plugin.md` | deep | Sample plugin artifact requirements and dev/QA distribution rule. |
| Specs | `docs/specs/license-import.md` | targeted | Plugins > License import shortcut and non-disclosing reject vocabulary. |
| Specs | `docs/specs/work-sessions.md` | deep | Plugin disable/uninstall controlled close/cancel behavior and AC-019 continuity. |
| Specs | `docs/specs/audit-and-diagnostics.md` | deep | Audit event taxonomy, diagnostics privacy, no telemetry. |
| Schemas | `docs/schemas/dobby-plugin-manifest.schema.json` | deep | Manifest fields, required values, operation verbs, signature shape. |
| Source code | `src/Dobby.Core/Result.cs` | deep | Existing `Result<T>` and `ErrorCode` closed surface. |
| Source code | `src/Dobby.Audit/AuditEventName.cs` | deep | Existing plugin lifecycle/install audit names. |
| Source code | `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs` | deep | Confirms `plugins.management` and verbs already exist. |
| Source code | `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs` | deep | Non-disclosing deny policy and grant model. |
| Source code | `src/Dobby.Security/Trust/PluginTrustVerifier.cs` | deep | Production rejection of `dobby.dev-qa` and signature verification shape. |
| Source code | `src/Dobby.Security/Trust/PluginTrustPublicKeyProvider.cs` | deep | Selected production/devQA embedded resource behavior. |
| Source code | `src/Dobby.Security/Dobby.Security.csproj` | deep | Trust profile conditioned embedded resource selection. |
| Source code | `src/Dobby.Security/Authentication/RecoveryResponsePayload.cs` | deep | Recovery response payload/envelope shape. |
| Source code | `src/Dobby.Security/Authentication/RecoveryCanonicalJsonWriter.cs` | deep | Recovery payload canonical bytes. |
| Source code | `src/Dobby.Security/Authentication/RecoveryResponseImporter.cs` | deep | Recovery verification and signature format accepted by host. |
| Source code | `src/Dobby.Licensing/LicenseArtifact.cs` | targeted | Existing license artifact read model. |
| Source code | `src/Dobby.Licensing/LicenseCanonicalJsonWriter.cs` | targeted | License canonicalization for future Admin generation. |
| Source code | `src/Dobby.Licensing/LicenseSignatureVerifier.cs` | targeted | License DER/base64url signature verification; distinct from recovery. |
| Source code | `src/Dobby.PluginFramework/Manifest/PluginManifest.cs` | deep | Manifest typed model and current fields. |
| Source code | `src/Dobby.PluginFramework/Packaging/PluginPackageDescriptor.cs` | deep | Package archive/descriptor/outcome contracts. |
| Source code | `src/Dobby.PluginFramework/Packaging/PluginPackageReader.cs` | deep | Directory/archive reading and manifest parsing behavior. |
| Source code | `src/Dobby.PluginFramework/Packaging/PluginPackageValidator.cs` | deep | Validation reasons, trust check, forbidden content, fingerprint flow. |
| Source code | `src/Dobby.PluginFramework/Packaging/PluginPackageFingerprint.cs` | deep | Canonical signed package content and fingerprint semantics. |
| Source code | `src/Dobby.PluginFramework/Packaging/PluginPackageValidationReason.cs` | deep | Existing typed package reason shape. |
| Source code | `src/Dobby.PluginFramework/Packaging/PluginPackageInstaller.cs` | deep | Install state/cache writes and rollback behavior. |
| Source code | `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs` | deep | Authoritative installed state domain port. |
| Source code | `src/Dobby.PluginFramework/Packaging/InstalledPluginMetadataCache.cs` | deep | Durable manager list/detail read-model port. |
| Source code | `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs` | deep | Host install orchestration, license mode guard, audit/diagnostics. |
| Source code | `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs` | deep | Post-login migration, activation, revalidation, license gating. |
| Source code | `src/Dobby.Host/Plugins/HostInstalledPluginStateStore.cs` | deep | SQLite adapter for installed state. |
| Source code | `src/Dobby.Host/Plugins/HostInstalledPluginMetadataCache.cs` | deep | SQLite adapter for metadata cache. |
| Source code | `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs` | deep | Current post-login migration registry; new lifecycle schema must be registered here. |
| Source code | `src/Dobby.Infrastructure/Persistence/HostPersistenceMigrator.cs` | targeted | Migration checksum/metadata behavior; existing migrations must not be edited after recording. |
| Source code | `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` | targeted | Embedded SQL migration resources; new lifecycle migration must be embedded here. |
| Source code | `src/Dobby.Host/Plugins/PluginInstallStagingArea.cs` | targeted | Staging/runtime root and cleanup behavior. |
| Source code | `src/Dobby.Host/Plugins/PluginRuntimeAuditCoordinator.cs` | deep | Existing install/activation audit emission and missing lifecycle audit methods. |
| Source code | `src/Dobby.Host/Plugins/PluginRuntimeDiagnosticsCoordinator.cs` | deep | Existing diagnostic markers. |
| Source code | `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` | deep | Composition root wiring for plugin runtime, install, license, authorization. |
| Source code | `src/Dobby.Host/Login/HostLoginRuntime.cs` | targeted | Runtime record exposes coordinators consumed by shell. |
| Source code | `src/Dobby.Host/App.xaml.cs` | deep | Post-login shell wiring, plugin activation, license surface. |
| Source code | `src/Dobby.Host/MainWindow.xaml` | deep | Current shell view lacks Plugins Manager UI. |
| Source code | `src/Dobby.Host/MainWindow.xaml.cs` | deep | Navigation, plugin page host, license import click path, work-session handling. |
| Source code | `src/Dobby.Host/appsettings.json` | deep | Production runtime config and `AllowPreLoginDiscovery=false`. |
| Source code | `src/Dobby.Shell/ShellViewModel.cs` | deep | Current shell bindable state includes plugin pages/license, not manager lifecycle. |
| Source code | `src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs` | deep | Existing plugin-disable close-session flow. |
| Source code | `src/Dobby.Localization/HostResourceKeys.cs` | deep | Current host localization keys; manager-specific keys absent. |
| Source code | `src/Dobby.Localization/HostStringCatalog.cs` | targeted | Existing az-AZ fallback strings and license labels. |
| Source code | `src/Dobby.Localization/Resources/Host.az-AZ.resx` | targeted | Resource presence for plugin/license labels. |
| Source code | `src/Dobby.Configuration/DobbyOptions.cs` | targeted | Runtime options aggregate. |
| Source code | `src/Dobby.Configuration/PluginsOptions.cs` | deep | Only `AllowPreLoginDiscovery` option exists. |
| Source code | `src/Dobby.Configuration/DobbyOptionsValidator.cs` | deep | P1 pre-login discovery fail-closed guard. |
| Source code | `src/Dobby.Admin/Commands/AdminCommandHost.cs` | deep | Current Admin CLI skeleton and pre-decision option rejection behavior to replace with the approved contract. |
| Source code | `src/Dobby.Admin/Dobby.Admin.csproj` | deep | Current Admin references are narrower than architecture map allows. |
| Source code | `src/Dobby.Admin/App.xaml` | targeted | Admin WPF app exists. |
| Source code | `src/Dobby.Admin/App.xaml.cs` | targeted | Admin startup surface exists. |
| Source code | `src/Dobby.Admin/MainWindow.xaml` | targeted | Placeholder Admin UI. |
| Source code | `src/Dobby.Admin/MainWindow.xaml.cs` | targeted | Placeholder Admin code-behind. |
| Source code | `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj` | deep | Sample currently references only Contracts/SDK. |
| Source code | `samples/Dobby.Sample.Plugin/SamplePlugin.cs` | deep | Existing sample page/progress scaffold. |
| Source code | `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` | deep | Current scaffold missing required Sprint 10 sample metadata/artifact fields. |
| Source code | `tools/sign-plugin-package/README.md` | deep | Existing wrapper folder owner; option names deliberately absent. |
| Source code | `tools/create-plugin-license/README.md` | deep | Existing wrapper folder owner; option names deliberately absent. |
| Source code | `tools/create-recovery-response/README.md` | deep | Existing wrapper folder owner; option names deliberately absent. |
| Tests | `tests/Dobby.UnitTests/Audit/AuditEventNameTests.cs` | deep | Current audit taxonomy test includes plugin lifecycle events. |
| Tests | `tests/Dobby.UnitTests/Host/HostPluginPackageInstallCoordinatorTests.cs` | targeted | Existing install coordinator test surface. |
| Tests | `tests/Dobby.UnitTests/Host/HostPluginRuntimeCoordinatorTests.cs` | targeted | Existing runtime/activation test surface. |
| Tests | `tests/Dobby.UnitTests/Host/PluginRuntimeAuditCoordinatorTests.cs` | targeted | Existing plugin audit test surface. |
| Tests | `tests/Dobby.UnitTests/Host/PluginRuntimeDiagnosticsCoordinatorTests.cs` | targeted | Existing diagnostics marker test surface. |
| Tests | `tests/Dobby.UnitTests/PluginFramework/Packaging/PluginPackageInstallerTests.cs` | targeted | Current install state/cache rollback tests. |
| Tests | `tests/Dobby.UnitTests/PluginFramework/Packaging/PluginPackageValidatorTests.cs` | targeted | Package validation/reason coverage. |
| Tests | `tests/Dobby.UnitTests/Recovery/RecoveryResponseImporterUnitTests.cs` | targeted | Host recovery response verification semantics. |
| Tests | `tests/Dobby.UnitTests/Recovery/RecoveryCanonicalJsonWriterTests.cs` | targeted | Recovery canonical JSON semantics. |
| Tests | `tests/Dobby.UnitTests/Licensing/LicenseArtifactParserTests.cs` | targeted | Existing license import/parser coverage. |
| Tests | `tests/Dobby.UnitTests/Licensing/LicenseSignatureVerifierTests.cs` | targeted | License signature verification semantics. |
| Tests | `tests/Dobby.IntegrationTests/Plugins/PluginIntegrationSuiteTests.cs` | targeted | Plugin integration baseline. |
| Tests | `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs` | targeted | Activation/runtime integration baseline. |
| Tests | `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs` | targeted | Installed state/cache persistence baseline. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs` | deep | Production/devQA trust resource packaging guard. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs` | deep | Sample plugin project boundary guard. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/PluginNoInfrastructureReferenceTests.cs` | deep | Plugin infrastructure/package reference guard. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | deep | Package pin and package-reference governance guard. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` | targeted | UTF-8/extension scan baseline. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/Sprint8ReleaseEvidenceTests.cs` | targeted | Sprint 8 evidence test continuity. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/Sprint9ReleaseEvidenceTests.cs` | targeted | Sprint 9 evidence test continuity. |
| Tests | `tests/Dobby.IntegrationTests/Recovery/RecoveryRequestExportTests.cs` | targeted | Host recovery request evidence baseline. |
| Tests | `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs` | targeted | Sprint 5 TEST-ONLY recovery import fixture baseline. |
| Tests | `tests/Dobby.SmokeTests/ShellSmokeTests.cs` | deep | Current smoke test for pre-decision Admin option rejection; Sprint 10 must update coverage to accepted approved commands and redacted outputs. |
| Project / config / build files | `tests/Dobby.UnitTests/Dobby.UnitTests.csproj` | deep | Test references; no Admin project reference today. |
| Project / config / build files | `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj` | deep | Integration references and TFM. |
| Project / config / build files | `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj` | deep | Smoke currently references `Dobby.Admin`. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | deep | Verified Sprint 8/Sprint 9 closure and test counts. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/decision-log.md` | deep | D-13, D-22-D-29 closure and Admin/Sprint 10 gates. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/storage-materialization.md` | targeted | Append-only numbered migration authority and `StorageMetadata` checksum ledger. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/test-fixture-authority.md` | targeted | Sprint 5 TEST-ONLY recovery fixture boundary. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` | targeted | Existing package pin evidence and no-new-pin policy. |

Total inspected sources used for this plan: 122.

## 3. Sprint Scope From Master Sprint Plan

- Master-plan goal quote: "Complete P1 plugin management workflows and the artifacts used to prove package, license, recovery, and plugin behavior." Source: `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 10.
- Master-plan task groups:
  - **Plugins Manager:** implement list/details/install/enable/disable/uninstall/status/fault handling, permission target `plugins.management`, user-facing rejection reasons, and lifecycle audit. Source: `SPRINT_IMPLEMENTATION_PLAN.md`; behavior source `docs/specs/plugins-manager.md`.
  - **Sample Plugin:** complete/finalize existing dev/QA signed sample plugin scaffold covering manifest, extension hosting, Main Content page, operation target denial, `Result` boundary, fault behavior, progress, and work session behavior. Source: `SPRINT_IMPLEMENTATION_PLAN.md`; behavior source `docs/specs/sample-plugin.md`.
  - **Admin Artifact Tools:** provide scripted flows for `sign-plugin-package`, `create-plugin-license`, optional `verify-plugin-package`, and `create-recovery-response` through documented Admin boundaries. Sprint 10 owns AC-007 technical-path and host-import compatibility evidence only; production recovery response release evidence follows ADR-0033 vendor-side signing custody and is deferred by `HD-S10-002`/D-33 to Sprint 11/vendor handover. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/admin-tool.md`, `docs/adr/ADR-0033-signing-key-ownership.md`, `docs/adr/ADR-0039-owner-password-recovery.md`.
  - **Trust Profile Guard:** ensure dev/QA `dobby.dev-qa` artifacts work only in dev/QA profile and are rejected by production builds. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/adr/ADR-0033-signing-key-ownership.md`, `docs/specs/sample-plugin.md`.
- Acceptance / exit expectations and AC ownership:
  - Primary Sprint 10 AC ownership: `AC-031` and `AC-036`. Source: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/07_ACCEPTANCE_CRITERIA.md`.
  - Admin artifact tooling owns the `create-recovery-response` AC-007 technical path and host-import compatibility evidence and supports AC-018/AC-034 evidence. Sprint 5 remains host-side AC-007 owner, while production/Admin `.dobbyrecovery` release evidence is deferred to Sprint 11/vendor handover by `HD-S10-002` and D-33. Source: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/07_ACCEPTANCE_CRITERIA.md`, `artifacts/release-evidence/skeleton-local/decision-log.md`.
  - Verification expected by master plan: sample plugin install/run smoke; integration tests for lifecycle state changes/lifecycle audit; Admin artifact generation and exit-behavior tests against the approved option contract; production build check rejecting dev/QA packages/licenses. Source: `SPRINT_IMPLEMENTATION_PLAN.md`.
- Dependencies:
  - Sprint 5 recovery request/import contract and TEST-ONLY fixture boundary. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`.
  - Sprint 8 plugin runtime, package trust, installed state/cache, devQA/production trust profile foundation. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
  - Sprint 9 license import/runtime mode enforcement. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
  - ADR-0023, ADR-0033, ADR-0045, and specs `docs/specs/admin-tool.md`, `docs/specs/plugins-manager.md`, `docs/specs/sample-plugin.md`.
- Later sprints depending on this sprint:
  - Sprint 11 release gates consolidate Sprint 10 Admin recovery technical-path and host-import compatibility evidence, production/Admin AC-007 release evidence authorized by vendor handover, plugin/package/license smoke, production dev/QA trust exclusion, and release static scans. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`.
- Master-plan explicit out-of-scope items relevant here:
  - Plugin update/repair/downgrade/marketplace/catalog and production sample feature are out of P1 scope. Source: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/plugins-manager.md`.
  - Full Admin UI is P1-if-time/P2 and must not block scripted flows. Source: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/admin-tool.md`, `docs/adr/ADR-0023-admin-host-boundary.md`.
  - Admin exact option names must follow the approved `docs/specs/admin-tool.md` contract and must not be inferred from illustrative examples in `docs/09_BUILD_RUN_AND_RELEASE.md`. Source: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`.

## 4. Prior Sprint Continuity Check

| Sprint | Verified present now | Missing / carried to Sprint 10 | Readiness conclusion |
|---|---|---|---|
| Sprint 5 | Host-side recovery request/export/import, TEST-ONLY fixture authority, and recovery importer tests exist in `src/Dobby.Security/Authentication/RecoveryRequestExporter.cs`, `src/Dobby.Security/Authentication/RecoveryResponseImporter.cs`, `src/Dobby.Host/Login/RecoveryResponseImportCoordinator.cs`, `tests/Dobby.IntegrationTests/Recovery/RecoveryRequestExportTests.cs`, `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs`, and `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`. | Production/Admin `.dobbyrecovery` release evidence was originally deferred to Sprint 10/11 by `SPRINT_IMPLEMENTATION_PLAN.md` and D-13; `HD-S10-002` and D-33 now keep production release evidence deferred to Sprint 11/vendor handover while Sprint 10 implements the technical path. | Host-side dependency ready; Admin option contract is now closed by `HD-S10-001`, while production/Admin recovery release evidence is deferred by `HD-S10-002` and D-33. |
| Sprint 6 | Operation auth infrastructure and `plugins.management` target/verbs are present in `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs` and enforced by `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`. | Sprint 10 must use `plugins.management.view/import/configure/delete`; do not add new operation target names unless docs/ADR change. | Authorization dependency ready. |
| Sprint 7 | Shell/navigation/work session surfaces exist in `src/Dobby.Shell/ShellViewModel.cs`, `src/Dobby.Host/MainWindow.xaml`, `src/Dobby.Host/MainWindow.xaml.cs`, and `src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs`; plugin disable work-session request already exists. | No Plugins Manager list/detail UI exists yet; localization keys are mostly general plugin/license labels in `src/Dobby.Localization/HostResourceKeys.cs` and `src/Dobby.Localization/Resources/Host.az-AZ.resx`. | Shell dependency ready; manager UI is new Sprint 10 work. |
| Sprint 8 | Package reader/validator/fingerprint/installer/trust profile/runtime activation are present in `src/Dobby.PluginFramework/Packaging/`, `src/Dobby.Security/Trust/`, `src/Dobby.Host/Plugins/`, and verified by `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`, `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`, `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs`, and `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. | Installed state has enablement/status/removal ports but lacks full Pending uninstall/data-choice state; sample plugin scaffold is incomplete. | Runtime/package dependency ready; manager lifecycle and sample completion are Sprint 10 work. |
| Sprint 9 | License parsing, signature verification, import, state, current context, and runtime mode enforcement exist in `src/Dobby.Licensing/`, `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`, `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`, and tests under `tests/Dobby.UnitTests/Licensing/` and `tests/Dobby.IntegrationTests/Licensing/`. | Admin license generation artifacts are not implemented; Sprint 9 decision D-29 explicitly disallows product/Admin signed license fixtures. | License runtime dependency ready; Admin generation remains Sprint 10 and must not reuse test-only fixtures as product/Admin artifacts. |

Carry-over decisions and risks:
- `HD-S10-001`: DECIDED/CLOSED. Admin CLI exact option names, option sets, required/optional/default semantics, redaction, profile mapping, and exit-code behavior are approved in `docs/specs/admin-tool.md` and `decision-log.md` D-30. The current `AdminCommandHost` skeleton still needs implementation to replace pre-decision option rejection.
- `HD-S10-002`: DECIDED: production evidence deferred. Production vendor signing material/custody remains governed by ADR-0033 and Sprint 11/vendor handover; Sprint 10 must not commit private keys or treat Sprint 5 TEST-ONLY/dev/QA recovery artifacts as production release evidence.
- `HD-S10-003`: DECIDED: optional, not Sprint 10 release gate. `verify-plugin-package` may remain unimplemented unless implementation chooses to add optional debug/smoke support.
- `HD-S10-004`: DECIDED: D-33 amends `SPRINT_IMPLEMENTATION_PLAN.md` for the AC-007 evidence split. Sprint 10 owns only technical-path and host-import compatibility evidence; production/Admin AC-007 release evidence remains a Sprint 11/vendor handover gate.

Readiness conclusion: dependency code is present and the Admin public option contract is approved, so Sprint 10 implementation may proceed. Production/Admin `.dobbyrecovery` release evidence remains deferred to Sprint 11/vendor handover and must not be claimed by Sprint 10 technical-path or host-import compatibility proof.

## 5. Current Codebase State

### `Dobby.Core`

- `src/Dobby.Core/Result.cs` exposes `Result`, `Result<T>`, and the current `ErrorCode` registry. Sprint 10 must use existing error codes such as `InvalidInput`, `Forbidden`, `ValidationFailed`, `IoFailed`, `DatabaseUnavailable`, `LicenseDenied`, `LicenseReadOnly`, `LicenseBindingMismatch`, `Cancelled`, and `PluginInternalError`; adding new closed-set codes requires authority. Sources: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`, `src/Dobby.Core/Result.cs`.

### `Dobby.Security`

- `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs` already defines `plugins.management` with `view`, `import`, `configure`, and `delete`; Sprint 10 must wire manager actions to this target, not create a new target. Source: `docs/specs/plugins-manager.md`, `docs/adr/ADR-0048-operation-based-authorization.md`.
- `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs` returns non-disclosing deny reason `permission-denied`; UI must avoid revealing grant details. Source: `docs/specs/plugins-manager.md`, `docs/specs/audit-and-diagnostics.md`.
- `src/Dobby.Security/Trust/PluginTrustVerifier.cs` accepts `vendorId="dobby.dev-qa"` only in `DevQa` profile and rejects it in production; `src/Dobby.Security/Dobby.Security.csproj` embeds only one selected trust profile resource. Sources: `docs/adr/ADR-0033-signing-key-ownership.md`, `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`.
- `src/Dobby.Security/Authentication/RecoveryResponsePayload.cs`, `RecoveryCanonicalJsonWriter.cs`, and `RecoveryResponseImporter.cs` define the host-side `.dobbyrecovery` format and verification rules; Admin generation must emit the same payload/envelope semantics. Source: `docs/adr/ADR-0039-owner-password-recovery.md`.

### `Dobby.PluginFramework`

- `src/Dobby.PluginFramework/Packaging/PluginPackageReader.cs`, `PluginPackageValidator.cs`, `PluginPackageFingerprint.cs`, and `PluginPackageDescriptor.cs` provide current `.dobbyplugin` read/validate/fingerprint primitives. Admin signing/verification should reuse these domain shapes where possible and not create a second package-validity model. Sources: `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md`, `docs/specs/admin-tool.md`.
- `InstalledPluginStateStore.cs` and `InstalledPluginMetadataCache.cs` define authoritative installed state and derived metadata cache ports; current records include plugin id, version, fingerprint, display name, description, `IsEnabled`, validation summary, runtime path, status summary, and timestamps. Sources: `artifacts/release-evidence/skeleton-local/decision-log.md` D-24, `docs/specs/plugins-manager.md`.
- Mismatch: current installed state/cache do not model `Pending uninstall`, restart-required deletion, activated-in-current-process distinction, or keep/delete data choice required by AC-031. Sources: `docs/07_ACCEPTANCE_CRITERIA.md`, `docs/specs/plugins-manager.md`, `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs`.

### `Dobby.Host`

- `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs` currently handles local `.dobbyplugin` install, validation, runtime license mode rejection, audit, and diagnostics. Manager install should route through this coordinator, adding permission/background UI orchestration outside it. Sources: `docs/specs/plugins-manager.md`, `docs/specs/license-import.md`.
- `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs` revalidates installed packages, checks license runtime mode, seeds plugin operation targets, activates only enabled plugins, builds navigation contributions, reports progress, and cleans staging/runtime orphans. Lifecycle changes must keep this source of truth intact. Sources: `docs/specs/plugins-manager.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
- `src/Dobby.Host/Plugins/HostInstalledPluginStateStore.cs` and `HostInstalledPluginMetadataCache.cs` persist state/cache to tables created by `src/Dobby.Infrastructure/Persistence/Migrations/0002_postlogin_bootstrap.sql`. Schema currently lacks explicit pending uninstall/delete-data columns; adding durable lifecycle state requires an append-only numbered post-login migration, registration in `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs`, and embedding in `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`. Do not edit `0002_postlogin_bootstrap.sql`, `0003_license_state.sql`, or `0004_license_lifecycle_inputs.sql` for Sprint 10 lifecycle fields because applied migrations are checksum-recorded in `StorageMetadata`. Sources: `docs/08_CODING_STANDARDS.md`, `artifacts/release-evidence/skeleton-local/storage-materialization.md`, `src/Dobby.Infrastructure/Persistence/HostPersistenceMigrator.cs`.
- `src/Dobby.Host/Plugins/PluginRuntimeAuditCoordinator.cs` emits install and activation audit events but does not yet expose methods for `plugin.enable`, `plugin.disable`, or `plugin.uninstall` despite names existing in `src/Dobby.Audit/AuditEventName.cs`. Source: `docs/specs/plugins-manager.md`, `docs/specs/audit-and-diagnostics.md`.
- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` wires post-login plugin state, metadata cache, staging area, install coordinator, runtime coordinator, license import, operation authorization, and background reporter. Sprint 10 must extend this composition root rather than create pre-login plugin discovery. Source: `src/Dobby.Configuration/DobbyOptionsValidator.cs`.

### `Dobby.Shell` and `Dobby.Localization`

- `src/Dobby.Shell/ShellViewModel.cs`, `src/Dobby.Host/MainWindow.xaml`, and `src/Dobby.Host/MainWindow.xaml.cs` currently expose navigation, plugin page rendering, work sessions, settings/about, diagnostics export, and `Plugins > License` import. No Plugins Manager list/detail/install/enable/disable/uninstall UI state exists. Sources: `docs/specs/plugins-manager.md`, `docs/07_ACCEPTANCE_CRITERIA.md`.
- `src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs` already has `RequestPluginDisable`, so disable/uninstall flows should reuse this close-session choice for active plugin work sessions. Sources: `docs/specs/work-sessions.md`, `docs/specs/sample-plugin.md`, `AC-019` in `docs/07_ACCEPTANCE_CRITERIA.md`.
- `src/Dobby.Localization/HostResourceKeys.cs`, `HostStringCatalog.cs`, and `Resources/Host.az-AZ.resx` have general plugin and license labels but no full manager action/status/rejection labels. Add keys only for user-visible Sprint 10 text and preserve resource ownership rules in `docs/08_CODING_STANDARDS.md`.

### `Dobby.Licensing`

- `src/Dobby.Licensing/LicenseArtifact.cs`, `LicenseCanonicalJsonWriter.cs`, `LicenseSignatureVerifier.cs`, `LicenseImportService.cs`, and related tests support import/verification/runtime mode. Admin generation must respect existing v2 import contract and not bypass validation. Sources: `docs/specs/license-import.md`, `ADR-0051`, `ADR-0053`.
- Mismatch: no product/Admin license artifact generation service exists; Sprint 10 needs scripted generation using the approved Admin CLI contract. Source: `docs/specs/admin-tool.md`.

### `Dobby.Admin` and `tools/`

- `src/Dobby.Admin/Commands/AdminCommandHost.cs` recognizes `sign-plugin-package`, `create-plugin-license`, and `create-recovery-response`; unknown commands and pre-decision options currently return exit code `64`. Sprint 10 must replace that skeleton behavior with the approved `docs/specs/admin-tool.md` contract and update smoke coverage. Sources: `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`.
- `src/Dobby.Admin/App.xaml` currently uses `StartupUri="MainWindow.xaml"` and `src/Dobby.Admin/App.xaml.cs` has no command-line startup routing, so `Dobby.Admin.exe --cli <command>` is not yet an executable process path. Sprint 10 must route CLI startup before opening the WPF window. Sources: `docs/specs/admin-tool.md`, `src/Dobby.Admin/App.xaml`, `src/Dobby.Admin/App.xaml.cs`.
- Existing wrapper folders are `tools/sign-plugin-package/`, `tools/create-plugin-license/`, and `tools/create-recovery-response/`; their READMEs may need implementation-time updates to match the approved contract. `tools/verify-plugin-package/` does not exist; the command is optional P1 support only. Sources: `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`.
- `src/Dobby.Admin/Dobby.Admin.csproj` currently references `Dobby.Application`, `Dobby.Core`, and `Dobby.Security`; the architecture map allows Admin to reference additional shared projects, but new references must be justified by actual artifact-service reuse. Sources: `docs/02_ARCHITECTURE_MAP.md`, `docs/adr/ADR-0023-admin-host-boundary.md`.

### `samples/Dobby.Sample.Plugin`

- `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj` references only `Dobby.Contracts` and `Dobby.SDK`, satisfying current architecture tests. Source: `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`.
- `samples/Dobby.Sample.Plugin/SamplePlugin.cs` exposes a page and background progress but does not yet cover the complete greet operation permission deny/success/invalid input/fault/work-session behavior required by `docs/specs/sample-plugin.md` and AC-036.
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` is a scaffold and lacks `vendorId`, `entryAssembly`, `displayName`, `description`, `icon`, `files`, top-toolbar/sidebar routing, and real dev/QA signature. Source: `docs/schemas/dobby-plugin-manifest.schema.json`, `docs/specs/sample-plugin.md`.

## 6. Documentation and ADR Alignment

### Fully aligned, no planning-time edits

- `SPRINT_IMPLEMENTATION_PLAN.md` aligns with current docs/specs: Sprint 10 owns manager/sample/Admin tools/trust guard and recorded the prior Admin option decision dependency. No master-plan edit in this authority patch. Source: Sprint 10 section.
- `docs/specs/plugins-manager.md` aligns with `AC-031` and existing authorization target `plugins.management`; implementation work remains. Sources: `docs/07_ACCEPTANCE_CRITERIA.md`, `src/Dobby.Security/Authorization/BuiltInOperationTargets.cs`.
- `docs/specs/sample-plugin.md` aligns with current architecture/test boundaries; implementation work remains in sample scaffold. Sources: `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`, `samples/Dobby.Sample.Plugin/`.
- `docs/specs/admin-tool.md` now carries the `HD-S10-001` canonical Admin CLI contract; implementation must update the current `AdminCommandHost` skeleton to match it. Source: `src/Dobby.Admin/Commands/AdminCommandHost.cs`.
- `docs/09_BUILD_RUN_AND_RELEASE.md` aligns with Admin wrapper folder names and production dev/QA exclusion requirement; implementation evidence remains. Sources: `tools/*/README.md`, `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`.

### Documents requiring only implementation-time evidence appends

- `artifacts/release-evidence/skeleton-local/architecture-baseline.md`: append Sprint 10 closure evidence, test counts, and artifact/tool results after implementation. Evidence convention from Sprint 8/9 sections.
- `artifacts/release-evidence/skeleton-local/decision-log.md`: `HD-S10-001`, `HD-S10-002`, `HD-S10-003`, and `HD-S10-004` are recorded in D-30 through D-33; append implementation evidence only after verified Sprint 10 work. Existing D-13, D-23, and D-29 continue to govern prior boundaries.
- `artifacts/release-evidence/<build-id>/packaging-static-scan.md`: implementation-time release evidence path for sample/devQA exclusion is defined by `docs/09_BUILD_RUN_AND_RELEASE.md`; do not create it during planning.

### Documents that may need an implementation-time note, not planning edit

- `docs/specs/admin-tool.md`: updated by this authority patch with the human-approved option table; implementation-time edits should be limited to operator examples or evidence notes consistent with that contract.
- `docs/09_BUILD_RUN_AND_RELEASE.md`: only update if wrapper behavior or release evidence path changes by approved tooling decision. Current illustrative option names are not canonical.
- `docs/specs/plugins-manager.md` and `docs/specs/sample-plugin.md`: no expected content update; if implementation discovers an unavoidable behavior mismatch, stop and request authority rather than edit spec.

### ADR alignment classification

| ADR | Classification | Sprint 10 impact |
|---|---|---|
| `ADR-0023-admin-host-boundary.md` | Aligned; open implementation gate for minimal Admin CLI/services. | Admin remains standalone vendor-side tool, not customer host/plugin; no host business duplication. |
| `ADR-0032-plugin-signature-trust-model.md` | Aligned; no action. | Use existing offline ECDSA trust verifier and embedded public keys. |
| `ADR-0033-signing-key-ownership.md` | Aligned, but production private-key custody remains human/release gate. | Dev/QA sample/license artifacts use dev/QA material; production recovery uses vendor-side signing custody. |
| `ADR-0036-plugin-manifest-schema.md` | Aligned; implementation must satisfy schema. | Sample manifest and Admin signing must match schema and canonical fields. |
| `ADR-0037-plugin-compatibility-and-versioning.md` | Aligned; no action. | Sample version/minHost must be stable SemVer without prerelease. |
| `ADR-0039-owner-password-recovery.md` | Aligned; implementation work. | `create-recovery-response` must emit signed `.dobbyrecovery` with no user picker/target user fields. |
| `ADR-0045-plugin-package-and-license-artifact-boundary.md` | Aligned; implementation work. | Package/license separation and forbidden content checks remain authoritative. |
| `ADR-0048-operation-based-authorization.md` | Aligned via existing target. | Manager actions use existing `plugins.management` verbs; sample operation target from signed manifest. |
| `ADR-0051-license-lifecycle-v2.md` | Aligned; implementation support. | Admin-created `.dobbylicense` must import through current v2 path. |
| `ADR-0053-capability-surface-runtime-mode.md` | Aligned; no action. | Manager/sample must respect host-enforced runtime mode; no raw license details to plugin/UI. |

### Specs and schemas alignment

- `docs/schemas/dobby-plugin-manifest.schema.json` requires fields not present in current sample scaffold; Sprint 10 must update the sample artifact and tests. Source: schema and `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json`.
- `docs/specs/admin-tool.md` exact option table is now present; this removes the Admin public option gate while preserving production evidence deferral under `HD-S10-002` and D-33.

Net documentation footprint summary: this authority patch updates the Admin option spec, master-plan AC-007 evidence split, Sprint 10 plan status, and decision log only. Implementation may append release evidence after verified work.

## 7. Files to Create

Naming rule: every new file must map to a documented role in `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/02_ARCHITECTURE_MAP.md`, an ADR/spec/schema, current code ownership, or existing test/evidence convention. If implementation discovers a needed name that is not source-backed, stop and record it under §7a before creating it.

| Placement | Filename | Purpose | Source basis | Associated tests | Boundary / architecture restrictions |
|---|---|---|---|---|---|
| `tools/sign-plugin-package/` | `sign-plugin-package.ps1` | Script wrapper invoking published `Dobby.Admin.exe --cli sign-plugin-package` with the approved contract. | `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, existing `tools/sign-plugin-package/README.md` | Admin command/wrapper tests for approved option table. | Must preserve exit code/stdout/stderr, no private key/raw path/stack leak. |
| `tools/create-plugin-license/` | `create-plugin-license.ps1` | Script wrapper invoking `Dobby.Admin.exe --cli create-plugin-license` with the approved contract. | `docs/specs/admin-tool.md`, existing README | License generation/import smoke tests for approved contract. | Dev/QA smoke material only for sample/license; no production private key in repo. |
| `tools/create-recovery-response/` | `create-recovery-response.ps1` | Script wrapper invoking `Dobby.Admin.exe --cli create-recovery-response` with the approved contract. | `docs/specs/admin-tool.md`, `docs/adr/ADR-0039-owner-password-recovery.md`, existing README | Recovery generation and host import verification tests for approved contract. | AC-007 technical path and host-import compatibility follow ADR-0033/ADR-0039; production/Admin release evidence is deferred by `HD-S10-002` and D-33. |
| `tools/verify-plugin-package/` | optional `README.md` and optional wrapper | Optional debug/smoke support only; not a Sprint 10 release gate. | `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md` | Optional package verification tests only if implemented. | Must not replace install-time validation; do not make required for Sprint 10 closure. |
| `src/Dobby.Infrastructure/Persistence/Migrations/` | `0005_plugin_lifecycle_state.sql` | Append-only post-login lifecycle migration for pending uninstall, restart-required deletion, data-retention choice, and activated/current-process tracking if durable schema expansion is required. | `docs/specs/plugins-manager.md`, `docs/08_CODING_STANDARDS.md`, `artifacts/release-evidence/skeleton-local/storage-materialization.md` | Fresh and already-migrated persistence tests, including `StorageMetadata` recording. | Do not edit `0002`, `0003`, or `0004`; migration must be registered and embedded before it can apply. |
| `samples/Dobby.Sample.Plugin/` | sample icon asset file, if no suitable existing file exists | Required manifest icon with minimum 64x64 PNG per sample/plugin manager specs. | `docs/specs/sample-plugin.md`, `docs/specs/plugins-manager.md` | Sample package manifest/validation tests. | File name is not source-fixed; see `A-S10-005`. Must not include production trust/private material. |
| `tests/Dobby.UnitTests/Admin/` or existing smoke tests | Admin command/service test files for the approved option contract | Unit-level coverage for parsed DTOs, exit code/stdout/stderr behavior, redaction, and command dispatch. | `docs/specs/admin-tool.md`, `tests/Dobby.SmokeTests/ShellSmokeTests.cs` | New or extended Admin tests. | Exact file/class names are assumptions; UnitTests currently does not reference `Dobby.Admin`, so project reference changes may be needed and justified. |
| `tests/Dobby.UnitTests/Host/` / `tests/Dobby.UnitTests/PluginFramework/Packaging/` | manager lifecycle test files or extensions | Enable/disable/uninstall/pending uninstall/status state tests. | `docs/specs/plugins-manager.md`, existing host/plugin framework tests | New/extended lifecycle tests. | Prefer extending existing tested owner areas before creating new files. |
| `tests/Dobby.IntegrationTests/Plugins/` / `tests/Dobby.IntegrationTests/Persistence/` | lifecycle integration tests or extensions | AC-031 install/list/status/enable/disable/uninstall/pending deletion/cache persistence coverage. | `docs/07_ACCEPTANCE_CRITERIA.md`, existing plugin/persistence integration tests | New/extended integration tests. | Must use existing persistence/runtime harnesses; no pre-login discovery. |
| `tests/Dobby.IntegrationTests/Architecture/` | production devQA exclusion test or extension | Static scan for sample package/license/preinstalled runtime/devQA trust metadata in production artifacts. | `docs/09_BUILD_RUN_AND_RELEASE.md`, `ADR-0033`, existing architecture tests | New/extended architecture test. | Exact class/file name assumption; do not duplicate `PluginTrustResourcePackagingTests` unnecessarily. |
| `artifacts/release-evidence/<build-id>/` | `packaging-static-scan.md` at implementation/release time | Release evidence that production package excludes sample/devQA artifacts. | `docs/09_BUILD_RUN_AND_RELEASE.md` | Architecture/static scan tests. | Build-id-specific evidence only; do not create during planning. |

## 7a. Implementation assumptions for new files

### A-S10-001 — Plugin lifecycle service file placement

- Assumption: If existing `HostPluginRuntimeCoordinator`, `PluginPackageInstaller`, state store, and metadata cache become too broad, create a focused lifecycle orchestration file under the current plugin ownership boundary: `src/Dobby.PluginFramework/Packaging/` for domain ports/outcomes or `src/Dobby.Host/Plugins/` for host composition/adapters.
- Cited source: `docs/02_ARCHITECTURE_MAP.md` rows for `Dobby.PluginFramework` plugin management/packaging and `Dobby.Host.Plugins`; `docs/08_CODING_STANDARDS.md` namespace table; current files under `src/Dobby.PluginFramework/Packaging/` and `src/Dobby.Host/Plugins/`.
- Risk if wrong: a new lifecycle type could cross persistence/UI/security boundaries or duplicate runtime coordinator responsibilities.
- Fallback: stop and request a namespace/file placement decision before creating a new public or skeleton-critical lifecycle file.

### A-S10-002 — Plugins Manager shell model/view file names

- Assumption: If `ShellViewModel`/`MainWindow.xaml` cannot remain maintainable, create manager-specific bindable model/view helper files under existing `Dobby.Shell`/`Dobby.Host` ownership rather than a new project.
- Cited source: `docs/specs/plugins-manager.md`, `docs/02_ARCHITECTURE_MAP.md` row 79, `docs/08_CODING_STANDARDS.md` MVVM/file-structure rules, current `src/Dobby.Shell/ShellViewModel.cs` and `src/Dobby.Host/MainWindow.xaml`.
- Risk if wrong: UI logic may land in code-behind or a new unsupported project/namespace.
- Fallback: keep changes inside current shell files or ask for an architecture/documentation amendment.

### A-S10-003 — Admin artifact service names and project references

- Assumption: Admin artifact generation may require new service classes under `src/Dobby.Admin/` and additional references to existing shared projects (`Dobby.PluginFramework`, `Dobby.Licensing`, `Dobby.Contracts`, `Dobby.Localization`, `Dobby.Diagnostics`, `Dobby.Audit`) only when each reference is used for shared behavior.
- Cited source: `docs/02_ARCHITECTURE_MAP.md` `Dobby.Admin` dependency row, `docs/adr/ADR-0023-admin-host-boundary.md`, current `src/Dobby.Admin/Dobby.Admin.csproj`.
- Risk if wrong: Admin could duplicate host business rules or over-reference projects.
- Fallback: implement artifact logic in an existing shared owner project when already owned there, or stop for a placement decision.

### A-S10-004 — Wrapper script names and option contract are approved

- Assumption: Script filenames may match their canonical tool folder/command names (`sign-plugin-package.ps1`, `create-plugin-license.ps1`, `create-recovery-response.ps1`) because folder/script names are stable, and options must match the approved `docs/specs/admin-tool.md` contract.
- Cited source: `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, existing `tools/*/README.md`, `decision-log.md` D-30.
- Risk if wrong: wrapper docs/tests may drift from the approved public command-line contract.
- Fallback: stop and update the Admin spec/decision-log authority before implementing any contract change.

### A-S10-005 — Sample plugin icon/resource filenames

- Assumption: A PNG icon asset may be added under `samples/Dobby.Sample.Plugin/` with an implementation-chosen filename if no existing suitable file exists.
- Cited source: `docs/specs/sample-plugin.md`, `docs/specs/plugins-manager.md`, `docs/schemas/dobby-plugin-manifest.schema.json`.
- Risk if wrong: sample package validation may depend on an asset naming convention not currently documented.
- Fallback: use the most local descriptive filename and cover it with manifest/package validation tests; if reviewers require a canonical name, request spec clarification.

### A-S10-006 — Production devQA exclusion scan test placement

- Assumption: The static scan can be implemented by extending `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs` or `PackageGovernanceTests.cs`, or by adding a new architecture test file if extension would blur responsibilities.
- Cited source: `docs/09_BUILD_RUN_AND_RELEASE.md`, `docs/adr/ADR-0033-signing-key-ownership.md`, existing architecture tests.
- Risk if wrong: duplicate or overlapping architecture tests make release evidence harder to audit.
- Fallback: prefer extending the nearest existing architecture test; only create a new file after documenting why neither owner fits.

Implementation assumption count: 6.

## 8. Files to Modify

| Path | Exact change scope | Reason | Boundary impact | Required test updates |
|---|---|---|---|---|
| `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs` | Extend domain record/ports only if needed for Pending uninstall, restart-required deletion, data-retention choice, and activated/current-process tracking. | AC-031 requires lifecycle states beyond current `IsEnabled`/`StatusSummary`. | Domain port only; no `Dobby.Infrastructure` dependency. | Unit tests for state transitions; integration persistence tests. |
| `src/Dobby.PluginFramework/Packaging/InstalledPluginMetadataCache.cs` | Extend manager read model only for fields required by list/detail/status/restart-required display. | Manager list/details must show name/version/status/description and restart-required state. | Derived cache remains rebuildable, not source of truth. | Cache rebuild/persistence tests. |
| `src/Dobby.PluginFramework/Packaging/PluginPackageInstaller.cs` | Preserve install rollback and update initial status/cache fields for new status model. | New lifecycle state must not break install atomicity. | Package/install domain only. | Existing installer tests plus new state/cache tests. |
| `src/Dobby.Host/Plugins/HostInstalledPluginStateStore.cs` | Persist new authoritative lifecycle fields and transition methods; add parameterized SQL only. | Durable AC-031 state. | Host-owned adapter may reference persistence; no UI logic. | `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs`. |
| `src/Dobby.Host/Plugins/HostInstalledPluginMetadataCache.cs` | Persist/cache new read-model fields and rebuild behavior. | Manager read model and restart state. | Derived cache remains host adapter. | Persistence tests. |
| `src/Dobby.Infrastructure/Persistence/Migrations/0005_plugin_lifecycle_state.sql` | Add a new deterministic append-only migration if lifecycle schema expansion is required. | Current `0002_postlogin_bootstrap.sql` lacks pending uninstall/data choice fields, and earlier migrations are checksum-recorded. | Migration naming/style governed by `docs/08_CODING_STANDARDS.md`; do not edit `0002`, `0003`, or `0004`. | Fresh and already-migrated persistence tests, including migration metadata recording. |
| `src/Dobby.Infrastructure/Persistence/PostLoginPluginPersistenceMigrations.cs` | Register the new `0005_plugin_lifecycle_state` migration ID and load its embedded SQL resource. | `HostPluginRuntimeCoordinator.ApplyPostLoginMigrations()` applies only migrations present in `PostLoginPluginPersistenceMigrations.All`. | Registry-only change; preserve sorted numbered migration order and checksum behavior. | Persistence migration tests prove `0005` is applied and recorded after `0002`-`0004`. |
| `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` | Embed `Persistence\Migrations\0005_plugin_lifecycle_state.sql` with the matching logical resource name. | `PostLoginPluginPersistenceMigrations.LoadSql` reads embedded SQL resources. | No package pin or target-framework change. | Build plus migration tests that would fail if the resource is missing. |
| `src/Dobby.Host/Plugins/PluginInstallStagingArea.cs` | Add host-controlled deletion scheduling/runtime cleanup behavior only if lifecycle cannot be implemented through existing cleanup methods. | Pending uninstall requires runtime file deletion on next app start before fresh discovery. | Filesystem adapter remains host-owned. | Staging/recovery cleanup tests. |
| `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs` | Skip/disable/hide pending uninstall plugins, delete scheduled runtime files during post-login recovery, preserve revalidation/license gates. | AC-031 requires disabled/hidden/pending behavior and next-start deletion. | Must not add pre-login discovery. | Runtime coordinator tests and integration activation tests. |
| `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs` | Add manager-facing install result/rejection mapping only if current result lacks necessary typed user feedback. | Manager install UI needs typed non-raw reasons. | Keep package validation in PluginFramework/Security. | Host coordinator tests. |
| `src/Dobby.Host/Plugins/PluginRuntimeAuditCoordinator.cs` | Add audit methods for `plugin.enable`, `plugin.disable`, `plugin.uninstall` using existing `AuditEventName` constants and redacted details. | AC-031/spec require lifecycle audit. | No new audit names without ADR/spec approval. | Plugin audit coordinator tests and audit taxonomy tests if constants change. |
| `src/Dobby.Host/Plugins/PluginRuntimeDiagnosticsCoordinator.cs` | Add non-secret lifecycle diagnostics only if needed for support/review. | Diagnostics spec allows plugin lifecycle troubleshooting detail. | No raw paths/secrets/private keys/raw license/recovery payload. | Diagnostics tests. |
| `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` and `src/Dobby.Host/Login/HostLoginRuntime.cs` | Wire any new manager/lifecycle coordinator and expose only post-login surfaces needed by shell. | Existing composition root owns plugin runtime/install/license wiring. | No pre-login discovery; no service locator. | Login/runtime wiring tests or host unit tests. |
| `src/Dobby.Host/App.xaml.cs` | Configure manager surface permissions and shell state using `plugins.management` verbs; preserve existing license setup. | Manager UI authorization. | Use `OperationAuthorizationPolicy`; no UI-only authorization. | Shell/host tests. |
| `src/Dobby.Host/MainWindow.xaml` | Add Plugins Manager list/detail/action/import/confirmation states in the existing shell surface or a new sourced view. | AC-031 UI. | View-only wiring; no business rules in XAML/code-behind. | Snapshot/static UI tests. |
| `src/Dobby.Host/MainWindow.xaml.cs` | Add click/adapter wiring for file picker/confirm dialogs only where binding is not practical; delegate business rules to coordinators/view models. | Install/uninstall confirmation and action invocation. | Code-behind remains view wiring per `docs/08_CODING_STANDARDS.md`; no direct business rules. | Host UI interaction tests. |
| `src/Dobby.Shell/ShellViewModel.cs` | Add bindable manager state/actions/feedback or delegate to sourced manager model; keep OS dialogs out. | AC-031 UI state. | ViewModel does not open file dialogs or hold WPF controls. | Shell view model tests. |
| `src/Dobby.Shell/WorkSessions/WorkSessionCloseCoordinator.cs` | Extend only if uninstall needs a separate active-session close reason; otherwise use existing `PluginDisable`. | AC-019/AC-031 active plugin disable/uninstall flow. | Preserve work-session choices `Cancel/Minimize/Close`. | Work session integration tests. |
| `src/Dobby.Localization/HostResourceKeys.cs`, `src/Dobby.Localization/HostStringCatalog.cs`, `src/Dobby.Localization/Resources/Host.az-AZ.resx` | Add manager action/status/rejection/confirmation labels only. | User-visible UI and non-disclosing rejection labels. | Resource keys remain host-owned; plugin resource ownership not mixed. | Localization visible-key tests. |
| `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` | Fill required schema fields, dev/QA vendor id, entry assembly, display metadata, icon, files, extension points, operation target, signature. | AC-036 sample artifact. | No `.dobbylicense`, customer data, device fingerprint, private key, host config/migrations. | Manifest/package validation tests. |
| `samples/Dobby.Sample.Plugin/SamplePlugin.cs` | Add/finish greet operation behavior, permission denial behavior through contract surface, invalid-input `Result`, fault isolation trigger, progress/work-session behavior as supported by current Contracts/SDK. | AC-036. | References only `Dobby.Contracts` and `Dobby.SDK`; no WPF/Host/Infrastructure. | Sample integration/contract tests and architecture boundary tests. |
| `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj` | Add only required non-code package assets/resources; preserve Contracts/SDK-only project references. | Sample manifest/icon/package output. | No host/infrastructure/package persistence refs. | `PluginAndSdkBoundaryTests`, `PluginNoInfrastructureReferenceTests`. |
| `src/Dobby.Admin/Commands/AdminCommandHost.cs` | Replace skeleton ready/pre-decision behavior with approved command parsing, parsed DTO handoff, exit code/stdout/stderr contract from `docs/specs/admin-tool.md`; keep it callable from process startup. | Admin scripted flows. | CLI only parses/validates input; business/signing rules in services. | Admin command tests and process-level smoke tests. |
| `src/Dobby.Admin/Dobby.Admin.csproj` | Add shared project references only as needed by Admin artifact service reuse. | Admin may need PluginFramework/Licensing/Contracts per architecture map. | No new package pins without approval. | Package/project boundary tests. |
| `src/Dobby.Admin/App.xaml`, `src/Dobby.Admin/App.xaml.cs` | Add required CLI startup routing: detect `--cli`, call `AdminCommandHost`, write redacted stdout/stderr, exit with the command result, and avoid opening `MainWindow` in CLI mode. Remove or bypass `StartupUri` as needed. | `docs/specs/admin-tool.md` makes `Dobby.Admin.exe --cli <command>` canonical for wrappers. | Admin remains standalone; full Admin UI is still P1-if-time/P2 and must not block CLI. | Process-level executable/wrapper smoke tests for exit code/stdout/stderr behavior. |
| `src/Dobby.Admin/MainWindow.xaml*` | Minimal UI only if needed for non-CLI mode; full Admin UI is not required and must not block CLI. | ADR-0023 allows minimal CLI/simple UI path; full UI P1-if-time/P2. | Non-CLI UI must not run for `--cli` invocations. | Admin smoke tests only if non-CLI UI behavior changes. |
| `tools/*/README.md` | Update to match the approved option table when wrappers are implemented. | Tool operator docs/evidence. | Do not drift from `docs/specs/admin-tool.md`; `docs/09_BUILD_RUN_AND_RELEASE.md` examples remain illustrative unless separately updated. | Tool/readme static tests if present. |
| `tests/**` | Add/extend targeted unit, integration, architecture, and smoke tests described in §14. | Required AC/evidence coverage. | Test project references follow current TFM/package governance. | Full test suite at closure. |
| `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | Append Sprint 10 evidence only after implementation and verification. | Release evidence continuity. | No planning-time edit. | Sprint 10 release evidence test if added. |
| `artifacts/release-evidence/skeleton-local/decision-log.md` | Append only if human decision closes option/key gate. | Authority closure. | No invented decision IDs. | Evidence/decision tests if added. |

## 9. Files Not Touched

| Surface | Do not touch because | Source basis |
|---|---|---|
| `SPRINT_IMPLEMENTATION_PLAN.md` | Master plan is authority input, not sprint output. | User prompt; `SPRINT_IMPLEMENTATION_PLAN.md`. |
| `SPRINT_1_EXECUTION_PLAN.md` through `SPRINT_9_EXECUTION_PLAN.md` | Prior plans are inputs; Sprint 10 must not rewrite predecessor evidence. | User prompt; prior plan discovery. |
| `SPRINT_11_*` or release sprint scope | Sprint 11 owns installer/release-gate consolidation; Sprint 10 only supplies named inputs. | `SPRINT_IMPLEMENTATION_PLAN.md`. |
| `docs/adr/**` | ADR changes require authority; Sprint 10 should implement existing ADRs. | User prompt; `docs/08_CODING_STANDARDS.md`. |
| `docs/specs/**` and `docs/schemas/**` except the approved `docs/specs/admin-tool.md` contract update | Specs/schema are current behavior authority; only update after human/tooling decision or ADR/spec amendment. | User prompt; `docs/specs/admin-tool.md`; D-30. |
| `docs/09_BUILD_RUN_AND_RELEASE.md` | Current Admin option examples are illustrative; do not freeze option names by editing docs. | `docs/09_BUILD_RUN_AND_RELEASE.md`. |
| `Directory.Packages.props` | No new package pins needed or approved for Sprint 10; new pins require human decision. | `docs/03_TECH_STACK_AND_VERSIONS.md`, `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`. |
| `global.json`, `Directory.Build.props` | Toolchain/build policy already pinned. | Current files and `docs/03_TECH_STACK_AND_VERSIONS.md`. |
| `src/Dobby.Core/Result.cs` | Do not add new `ErrorCode` values unless explicitly approved. | `docs/04_RESULT_VS_EXCEPTION_POLICY.md`. |
| Plugin-visible Contracts/SDK public surfaces | Do not widen unless sample plugin behavior cannot be implemented with existing contracts and a source-backed decision approves widening. | `docs/02_ARCHITECTURE_MAP.md`, `docs/specs/sample-plugin.md`, `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`. |
| Print-boundary files such as `src/Dobby.Application/Printing/PrintTargetClassifier.cs` | Sprint 9 completed print/license boundary sunset; Sprint 10 has no print scope. | `SPRINT_9_EXECUTION_PLAN.md`, `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. |
| Branch Shared, PostgreSQL, `Dobby.Update`, installer/MSIX/WiX artifacts | Explicitly outside P1 critical Sprint 10 scope or Sprint 11/future. | `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/02_ARCHITECTURE_MAP.md`, `docs/03_TECH_STACK_AND_VERSIONS.md`. |
| Marketplace/catalog/update/repair/downgrade plugin features | Explicitly out of P1/Sprint 10. | `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/plugins-manager.md`. |
| Production private key files or arbitrary runtime trust-anchor paths | Forbidden in repo/runtime resources; production custody is vendor-side. | `docs/adr/ADR-0033-signing-key-ownership.md`, `artifacts/release-evidence/skeleton-local/decision-log.md`. |
| Sprint 5 TEST-ONLY recovery fixtures as product/Admin outputs | Sprint 5 fixtures are host-side test evidence only; Sprint 10 AC-007 technical-path evidence is separate from Sprint 11 production/Admin `.dobbyrecovery` release evidence. | `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`, D-13 and D-33 in `decision-log.md`. |
| Customer production package default sample inclusion | Sample plugin is dev/QA verification artifact, not default visible/shipped feature. | `docs/specs/sample-plugin.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`. |

## 10. Error / Result / Policy Mapping

| Surface | Expected failures use | Existing `ErrorCode` values | Local sub-codes / reason strings | Audit / diagnostics | User-facing policy |
|---|---|---|---|---|---|
| Manager view/list | `Result<IReadOnlyList<InstalledPluginMetadata>>` or bindable empty/error state | `DatabaseUnavailable`, `Forbidden`/non-disclosing deny if authorization fails | Status values from `docs/specs/plugins-manager.md`: `Enabled`, `Disabled`, `Skipped`, `Rejected`, `Faulted`, `ReadOnlyExpired`, `Pending uninstall` | Diagnostics only for data load failures; no raw DB details. | Empty/error/loading states localized; no stack traces. |
| Install local `.dobbyplugin` | Existing `HostPluginPackageInstallCoordinator.InstallPackage` `Result<InstalledPluginState>` | `InvalidInput`, `ValidationFailed`, `IoFailed`, `DatabaseUnavailable`, `LicenseReadOnly`, `LicenseDenied`, `LicenseBindingMismatch` | `invalid-manifest`, `signature-failed`, `trust-failed`, `forbidden-content`, `hash-mismatch`, `compatibility-mismatch`; current code also uses `unsupported-package-source`, `license-read-only`, `install-failed`. | Existing install audit names; diagnostics marker `plugin.install.rejected:<reason>`. | Non-disclosing typed reason label; no raw paths except sanitized package name if already approved in audit. |
| Enable/disable | `Result` | `NotFound`, `Forbidden`, `DatabaseUnavailable`, `Cancelled` | Status summary `Enabled`/`Disabled`; no new error code. | Use existing `plugin.enable`, `plugin.disable` audit names in `AuditEventName`; add non-secret details. | Permission denial shown as non-disclosing localized message. |
| Uninstall | `Result` | `NotFound`, `Forbidden`, `DatabaseUnavailable`, `IoFailed`, `Cancelled` | Data choice: keep/delete data; lifecycle status `Pending uninstall` for activated plugin. | Use existing `plugin.uninstall`; diagnostics may record schedule/delete outcome without secrets/full raw paths. | Confirmation required; cannot proceed without keep/delete data choice. |
| Activation after lifecycle changes | Existing activation `Result<PluginActivationExecutionOutcome>` | `Cancelled`, `DatabaseUnavailable`, `PluginInternalError`, `ValidationFailed`, license errors | `license-denied`, `license-binding-mismatch`, dependency/compatibility/package reasons already used. | Existing `plugin.activation.skipped`, `plugin.activation.rejected`, `plugin.runtime.faulted`. | Disabled/pending uninstall plugins hidden from navigation/runtime. |
| Sample plugin page/action | `Result<T>` through Contracts/SDK | `InvalidInput`, `Forbidden`, `UnsupportedOperation`, `PluginInternalError`, `Cancelled` | Sample operation target `com.dobby.sample.greet.execute`; no new host audit name. | Runtime faults become plugin fault diagnostics/audit through existing boundary. | Permission deny does not reveal grant internals; invalid input returns typed result. |
| Admin `sign-plugin-package` | Command result with exit code/stdout/stderr, plus service `Result` internally | Existing codes only; map to exit nonzero | Typed stderr reason codes per approved contract. | Operator-side diagnostics; no host audit. | stdout artifact path/redacted summary; stderr no private key/raw payload/full path/stack trace. |
| Admin `create-plugin-license` | Same as above | Existing codes only | Existing license reject vocabulary for compatibility with import: `invalid-license-file`, `invalid-signature`, `untrusted-vendor`, `binding-mismatch`, `package-mismatch`, `older-revision`, `permission-denied`. | Operator diagnostics; host audit only on later import. | Redacted summary only; no raw license payload. |
| Admin `create-recovery-response` | Same as above | Existing codes only | Recovery outcomes compatible with `success`, `signature-fail`, `expired`, `replay`, `device-mismatch` where relevant to verification; generation failures use typed non-secret stderr codes per approved contract. | Operator diagnostics; host audit only on import. | No managed-user picker or target user fields. |

Plugin-host boundary statement: plugin code must see only `Dobby.Contracts` and `Dobby.SDK`; host internal state, WPF controls, persistence, signing material, and authorization grant details remain host-owned. Sources: `docs/specs/sample-plugin.md`, `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`.

Result-vs-exception split discipline:
- Expected validation, permission, package, license, lifecycle, and artifact failures use `Result`/typed command results. Source: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.
- Programmer/invariant failures may throw; public/plugin boundaries translate non-fatal exceptions to existing `ErrorCode` values. Source: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.
- Cancellation may throw internally but maps to `ErrorCode.Cancelled` at public/plugin boundaries. Source: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.
- Fatal exceptions (`OutOfMemoryException`, `StackOverflowException`) are not swallowed. Source: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.

No new audit event names, `ErrorCode` values, operation verbs, trust profiles, or license reject reason names are approved in this plan.

## 11. Technology Stack and Versions

| Area | Current version / constraint | Sprint 10 consequence |
|---|---|---|
| .NET SDK | `global.json` pins `10.0.202`, `rollForward=latestPatch`. | Do not change SDK pin. |
| C# / build | `Directory.Build.props` sets nullable enable, implicit usings, `LangVersion=14.0`, warnings as errors, latest analysis, deterministic build. | Sprint 10 code must compile warning-free and follow C# 14/nullable rules. |
| Package management | `Directory.Packages.props` uses central package management. | No inline versions; no new package pins without human approval. |
| UI | WPF, `WPF-UI` `4.3.0`, `CommunityToolkit.Mvvm` `8.4.2`. | Manager UI uses existing WPF stack; no commercial UI package. |
| Tests | xUnit `2.9.3`, Shouldly `4.3.0`, NSubstitute `5.3.0`, Verify.Xunit `31.12.5`, Microsoft.NET.Test.Sdk `18.5.1`. | Use existing test stack only. |
| Plugin manifest validation | `JsonSchema.Net` `9.2.0`, approved only for manifest schema validation in `Dobby.PluginFramework`. | Do not reuse `JsonSchema.Net` outside approved scope without authority. |
| SemVer | `NuGet.Versioning` `7.3.1`. | Sample version/dependencies and compatibility use existing SemVer tools. |
| Crypto | BCL `System.Security.Cryptography`; `System.Security.Cryptography.ProtectedData` `10.0.7` already pinned. | ECDSA signing/verifying uses BCL; do not add crypto packages. |
| Persistence | `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core`/provider `3.0.3`, Dapper `2.1.72`. | Lifecycle schema/state changes use existing persistence stack. |
| Native assets | Encrypted SQLite native provider evidence is existing release scope; plugin trust PEM hashes are recorded in `PluginTrustResourcePackagingTests.cs`. | Sprint 10 must not add native assets or private key material. |
| TFM | Host/Shell/Admin/tests are `net10.0-windows`; Contracts/SDK/Core/PluginFramework/Licensing/Security are mostly `net10.0`; Sample plugin is `net10.0`. | Preserve platform-neutral plugin-facing surfaces; Admin remains WPF `WinExe`. |
| Encoding | UTF-8 no BOM, final newline, no trailing whitespace. | Applies to scripts, manifests, docs/evidence, code. |

Package pin impact:
- Allowed without new pins: BCL zip/JSON/crypto/file IO, existing WPF/UI, existing test stack, existing SQLite/Dapper, existing PluginFramework/Licensing/Security packages.
- Requires new human approval: any new NuGet package, package version bump, commercial UI dependency, alternate CLI parser package, alternate JSON canonicalization package, new crypto/signing package, WiX/MSIX/installer tooling change. Sources: `docs/03_TECH_STACK_AND_VERSIONS.md`, `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`.

Zero-warning / format / encoding constraints:
- Run formatter and tests at implementation closure, but planning agent did not run them per prompt. Source: `docs/08_CODING_STANDARDS.md`, `Directory.Build.props`.
- Do not add `Console.WriteLine`, `dynamic`, `#region`, public mutable fields, UI-thread blocking waits, or direct OS dialogs from ViewModels. Source: `docs/08_CODING_STANDARDS.md`.

## 12. Code Work Plan

### Pre-flight

1. Confirm `HD-S10-001` approved contract before Admin tool implementation.
   - Action: check `docs/specs/admin-tool.md` and `artifacts/release-evidence/skeleton-local/decision-log.md` D-30 for the approved option table covering `sign-plugin-package`, `create-plugin-license`, `create-recovery-response`, and optional `verify-plugin-package`.
   - Success criteria: approved table exists with option names/sets, required/optional flags, defaults, output paths, profile mapping, exit codes, and redaction contract.
   - Out-of-scope guardrail: do not infer option names from illustrative examples in `docs/09_BUILD_RUN_AND_RELEASE.md`.

2. Lock test-first slices for manager lifecycle, sample plugin, Admin commands, and trust exclusion.
   - Action: create/extend tests named in §14 before production changes.
   - Success criteria: tests fail for current missing behavior and cite AC/spec behavior.
   - Out-of-scope guardrail: do not create tests for marketplace/update/repair/downgrade/full Admin UI.

3. Reconfirm current package/toolchain pins.
   - Action: inspect `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and package governance tests.
   - Success criteria: implementation does not require new packages; if it does, stop for human package-pin decision.
   - Out-of-scope guardrail: no package upgrades or CLI parser package.

### Implementation

4. Implement installed plugin lifecycle state model.
   - Action: extend existing `InstalledPluginState`, metadata cache, host persistence adapters, and append-only migration only as required for `Enabled`, `Disabled`, `Pending uninstall`, restart-required, and data-choice behavior. If schema expansion is needed, create `0005_plugin_lifecycle_state.sql`, register it in `PostLoginPluginPersistenceMigrations.cs`, and embed it in `Dobby.Infrastructure.csproj`.
   - Success criteria: state/cache/unit/persistence tests cover enable, disable, uninstall never-activated immediate delete, activated pending uninstall, cache rebuild, fresh database migration, already-migrated `0002`-`0004` upgrade, and `StorageMetadata` recording for `0005_plugin_lifecycle_state`.
   - Out-of-scope guardrail: no PluginFramework dependency on `Dobby.Infrastructure`; do not edit existing `0002`, `0003`, or `0004` migrations because recorded migration checksums must remain stable.

5. Implement manager lifecycle orchestration in current host/plugin boundaries.
   - Action: add coordinator behavior for list/detail/enable/disable/uninstall/install permission checks using `OperationAuthorizationPolicy` and `plugins.management` verbs.
   - Success criteria: permission-denied paths fail closed and non-disclosing; audit events emitted for enable/disable/uninstall/install; runtime activation excludes disabled/pending uninstall plugins.
   - Out-of-scope guardrail: UI visibility alone is not authorization.

6. Wire Plugins Manager UI and localization.
   - Action: add post-login shell state, list/detail/actions/import/confirmation/restart-required/error/loading/empty states; add localized resource keys.
   - Success criteria: AC-031 UI state is testable; file picker/dialog work stays behind host-owned adapters/code-behind wiring; background operations are reported for long-running install/uninstall work.
   - Out-of-scope guardrail: no OS dialogs from ViewModels; no full redesign of shell.

7. Finalize sample plugin artifact.
   - Action: update sample manifest to schema/spec, add dev/QA `vendorId`, display metadata, icon, files, routes, operation target; implement sample page/action/progress/fault/Result behavior through Contracts/SDK only.
   - Success criteria: package validation passes under DevQa profile, production rejects dev/QA artifact, sample action deny/success/invalid/fault/work-session behavior is covered.
   - Out-of-scope guardrail: sample is not bundled/preinstalled/default-visible in production.

8. Implement Admin artifact services and CLI command dispatch from the approved `HD-S10-001` contract.
   - Action: parse approved options into DTOs, call shared application/security/plugin/licensing services, write artifacts and redacted reports, preserve exit code/stdout/stderr contract, and wire `Dobby.Admin` process startup so `Dobby.Admin.exe --cli <command>` invokes `AdminCommandHost` without opening the WPF window.
   - Success criteria: `sign-plugin-package`, `create-plugin-license`, and `create-recovery-response` generate artifacts that current host/package/license/recovery import paths verify; recovery-response proof uses approved non-production or otherwise available non-release signing material; executable/wrapper smoke tests prove exit code/stdout/stderr behavior through the real process path; optional `verify-plugin-package` remains optional debug/smoke support and not a Sprint 10 release gate.
   - Out-of-scope guardrail: no production private key committed, no separate `Dobby.Admin.Cli` project, no host runtime data access, no full Admin UI blocker, and no WPF `MainWindow` startup for `--cli` invocations.

9. Implement trust/profile and production packaging guard tests.
   - Action: extend architecture/static scans to prove production excludes sample plugin package, sample license, preinstalled sample runtime folder, dev/QA public key, and dev/QA trust metadata.
   - Success criteria: production profile rejects dev/QA packages/licenses and static scan fails if dev/QA artifacts appear in customer package evidence.
   - Out-of-scope guardrail: do not treat manual attestation alone as release evidence.

### Evidence / closure

10. Run focused tests as slices land, then full verification at closure.
    - Action: run relevant unit/integration/smoke/architecture tests and final restore/build/test/format as allowed by implementation phase.
    - Success criteria: no warning/build/test/format failures; test count increases from Sprint 9 baseline or unchanged only with documented reason.
    - Out-of-scope guardrail: planning agent did not run these; implementation agent must.

11. Append release evidence.
    - Action: update `artifacts/release-evidence/skeleton-local/architecture-baseline.md` and build-id static scan evidence after verification; append decision-log only for actual human decisions.
    - Success criteria: evidence names commands, test results, artifact hashes/paths where non-secret, and unresolved gates if any.
    - Out-of-scope guardrail: do not write private keys, raw license payloads, raw recovery material, or full sensitive paths.

12. Final review audit.
    - Action: verify §17 reviewer focus items and §18 final checklist.
    - Success criteria: no unresolved authority gate remains for implemented lanes; optional lanes such as `verify-plugin-package` are explicitly excluded unless implemented.
    - Out-of-scope guardrail: do not claim production/Admin `.dobbyrecovery` release evidence; `HD-S10-002` and D-33 defer it to Sprint 11/vendor handover.

## 13. Decisions and Risks

### Closed authority gates

| Gate | Disposition | Evidence |
|---|---|---|
| D-13 | Sprint 5 may use TEST-ONLY recovery fixtures; later recovery evidence was originally deferred to Sprint 10/11. | `artifacts/release-evidence/skeleton-local/decision-log.md`, `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`. |
| D-22 | `JsonSchema.Net` approved only for manifest validation. | `artifacts/release-evidence/skeleton-local/decision-log.md`, `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`. |
| D-23 | Production/devQA trust resources approved with constraints; production must not include/accept devQA trust. | `decision-log.md`, `src/Dobby.Security/Dobby.Security.csproj`, `PluginTrustResourcePackagingTests.cs`. |
| D-24 | Installed state authoritative; metadata cache derived/rebuildable; PluginFramework must not depend on Infrastructure. | `decision-log.md`, `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs`, `HostInstalledPluginStateStore.cs`. |
| D-25 | Sprint 8 namespace constraints for Packaging/Trust/Host.Plugins/Shell.PluginPages. | `decision-log.md`, `docs/08_CODING_STANDARDS.md`. |
| D-29 / `HD-S9-004` | Sprint 9 generated signed license test material may be test-only; no product/Admin signed license fixtures. | `decision-log.md`, `tests/Dobby.UnitTests/Licensing/LicenseArtifactFixtureBuilder.cs`. |
| D-33 / `HD-S10-004` | Master-plan AC-007 split: Sprint 10 owns technical-path/host-import compatibility evidence only; production/Admin AC-007 release evidence is Sprint 11/vendor handover. | `decision-log.md`, `SPRINT_IMPLEMENTATION_PLAN.md`, this plan. |

### Approved implementation choices grounded in source

- Use existing `plugins.management` target and verbs, not a new target. Source: `BuiltInOperationTargets.cs`, `ADR-0048`, `docs/specs/plugins-manager.md`.
- Use existing audit names for plugin lifecycle; do not add audit names. Source: `AuditEventName.cs`, `docs/specs/audit-and-diagnostics.md`.
- Use existing `Dobby.Admin.exe --cli <command>` invocation model and wrapper folders. Source: `docs/specs/admin-tool.md`, `tools/*/README.md`.
- Keep sample plugin `net10.0` and Contracts/SDK-only references. Source: `samples/Dobby.Sample.Plugin/Dobby.Sample.Plugin.csproj`, `PluginAndSdkBoundaryTests.cs`.

### Risks and mitigations

| Risk | Impact | Mitigation / test |
|---|---|---|
| Admin implementation drifts from approved option table. | Public wrapper/tests become inconsistent with the canonical contract. | Implement from `docs/specs/admin-tool.md`, pin parser tests to D-30, and keep `docs/09_BUILD_RUN_AND_RELEASE.md` option examples illustrative unless separately updated. |
| Admin parser works only as an in-process helper. | Wrapper smoke automation invokes `Dobby.Admin.exe --cli <command>` but the executable could still open the WPF window or skip stdout/stderr/exit-code handling. | Route `--cli` in `App.xaml.cs`, remove/bypass `StartupUri` for CLI mode, and add process-level executable/wrapper smoke coverage. |
| Pending uninstall requires schema change. | Persistence migration/state complexity and checksum mismatch risk if old migrations are edited. | Add deterministic append-only `0005_plugin_lifecycle_state` migration, register it in `PostLoginPluginPersistenceMigrations.cs`, embed it in `Dobby.Infrastructure.csproj`, and test fresh/already-migrated databases plus `StorageMetadata` recording. Sources: `0002_postlogin_bootstrap.sql`, `PostLoginPluginPersistenceMigrations.cs`, `HostPersistenceMigrator.cs`, `docs/08_CODING_STANDARDS.md`, `storage-materialization.md`. |
| Manager UI accidentally becomes authorization enforcement. | Permission bypass. | Test policy checks on commands; UI only reflects policy; use `OperationAuthorizationPolicy`. |
| Sample plugin references host/WPF/infrastructure. | Violates AC-043 and plugin boundary. | `PluginAndSdkBoundaryTests.cs`, `PluginNoInfrastructureReferenceTests.cs`, package validator forbidden content checks. |
| Dev/QA artifacts leak into production package. | Release-blocking security issue. | Extend static packaging scan per `docs/09_BUILD_RUN_AND_RELEASE.md`; production `PluginTrustVerifier` rejects `dobby.dev-qa`. |
| Recovery signature format confused with plugin/license signature format. | Admin-generated recovery artifacts rejected by host or unsafe. | Tests must verify ADR-0039 P1363 standard base64 for recovery and existing DER/base64url for plugin/license. |
| Long install/uninstall blocks UI thread. | Violates AC-046/NFR-021. | Background operation reporter tests; no `.Result`/`.Wait()` on UI path. |

### Carry-over requiring no new decision

- Sprint 5 host-side AC-007 remains valid only for TEST-ONLY fixture closure; Sprint 10 generates Admin path evidence separately. Source: D-13.
- Sprint 8 production/devQA trust profiles remain approved; Sprint 10 extends evidence, not trust model. Source: D-23.
- Sprint 9 license import/runtime mode remains owner for AC-034 host import behavior; Sprint 10 supports it by generating detached license artifacts. Source: `SPRINT_IMPLEMENTATION_PLAN.md`.

### Human decisions resolved

| ID | Disposition | Implementation effect | Source basis |
|---|---|---|---|
| `HD-S10-001` | DECIDED/CLOSED. Approved command-specific Admin CLI contract. | Public wrapper scripts, command parsing tests, operator docs, and artifact generation must use `docs/specs/admin-tool.md`; no option names are inferred from illustrative release docs. | `decision-log.md` D-30, `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, `tools/*/README.md`. |
| `HD-S10-002` | DECIDED: production evidence deferred. | Sprint 10 may implement fail-closed Admin recovery technical path, but production/Admin `.dobbyrecovery` release evidence remains Sprint 11/vendor handover and must not be claimed. | `decision-log.md` D-31/D-33, `docs/adr/ADR-0033-signing-key-ownership.md`, `docs/adr/ADR-0039-owner-password-recovery.md`, D-13 in `decision-log.md`, `SPRINT_IMPLEMENTATION_PLAN.md`. |
| `HD-S10-003` | DECIDED: optional, not Sprint 10 release gate. | `verify-plugin-package` may remain unimplemented; if implemented, it follows `docs/specs/admin-tool.md` and must not replace install-time package verification. | `decision-log.md` D-32, `docs/specs/admin-tool.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, `SPRINT_IMPLEMENTATION_PLAN.md`. |
| `HD-S10-004` | DECIDED: controlling authority amendment for AC-007 evidence split. | Sprint 10 closure may include AC-007 technical-path and host-import compatibility evidence only; production/Admin AC-007 release evidence remains Sprint 11/vendor handover. | `decision-log.md` D-33, `SPRINT_IMPLEMENTATION_PLAN.md`, this plan. |

## 14. Test Plan

Previous Sprint 9 verified baseline: Unit 570, Integration 213, Smoke 6, with `dotnet restore`, build, test, and format passing on 2026-06-18 in `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. Sprint 10 should add tests for new behavior; unchanged counts require documented justification.

### Unit tests

| File / placement | Scenarios | AC coverage |
|---|---|---|
| Extend `tests/Dobby.UnitTests/Host/HostPluginPackageInstallCoordinatorTests.cs` | Permission-aware manager install rejection if coordinator surface changes; local `.dobbyplugin` only; typed reason mapping preserved; ReadOnlyExpired rejection unchanged. | AC-004, AC-031, AC-034 support. |
| Extend or add under `tests/Dobby.UnitTests/Host/` for lifecycle coordinator | Enable/disable/uninstall permission checks; missing plugin `NotFound`; active plugin requests work-session close; pending uninstall scheduling; audit calls. | AC-031, AC-019. |
| Extend `tests/Dobby.UnitTests/Host/PluginRuntimeAuditCoordinatorTests.cs` | `plugin.enable`, `plugin.disable`, `plugin.uninstall` events use existing names and redacted details. | AC-031. |
| Extend `tests/Dobby.UnitTests/Host/PluginRuntimeDiagnosticsCoordinatorTests.cs` | Lifecycle diagnostics markers contain no raw secret/private key/raw license/recovery payload. | AC-032, NFR-020. |
| Extend `tests/Dobby.UnitTests/PluginFramework/Packaging/PluginPackageInstallerTests.cs` | New lifecycle fields initialized and rollback/cache consistency preserved. | AC-031, AC-018. |
| Extend `tests/Dobby.UnitTests/PluginFramework/Packaging/PluginPackageValidatorTests.cs` | Sample manifest/package forbidden content, icon/files/signature behavior, dev/QA vendor trust mapping as testable without production package generation. | AC-018, AC-036. |
| Extend `tests/Dobby.UnitTests/Shell/PluginPageRenderRegistryTests.cs` or add shell manager tests | Manager list/detail/action state, confirmation state, busy state, feedback localization keys. | AC-031. |
| Extend `tests/Dobby.UnitTests/Localization/*` | Manager labels exist in `HostResourceKeys`, fallback catalog, and az-AZ resource where required. | AC-031, localization boundary. |
| Add Admin command tests for approved `HD-S10-001` contract | Approved option parsing, parsed DTO handoff, unknown command/invalid option exit `64`, validation conflicts exit `65`, unavailable signing material exit `66`, stdout/stderr redaction, no raw key/path/stack leak. | AC-007 technical-path support, AC-018 support, AC-034 support. |
| Add Admin recovery generation tests under `HD-S10-002`/D-33 deferral | `.dobbyrecovery` payload fields, no target user fields, P1363 standard base64 signature, host importer accepts generated response using approved non-production or otherwise available non-release signing material; non-release technical-path proof is not production/Admin AC-007 release evidence. | AC-007 technical-path support; production release evidence deferred. |
| Add Admin package/license generation tests for approved `HD-S10-001` contract | `.dobbyplugin` signature/fingerprint, detached `.dobbylicense` v2 canonical payload and DER/base64url signature compatible with current import verifier. | AC-018, AC-034, AC-036. |

### Integration tests

| File / placement | Scenarios | AC coverage |
|---|---|---|
| Extend `tests/Dobby.IntegrationTests/Persistence/PluginInstallStatePersistenceTests.cs` | New lifecycle schema migration, `PostLoginPluginPersistenceMigrations` registration, embedded SQL resource availability, `StorageMetadata` record, fresh database application, already-migrated `0002`-`0004` upgrade, state/cache persistence, cache rebuild, pending uninstall fields, immediate delete vs scheduled delete state. | AC-031. |
| Extend `tests/Dobby.IntegrationTests/Plugins/PluginIntegrationSuiteTests.cs` | Install sample plugin, list/details display, status updates, enable/disable, uninstall confirmation model, lifecycle audit trail. | AC-031, AC-036. |
| Extend `tests/Dobby.IntegrationTests/Plugins/PluginActivationRuntimeIntegrationTests.cs` | Disabled/pending uninstall plugins do not activate or contribute navigation; restart cleanup before fresh discovery; dependencies still skipped per ADR-0009. | AC-005, AC-031, AC-032. |
| Extend `tests/Dobby.IntegrationTests/Recovery/RecoveryResponseImportFixtureTests.cs` or add Admin-generated fixture test under `HD-S10-002`/D-33 deferral | Host accepts Admin-generated `.dobbyrecovery` using approved non-production or otherwise available non-release signing material, not Sprint 5 TEST-ONLY material; no target user fields; production/Admin release evidence is not claimed until Sprint 11/vendor handover. | AC-007 technical-path support. |
| Extend `tests/Dobby.IntegrationTests/Licensing/LicenseImportIntegrationTests.cs` | Admin-generated sample `.dobbylicense` imports through existing `Plugins > License` flow and package fingerprint binding. | AC-034. |
| Extend `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs` or `PackageGovernanceTests.cs` | Production package/static scan fails for devQA public key, devQA trust profile metadata, sample `.dobbyplugin`, sample `.dobbylicense`, preinstalled sample runtime folder. | AC-018, AC-036, release gate. |
| Extend `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs` | Sample plugin still references only `Dobby.Contracts` and `Dobby.SDK`. | AC-043, AC-036. |
| Extend `tests/Dobby.IntegrationTests/Architecture/PluginNoInfrastructureReferenceTests.cs` | Sample/plugin projects do not reference host/infrastructure/persistence packages. | AC-043. |

### Smoke tests

| File / placement | Scenarios | AC coverage |
|---|---|---|
| Extend `tests/Dobby.SmokeTests/ShellSmokeTests.cs` or add Admin process smoke coverage | Replace pre-decision option rejection coverage with process-level `Dobby.Admin.exe --cli <command>` or wrapper invocations, invalid/foreign option exit `64`, validation conflict exit `65`, signing-material-unavailable exit `66`, redacted stdout/stderr, and proof the WPF window is not opened in CLI mode. | Admin tool exit behavior. |
| Add sample plugin smoke | Build/sign/install sample `.dobbyplugin`, launch page, permission-denied action, success/invalid/fault paths, progress/work-session behavior. | AC-036, AC-031. |
| Add production trust smoke/static check | Production profile rejects dev/QA sample package/license. | AC-018, AC-036. |

### Manual / out-of-band notes

- Full Admin UI is not a release blocker; do not require manual UI evidence for full Admin composition. Source: `docs/specs/admin-tool.md`, `ADR-0023`.
- Production private-key custody and handover may require out-of-band vendor/operator evidence; record under `HD-S10-002`/D-33 or Sprint 11 release evidence, not in source files. Source: `ADR-0033`, `SPRINT_IMPLEMENTATION_PLAN.md`.
- Manual reviewer attestation alone is insufficient for sample/devQA production packaging exclusion; automated static scan evidence is required. Source: `docs/09_BUILD_RUN_AND_RELEASE.md`.

## 15. Out of Scope

- Full Admin UI composition, customer CRUD, signing wizard, license generator wizard, batch operations, and audit viewer. Sources: `docs/specs/admin-tool.md`, `ADR-0023`, `SPRINT_IMPLEMENTATION_PLAN.md`.
- Plugin update, repair, downgrade, marketplace, catalog, local store, hot reload, production sample feature, and production default sample visibility. Sources: `docs/specs/plugins-manager.md`, `docs/specs/sample-plugin.md`.
- New package pins, package upgrades, alternate CLI parser packages, alternate crypto packages, or commercial UI dependencies. Sources: `Directory.Packages.props`, `docs/03_TECH_STACK_AND_VERSIONS.md`, `PackageGovernanceTests.cs`.
- New `ErrorCode` values, new audit event names, new operation verbs, new trust profiles, or new license reject reasons without authority. Sources: `docs/04_RESULT_VS_EXCEPTION_POLICY.md`, `AuditEventName.cs`, `ADR-0048`, `docs/specs/audit-and-diagnostics.md`.
- Branch Shared runtime, PostgreSQL/Npgsql, BranchSetup, `Dobby.Update`, installer/MSIX/WiX release work, auto-update, online trust/telemetry, and vendor uploads. Sources: `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/audit-and-diagnostics.md`.
- Reworking Sprint 5 recovery host behavior, Sprint 8 runtime/trust architecture, or Sprint 9 license import/runtime mode unless required by current Sprint 10 integration tests. Sources: prior plans and architecture baseline.
- Editing ADRs/specs/schemas/master plan/predecessor plans during implementation unless a separate human-approved documentation task exists. Source: user prompt and authority model.

## 16. Definition of Done

Build / test / format expectations:
- `dotnet restore` succeeds with current pins.
- Solution build succeeds with warnings as errors.
- Unit, integration, smoke, architecture, and focused sample/Admin tests pass.
- `dotnet format` passes or produces no uncommitted formatting drift.
- UTF-8/no-BOM/final-newline/no-trailing-whitespace checks pass.

Architecture / boundary expectations:
- `Dobby.PluginFramework` still has no `Dobby.Infrastructure` dependency for installed state/cache domain ports.
- Sample plugin still references only `Dobby.Contracts` and `Dobby.SDK`.
- `Dobby.Admin` remains standalone vendor-side tool and does not read host runtime data or ship to customer host.
- `Dobby.Admin.exe --cli <command>` is a real process path that calls `AdminCommandHost`, preserves redacted stdout/stderr and exit code behavior, and does not open the WPF window.
- Production security assembly/package has no active devQA trust material and rejects `dobby.dev-qa`.
- No new package pins, audit names, operation targets, or `ErrorCode` values unless a cited human decision was recorded.
- Lifecycle schema changes use append-only `0005_plugin_lifecycle_state` migration registration/resource embedding; existing `0002`-`0004` migration checksums are not changed.

Per-AC expectations:

| AC | Done evidence |
|---|---|
| AC-007 technical-path support | Admin `create-recovery-response` can generate a signed `.dobbyrecovery` from documented request input using approved non-production or otherwise available non-release signing material; host import tests accept it; Sprint 5 TEST-ONLY fixtures remain separate; production/Admin release evidence is not claimed. |
| AC-018 support | Signed `.dobbyplugin` package and detached `.dobbylicense` are generated/validated/imported through existing package/license boundaries; package has no forbidden license/customer/device/private-key/host config/migration content. |
| AC-031 primary | Plugins Manager list/detail/install/enable/disable/uninstall/pending uninstall/data choice/restart-required behavior is covered by integration/snapshot tests, append-only lifecycle migration evidence, and lifecycle audit evidence. |
| AC-034 support | Admin-generated sample `.dobbylicense` imports through existing `Plugins > License` path; Sprint 9 remains host import owner. |
| AC-036 primary | Signed sample plugin dev/QA artifact covers manifest, operation target, permission deny, Main Content, `Result` success/invalid, fault isolation, progress, work session behavior, and no production default visibility. |
| AC-043 | Sample plugin project/package boundary tests prove Contracts/SDK-only references and no host/infrastructure leakage. |
| AC-046 | Install/license import/storage-related manager actions use background operation/non-blocking UI patterns; no UI-thread blocking waits. |

Evidence expectations:
- Append Sprint 10 block to `artifacts/release-evidence/skeleton-local/architecture-baseline.md` with test counts, commands run, artifact outputs, and scope notes.
- `HD-S10-001`, `HD-S10-002`, `HD-S10-003`, and `HD-S10-004` decisions are recorded in `artifacts/release-evidence/skeleton-local/decision-log.md`; append implementation evidence after verified Sprint 10 work.
- Produce `artifacts/release-evidence/<build-id>/packaging-static-scan.md` during release/evidence run for sample/devQA production exclusion per `docs/09_BUILD_RUN_AND_RELEASE.md`.
- Record non-secret artifact hashes/fingerprints where useful; never record private keys, raw license payloads, raw recovery material, or raw secrets.

Open items allowed at close:
- Optional `verify-plugin-package` may remain unimplemented because `HD-S10-003` decides it is optional and not needed for Sprint 10 release evidence.
- Full Admin UI may remain P1-if-time/P2.

Items that must be closed for Sprint 10 completion:
- Implementation and tests for required Admin wrapper option behavior from the approved `HD-S10-001` contract, including process-level `Dobby.Admin.exe --cli <command>` routing.
- Implementation and tests for any required lifecycle schema expansion, including `0005_plugin_lifecycle_state` migration registration/resource embedding and fresh/already-migrated database coverage.
- Required manager/sample/trust-exclusion tests and evidence.
- AC-007 technical-path and host-import compatibility evidence for required `create-recovery-response` implementation must be closed; production/Admin AC-007 release evidence is not a Sprint 10 completion item and remains deferred by `HD-S10-002`/D-33 to Sprint 11/vendor handover.

## 17. Reviewer Focus

1. Verify Sprint 10 scope and resolved/deferred gates against `SPRINT_IMPLEMENTATION_PLAN.md`, `docs/specs/admin-tool.md`, and `docs/09_BUILD_RUN_AND_RELEASE.md`.
2. Inspect manager lifecycle implementation in `src/Dobby.PluginFramework/Packaging/`, `src/Dobby.Host/Plugins/`, `src/Dobby.Shell/`, and `src/Dobby.Host/` for AC-031 and boundary preservation.
3. Inspect `0005_plugin_lifecycle_state.sql`, `PostLoginPluginPersistenceMigrations.cs`, and `Dobby.Infrastructure.csproj` if lifecycle schema changes were required; confirm existing migration checksums were not changed and fresh/already-migrated persistence tests cover `StorageMetadata` recording.
4. Inspect authorization checks for `plugins.management.view/import/configure/delete` in code and tests; confirm UI visibility is not the enforcement mechanism.
5. Inspect lifecycle audit coverage for `plugin.enable`, `plugin.disable`, and `plugin.uninstall` using `src/Dobby.Audit/AuditEventName.cs` and `tests/Dobby.UnitTests/Host/PluginRuntimeAuditCoordinatorTests.cs`.
6. Inspect sample plugin manifest and code in `samples/Dobby.Sample.Plugin/` against `docs/specs/sample-plugin.md` and `docs/schemas/dobby-plugin-manifest.schema.json`.
7. Inspect `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs` and `PluginNoInfrastructureReferenceTests.cs` for sample boundary enforcement.
8. Inspect Admin command implementation and tests against `HD-S10-001`; confirm option names match the approved table and no illustrative option names leaked from `docs/09_BUILD_RUN_AND_RELEASE.md`.
9. Inspect `Dobby.Admin` startup and process-level smoke coverage; confirm `Dobby.Admin.exe --cli <command>` writes redacted stdout/stderr, exits with the command result, and does not open `MainWindow` in CLI mode.
10. Inspect recovery generation tests for ADR-0039 P1363 standard base64 signatures and no target user fields.
11. Inspect plugin/license generation tests for ADR-0045/ADR-0051 DER/base64url and detached artifact boundaries.
12. Inspect production devQA exclusion static scan and `PluginTrustResourcePackagingTests.cs`; confirm production rejects `dobby.dev-qa`.
13. Inspect package governance: `Directory.Packages.props` unchanged unless a human-approved pin decision exists; `PackageGovernanceTests.cs` passes.
14. Inspect release evidence appended to `artifacts/release-evidence/skeleton-local/architecture-baseline.md` and build-id `packaging-static-scan.md`.
15. Confirm no ADR/spec/schema/master/prior plan edits occurred unless explicitly approved.

## 18. Final Execution Checklist

### Before starting

- [ ] Confirm `SPRINT_10_EXECUTION_PLAN.md` is being executed, not re-planned.
- [ ] Confirm `HD-S10-001` approved contract in `docs/specs/admin-tool.md` and D-30.
- [ ] Confirm `HD-S10-002`/D-33 production recovery evidence remains deferred to Sprint 11/vendor handover unless a separate release-evidence decision closes it.
- [ ] Confirm optional `verify-plugin-package` stance (`HD-S10-003` / D-32).
- [ ] Re-read `docs/specs/plugins-manager.md`, `docs/specs/sample-plugin.md`, `docs/specs/admin-tool.md`, `ADR-0033`, `ADR-0039`, and `ADR-0045`.
- [ ] Confirm no new package pins are needed.

### During implementation

- [ ] Write failing tests before production code for each manager/sample/Admin/trust-exclusion slice.
- [ ] Use `plugins.management` existing verbs; no new operation target names.
- [ ] Use existing audit names; no new audit event names.
- [ ] Use existing `ErrorCode` values; no new closed-set codes.
- [ ] Keep PluginFramework domain ports free of Infrastructure dependencies.
- [ ] Use an append-only lifecycle migration if schema changes are needed; do not edit `0002`, `0003`, or `0004`.
- [ ] Register and embed any new lifecycle migration before relying on it in runtime or tests.
- [ ] Keep sample plugin Contracts/SDK-only and `net10.0`.
- [ ] Keep ViewModels free of OS dialogs and WPF control references.
- [ ] Keep long file/package/license work off UI thread and report progress.
- [ ] Keep Admin CLI parsing separate from business/signing rules.
- [ ] Route `Dobby.Admin.exe --cli <command>` through Admin process startup without opening `MainWindow`.
- [ ] Preserve wrapper exit code/stdout/stderr redaction.
- [ ] Do not commit private keys, raw license/recovery payloads, production signing material, or devQA artifacts into production package paths.

### Before finishing

- [ ] Run focused unit/integration/smoke/architecture tests for changed areas.
- [ ] Run full restore/build/test/format verification.
- [ ] Verify lifecycle migration registration/resource embedding with fresh and already-migrated database tests if schema changed.
- [ ] Verify Admin CLI process/wrapper smoke coverage, not only `AdminCommandHost` in-process tests.
- [ ] Verify sample/devQA production exclusion static scan.
- [ ] Verify release evidence contains test counts and non-secret artifact evidence.
- [ ] Verify no forbidden files/surfaces from §9 were touched.
- [ ] Verify all open authority gates are either closed with evidence or explicitly block Sprint 10 closure.

## 19. Planning Coverage and Confidence

| Topic | Coverage | Justification |
|---|---|---|
| Sprint scope | High | Master Sprint 10 section and cross-sprint dependencies were read and mapped to ACs. |
| Prior continuity | High | Sprints 5/8/9 claims were verified against current code, tests, and release evidence. |
| Plugins Manager behavior | High | `docs/specs/plugins-manager.md`, AC-031, current state/cache/runtime/shell code, and tests were inspected. |
| Sample plugin behavior | High | Sample spec, schema, current sample files, and architecture tests were inspected; gaps are concrete. |
| Admin artifact tooling | Medium | Spec/ADR/current skeleton were inspected; option table is now approved, while production recovery release evidence remains deferred by `HD-S10-002`/D-33. |
| Trust profile guard | High | ADR-0033, current trust verifier/provider/project file, architecture tests, and release docs align. |
| License generation support | Medium | Import/verification code is clear; generator surface is not implemented and option/key decisions affect exact workflow. |
| Recovery generation support | Medium | Host verification and ADR semantics are clear; production/Admin generation depends on approved options and key custody. |
| UI implementation detail | Medium | Current shell is simple and lacks manager surface; exact file split is an implementation assumption. |
| Test plan | High | Existing test files and required new coverage areas are mapped to ACs. |
| Package/toolchain impact | High | Current pins and package governance tests inspected; no new package is needed by plan. |
| Evidence closure | High | Existing release evidence conventions and Sprint 11 dependencies inspected. |

Confidence limits:
- Admin public CLI behavior must match the approved `HD-S10-001` contract in `docs/specs/admin-tool.md`.
- Production/Admin `.dobbyrecovery` release evidence remains deferred by `HD-S10-002`/D-33 to Sprint 11/vendor handover unless a later release-evidence decision closes it.
- Exact new file names for manager lifecycle/UI/Admin service classes remain implementation assumptions unless an existing file is extended.
- Sprint 11 remains responsible for final release gate consolidation and installer/release packaging closure.

Planning stance: READY FOR SPRINT 10 IMPLEMENTATION WITH PRODUCTION EVIDENCE DEFERRAL. Admin tool option names/sets are resolved by `HD-S10-001`; `HD-S10-002` and D-33 defer production/Admin `.dobbyrecovery` release evidence to Sprint 11/vendor handover; `HD-S10-003` keeps `verify-plugin-package` optional and outside the Sprint 10 release gate; `HD-S10-004` makes Sprint 10 AC-007 work technical-path/host-import compatibility evidence only.
