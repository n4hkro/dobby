# Sprint 3 Execution Plan — Configuration, Runtime Folders, Diagnostics, and Audit

## 1. Sprint Summary

- Sprint number and title: Sprint 3 — `Configuration, Runtime Folders, Diagnostics, and Audit`.
- Main goal: establish host configuration floors, runtime folder layout, local-only diagnostics, and append-only audit boundary before auth and shell startup.
- Expected deliverable: ADR-0027 P1 Configuration Schema bound in source order from packaged `appsettings.json`, `%LocalAppData%/Dobby/config/` runtime override, environment variables, and development user secrets with fail-closed validation; runtime folder creation/fail-closed per storage spec §3.1; local-only structured diagnostics sink with full write-time redaction, 30-day default retention that fails closed below 30 unless an explicit documented support/privacy policy is introduced, finite total-size cap, and rebuild-less log-level configuration via `Serilog.Settings.Configuration` from a top-level `Serilog` section; diagnostics export boundary with redaction, exclusion rules, no telemetry/upload, and `diagnostics.export.created` audit contract; append-only audit boundary with canonical event taxonomy, minimum payload (`timestamp`, `actor`, `action`, `target`, `result`, `correlationId`, optional `details`), redaction, 365-day retention floor.
- Why this sprint: Sprints 4-11 read config keys, consume the runtime folder tree, and emit audit/diagnostics events.
- Readiness: **partially blocked**. Ready: config binding, runtime folders, diagnostics expansion, in-memory audit. Blocked per master: durable audit persistence (Sprint 4), protected-storage primitive materialization (Sprint 4 + ADR-0040 §3.1). Carry-overs: `TOOLCHAIN-PIN-APPROVAL-MISSING`/`TOOLCHAIN-PIN-REPRESENTATION-MISSING` (Sprint 3 plans zero new `PackageReference`); Sprint 2 path (a) print-boundary renewal (Sprint 9 sunset). Sprint 1 placeholder `Dobby:Security.*` disposition is resolved as ADR-0027 canonical migration to `Dobby:Auth.*` (see §13.9).
- Strategy: bind full ADR-0027 schema in `Dobby.Configuration` typed POCOs + `IValidateOptions<T>`; add host configuration-source wiring for all four ADR-0027 §2 sources; migrate `src/Dobby.Host/appsettings.json` to canonical `Dobby` schema plus a top-level `Serilog` section for rebuild-less `MinimumLevel` configuration; add runtime folder prepare + fail-closed at `Dobby.Host/Startup`; wire WPF `App` startup so bootstrap runs before login/main window construction; configure Serilog local rolling file diagnostics with sink-boundary redaction; expand `DiagnosticRedactor` vocabulary; add diagnostics export boundary; add `localization.missing-key` event + startup milestone constants per ADR-0041 §6; replace placeholder audit trail with `IAuditTrail` + canonical event-name constants + `AuditResult` enum + audit-owned `Details` sanitization/rejection boundary; land AC-010/AC-033/AC-052 unit and integration tests. No durable persistence, no protected-storage primitive name invention, no new `PackageReference`.
- Dependencies: Sprint 1 guardrails; Sprint 2 Core primitives (`CorrelationId`, `CorrelationContextAccessor.PushScope`). Sprints 4-11 depend on Sprint 3 outputs.

## 2. Sources Read

**Master sprint plan**: `SPRINT_IMPLEMENTATION_PLAN.md` — `deep` (Sprint 3 scope, AC, blockers; cross-sprint Sprint 4/5/7/9 boundaries).

**Previous sprint plans**: `SPRINT_1_EXECUTION_PLAN.md` — `targeted` (blockers, deferred drift). `SPRINT_2_EXECUTION_PLAN.md` — `targeted` (carry-overs, DoD, 111-test baseline 2026-05-12).

**Later sprint references** (`boundary/context` only): Sprint 4 (Sprint 3 must not materialize durable persistence); Sprint 5 (pre-login audit/diagnostics open per ADR-0041); Sprint 7 (AC-033 action surface split); Sprint 9 (print-boundary sunset).

**Repo-root files** (`targeted` unless noted): `README.md` (boundary/context), `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `Dobby.sln` (boundary/context), `.editorconfig` (boundary/context), `.gitignore` (boundary/context). None modified by Sprint 3.

**Product/architecture/requirements docs**: `01_PRODUCT_SCOPE.md` (boundary/context), `02_ARCHITECTURE_MAP.md` `deep` (Rows 44-47 Configuration, 65-68 Diagnostics, 69-70 Audit, 87-89 Infrastructure, Cədvəl 2 dependency table), `03_TECH_STACK_AND_VERSIONS.md` `targeted` (config/logging Tier 1 pins), `04_RESULT_VS_EXCEPTION_POLICY.md` (boundary/context), `05_FUNCTIONAL_REQUIREMENTS.md` `targeted` (FR-011, FR-027), `06_NON_FUNCTIONAL_REQUIREMENTS.md` `targeted` (NFR-011, NFR-020), `07_ACCEPTANCE_CRITERIA.md` `deep` (AC-010, AC-033, AC-052; AC-048 contribution), `08_CODING_STANDARDS.md` `targeted` (namespace/file rules), `09_BUILD_RUN_AND_RELEASE.md` (boundary/context).

**ADRs**: `ADR-0016` `deep` (Serilog local sinks, structured fields, write-time redaction); `ADR-0019` `deep` (append-only, minimum payload); `ADR-0027` `deep` (P1 Configuration Schema 18 keys, source order, env mapping `DOBBY__<SECTION>__<KEY>`, fail-closed); `ADR-0029` `targeted` (correlation source from Sprint 2); `ADR-0038` `targeted` (PasswordKdfMode + LoginThrottleStepsSeconds keys); `ADR-0040` `deep` (§3.1 stop condition on inventing storage abstraction names); `ADR-0041` `targeted` (allowed pre-login: diagnostics sink init + audit storage open; milestone names); `ADR-0043` (boundary/context); `ADR-0051` `targeted` (license import permission boundary); `ADR-0054` (boundary/context — Sprint 4); `ADR-0055` `deep` (DPAPI scope, failure-mode fail-closed contract); `ADR-0056`/`ADR-0057` (boundary/context — Branch Shared docs-only).

**Specs**: `docs/specs/audit-and-diagnostics.md` `deep` (event taxonomy ~40 names in 8 categories; minimum payload; retention floors; write-time redaction list; `localization.missing-key` event; markers; no-telemetry default; export + `diagnostics.export.created`). `docs/specs/storage-and-data-protection.md` `deep` (§3.1 runtime layout; runtime folder recovery; §5a initial-skeleton block on concrete storage materialization). `docs/specs/license-import.md` `targeted` (license import permission boundary; runtime flow remains Sprint 9).

**Schemas**: none modified; `docs/schemas/*.json` read as boundary protection only.

**Source code**: `src/Dobby.Configuration/DobbyOptions.cs` `deep` (46 lines, Sprint 1 placeholder), `Dobby.Configuration.csproj` `targeted`; `src/Dobby.Audit/AuditEvent.cs` `deep` (26 lines, no `result`/`details`), `Dobby.Audit.csproj` `targeted`; `src/Dobby.Diagnostics/DiagnosticRedactor.cs` `deep` (20 lines, narrow regex), `Dobby.Diagnostics.csproj` `targeted`; `src/Dobby.Host/appsettings.json` `deep` (14 lines, non-canonical keys), `Dobby.Host.csproj` `targeted` (all needed packages already referenced, including `UserSecretsId`); `src/Dobby.Host/App.xaml`/`App.xaml.cs` `deep` (`StartupUri` currently bypasses bootstrap); `src/Dobby.Host/Startup/HostBootstrapper.cs` `deep` (placeholder labels); `src/Dobby.Core/CorrelationId.cs` `targeted` (Sprint 2 shape verified); `src/Dobby.Security/Authentication/Pbkdf2PasswordHasher.cs` `targeted` (existing `KdfPolicy.SkeletonPbkdf2Fallback` PBKDF2 floors); `src/Dobby.Infrastructure/**` (boundary/context — only Printing/ slice).

**Tests**: `tests/Dobby.UnitTests/Configuration/ConfigurationTests.cs` `deep`; `tests/Dobby.UnitTests/Diagnostics/DiagnosticsAndAuditTests.cs` `deep` (uses non-canonical `"auth.login.succeeded"`); `tests/Dobby.UnitTests/Security/SecurityTests.cs` `targeted` (existing PBKDF2 floor coverage); `tests/Dobby.UnitTests/Dobby.UnitTests.csproj` `targeted` (already references Configuration/Audit/Diagnostics); `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj` `targeted`; `tests/Dobby.IntegrationTests/Architecture/*.cs` `targeted` (6 files must stay green); `tests/Dobby.SmokeTests/ShellSmokeTests.cs` (boundary/context).

**Project/build files**: `Directory.Packages.props`/`Directory.Build.props`/`global.json`/`Dobby.sln` `targeted`, not modified. `appsettings.json` `deep`, modified.

**Release evidence**: `artifacts/release-evidence/skeleton-local/architecture-baseline.md` `deep` (Sprint 2 path (a) renewed 2026-05-12; 111 tests green; Sprint 9 sunset); `toolchain-pin-gate.md` `deep` (blockers open); `auth-kdf-selection.md` `targeted` (PBKDF2 recorded). None modified by Sprint 3.

## 3. Sprint Scope From Master Sprint Plan

Sprint 3 block of `SPRINT_IMPLEMENTATION_PLAN.md` (summary):

- **Scope**: configuration source order and security floors; runtime folder layout under `%LocalAppData%/Dobby/`; local-only logging/diagnostics; audit boundary (event names, minimum payload, redaction, retention floors); diagnostics export rules without telemetry.
- **Task groups**: Configuration Model, Runtime Folder Handling, Diagnostics, Audit.
- **Dependencies**: Sprints 1-2; ADR-0016, ADR-0019, ADR-0027, ADR-0040, ADR-0055; audit-and-diagnostics spec; storage-and-data-protection spec.
- **Primary AC**: `AC-010`, `AC-033`, `AC-052`. Contributes to `AC-048` (Azerbaijani diacritic round-trip — Sprint 3 audit-payload encoding path).
- **Master verification**: unit tests for config binding/validation and weaker-value rejection; audit event shape tests; redaction tests for logs and exports; AC-052 retention-floor tests; runtime folder recovery/fail-closed tests.
- **Master risks/blockers**: durable audit persistence → Sprint 4; exact host-internal protected-storage method surfaces → architecture-approved table.
- **Exit condition**: config, local diagnostics, audit boundaries, and runtime folder behavior ready for persistence integration.
- **Prior dependencies**: Sprint 1 (guardrails, evidence, 50-test baseline); Sprint 2 (Core primitives, 111-test baseline).
- **Later sprint consumers**: Sprint 4 (durable audit), Sprint 5 (pre-login audit/diagnostics open, auth events), Sprint 6 (settings + permission audit), Sprint 7 (shell diagnostics-export action; AC-048 UI), Sprint 8 (plugin install/activation audit), Sprint 9 (license audit), Sprint 11 (release retention/cap evidence).
- **Explicit out-of-scope (master)**: durable persistence; protected-storage primitive materialization; credential/session/operation/settings/shell/plugin/license runtimes; diagnostics-export UI action; installer/release packaging; Branch Shared runtime; print-boundary sunset; new `PackageReference` or pin changes; ADR/spec/schema amendments.

## 4. Prior Sprint Continuity Check

- **Previous plans inspected**: `SPRINT_1_EXECUTION_PLAN.md`, `SPRINT_2_EXECUTION_PLAN.md`.
- **Expected outputs verified**: Sprint 1 — `DobbyOptions`/`SecurityOptions`/validator, `AuditEvent`/`InMemoryAuditTrail`, `DiagnosticRedactor`/`StartupMilestone`, `HostBootstrapper`, existing tests, architecture guardrails, evidence artifacts, locked `ErrorCode` registry. Sprint 2 — `CorrelationContextAccessor.PushScope` in `src/Dobby.Core/CorrelationId.cs`; `ContractsCoreSurfaceTests.cs` present (14624 bytes); `architecture-baseline.md` contains `## Sprint 2 Reconfirmation` with 2026-05-12 path (a) renewal and 111-test pass record.
- **Missing prerequisites**: no human-approved package pin list; no test-readable pin-list representation; no human-approved storage abstraction table. Sprint 1 `Dobby:Security.*` disposition is resolved in §13.9.
- **Carry-over blockers**: `SPRINT1-INFRA-APP-PRINT-BOUNDARY` (Sprint 9); `TOOLCHAIN-PIN-APPROVAL-MISSING`/`TOOLCHAIN-PIN-REPRESENTATION-MISSING` (open); ADR-0040 §3.1 storage abstraction naming (open).
- **Must not be re-planned**: Sprint 1 architecture guardrails, skeleton scaffolds, evidence files, `ErrorCode` baseline, `auth-kdf-selection.md`; Sprint 2 Core/Contracts/SDK corrections, `ContractsCoreSurfaceTests`, route identity tests, plugin exception mapping tests, path (a) renewal record.
- **Overall readiness**: **partially blocked**. Ready: config binding, runtime folders, diagnostics expansion, in-memory audit, and `Dobby:Security.*` migration per §13.9. Blocked/deferred: durable persistence, protected-storage primitives, diagnostics-export UI, retention cleanup scheduler, package-bearing changes.

## 5. Current Codebase State

- **Modules**: `Dobby.Configuration` (46 lines, `DobbyOptions`/`SecurityOptions`/validator, `net10.0`, pkgs `Microsoft.Extensions.Options`+`Microsoft.Extensions.Options.ConfigurationExtensions`, ref `Dobby.Core`); `Dobby.Audit` (26 lines, `AuditEvent` record + `InMemoryAuditTrail`, `net10.0`, ref `Dobby.Core`); `Dobby.Diagnostics` (20 lines, `DiagnosticRedactor.Redact` + `StartupMilestone(CorrelationId:string)`, `net10.0`, pkg `Microsoft.Extensions.Logging`, ref `Dobby.Core`); `Dobby.Host` (WPF, references all 14 production projects + Serilog/Config packages); `Dobby.Core` (Sprint 2 shape with `CorrelationContextAccessor.PushScope`); `Dobby.Security` (existing PBKDF2 skeleton KDF floor code only for auth, no Sprint 3 edits); `Dobby.Infrastructure` (only `Printing/` slice).
- **Existing interfaces/classes**: `DobbyOptions{Security}`, `SecurityOptions{Pbkdf2Iterations,SaltBytes,LoginThrottleWindowSeconds}`, `DobbyOptionsValidator`; `AuditEvent(EventName,ActorId,Target,CorrelationId,OccurredAt)`, `InMemoryAuditTrail`; `DiagnosticRedactor.Redact(string)`, `StartupMilestone`; `CorrelationId`, `ICorrelationIdGenerator`, `CorrelationIdGenerator`, `ICorrelationContextAccessor`, `CorrelationContextAccessor.PushScope`; `KdfPolicy.SkeletonPbkdf2Fallback`, `Pbkdf2PasswordHasher`; `HostBootstrapper.DescribeStartupPlan()`.
- **Existing tests**: `ConfigurationTests.Security_floors_cannot_be_weakened_by_config`; `DiagnosticsAndAuditTests.Diagnostics_redactor_removes_sensitive_values` + `Audit_trail_is_append_only_from_public_boundary` (uses non-canonical event name); `SecurityTests.Pbkdf2_password_hasher_uses_floor_policy_and_verifies_password`; all Sprint 1/2 architecture+smoke tests.
- **Existing configuration**: `appsettings.json` has non-canonical `Dobby:Security` + `Dobby:Runtime{StorageMode,DefaultCulture,Telemetry}`; `Directory.Packages.props` pins all needed packages; `Directory.Build.props` has `TreatWarningsAsErrors=true`.
- **Existing gaps**: no POCO/binding/validation for ADR-0027 §6a 18 canonical keys; no complete configuration-source wiring for packaged defaults, runtime override, environment variables, and development user secrets; no runtime folder preparation; no WPF startup hook that invokes bootstrap before login/main window construction; no fail-closed contract for missing protected material/provider metadata; no Serilog local rolling file sink configuration; no sink-boundary write-time redaction enforcement; `DiagnosticRedactor` vocabulary subset of spec; no `localization.missing-key` event; no canonical startup-milestone constants; `AuditEvent` missing `Result`/`Details`, canonical payload mapping, canonical lowercase `result` value mapping, and a `Details` raw-secret prevention boundary; no `IAuditTrail` interface; no audit 365-day floor wiring; no diagnostics 30-day default + finite cap wiring; no `Storage.RequireDataAtRestProtection=true` floor wiring; no `Plugins.AllowPreLoginDiscovery=false` floor; no `Licensing.ImportRequiresPermission=true` floor; no AC-033 no-telemetry integration test; no AC-052 retention integration test; no runtime folder integration test; no diagnostics export package boundary or `diagnostics.export.created` audit emit.
- **Mismatches code vs docs**:
  1. `Dobby:Security.*` keys not in ADR-0027 §6a (canonical: `Dobby:Auth:LoginThrottleStepsSeconds`/`PasswordKdfMode`). **Resolved in §13.9: migrate to canonical `Dobby:Auth.*`; do not retain competing `Dobby:Security.*` schema.**
  2. `Dobby:Runtime.{StorageMode,DefaultCulture,Telemetry}` not canonical (`Dobby:Storage:ProviderMode`, `Dobby:Localization:DefaultCulture`; no `Telemetry` key — no-telemetry built-in).
  3. `DiagnosticsAndAuditTests` audit test uses `"auth.login.succeeded"`; taxonomy uses `user.login`.
  4. `StartupMilestone.CorrelationId` typed as `string`; sources via `Dobby.Core.CorrelationId.ToString()`.
  5. Runtime folder layout (storage spec §3.1) not created at startup.
  6. ADR-0041 §6 milestone names not emitted; Sprint 3 owns `startup.host-built`/`config-loaded`/`login-visible`.
- **Mismatches prev-plans vs code**: none (Sprint 2 §15 excluded Sprint 3 scope).

## 6. Documentation and ADR Alignment

- **Docs mapping**: `01` (privacy default-local consumed); `02` (Rows 44-47/65-68/69-70 implemented; Cədvəl 2 edges respected); `03` (Tier 1 pins consumed); `04` (Result/exception for typed audit failures); `05` (FR-011, FR-027); `06` (NFR-011, NFR-020); `07` (AC-010, AC-033, AC-052, AC-048 contribution); `08` (file-scoped namespace, UTF-8 no-BOM, banned segments); `09` (verification commands); audit-and-diagnostics spec (primary target); storage-and-data-protection spec (folder layout + fail-closed).
- **ADR classification**:

| ADR | Sprint 3 classification | Reason |
|---|---|---|
| ADR-0001 | No action | Contracts boundary unchanged. |
| ADR-0016 | No action | Sprint 3 implements; no ADR update. |
| ADR-0019 | No action | Sprint 3 implements; no ADR update. |
| ADR-0027 | No action. §13.9 resolves the Sprint 1 placeholder by migrating to ADR-0027 canonical `Dobby:Auth.*`; Sprint 3 does not update the ADR or create competing section names. |
| ADR-0029 | No action | Consumed from Sprint 2. |
| ADR-0038 | No action | Sprint 5 runtime; Sprint 3 binds selector per ADR-0027. |
| ADR-0040 | No action | Sprint 3 respects §3.1 stop condition. |
| ADR-0041 | No action | Sprint 3 implements pre-login milestones only. |
| ADR-0043 | No action | Localization defaults bound. |
| ADR-0051 | No action | Consumed only for `Licensing.ImportRequiresPermission=true`; license import runtime remains Sprint 9. |
| ADR-0054 | No action | Sprint 4. Sprint 3 binds only the `RequireDataAtRestProtection=true` floor. |
| ADR-0055 | No action | Sprint 4. Sprint 3 honors failure-mode contract at folder layer. |
| ADR-0056/0057 | No action | Docs-only; `BranchShared.RuntimeEnabled=false`. |

No ADR is updated, closed, or created in Sprint 3.

## 7. Files to Create

Layout `Resolved`: option POCOs live flat under `namespace Dobby.Configuration`. Do not create per-section `Dobby.Configuration.*` sub-folders or namespaces in Sprint 3; no canonical table row or docs amendment exists for them.

1. `src/Dobby.Configuration/RuntimeOptions.cs` — `DataRoot` (string path, default `%LocalAppData%/Dobby/`; fail-closed validator accepts only absolute or profile-relative local paths and rejects UNC/network/share paths), `EnvironmentName` (enum `Production`/`Development`/`Test`). Basis: ADR-0027 §6a rows 1-2. Test: `RuntimeOptionsTests.cs`.
2. `src/Dobby.Configuration/StorageOptions.cs` — `ProviderMode` (enum default `ProviderNeutralSkeleton`), `RequireDataAtRestProtection` (bool default `true`, **non-weakenable**), `BusyTimeoutMilliseconds` (int default `5000`, range `[100,30000]`). Basis: ADR-0027 §6a rows 3-5; ADR-0054. Test: `StorageOptionsTests.cs`.
3. `src/Dobby.Configuration/AuthOptions.cs` — `LoginThrottleStepsSeconds` (int array default `[0,0,10,30,60]`, floor non-weakenable), `PasswordKdfMode` (runtime allow-list `Argon2id`/`Pbkdf2Sha256`; Sprint 3 config value `Pbkdf2Sha256` per §13.9 and `auth-kdf-selection.md`; `HumanApprovedPinRequired` is the ADR-0027 §6a stop-condition sentinel and must fail closed if it appears in effective config after the recorded PBKDF2 selection). Basis: ADR-0027 §6a rows 6-7; ADR-0038. Test: `AuthOptionsTests.cs`.
4. `src/Dobby.Configuration/SessionOptions.cs` — `AbsoluteTimeoutMinutes` (default `480`, range `[15,1440]`), `IdleTimeoutMinutes` (default `30`, range `[5,480]`), `AutoLockEnabledByDefault` (default `false`). Basis: ADR-0027 §6a rows 8-10. Test: `SessionOptionsTests.cs`.
5. `src/Dobby.Configuration/DiagnosticsOptions.cs` — `TechnicalLogRetentionDays` (int default `30`, **fail closed below 30** unless an explicit documented support/privacy policy is introduced; Sprint 3 introduces no such policy), `TechnicalLogTotalSizeCapMegabytes` (int default `256`, range `[32,2048]`, **finite cap required**). Basis: ADR-0027 §6a rows 11-12; audit-and-diagnostics spec; AC-052. Test: `DiagnosticsOptionsTests.cs`.
6. `src/Dobby.Configuration/AuditOptions.cs` — `RetentionDays` (int default `365`, **≥365 floor**). Basis: ADR-0027 §6a row 13; audit spec; AC-052. Test: `AuditOptionsTests.cs`.
7. `src/Dobby.Configuration/LocalizationOptions.cs` — `DefaultCulture` (string BCP-47 default `az-AZ`, immutable without ADR-0043 amendment), `SupportedCultures` (array default `["az-AZ","en-US","tr-TR"]`, **must include `az-AZ`**). Basis: ADR-0027 §6a rows 14-15; ADR-0043. Test: `LocalizationOptionsTests.cs`.
8. `src/Dobby.Configuration/PluginsOptions.cs` — `AllowPreLoginDiscovery` (bool default `false`, **cannot be `true` in P1**). Basis: ADR-0027 §6a row 16; ADR-0041. Test: `PluginsOptionsTests.cs`.
9. `src/Dobby.Configuration/LicensingOptions.cs` — `ImportRequiresPermission` (bool default `true`, **non-weakenable**). Basis: ADR-0027 §6a row 17; license import spec. Test: `LicensingOptionsTests.cs`.
10. `src/Dobby.Configuration/BranchSharedOptions.cs` — `RuntimeEnabled` (bool default `false`). Basis: ADR-0027 §6a row 18; ADR-0056/0057. Test: `BranchSharedOptionsTests.cs`.
11. `src/Dobby.Configuration/DobbyOptionsValidator.cs` — split validator from `DobbyOptions.cs`; owns aggregate fail-closed validation for §7.1-§7.10 options. Basis: coding standards §4 one public top-level type per file. Test: `ConfigurationTests` + per-section option tests.
12. `src/Dobby.Audit/AuditResult.cs` — enum `Success`/`Failed`/`Denied` plus explicit canonical payload-value mapping `Success -> "success"`, `Failed -> "failed"`, `Denied -> "denied"`; no default enum-name serialization is accepted for audit payloads. Basis: ADR-0019 minimum payload `result`; audit spec minimum payload. Test: covered in `AuditEventTests.cs`.
13. `src/Dobby.Audit/AuditEventName.cs` — `public const string UserLogin = "user.login";`, `DiagnosticsExportCreated = "diagnostics.export.created"`, and remaining canonical names per audit spec §Audit Event Taxonomy (Auth/Session, User Management, Permission, Plugin Install/Trust, Plugin Lifecycle, Recovery/License, Diagnostics Export, Security Verification). Basis: audit spec §Audit Event Taxonomy. Test: `AuditEventNameTests.cs` (verbatim match per category).
14. `src/Dobby.Audit/IAuditTrail.cs` — `void Append(AuditEvent)`; `IReadOnlyList<AuditEvent> Snapshot()`. Append-only public API only. `Append` must pass through the audit-owned `Details` sanitization/rejection boundary and fail closed before storage if forbidden raw-secret details are present. Basis: ADR-0019. Test: `InMemoryAuditTrailTests.cs`, `AuditDetailsSanitizerTests.cs`.
15. `src/Dobby.Audit/AuditDetailsSanitizer.cs` — internal audit-owned boundary used by `IAuditTrail` implementations to prevent raw secret material in `AuditEvent.Details`; rejects forbidden detail keys and assignment-shaped values matching audit spec vocabulary (`password`, `passwordHash`, `token`, `privateKey`, `signature`, `session`, `sessionMaterial`, `rememberMe`, `rememberMeSecret`, `recovery`, `recoverySecret`, `recoveryNonce`, `recoveryResponse`, `licensePayload`, `licenseArtifact`, `licenseBinding`). It must use only `Dobby.Audit`'s currently allowed dependencies (`Dobby.Core` via existing project reference; no `Dobby.Audit -> Dobby.Diagnostics` edge; no shared cross-module sanitizer type invented). Basis: audit spec minimum payload/details rule; ADR-0019; architecture map Cədvəl 2. Test: `AuditDetailsSanitizerTests.cs`.
16. `src/Dobby.Diagnostics/LocalizationMissingKeyEvent.cs` — record with `Culture`, `ResourceKey`, `ResourceScope` (`shell`/`built-in`/`plugin:<id>`), `FallbackChainResult` (`found-in-fallback`/`not-found-anywhere`); dedupe tuple `(culture, resourceKey, resourceScope)`; severity floor `Warning`. Basis: audit-and-diagnostics spec §Diagnostics Events `localization.missing-key`. Test: `LocalizationMissingKeyEventTests.cs`.
17. `src/Dobby.Diagnostics/StartupMilestoneNames.cs` — const strings `HostBuilt="startup.host-built"`, `ConfigLoaded`, `LoginVisible`, `PreAuthMetadataComplete`, `SessionAuthenticated`, `ShellFirstPaint`. Sprint 3 emits pre-login subset only. Basis: ADR-0041 §6. Test: `StartupMilestoneNamesTests.cs`.
18. `src/Dobby.Host/RuntimeFolderLayout.cs` — flat `namespace Dobby.Host`; const folder names + descriptor for required tree (`data`, `config`, `logs`, `plugins`, `licenses`, `recovery`, `backup`, `temp`). Basis: storage spec §3.1. Test: `RuntimeFolderLayoutTests.cs`.
19. `src/Dobby.Host/RuntimeFolderPreparer.cs` — flat `namespace Dobby.Host`; accepts `RuntimeOptions` + `CorrelationId`, calls BCL `Directory.CreateDirectory`, emits structured log via `ILogger<RuntimeFolderPreparer>`, fails closed on missing required-folder creation or missing-protected-material prerequisite (entry-point contract — actual protected-storage probe is Sprint 4). Basis: storage spec §Runtime folder recovery; ADR-0055 §10. Test: `RuntimeFolderRecoveryTests.cs`.
20. `src/Dobby.Host/Startup/HostConfigurationSources.cs` — builds host configuration in ADR-0027 §2 order: packaged defaults/`appsettings.json`, `%LocalAppData%/Dobby/config/` runtime override, `DOBBY__<SECTION>__<KEY>` environment variables, then development user secrets only in development builds/host environment; detects unknown top-level `Dobby:*` sections and emits development warnings or production diagnostics entries without changing runtime behavior. The expected top-level `Serilog` section is not a `Dobby:*` section and must not be flagged by this unknown-section check. It must not perform network or heavy IO before login first paint. Basis: ADR-0027 §2 and §6a; tech stack Serilog.Settings.Configuration row. Test: `ConfigurationSourceOrderTests.cs`, `DobbyOptionsBindingTests.cs`.
21. `src/Dobby.Host/LocalDiagnosticsConfigurator.cs` — flat `namespace Dobby.Host`; configures Serilog local rolling file diagnostics under `<RuntimeOptions.DataRoot>/logs/` (default `%LocalAppData%/Dobby/logs/`), structured fields, rebuild-less log-level configuration from the top-level `Serilog:MinimumLevel` configuration bound by `Serilog.Settings.Configuration`, 30-day default retention that fails closed below 30 unless an explicit documented support/privacy policy is introduced, finite total-size cap behavior, and sink-boundary redaction before any event reaches the file sink. Basis: ADR-0016 §1/§2/§5/§6; audit-and-diagnostics spec §Retention and §Write-time redaction; storage spec §3.1; tech stack Serilog.Settings.Configuration row. Test: `LocalDiagnosticsSinkTests.cs`.
22. `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs` — flat `namespace Dobby.Diagnostics`; creates the Sprint 3 diagnostics export boundary without a UI hook: explicit-call-only local package construction, allowed/excluded content rules, export-time redaction, no telemetry/upload path, and export metadata that Host wiring can audit as `diagnostics.export.created` through `IAuditTrail`. Basis: audit-and-diagnostics spec §Diagnostics Export; AC-033; architecture map Row 66. Test: `DiagnosticsExportBoundaryTests.cs`.

Additional split files required by coding standards §4:

- `src/Dobby.Audit/InMemoryAuditTrail.cs` — split current `InMemoryAuditTrail` public type out of `AuditEvent.cs` while keeping flat `namespace Dobby.Audit`. Basis: coding standards one-public-type-per-file rule. Test: `InMemoryAuditTrailTests.cs`.
- `src/Dobby.Diagnostics/StartupMilestone.cs` — split current `StartupMilestone` public record out of `DiagnosticRedactor.cs` while keeping flat `namespace Dobby.Diagnostics`. Basis: coding standards one-public-type-per-file rule. Test: `StartupMilestoneNamesTests.cs` / `StartupBootstrapWiringTests.cs` / smoke.
- `src/Dobby.Host/Startup/StartupPlan.cs` — split current `StartupPlan` public record out of `HostBootstrapper.cs` while keeping approved startup namespace. Basis: coding standards one-public-type-per-file rule. Test: `StartupBootstrapWiringTests.cs` / smoke.

Test files (new, under existing test projects): `RuntimeOptionsTests.cs`, `StorageOptionsTests.cs`, `AuthOptionsTests.cs`, `SessionOptionsTests.cs`, `DiagnosticsOptionsTests.cs`, `AuditOptionsTests.cs`, `LocalizationOptionsTests.cs`, `PluginsOptionsTests.cs`, `LicensingOptionsTests.cs`, `BranchSharedOptionsTests.cs` under `tests/Dobby.UnitTests/Configuration/`; `AuditEventTests.cs`, `AuditEventNameTests.cs`, `AuditDetailsSanitizerTests.cs`, `InMemoryAuditTrailTests.cs` under `tests/Dobby.UnitTests/Audit/` (new subfolder); `DiagnosticRedactorTests.cs`, `LocalizationMissingKeyEventTests.cs`, `StartupMilestoneNamesTests.cs` under `tests/Dobby.UnitTests/Diagnostics/`; `DobbyOptionsBindingTests.cs`, `ConfigurationSourceOrderTests.cs`, `RetentionFloorIntegrationTests.cs`, `NoTelemetryDefaultTests.cs` under `tests/Dobby.IntegrationTests/Configuration/` (new subfolder); `RuntimeFolderLayoutTests.cs`, `RuntimeFolderRecoveryTests.cs`, `StartupBootstrapWiringTests.cs` under `tests/Dobby.IntegrationTests/Runtime/` (new subfolder); `LocalDiagnosticsSinkTests.cs`, `DiagnosticsExportBoundaryTests.cs` under `tests/Dobby.IntegrationTests/Diagnostics/` (new subfolder).

## 8. Files to Modify

1. `src/Dobby.Configuration/DobbyOptions.cs` — add nested properties for each new POCO (§7.1-§7.10); remove Sprint 1 placeholder `SecurityOptions` and `DobbyOptions.Security` per §13.9; move `DobbyOptionsValidator` to `src/Dobby.Configuration/DobbyOptionsValidator.cs`; keep one public top-level type per file. Risk: `TreatWarningsAsErrors=true` flags nullable drift. Tests: `ConfigurationTests` + per-section.
   Companion file: `src/Dobby.Configuration/DobbyOptionsValidator.cs` expands `DobbyOptionsValidator.Validate` with all fail-closed rules from §7 (RequireDataAtRestProtection=true; Runtime.DataRoot local-only path validation that rejects UNC/network/share paths; Localization.DefaultCulture must remain `az-AZ`; Audit.RetentionDays≥365; Diagnostics.TechnicalLogRetentionDays default 30 and fail-closed below 30 unless an explicit documented support/privacy policy is introduced; Diagnostics finite cap required; `PasswordKdfMode` runtime allow-list `Argon2id`/`Pbkdf2Sha256`, with `HumanApprovedPinRequired` treated as the ADR-0027 §6a stop-condition sentinel and KDF floor behavior governed by ADR-0038; `LoginThrottleStepsSeconds` per-step floor `[0,0,10,30,60]`; session ranges; SupportedCultures must include `az-AZ`; AllowPreLoginDiscovery=false; ImportRequiresPermission=true; BranchShared.RuntimeEnabled=false).
2. `src/Dobby.Audit/AuditEvent.cs` — add `Result` (`AuditResult`) and optional `Details` (`IReadOnlyDictionary<string,string>?`) fields; split `InMemoryAuditTrail` into `src/Dobby.Audit/InMemoryAuditTrail.cs` and make it implement `IAuditTrail`; preserve append-only API; explicitly map C# field names to canonical payload names: `OccurredAt` → `timestamp`, `ActorId` → `actor`, `EventName` → `action`, `Target` → `target`, `Result` → `result`, `CorrelationId` → `correlationId`, `Details` → `details`; explicitly map `AuditResult` values to canonical lowercase payload values: `Success` → `success`, `Failed` → `failed`, `Denied` → `denied`. `InMemoryAuditTrail.Append` must invoke `AuditDetailsSanitizer` before storing so raw password/password hash/token/private key/signature/session/remember-me/recovery/license material cannot be persisted through `Details`. Tests: `AuditEventTests`, `AuditDetailsSanitizerTests`, `InMemoryAuditTrailTests`, migrate existing test.
3. `src/Dobby.Diagnostics/DiagnosticRedactor.cs` — expand `GeneratedRegex` to cover full spec vocabulary (`password`, `passwordHash`, `token`, `privateKey`, `signature`, `session`, `sessionMaterial`, `rememberMe`, `rememberMeSecret`, `recovery`, `recoverySecret`, `recoveryNonce`, `recoveryResponse`, `licensePayload`, `licenseArtifact`, `licenseBinding`, `deviceFingerprint`, `entropy`); add structured-dictionary `RedactFields` overload returning new immutable dict; split `StartupMilestone` into `src/Dobby.Diagnostics/StartupMilestone.cs`. Preserve non-secret keys (`correlationId`, `pluginId`, `operationId`, `status`, `ok`). Tests: `DiagnosticRedactorTests`.
4. `src/Dobby.Host/appsettings.json` — replace with ADR-0027 P1 Configuration Schema canonical JSON (top-level `Dobby` with `Runtime`/`Storage`/`Auth`/`Session`/`Diagnostics`/`Audit`/`Localization`/`Plugins`/`Licensing`/`BranchShared` sub-sections at documented defaults); add a sibling top-level `Serilog` section for `MinimumLevel` binding through `Serilog.Settings.Configuration` so log level can change without rebuild; remove `Dobby:Security`; set `Dobby:Auth:PasswordKdfMode = "Pbkdf2Sha256"` per §13.9 and `auth-kdf-selection.md`. Keep UTF-8 no-BOM. Tests: `DobbyOptionsBindingTests`, `RetentionFloorIntegrationTests`, `NoTelemetryDefaultTests`, `LocalDiagnosticsSinkTests`.
5. `src/Dobby.Host/Startup/HostBootstrapper.cs` — keep placeholder `DescribeStartupPlan` shape (consumed by smoke); split `StartupPlan` into `src/Dobby.Host/Startup/StartupPlan.cs`; add `Bootstrap(...)` composition entry-point that builds configuration via `HostConfigurationSources`, configures `LocalDiagnosticsConfigurator`, prepares runtime folders via `RuntimeFolderPreparer`, emits `StartupMilestoneNames.HostBuilt`/`ConfigLoaded`/`LoginVisible`, and verifies config binding before the login/main window is constructed. `single-instance` and `login-window` placeholders remain Sprint 5. Tests: `RuntimeFolderLayoutTests`/`RuntimeFolderRecoveryTests`/`StartupBootstrapWiringTests`/`LocalDiagnosticsSinkTests`; smoke stays green.
6. `src/Dobby.Host/App.xaml` — remove or neutralize `StartupUri="MainWindow.xaml"` so WPF cannot construct `MainWindow` before the bootstrap path runs. Tests: `StartupBootstrapWiringTests`; smoke stays green.
7. `src/Dobby.Host/App.xaml.cs` — override `OnStartup` (or equivalent WPF startup hook) to invoke `HostBootstrapper.Bootstrap(...)` before showing the login/main window; ensure bootstrap failure fails closed rather than showing the shell. Tests: `StartupBootstrapWiringTests`; smoke stays green.
8. `tests/Dobby.UnitTests/Configuration/ConfigurationTests.cs` — rewrite `Security_floors_cannot_be_weakened_by_config` for §13.9 option (b): assert `PasswordKdfMode` accepts only the runtime values `Argon2id`/`Pbkdf2Sha256`, rejects values outside that allow-list, and fails closed on `HumanApprovedPinRequired` as the ADR-0027 §6a stop-condition sentinel; assert `LoginThrottleStepsSeconds` cannot be lowered below `[0,0,10,30,60]`; this is a semantic rewrite from scalar `LoginThrottleWindowSeconds` to array `LoginThrottleStepsSeconds`, not a rename.
9. `tests/Dobby.UnitTests/Diagnostics/DiagnosticsAndAuditTests.cs` — migrate `Audit_trail_is_append_only_from_public_boundary` to use `AuditEventName.UserLogin` + `AuditResult.Success`; move redactor test to `DiagnosticRedactorTests.cs` (optional).

## 9. Code Work Plan

1. **Baseline confirmation**: read `architecture-baseline.md`/`toolchain-pin-gate.md`; confirm 111-test baseline and path (a) record present; confirm Sprint 3-owning files match §5.
2. **Resolved disposition check (§13.9)**: confirm option (b) remains recorded and ADR-0027 §6a is the migration authority; remove `Dobby:Security.*` from config/options/tests and migrate to canonical `Dobby:Auth.*`.
3. **Create POCOs** (§7.1-§7.10): files only; `dotnet build` must pass.
4. **Add configuration-source wiring** (§7.20): packaged defaults, runtime override under `%LocalAppData%/Dobby/config/`, environment variables, dev user secrets in development builds, and ADR-0027 unknown top-level `Dobby:*` warning/diagnostics behavior; the expected sibling top-level `Serilog` section is allowed for Serilog.Settings.Configuration and is not part of unknown `Dobby:*` detection; precedence and unknown-section tests must fail before implementation.
5. **Expand `DobbyOptions`/Validator** (§7.11, §8.1): aggregate POCOs, split validator file, add all floor validations, including `Runtime.DataRoot` local-only validation and `Localization.DefaultCulture == "az-AZ"`.
6. **Add audit types** (§7.12-§7.15): `AuditResult`, `AuditEventName` constants, `IAuditTrail`, and audit-owned `AuditDetailsSanitizer`.
7. **Modify `AuditEvent`/split `InMemoryAuditTrail`** (§8.2): add `Result`/`Details`; implement `IAuditTrail`; preserve append-only; lock canonical field-name and lowercase result-value payload mapping; invoke audit-owned `Details` sanitization/rejection before storing; satisfy one-public-type-per-file.
8. **Expand diagnostics primitives** (§7.16, §7.17, §8.3): full redactor vocabulary; missing-key event; milestone constants.
9. **Configure local diagnostics sink** (§7.21, §8.5): Serilog rolling file sink under `<RuntimeOptions.DataRoot>/logs/` (default `%LocalAppData%/Dobby/logs/`), structured fields, sink-boundary redaction, retention, finite cap, and top-level `Serilog:MinimumLevel` log-level config.
10. **Add diagnostics export boundary** (§7.22, §13.6): local package builder in `Dobby.Diagnostics`, redaction/exclusion rules, no telemetry/upload path, and Host-side `diagnostics.export.created` emit through `IAuditTrail`; no UI action.
11. **Add runtime folders** (§7.18, §7.19, §8.5): layout + preparer; wire into `HostBootstrapper`.
12. **Wire real WPF startup** (§8.6, §8.7): ensure `App.xaml`/`App.xaml.cs` invokes bootstrap before login/main window construction.
13. **Migrate `appsettings.json`** (§8.4): canonical `Dobby` schema plus top-level `Serilog` minimum-level section; UTF-8 no-BOM; `EncodingBoundaryTests` stays green.
14. **Add tests** (§7 test list + §8.8, §8.9): unit + integration; expected total ~145-170 tests.
15. **Full verification**: `dotnet restore`/`build`/`test`/`format` — all green.

Out-of-scope guardrail at every step: no new `PackageReference`; no `ArchitectureDependencyBoundaryTests`/`ContractsCoreSurfaceTests` regression; no change to `architecture-baseline.md` or `toolchain-pin-gate.md`.

## 10. Test Plan

**Existing tests Sprint 3 keeps green**: all 111 Sprint 2-baseline tests including `ConfigurationTests` (Sprint 1 floor test, adjusted per §13), `DiagnosticsAndAuditTests` (migrated to canonical event name), all 6 architecture integration tests, smoke test.

**New unit tests** (each `[Fact]`/`[Theory]` with Shouldly assertions, file-scoped namespaces, `sealed` classes):

| File | Verifies |
|---|---|
| `RuntimeOptionsTests.cs` | `DataRoot` path shape; local-only path validation; UNC/network/share path rejection; `EnvironmentName` enum validation. |
| `StorageOptionsTests.cs` | `RequireDataAtRestProtection=true` non-weakenable; `BusyTimeoutMilliseconds` range; `ProviderMode` enum. |
| `AuthOptionsTests.cs` | `LoginThrottleStepsSeconds` floor; `PasswordKdfMode` accepts runtime values `Argon2id`/`Pbkdf2Sha256`, rejects values outside the allow-list, and fails closed on `HumanApprovedPinRequired` as the ADR-0027 §6a stop-condition sentinel. |
| `SessionOptionsTests.cs` | `AbsoluteTimeoutMinutes` range; `IdleTimeoutMinutes` range; `AutoLockEnabledByDefault` default. |
| `DiagnosticsOptionsTests.cs` | `TechnicalLogRetentionDays` default 30 and fail-closed below 30 unless an explicit documented support/privacy policy exists; `TechnicalLogTotalSizeCapMegabytes` finite, range `[32,2048]`. |
| `AuditOptionsTests.cs` | `RetentionDays ≥ 365` (AC-052 unit hook). |
| `LocalizationOptionsTests.cs` | `DefaultCulture=az-AZ`; non-`az-AZ` override rejected unless a future ADR amends ADR-0043; `SupportedCultures` must include `az-AZ`. |
| `PluginsOptionsTests.cs` | `AllowPreLoginDiscovery=false` non-weakenable. |
| `LicensingOptionsTests.cs` | `ImportRequiresPermission=true` non-weakenable. |
| `BranchSharedOptionsTests.cs` | `RuntimeEnabled=false`. |
| `AuditEventTests.cs` | All required fields; `Result` enum; `Details` optional dictionary contract; canonical event-name constants accepted; mapping from C# field names to canonical payload names (`timestamp`, `actor`, `action`, `target`, `result`, `correlationId`, `details`) locked; canonical lowercase `AuditResult` payload values locked (`success`, `failed`, `denied`). |
| `AuditEventNameTests.cs` | Each constant matches the audit-spec canonical string verbatim per category; ~40 names. |
| `AuditDetailsSanitizerTests.cs` | `IAuditTrail.Append`/audit-owned sanitizer rejects or removes `Details` entries with forbidden raw-secret keys or assignment-shaped raw secret values for password, password hash, token, private key, signature, session/session material, remember-me secret, recovery nonce/response/secret, and raw license payload/artifact/binding; preserves non-secret details such as correlation IDs, plugin IDs, operation IDs, status, and support-safe nonce hashes. |
| `InMemoryAuditTrailTests.cs` | Append + snapshot; no mutation post-snapshot; multi-event ordering; null rejection; append path cannot persist forbidden raw-secret `Details`. |
| `DiagnosticRedactorTests.cs` | Full write-time vocabulary redaction; non-secret keys preserved; dictionary overload. |
| `LocalizationMissingKeyEventTests.cs` | Record shape; dedupe tuple; severity `Warning`. |
| `StartupMilestoneNamesTests.cs` | Canonical const strings match ADR-0041 §6 verbatim. |

**New integration tests**:

| File | Verifies |
|---|---|
| `DobbyOptionsBindingTests.cs` | JSON+env round-trip for all 18 ADR-0027 keys; deterministic mapping `DOBBY__<SECTION>__<KEY>`; unknown top-level `Dobby:*` section has no runtime effect and emits development warning / production diagnostics entry per ADR-0027 §6a; top-level `Serilog` is accepted as the logging configuration section and is not treated as unknown `Dobby:*` config. |
| `ConfigurationSourceOrderTests.cs` | ADR-0027 §2 precedence: packaged defaults < runtime override under `%LocalAppData%/Dobby/config/` < environment variables < development user secrets; production does not load user secrets; malformed security-critical values fail closed. |
| `RetentionFloorIntegrationTests.cs` | **AC-052**: audit ≥365; diagnostics default 30; diagnostics values below 30 fail closed unless an explicit documented support/privacy policy exists; finite cap required; downward retention-floor attempts rejected at startup. |
| `NoTelemetryDefaultTests.cs` | **AC-033**: no telemetry/upload surface in default config; no auto-export. |
| `RuntimeFolderLayoutTests.cs` | 8 subfolder names match storage spec §3.1 table. |
| `RuntimeFolderRecoveryTests.cs` | Temp `DataRoot` fixture: creation on missing; recreation on optional; fail-closed on missing protected-material prerequisite stub. |
| `StartupBootstrapWiringTests.cs` | WPF startup path invokes `HostBootstrapper.Bootstrap` before login/main window construction; `StartupUri` cannot bypass bootstrap; bootstrap failure fails closed. |
| `LocalDiagnosticsSinkTests.cs` | With an isolated temp/profile `RuntimeOptions.DataRoot`, Serilog rolling file sink writes only under `<DataRoot>/logs/` and the default mapping resolves to `%LocalAppData%/Dobby/logs/`; structured fields include correlation/source/level; sink-boundary redaction applies before write; retention, finite total-size cap, and rebuild-less log-level config from top-level `Serilog:MinimumLevel` are enforced, including a runtime config change without rebuild. |
| `DiagnosticsExportBoundaryTests.cs` | Explicit-call-only local export package builder in `Dobby.Diagnostics` includes allowed diagnostics and excludes telemetry/raw secrets/raw license payloads; export applies redaction; Host wiring emits `AuditEventName.DiagnosticsExportCreated` through `IAuditTrail`. |

**Build/compile checks**: `dotnet build Dobby.sln --no-restore` — zero warnings under `TreatWarningsAsErrors=true`. `dotnet format Dobby.sln --verify-no-changes` — clean.

**Manual verification (release-gate, not Sprint 3 implementation)**: confirm the default `RuntimeOptions.DataRoot` resolves to `%LocalAppData%/Dobby/` and diagnostics files are created under its `logs/` subfolder on first run; AC-033 manual no-network on default install; no automatic diagnostics upload/export occurs.

## 11. Configuration, Build, and Tooling Changes

- **Project files**: none (no new `PackageReference`/`ProjectReference`; `Dobby.Host.csproj` already contains `UserSecretsId` and the required configuration/Serilog packages). Toolchain pin gate stays open; any package-bearing change requires human-approved closure first.
- **Solution file**: none.
- **Package references**: none. All needed packages (`Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Logging`, Serilog sinks via `Dobby.Host`) already referenced.
- **Build scripts**: none.
- **Runtime configuration**: `src/Dobby.Host/appsettings.json` migrated to ADR-0027 P1 `Dobby` schema plus top-level `Serilog:MinimumLevel` configuration for Serilog.Settings.Configuration (§8.4); `HostConfigurationSources` adds ADR-0027 §2 source order for runtime override, environment variables, and development user secrets without project-file changes and does not treat the sibling `Serilog` section as an unknown `Dobby:*` section.
- **Test configuration**: none at project level; existing test csprojs already reference Sprint 3-owning projects.
- **Schemas**: none.
- **Future verification commands**:

```
dotnet restore Dobby.sln
dotnet build Dobby.sln --no-restore
dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"
dotnet format Dobby.sln --verify-no-changes
```

Filtered:

```
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Dobby.UnitTests.Configuration"
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Dobby.UnitTests.Audit"
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Dobby.UnitTests.Diagnostics"
dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj --filter "FullyQualifiedName~Dobby.IntegrationTests.Configuration"
dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj --filter "FullyQualifiedName~Dobby.IntegrationTests.Runtime"
dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj --filter "FullyQualifiedName~Dobby.IntegrationTests.Diagnostics"
```

- **CI/release tooling**: none. Release tooling is Sprint 11.
- **Planning-time command restriction**: Sprint 3 planning runs no build/test/restore/package/migration/format/release commands; reserved for implementation agent.

## 12. Implementation Order

1. **Preparation**: inspect Sprint 1/2 evidence; confirm 111-test baseline; confirm path (a) record and toolchain pin gate state.
2. **Prerequisite validation**: confirm §13.9 is recorded as resolved option (b) with ADR-0027 §6a as the migration authority; apply canonical `Dobby:Auth.*` migration and do not retain `Dobby:Security.*`.
3. **File creation (POCO group)**: §7.1-§7.10 option POCOs + §7.11 validator split + §7.12-§7.17 audit/diagnostics primitives, including the coding-standards split files named in §7.
4. **File creation (runtime/diagnostics infrastructure group)**: §7.18-§7.21 host runtime/configuration/diagnostics sink files plus §7.22 diagnostics export boundary in `Dobby.Diagnostics`.
5. **Core implementation**: §8.1 validator expansion; §8.2 audit-event expansion + canonical payload mapping; §8.3 redactor expansion.
6. **Configuration and startup integration**: §8.4 `appsettings.json` migration; §8.5 `HostBootstrapper` wiring; §8.6/§8.7 WPF startup hook; verify `EncodingBoundaryTests`.
7. **Diagnostics and export integration**: Serilog sink setup, sink-boundary redaction, retention/cap behavior, explicit diagnostics export boundary in `Dobby.Diagnostics`, and Host-side `diagnostics.export.created` audit emit.
8. **Tests**: §10 unit + integration tests added incrementally with filtered runs.
9. **Verification**: full `dotnet restore`/`build`/`test`/`format` sequence per §11.
10. **Cleanup**: remove temp fixtures; confirm no orphan `Dobby:Security` references remain in config/options/tests.
11. **Documentation/ADR follow-up notes**: no edits; record observations for Sprint 4/5/7 planning; leave `architecture-baseline.md` and `toolchain-pin-gate.md` unmodified.

## 13. Risks, Blockers, and Human Decisions

### 13.1 `TOOLCHAIN-PIN-APPROVAL-MISSING` / `TOOLCHAIN-PIN-REPRESENTATION-MISSING`

- **Severity**: medium. **Affected**: any `.csproj` `PackageReference` change, any `Directory.Packages.props`/`Directory.Build.props`/`global.json` edit, any new toolchain pin-list file. **Why**: gate must close before first package-bearing change in any post-skeleton sprint. **Blocked**: adding new `PackageReference`. **Safe to proceed**: Sprint 3 plans no new `PackageReference`. **Mitigation**: §11 explicit boundary.

### 13.2 Storage abstraction naming (ADR-0040 §3.1)

- **Severity**: medium. **Affected**: any host-internal storage/audit/license/recovery primitive name. **Why**: invented names lock downstream design. **Blocked**: inventing `IHostPersistenceAuditPrimitive`-style names. **Safe**: Sprint 3 boundary keeps audit work in `Dobby.Audit` only (no `Dobby.Infrastructure` reference added); durable persistence is Sprint 4. **Mitigation**: §8.2 explicitly forbids inventing infrastructure-facing names.

### 13.3 Durable audit persistence depends on Sprint 4

- **Severity**: low (deferral, not a Sprint 3 blocker). **Affected**: `InMemoryAuditTrail` consumers (Sprint 5+). **Why**: Sprint 3 owns boundary contract only. **Safe**: Sprint 5 audit events route through `IAuditTrail`; Sprint 4 swaps in durable implementation. **Mitigation**: append-only public API guarantees implementation swap is transparent.

### 13.4 Protected-material probe deferred to Sprint 4 (ADR-0055)

- **Severity**: medium. **Affected**: `RuntimeFolderPreparer` fail-closed entry-point. **Why**: Sprint 3 cannot implement actual DPAPI probe without Sprint 4 protected-storage primitive. **Safe**: Sprint 3 wires the entry-point contract (fail-closed on missing prerequisite) using a stub-friendly seam that Sprint 4 fills. **Mitigation**: §7.19 documents the seam.

### 13.5 Retention cleanup scheduler ownership deferred

- **Severity**: low. **Affected**: actual audit retention cleanup (not Sprint 3). **Why**: audit-and-diagnostics spec §Audit retention enforcement note says "exact scheduler/runner yalnız həmin maintenance implementation phase-də bağlanır." **Safe**: Sprint 3 owns the 365-day **config floor** validation; actual cleanup is a later maintenance phase. **Mitigation**: §7/§10 tests target floor validation only.

### 13.6 Sprint 7 owns user-facing diagnostics export action

- **Severity**: low. **Affected**: AC-033 split. **Why**: Sprint 7 owns UI/action surface; Sprint 3 owns redaction/audit/no-telemetry export boundary. **Safe**: Sprint 3 implements the explicit-call-only package builder, exclusion rules, export redaction, no-upload guarantee, and `diagnostics.export.created` audit emit without wiring a user-facing UI command.

### 13.7 AC-048 Azerbaijani diacritic round-trip

- **Severity**: low. **Affected**: Sprint 3 audit-payload encoding path. **Why**: AC-048 round-trip evidence has UI ownership in Sprint 7 with Sprint 3 audit + Sprint 4 storage plumbing. **Safe**: Sprint 3 must not destroy Azerbaijani characters in audit payload or diagnostics output (UTF-8 encoding throughout). **Mitigation**: `AuditEventTests` and `DiagnosticRedactorTests` include Azerbaijani character round-trip checks.

### 13.8 Path (a) print-boundary renewal (Sprint 2 carry-over)

- **Severity**: low. **Affected**: `ArchitectureDependencyBoundaryTests` allowance + `architecture-baseline.md` reconfirmation section. **Why**: must not be touched until Sprint 9 sunset. **Safe**: Sprint 3 explicitly does not edit those files (§11) or change `Dobby.Infrastructure → Dobby.Application` edge.

### 13.9 `Resolved` — Sprint 1 `Dobby:Security.*` placeholder disposition

- **Severity**: resolved decision. **Affected**: `DobbyOptions`/`SecurityOptions`/`DobbyOptionsValidator`/`ConfigurationTests`/`appsettings.json` migration. **Why**: Sprint 1 placeholder `Dobby:Security.{Pbkdf2Iterations, SaltBytes, LoginThrottleWindowSeconds}` keys are not in ADR-0027 §6a canonical schema; ADR-0027 owns `Dobby:Auth.LoginThrottleStepsSeconds`/`PasswordKdfMode`, and ADR-0038 owns the non-configurable PBKDF2 floor constants.
- **Rejected option (a)**: preserving `Dobby:Security.*` alongside canonical `Dobby:Auth.*` would create a competing option section name, which ADR-0027 §6a forbids for P1 configuration.
- **Rejected option (c)**: amending ADR-0027 would require a separate ADR amendment outside Sprint 3's execution-plan patch scope and would keep Sprint 3 blocked.
- **Resolved**: Option (b) selected. Sprint 3 migrates Sprint 1 placeholder `Dobby:Security.*` configuration to ADR-0027 canonical `Dobby:Auth.*` keys.

Concrete consequences for Sprint 3 scope:

- `Dobby:Security` section is removed from `src/Dobby.Host/appsettings.json`.
- `SecurityOptions` class and `DobbyOptions.Security` property are removed from `src/Dobby.Configuration/DobbyOptions.cs`.
- `DobbyOptionsValidator` no longer validates `SecurityOptions`; its security rules instead target `AuthOptions.PasswordKdfMode` (runtime allow-list `Argon2id`/`Pbkdf2Sha256`, with `HumanApprovedPinRequired` treated as the ADR-0027 §6a stop-condition sentinel after the recorded PBKDF2 selection, and KDF floor behavior governed by ADR-0038) and `AuthOptions.LoginThrottleStepsSeconds` (per-step floor `[0,0,10,30,60]`).
- `ConfigurationTests.Security_floors_cannot_be_weakened_by_config` is rewritten to assert: (i) `PasswordKdfMode` accepts only runtime values `Argon2id`/`Pbkdf2Sha256`, rejects values outside that allow-list, and fails closed on `HumanApprovedPinRequired` as the ADR-0027 §6a stop-condition sentinel; (ii) `LoginThrottleStepsSeconds` cannot be lowered below the `[0,0,10,30,60]` floor. Note: this is a semantic rewrite, not a rename — `LoginThrottleWindowSeconds` (scalar) is replaced by `LoginThrottleStepsSeconds` (array).
- `appsettings.json` sets `Dobby:Auth:PasswordKdfMode = "Pbkdf2Sha256"`, consistent with `auth-kdf-selection.md` (PBKDF2-HMAC-SHA256 selection recorded) and the Sprint 3 runtime allow-list.
- PBKDF2 iteration count and salt-length floors remain non-configurable implementation constants. They already exist in `src/Dobby.Security/Authentication/Pbkdf2PasswordHasher.cs` as `KdfPolicy.SkeletonPbkdf2Fallback`/`Pbkdf2PasswordHasher` and are covered by `tests/Dobby.UnitTests/Security/SecurityTests.cs`. Sprint 3 does **not** modify, duplicate, rename, or re-floor those auth/KDF runtime types while migrating configuration to `Dobby:Auth.*`; Sprint 5 wires/enforces the existing KDF runtime against real login flows and the `AuthOptions.PasswordKdfMode` selector (AC-051 skeleton-time KDF floors). New exact type/file names for future auth runtime behavior remain Sprint 5's design choice and must not be invented in Sprint 3 (ADR-0040 §3.1 anti-name-invention).

### 13.10 `appsettings.json` non-canonical key drift

- **Severity**: low. **Affected**: `appsettings.json` migration. **Why**: `Dobby:Runtime.{StorageMode,DefaultCulture,Telemetry}` are non-canonical. **Safe**: migration to canonical schema is mechanical per ADR-0027 §6a. **Mitigation**: `Dobby:Runtime.Telemetry` is removed entirely (no-telemetry is built-in default per spec); `StorageMode` becomes `Dobby:Storage:ProviderMode`; `DefaultCulture` becomes `Dobby:Localization:DefaultCulture`.

### 13.11 `Resolved` — option POCO file layout

- **Severity**: resolved. **Affected**: file paths under `src/Dobby.Configuration/`. **Why**: no canonical sub-namespace table row exists for per-section sub-folders, and Sprint 3 must not add docs amendments. **Mitigation**: implementation agent uses flat `namespace Dobby.Configuration` for all option POCOs and does not create `Dobby.Configuration.*` sub-namespaces.

### 13.12 `Resolved` — host runtime/composition and diagnostics export placement

- **Severity**: resolved. **Affected**: `RuntimeFolderLayout`, `RuntimeFolderPreparer`, `LocalDiagnosticsConfigurator`, `DiagnosticsExportPackageBuilder`. **Why**: master plan puts runtime folders, diagnostics, and export boundary in Sprint 3 scope without approving new skeleton-critical `Dobby.Host.*` namespace placements, while architecture map Row 66 assigns the explicit local diagnostics export package flow to `Dobby.Diagnostics`. **Mitigation**: Sprint 3 creates host runtime/composition files flat under `src/Dobby.Host/` with `namespace Dobby.Host`; creates `DiagnosticsExportPackageBuilder` flat under `src/Dobby.Diagnostics/` with `namespace Dobby.Diagnostics`; and keeps Host responsible for composition/wiring plus audit emission through `IAuditTrail`. If a future human-approved storage abstraction table relocates a primitive, the move is captured in Sprint 4.

### 13.13 `Resolved` — audit `Details` raw-secret boundary

- **Severity**: resolved. **Affected**: `AuditEvent.Details`, `IAuditTrail.Append`, `InMemoryAuditTrail`. **Why**: audit spec minimum payload allows optional `details` but forbids raw secret, raw password/password hash, private key, raw license artifact/detail, raw recovery nonce, and raw recovery response material. `Dobby.Audit` cannot depend on `Dobby.Diagnostics` per architecture map Cədvəl 2, so diagnostics redaction alone does not protect audit payloads. **Mitigation**: Sprint 3 adds an audit-owned `AuditDetailsSanitizer`/append-time rejection boundary inside `Dobby.Audit`, uses only currently allowed `Dobby.Audit` dependencies, and locks tests proving forbidden raw-secret `Details` cannot be stored. No `Dobby.Audit -> Dobby.Diagnostics` reference and no shared cross-module sanitizer type are introduced.

## 14. Definition of Done

- All §7 files created.
- All §8 files modified.
- All §10 tests added (new files) and updated (`ConfigurationTests`, `DiagnosticsAndAuditTests`).
- `dotnet restore Dobby.sln` succeeds.
- `dotnet build Dobby.sln --no-restore` succeeds with **zero warnings**, **zero errors** under `TreatWarningsAsErrors=true`.
- `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` reports all tests passing; total count grows from 111 baseline to approximately 145-170.
- `dotnet format Dobby.sln --verify-no-changes` succeeds.
- All 6 architecture integration tests + smoke test stay green.
- `ConfigurationTests.Security_floors_cannot_be_weakened_by_config` rewritten for `AuthOptions.PasswordKdfMode` runtime allow-list, `HumanApprovedPinRequired` fail-closed stop-condition behavior, and `AuthOptions.LoginThrottleStepsSeconds` floor enforcement per §13.9 and passes.
- `DobbyOptionsValidator` split into `src/Dobby.Configuration/DobbyOptionsValidator.cs`; `Dobby.Configuration` keeps one public top-level type per file after Sprint 3 edits.
- `RuntimeOptions.DataRoot` accepts only local absolute/profile-relative paths and rejects UNC/network/share paths.
- `LocalizationOptions.DefaultCulture` remains `az-AZ`; attempts to override it fail closed unless a future ADR amends ADR-0043.
- No per-section `Dobby.Configuration.*` sub-namespace is created; Sprint 3 host runtime/composition files are flat under `src/Dobby.Host/` with `namespace Dobby.Host`; diagnostics export package construction is flat under `src/Dobby.Diagnostics/` with `namespace Dobby.Diagnostics`.
- Public types touched by Sprint 3 comply with coding standards §4 one-public-top-level-type-per-file, including `InMemoryAuditTrail.cs`, `StartupMilestone.cs`, and `StartupPlan.cs`.
- `DiagnosticsAndAuditTests.Audit_trail_is_append_only_from_public_boundary` migrated to canonical event name `AuditEventName.UserLogin` + `AuditResult.Success` and passes.
- Existing `Dobby.Security` PBKDF2 KDF runtime files/tests (`Pbkdf2PasswordHasher`, `KdfPolicy.SkeletonPbkdf2Fallback`, `SecurityTests.Pbkdf2_password_hasher_uses_floor_policy_and_verifies_password`) are not modified, duplicated, renamed, or re-floored by Sprint 3.
- `appsettings.json` reflects ADR-0027 P1 `Dobby` Configuration Schema plus the sibling top-level `Serilog:MinimumLevel` configuration section (UTF-8 no-BOM verified by `EncodingBoundaryTests`).
- `HostConfigurationSources` binds packaged defaults, runtime override, environment variables, and development user secrets in ADR-0027 §2 order; production does not load user secrets; unknown top-level `Dobby:*` sections emit development warnings and production diagnostics entries without changing runtime behavior; the sibling top-level `Serilog` section is accepted for logging configuration and is not flagged as unknown `Dobby:*`.
- `IAuditTrail` interface + `InMemoryAuditTrail` implementation in place; public API append-only; append path invokes audit-owned `Details` sanitization/rejection before storing.
- `AuditEvent` carries `Result` and optional `Details` per ADR-0019 minimum payload and has explicit C#-field-to-canonical-payload mapping plus canonical lowercase `AuditResult` payload-value mapping locked by tests.
- `AuditDetailsSanitizerTests` proves raw password/password hash/token/private key/signature/session/remember-me/recovery/license material cannot be persisted through audit `Details`; non-secret support-safe detail keys remain usable.
- `DiagnosticRedactor` covers the full audit-and-diagnostics spec write-time redaction vocabulary.
- Serilog local rolling file diagnostics sink writes under `<RuntimeOptions.DataRoot>/logs/` (default `%LocalAppData%/Dobby/logs/`), includes structured fields, enforces sink-boundary redaction, supports rebuild-less log-level configuration from top-level `Serilog:MinimumLevel`, honors 30-day default retention and fails closed below 30 unless an explicit documented support/privacy policy is introduced, enforces finite total-size cap behavior, and is covered by integration tests using isolated temp/profile roots.
- `DiagnosticsExportPackageBuilder` lives in `Dobby.Diagnostics`, enforces allowed/excluded content, export redaction, and no telemetry/upload behavior; Host wiring emits `diagnostics.export.created` through `IAuditTrail`.
- `RuntimeFolderPreparer` creates the storage-spec §3.1 folder tree at startup and fails closed on missing required folder or protected-material prerequisite stub.
- `App.xaml`/`App.xaml.cs` cannot bypass bootstrap; WPF startup invokes `HostBootstrapper.Bootstrap` before login/main window construction.
- Pre-login startup milestones `startup.host-built`/`startup.config-loaded`/`startup.login-visible` are emitted by `HostBootstrapper` through the real WPF startup path.
- No new `PackageReference` added to any production project.
- No change to `architecture-baseline.md`, `toolchain-pin-gate.md`, `auth-kdf-selection.md`, `Directory.Packages.props`, `Directory.Build.props`, `global.json`, or `Dobby.sln`.
- No ADR/spec/schema files modified.
- No durable-persistence/protected-storage primitive name invented.
- Sprint 9 print-boundary work not touched; `## Sprint 2 Reconfirmation` section and `IsTemporaryPrintBoundaryEdge` allowance unchanged.
- Sprint 3 exit condition satisfied: config, local diagnostics, audit boundaries, and runtime folder behavior ready for Sprint 4 persistence integration.

## 15. Out of Scope

- **Earlier-sprint work**: Sprint 1 baseline guardrails, evidence; Sprint 2 Core/Contracts/SDK corrections, `ContractsCoreSurfaceTests`, route identity tests, plugin exception mapping tests, path (a) renewal record.
- **Later-sprint work**: Sprint 4 durable persistence, protected-storage primitives, DPAPI/encryption adapters, concrete migrations/entities/repositories; Sprint 5 credential/session/single-instance/recovery runtime, KDF runtime, Owner bootstrap; Sprint 6 operation registry, user management, settings/About surfaces; Sprint 7 shell composition, navigation, theme, localization runtime, work sessions, diagnostics-export UI action; Sprint 8 plugin discovery/validation/activation runtime; Sprint 9 license import/enforcement, print-boundary sunset; Sprint 10 plugins manager UI, sample plugin completion, Admin tooling; Sprint 11 installer, release evidence, performance baseline.
- **Speculative future-proofing**: feature flags runtime, plugin-facing settings surface (P2), observability metrics/traces layer (P2), audit viewer UI (P2), diagnostics health-check UI (P2), retention cleanup scheduler design (deferred to maintenance implementation phase), runtime language switching (P2).
- **Unsupported edits**: ADR amendments/closures/new ADRs, spec edits, schema edits, documentation edits beyond `appsettings.json` (which is config, not docs).
- **Resolved migration**: §13.9 selects option (b); Sprint 3 removes `Dobby:Security.*` from config/options/tests and migrates to canonical `Dobby:Auth.*`.
- **Unrelated refactors**: namespace reorganizations beyond §7 file creation choices; renaming existing types; changing `Dobby.Core` correlation primitives.
- **Unrelated test rewrites**: any change to architecture integration tests, smoke test, or Sprint 2 unit tests beyond the migrations called out in §8.8/§8.9.
- **Package/config changes**: new `PackageReference`, `PackageVersion` additions, `Directory.Build.props`/`global.json`/`Dobby.sln` edits, new toolchain pin-list files, formatter/migration command execution.
- **Branch Shared runtime**: stays docs-only; `Dobby:BranchShared:RuntimeEnabled=false`.

## 16. Final Execution Checklist

### Before starting

- [ ] Read `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 3 block.
- [ ] Read `SPRINT_1_EXECUTION_PLAN.md` carry-over blockers + this plan §4.
- [ ] Read `SPRINT_2_EXECUTION_PLAN.md` §14 DoD + §15 out-of-scope.
- [ ] Read all §2 source files at full depth before editing.
- [ ] Confirm §13.9 is recorded as `Resolved` (Option (b)) and ADR-0027 §6a is the migration authority.
- [ ] Confirm `architecture-baseline.md` still contains `## Sprint 2 Reconfirmation` with 2026-05-12 path (a) renewal.
- [ ] Confirm `toolchain-pin-gate.md` blockers remain open; no new `PackageReference` planned.
- [ ] Run baseline `dotnet restore`/`build`/`test`/`format` — must be 111 tests green.

### During implementation

- [ ] §9 ordered steps 1-15.
- [ ] Per-step verification: `dotnet build` after each file batch; filtered `dotnet test` after each test file group.
- [ ] No new `PackageReference`.
- [ ] No edits to `architecture-baseline.md`/`toolchain-pin-gate.md`/`auth-kdf-selection.md`.
- [ ] No edits to ADRs/specs/schemas.
- [ ] No edits to `Directory.Packages.props`/`Directory.Build.props`/`global.json`/`Dobby.sln`.
- [ ] All new files UTF-8 no-BOM; `EncodingBoundaryTests` stays green.
- [ ] No `Dobby.Infrastructure` reference added to `Dobby.Audit` (durable persistence is Sprint 4).
- [ ] No `Dobby.Diagnostics` reference added to `Dobby.Audit`; audit `Details` redaction/rejection stays audit-owned.
- [ ] No edits, renames, duplicates, or re-flooring of existing `Dobby.Security` PBKDF2 KDF runtime files/tests.
- [ ] No invention of host-internal storage primitive names.
- [ ] `App.xaml` does not allow `StartupUri` to bypass `HostBootstrapper.Bootstrap`.
- [ ] Diagnostics sink writes only under `<RuntimeOptions.DataRoot>/logs/` (default `%LocalAppData%/Dobby/logs/`); integration tests use isolated temp/profile roots; no telemetry/upload/export path is automatic.

### Before finishing

- [ ] Run full verification command sequence (§11).
- [ ] All §14 DoD bullets satisfied.
- [ ] Test count grew from 111 to approximately 145-170.
- [ ] All architecture integration tests + smoke test pass.
- [ ] AC-010/AC-033/AC-052 integration tests pass.
- [ ] `AuditDetailsSanitizerTests`, `ConfigurationSourceOrderTests`, `StartupBootstrapWiringTests`, `LocalDiagnosticsSinkTests`, and `DiagnosticsExportBoundaryTests` pass.
- [ ] No out-of-scope file modified.
- [ ] Implementation observations recorded for Sprint 4 (durable audit store wiring point), Sprint 5 (credential runtime + audit event consumption), Sprint 7 (diagnostics-export action wiring point).

## 17. Planning Coverage and Confidence

- **Deeply verified**: master Sprint 3 scope; ADR-0016, ADR-0019, ADR-0027, ADR-0040, ADR-0055; audit-and-diagnostics spec; storage-and-data-protection spec; AC-010/AC-033/AC-052 acceptance criteria; current `Dobby.Configuration`/`Dobby.Audit`/`Dobby.Diagnostics`/`Dobby.Host` source state; existing `ConfigurationTests` and `DiagnosticsAndAuditTests`; `appsettings.json`; architecture map Cədvəl 2 rows for Sprint 3 owners.
- **Targeted inspection**: Sprint 1/Sprint 2 execution plans for continuity; ADR-0029/0038/0041/0043/0051/0054/0056/0057 boundary protection; `docs/specs/license-import.md` permission-boundary context; existing `Dobby.Security` PBKDF2 KDF floor implementation and test coverage; `Directory.Packages.props` pin coverage; integration test guardrails (`ArchitectureDependencyBoundaryTests`, `PackageGovernanceTests`, `EncodingBoundaryTests`); release-evidence artifacts.
- **Boundary/context only**: Sprint 4-11 master plan sections; Branch Shared schemas; full plugin manifest schema; non-Sprint-3 ADRs; smoke test internals.
- **Not verified**: behavior of Sprint 2's `ContractsCoreSurfaceTests` against the proposed Sprint 3 additions (assumed: Sprint 3 adds no Contracts surface, so no regression risk); behavior under non-Windows runtime (out of P1 scope per ADR-0040); actual DPAPI failure-mode latency (Sprint 4); plugin-side audit event consumption (Sprint 8); license-side audit event consumption (Sprint 9).
- **Confidence level**: **High** for ADR-0027 schema binding/source order, §13.9 `Dobby:Security.*` to `Dobby:Auth.*` migration, runtime folder handling, diagnostics redaction expansion, Serilog local sink wiring, in-memory audit boundary with audit-owned `Details` raw-secret prevention, diagnostics export boundary, and AC-052 floor-validation tests; **Medium** for `RuntimeFolderPreparer` protected-material seam (true probe is Sprint 4); **Lower** confidence (deliberately out of Sprint 3 scope) for durable audit persistence wiring, protected-storage primitive names, retention cleanup scheduler.
- **Reviewer focus**: §13.9 resolved migration must remove all `Dobby:Security.*` config/options/test references, treat `HumanApprovedPinRequired` as the ADR-0027 §6a fail-closed stop-condition sentinel, acknowledge existing `Dobby.Security` PBKDF2 KDF floor code/tests without modifying or duplicating them, and avoid inventing Sprint 5 KDF type/file names; §7.13 audit event name list (~40 constants must match audit-and-diagnostics spec verbatim); §7.12/§8.2 audit payload mapping must lock both field names and lowercase `AuditResult` values; §7.15 audit `Details` sanitizer must prevent raw-secret persistence without adding a `Dobby.Audit -> Dobby.Diagnostics` reference; §8.3 redactor vocabulary (must cover spec list without breaking non-secret keys); §8.4 `appsettings.json` migration (UTF-8 no-BOM; canonical `Dobby` key shape plus top-level `Serilog:MinimumLevel`); §7.20 configuration source order and unknown-section warning/diagnostics behavior (top-level `Serilog` is not unknown `Dobby:*`); §7.21 diagnostics sink-boundary redaction, `<RuntimeOptions.DataRoot>/logs/` resolution, isolated-root test behavior, 30-day fail-closed retention floor unless explicit policy exists, finite cap behavior, and rebuild-less log-level behavior; §7.22 diagnostics export boundary; §8.6/§8.7 WPF startup hook; §7.19 `RuntimeFolderPreparer` fail-closed seam (must not block Sprint 4 protected-storage primitive plug-in).
