# Sprint 4 Execution Plan — Protected Storage and Persistence Materialization

## 1. Sprint Summary

- Sprint number and title: Sprint 4 — `Protected Storage and Persistence Materialization`.
- Main goal: materialize the protected local persistence required for P1 release — encrypted DB-at-rest provider per `ADR-0054`, protected-storage scope mapping per `ADR-0055`, the closed pre-login bootstrap table set per `docs/specs/storage-and-data-protection.md` §3.1/§5a, post-login migration infrastructure, transactional write boundaries, foreign-key/busy-timeout guardrails and crash-recovery behavior — without exposing storage topology, provider, key material or backing paths to plugins.
- Expected deliverable: production-grade host persistence layer that closes `AC-037`, `AC-038`, `AC-042` using the approved Phase A storage/toolchain decisions recorded on 2026-05-14, plus a startup provider-enabled probe, a `Dobby` application-level entropy bootstrap, a `DobbyStableDeviceId` separate from that entropy, DPAPI scope mapping per `ADR-0055`, and protected-storage wrappers consumed by Sprint 5+ auth/session/recovery/license/audit flows.
- Why this sprint exists: every subsequent P1 sprint (`5` auth/session/recovery, `6` permissions/users/settings, `8` plugin install/cache, `9` license state, `10` plugin manager, `11` release evidence) consumes durable protected storage. The `SPRINT_IMPLEMENTATION_PLAN.md` cross-sprint dependency table calls Sprint 4 "the major release-blocking dependency for durable auth, sessions, permissions, licenses, plugin state, audit retention, and release storage proof". Sprint 3 finished the in-memory boundaries (`IAuditTrail`, configuration floors, runtime folder tree) that Sprint 4 backs with durable, protected implementations.
- Readiness: **READY FOR PHASE B AFTER PREFLIGHT**. The master plan blocker ("This sprint is blocked until documented human decisions are available.") is satisfied by the approved Phase A artifacts plus the approved Phase B decision anchor:
  - `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` records `Status: APPROVED` for Sprint 4 storage package/native asset scope and resolves `TOOLCHAIN-PIN-APPROVAL-MISSING` plus `TOOLCHAIN-PIN-REPRESENTATION-MISSING` for Phase A. Approved Sprint 4 dependencies are `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLite3MultipleCiphers v2.3.4` / `sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, and `Dapper` `2.1.72`. `Konscious.Security.Cryptography.Argon2` remains Sprint 5 KDF scope unless a separate human-approved scope pull amends this plan.
  - `artifacts/release-evidence/skeleton-local/storage-materialization.md` records `Status: APPROVED`, Phase A closure approver `n4hkro`, Phase A closure date `2026-05-14`, and `Phase B implementation may start: YES`. It approves the Sprint 4 primitive surface, local `PersistenceError` sub-code set, unchanged 17-entry Core `ErrorCode` registry, pre-login schema/migration authority, grant-row `Permissions`, reserved post-login migration ID, DB-at-rest cipher/KDF profile, native provider proof, Dapper selection, TFM/platform strategy, and Argon2 deferral.
  - `artifacts/release-evidence/skeleton-local/decision-log.md` records `phase-b-decisions-2026-05-14` as `APPROVED` by `n4hkro`, closing the reconciled D-01..D-12 implementation-detail decisions that Phase A intentionally left outside the storage/toolchain ledgers.
  - Phase B must still begin with a no-code preflight that verifies both Phase A ledgers are present, still `APPROVED`, still within the freeze window, and still match the approved package/schema/provider decisions; it must also verify the Phase B decision anchor is present and `APPROVED`. Any Phase A ledger drift before the first Sprint 4 implementation task reopens Phase A and stops implementation until a separate approval pass closes it.
- Strategy: consume the approved Phase A ledgers, then implement Sprint 4 in the ordered phases below. Sprint 4 will (a) add the approved Tier 3 storage/native production package pins and the required package-ledger consistency test, (b) materialize the closed pre-login table set in `Dobby.Infrastructure / Host Persistence` using approved type names and grant-row `Permissions`, (c) provide neutral post-login migration infrastructure and ordering tests without production post-login schema files, (d) wire `Dobby.Audit -> Dobby.Infrastructure` and `Dobby.Security -> Dobby.Infrastructure` `ProjectReference` edges already allowed by `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 only after the unresolved `Dobby.Infrastructure -> Dobby.Application` print-boundary edge is prevented from flowing transitively into those consumers and verified by architecture tests, (e) implement DPAPI-wrapped Dobby application-level entropy + separate `DobbyStableDeviceId` per `ADR-0055`, (f) implement the encrypted-provider startup probe + WAL/journal/temp protection + `foreign_keys=ON` + bounded `busy_timeout`, (g) replace `Dobby.Host.RuntimeFolderPreparer`'s stubbed `protectedMaterialPrerequisiteAvailable` with a real protected-material probe that generates first-run protected material on clean installs but fails closed for missing/malformed material after the approved initialized-state marker exists, (h) swap `InMemoryAuditTrail` for a durable `IAuditTrail` implementation, (i) close `AC-037`/`AC-038`/`AC-042` with integration tests and a crash-recovery smoke. No Sprint 5+ auth/session/recovery runtime, no shell, no license import, no plugin manager.
- Dependency summary: Sprint 4 builds on Sprint 1 (build/architecture baseline, evidence files, ErrorCode registry), Sprint 2 (`Dobby.Core` primitives, plugin-storage contracts, `ContractsCoreSurfaceTests`), and Sprint 3 (`ADR-0027` 18-key configuration model, `RuntimeFolderLayout`/`RuntimeFolderPreparer`, `IAuditTrail`/`InMemoryAuditTrail` boundary, `DiagnosticRedactor` sensitive assignment/key redaction, runtime config sources, Serilog local sink, `HostBootstrapper` WPF wiring). Sprint 4 unblocks Sprint 5 (auth/session/Owner bootstrap/recovery), Sprint 6 (permissions/users/settings), Sprint 8 (plugin install/cache), Sprint 9 (license state), Sprint 10 (plugin manager + sample plugin), Sprint 11 (release evidence including native storage proof and KDF latency).

## 2. Sources Read

**Master sprint plan**: `SPRINT_IMPLEMENTATION_PLAN.md` — `deep` (Sprint 4 §3, cross-sprint §4, original blocked/decision-dependent §5; Sprint 3 exit consumed; Sprint 5-11 boundary).

**Previous sprint plans**: `SPRINT_1_EXECUTION_PLAN.md` — `targeted` (Section 2 sources read, deferred drift, toolchain pin blockers). `SPRINT_2_EXECUTION_PLAN.md` — `targeted` (Section 1 deferred drift, path (a) renewal). `SPRINT_3_EXECUTION_PLAN.md` — `deep` (§5 current code state, §13 risk register including `TOOLCHAIN-PIN-APPROVAL-MISSING`/`13.2 ADR-0040 §3.1`/`13.3 durable audit deferral`/`13.4 protected-material seam`, §14 DoD, §15 out-of-scope).

**Later sprint references** (`boundary/context` only): Sprint 5 (consumes pre-login closed table set, host-side recovery import); Sprint 6 (consumes Permissions table); Sprint 8 (consumes plugin install/cache state, validates `Dobby.PluginFramework -> Dobby.Infrastructure` boundary stays unchanged unless amended); Sprint 9 (consumes license state + print-boundary sunset); Sprint 11 (consumes `AC-037`/`AC-038`/`AC-042` evidence + native storage proof + KDF baseline-latency).

**Repo-root files**: `README.md` (`boundary/context`); `global.json` `targeted` (SDK pin `10.0.202`, `latestPatch`); `Directory.Build.props` `targeted` (`Nullable=enable`, `LangVersion=14.0`, `TreatWarningsAsErrors=true`); `Directory.Packages.props` `deep` (current pin set — Tier 1 `System.Security.Cryptography.ProtectedData` `10.0.7` present; SQLite/Dapper/native encrypted provider absent; Argon2 absent and left to Sprint 5 KDF scope unless separately approved); `Dobby.sln` `targeted` (18 src + 3 tests + 1 sample); `.editorconfig`/`.gitignore` (`boundary/context`).

**Product / architecture / requirements docs**: `docs/01_PRODUCT_SCOPE.md` (`boundary/context`); `docs/02_ARCHITECTURE_MAP.md` `deep` (Cədvəl 1 Rows 87-89 `Dobby.Infrastructure`/`Hardware Identity`/`Host Persistence / Consumed Nonces`; Row 44-47 `Dobby.Configuration`; Row 69-70 `Dobby.Audit`; Row 34-41 `Dobby.Security`; Cədvəl 2 dependency table — `Dobby.Audit -> Dobby.Infrastructure` and `Dobby.Security -> Dobby.Infrastructure` allowed; Cədvəl 3 `Dobby.Infrastructure Boundary`, `Secure Storage Boundary`); `docs/03_TECH_STACK_AND_VERSIONS.md` `deep` (Storage row SQLite+Dapper, Secure storage row DPAPI, Section 8 Pin Tier table — Tier 3 `Microsoft.Data.Sqlite.Core`/`SQLitePCLRaw.core`/`Dapper`, Tier R `SQLitePCLRaw.bundle_*`, Target Framework Split `Dobby.Infrastructure` defaults `net10.0` with optional `net10.0-windows` adapter slice; Tier 2 `Konscious.Security.Cryptography.Argon2` noted as Sprint 5 KDF scope unless separately pulled forward); `docs/04_RESULT_VS_EXCEPTION_POLICY.md` (`boundary/context` — 17-entry ErrorCode registry locked); `docs/05_FUNCTIONAL_REQUIREMENTS.md` `targeted` (FR-007 plugin install signature, FR-016/FR-017 license binding, FR-022 throttle floor); `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` `deep` (NFR-007 local data protection, NFR-020 privacy default-local); `docs/07_ACCEPTANCE_CRITERIA.md` `deep` (AC-037 production DB-at-rest provider activation, AC-038 SQLite pragmas/WAL protection, AC-042 native storage proof, AC-051 KDF floors split — Sprint 5 owns selection/floors, Sprint 11 owns latency baseline; Sprint 4 does not claim AC-051 ownership); `docs/08_CODING_STANDARDS.md` `targeted` (one-public-type-per-file, banned segments, UTF-8 no-BOM); `docs/09_BUILD_RUN_AND_RELEASE.md` (`boundary/context`).

**ADRs**: `ADR-0001` (`boundary/context`); `ADR-0003` `targeted` (plugin storage compartments unchanged); `ADR-0019` `targeted` (audit append-only persistence becomes durable); `ADR-0027` `deep` (§6a 18-key schema; `Storage.ProviderMode` transition gate); `ADR-0029` (`boundary/context`); `ADR-0030` `deep` (host persistence ownership, 3-bucket taxonomy, plugin-invisible backing primitives); `ADR-0031` `targeted` (Session state shape; Sprint 5 runtime); `ADR-0038` `targeted` (DPAPI session material storage, KDF runtime is Sprint 5); `ADR-0039` `targeted` (consumed recovery nonces persistence under `Dobby.Infrastructure`); `ADR-0040` `deep` (§3.1 per-row ownership table, anti-name-invention stop condition, allowed direct edges `Dobby.Audit -> Dobby.Infrastructure` and `Dobby.Security -> Dobby.Infrastructure`, target framework split); `ADR-0041` `targeted` (pre-login allowed workload, login-first floor); `ADR-0051` `targeted` (license state schema; Sprint 9 runtime); `ADR-0052` `targeted` (CustomerDeploymentContext min field set); `ADR-0053` (`boundary/context`); `ADR-0054` `deep` (Option A SQLite full-DB encryption, AES-256-level floor, native binary supply chain, startup provider-enabled probe, deprecated bundle prohibition); `ADR-0055` `deep` (per-secret protected-storage scope mapping, Dobby application-level entropy invariant, `DobbyStableDeviceId` separation, failure modes); `ADR-0056`/`ADR-0057` (`boundary/context` — BranchShared runtime stays docs-only).

**Specs**: `docs/specs/storage-and-data-protection.md` `deep` (primary spec — §1 protection, §2 key source boundary, §3 taxonomy, §3.1 runtime layout, §4 plugin compartments, §5 implementation notes, §5a P1 Persistence Materialization Pass with anti-name-invention, pre-login closed table set, §6 verification minimums); `docs/specs/audit-and-diagnostics.md` `targeted` (audit append-only durable storage requirement, 365-day floor, retention enforcement); `docs/specs/session-lifecycle.md` `boundary/context` (Sprint 5 consumer); `docs/specs/user-management.md` `boundary/context` (Sprint 5/6 consumers); `docs/specs/license-import.md` `boundary/context` (Sprint 9 consumer); `docs/specs/plugins-manager.md` `boundary/context` (Sprint 8/10 consumers).

**Schemas**: `docs/schemas/dobby-plugin-manifest.schema.json` `boundary/context` (Sprint 8/manifest scope unchanged); `docs/schemas/dobby-plugin-license.schema.json` `boundary/context` (Sprint 9 scope unchanged); `docs/schemas/branchshared.schema.json` `boundary/context` (BranchShared docs-only).

**Source code**: `src/Dobby.Configuration/*` `deep` (10 option POCOs from Sprint 3, `DobbyOptionsValidator.cs` with floors; no `Dobby.Contracts` reference yet — Sprint 1 deferred drift; no `Dobby.Infrastructure` reference); `src/Dobby.Audit/*` `deep` (`IAuditTrail`/`InMemoryAuditTrail`/`AuditEvent`/`AuditEventName` 37 names/`AuditResult`/`AuditResultExtensions`/`AuditDetailsSanitizer` — references only `Dobby.Core`); `src/Dobby.Diagnostics/*` `targeted` (`DiagnosticRedactor` currently redacts sensitive assignment/key vocabulary to `<redacted>`; no separate path/guid/base64/email/long token families, plus `DiagnosticsExportPackageBuilder`, `LocalizationMissingKeyEvent`, `StartupMilestone`/`StartupMilestoneNames`); `src/Dobby.Host/*` `deep` (`HostConfigurationSources`, `LocalDiagnosticsConfigurator`, `RuntimeFolderLayout` 8 folders, `RuntimeFolderPreparer` with stubbed protected-material seam, `Startup/HostBootstrapper.cs` with WPF entry via `App.xaml.cs`, `appsettings.json` ADR-0027 canonical + top-level `Serilog`, `Trust/README.md`); `src/Dobby.Infrastructure/*` `deep` (`Dobby.Infrastructure.csproj` references `Dobby.Application` (print-boundary carry-over Sprint 9 sunset) + `Dobby.Core`; NO `Dobby.Contracts` reference yet; `Printing/WindowsPrintTargetClassifier.cs` only); `src/Dobby.Application/Printing/PrintTargetClassifier.cs` `targeted` (print-boundary carry-over); `src/Dobby.Security/*` `targeted` (`Dobby.Security.csproj` references `Dobby.Core`+`Dobby.Contracts` only; `Authentication/Pbkdf2PasswordHasher.cs` Sprint 1 PBKDF2 floor 600_000 iterations/16-byte salt; `Authorization/OperationKey.cs`); `src/Dobby.Core/CorrelationId.cs` `boundary/context`.

**Tests**: `tests/Dobby.UnitTests/Audit/*` `targeted` (`AuditDetailsSanitizerTests`, `AuditEventNameTests`, `AuditEventTests`, `InMemoryAuditTrailTests`); `tests/Dobby.UnitTests/Configuration/*` `targeted` (11 option tests); `tests/Dobby.UnitTests/Diagnostics/*` `targeted` (4 tests); `tests/Dobby.UnitTests/Security/SecurityTests.cs` `targeted` (PBKDF2 floor coverage); `tests/Dobby.IntegrationTests/Architecture/*` `deep` (`ArchitectureDependencyBoundaryTests` reads Cədvəl 2 + temp print-boundary allowance + ErrorCode 17-entry baseline; `ContractsCoreSurfaceTests` ADR-0001 type-level Contracts/Core surface + WPF-free Contracts; `EncodingBoundaryTests` UTF-8/no-BOM; `PackageGovernanceTests` central versions/test-only/Tier 0 BCL/Tier R rejected — `SQLitePCLRaw.bundle_*` rejected unless explicit opt-in; `PluginAndSdkBoundaryTests`; `ReleaseEvidenceTests`); `tests/Dobby.IntegrationTests/Configuration/*` `targeted` (`DobbyOptionsBindingTests`, `ConfigurationSourceOrderTests`, `RetentionFloorIntegrationTests`, `NoTelemetryDefaultTests`); `tests/Dobby.IntegrationTests/Diagnostics/*` `targeted` (`LocalDiagnosticsSinkTests`, `DiagnosticsExportBoundaryTests`); `tests/Dobby.IntegrationTests/Runtime/*` `deep` (`RuntimeFolderLayoutTests`, `RuntimeFolderRecoveryTests` — the `protectedMaterialPrerequisiteAvailable=false` fail-closed test fixes the Sprint 4 protected-material seam contract); `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs` `targeted`; `tests/Dobby.IntegrationTests/ProjectBoundaryTests.cs` `targeted`; `tests/Dobby.SmokeTests/ShellSmokeTests.cs` `boundary/context`.

**Project / config / build files**: `src/Dobby.Configuration/Dobby.Configuration.csproj` `targeted` (uses `Microsoft.Extensions.Options`+`.ConfigurationExtensions`, references `Dobby.Core`; no `Dobby.Contracts` yet); `src/Dobby.Audit/Dobby.Audit.csproj` `targeted` (references `Dobby.Core` only); `src/Dobby.Diagnostics/Dobby.Diagnostics.csproj` `targeted` (references `Dobby.Core`); `src/Dobby.Host/Dobby.Host.csproj` `deep` (WPF/Windows TFM `net10.0-windows`, references 14 production projects, `UserSecretsId=dobby-host-skeleton`, Serilog packages); `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` `deep` (TFM `net10.0`, `System.Security.Cryptography.ProtectedData` package, references `Dobby.Application` (print-boundary edge) + `Dobby.Core`); `src/Dobby.Security/Dobby.Security.csproj` `targeted`; `src/Dobby.Application/Dobby.Application.csproj` `boundary/context`; `tests/Dobby.UnitTests/Dobby.UnitTests.csproj` and `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj` `targeted` (test references — IntegrationTests references `Dobby.Host`+`Dobby.PluginFramework`+`Dobby.Security`).

**Release / evidence artifacts**: `artifacts/release-evidence/skeleton-local/architecture-baseline.md` `deep` (Sprint 1 + Sprint 2 Reconfirmation 2026-05-12 path (a) renewed; 111 tests Sprint 2 baseline; `SPRINT1-INFRA-APP-PRINT-BOUNDARY` Sprint 9 sunset; Sprint 4 Phase B D-01 decision note); `artifacts/release-evidence/skeleton-local/decision-log.md` `targeted` (Phase A approval anchor and Phase B D-01..D-12 approval anchor); `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` `deep` (Sprint 4 candidate pin representation `APPROVED`; `TOOLCHAIN-PIN-APPROVAL-MISSING`/`TOOLCHAIN-PIN-REPRESENTATION-MISSING` resolved for Phase A; table/JSON consistency test required as first package-governance task); `artifacts/release-evidence/skeleton-local/storage-materialization.md` `deep` (`APPROVED` Phase A decision ledger; approver `n4hkro`; closure date `2026-05-14`; `Phase B implementation may start: YES`; exact packages, primitive surface, DB-at-rest profile, schema/migration authority, Core ErrorCode no-expansion decision); `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md` `targeted` (PBKDF2-HMAC-SHA256 selected, skeleton-local 97.99 ms measured; release-candidate evidence regenerated under Sprint 11).

## 3. Sprint Scope From Master Sprint Plan

Sprint 4 in `SPRINT_IMPLEMENTATION_PLAN.md` is named `Protected Storage and Persistence Materialization`.

**Sprint goal**: deliver production-grade protected local persistence — encrypted DB-at-rest provider, protected-storage scope wrapping, pre-login closed bootstrap table set, post-login migration infrastructure, transactional commit/rollback, foreign-key/busy-timeout/WAL/journal/temp guardrails, crash-recovery behavior, and an at-startup provider-enabled probe — without leaking storage topology, provider identity, key material, or backing paths to plugins.

**Task groups** (verbatim from master plan §3 Sprint 4 + reconciled with `docs/specs/storage-and-data-protection.md` §5a):

**Task group 4.1 — Storage abstraction shape (`ADR-0040` §3.1 approved by Phase A ledger)**.
- Define the approved host-internal provider-neutral persistence primitive abstractions from `storage-materialization.md`: `IHostPersistenceConnectionFactory`, `IHostPersistenceTransactionScope`, `IHostPersistenceMigrator`, `IHostPersistenceCommandExecutor`, and local `PersistenceError` under `Dobby.Infrastructure.Persistence`. Exact method signatures remain implementation-phase details constrained by `ADR-0040` and the ledger. Core `ErrorCode` additions are not approved for Sprint 4.
- Confirm placement satisfies: not in `Dobby.Contracts`, not in `Dobby.SDK`, not in `Dobby.Application`/`Dobby.Shell`/`Dobby.UI` use-case surface; placement candidate: `Dobby.Infrastructure` (per Cədvəl 1 Row 87) or a co-owner project with explicit Cədvəl 2 amendment.
- Storage primitive abstractions do not surface raw `IDbConnection`/`SqliteConnection`/`Microsoft.Data.Sqlite` types past the host adapter boundary; consumers (`Dobby.Audit`, `Dobby.Security`, host-internal use cases) receive provider-neutral types only.

**Task group 4.2 — Encrypted SQLite provider materialization (`ADR-0054` Option A, Phase A package/provider approval consumed)**.
- Adopt the human-approved native encrypted SQLite provider (`SQLite3MultipleCiphers` upstream signed/prebuilt, self-built path, or approved equivalent; `SQLitePCLRaw.bundle_e_sqlcipher`/`bundle_sqlcipher` and similar deprecated bundle packages remain `Tier R` rejected per `PackageGovernanceTests.Rejected_or_deferred_packages_are_absent_without_human_opt_in` and tech-stack §8).
- Pin the approved package/native set from the Phase A ledgers in `Directory.Packages.props` only after preflight and the required package-ledger consistency test: `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, `Dapper` `2.1.72`, and the approved `SQLite3MultipleCiphers v2.3.4` native asset/package path.
- Wire the native provider via the approved provider initialization path so that production startup proves the cipher is loaded and active (provider-enabled probe at startup that fails closed when provider-disabled, provider-bypass or wrong native binary).
- Provider proof MUST be expressed as named assertions: open without key fails closed, open with wrong key fails closed, open with correct key succeeds while the raw header is not `SQLite format 3\0`, a test-only sentinel marker is absent from DB/WAL/journal/temp plaintext scans, and wrong-provider startup fails closed without writing the `«PersistenceOpened»` milestone.
- Encrypt the entire host SQLite database (including WAL/journal/temp side-files). AES-256-level confidentiality + integrity floor stands. Unprotected production DB mode forbidden by `ADR-0027` §6a `Dobby:Storage:RequireDataAtRestProtection=true`.

**Task group 4.3 — Protected-storage scope mapping (`ADR-0055`)**.
- Implement DPAPI wrappers in `Dobby.Infrastructure` that map each protected-storage scope per `ADR-0055`:
  - DB-at-rest provider key/DEK wrap, password-hash/auth credential material, owner recovery secret/consumed nonce material → DPAPI `LocalMachine` + Dobby application-level entropy.
  - Remember-me material → DPAPI `CurrentUser` + Dobby user/slot entropy.
  - Session material → DPAPI `CurrentUser` + Dobby session entropy.
  - Plugin `UserScopedStorage` secrets → DPAPI `CurrentUser`.
  - Shared domain/customer/device-bound secrets → DPAPI `LocalMachine` (Standalone mode); BranchShared runtime scope rows remain docs-only per `ADR-0056`/`ADR-0057`.
- DPAPI/Win32/native-provider concrete code MUST follow the approved Windows-only adapter/TFM strategy from `ADR-0040` + tech-stack TFM guidance: platform-neutral abstractions stay visible to `Dobby.Audit`/`Dobby.Security`, concrete Windows code is isolated inside the existing `Dobby.Infrastructure` project through a `net10.0;net10.0-windows` multi-target/conditional Windows adapter slice, and `CA1416`/platform analyzer warnings remain green under `TreatWarningsAsErrors=true`. Sprint 4 adds zero new projects; a separate `Dobby.Infrastructure.Windows` project requires a future architecture-map amendment.
- Generate and persist the Dobby application-level entropy as a protected blob under `%LocalAppData%/Dobby/data/` at first run; rotation policy follows host installation/uninstall lifecycle. Entropy material is never logged, never exported via diagnostics package, never returned across plugin boundary.
- Generate and persist `DobbyStableDeviceId` as a separate host-installation-bound random identity used for hardware/identity-bound binding contexts but NEVER as a key source for DPAPI scope or DB-at-rest key derivation (`NFR-007` floor; Cədvəl 3 `Secure Storage Boundary`).
- Malformed or missing protected references fail closed (no silent re-creation of secret material), per `ADR-0027` §6 protected-value handling.

**Task group 4.4 — Pre-login closed bootstrap table set (`docs/specs/storage-and-data-protection.md` §3.1)**.
- Materialize exactly the 11 tables allowed for pre-login startup work: `Users`, `Credentials`, `RememberMeSlot`, `Sessions`, `Permissions`, `AuditLog`, `ConsumedRecoveryNonces`, `CustomerDeploymentContext`, `HostConfiguration`, `StorageMetadata`, `DeviceIdentity`. The set is closed: no additional pre-login table without spec amendment.
- Entity/record type names, repository names, migration ownership and column/constraint shapes follow the same `ADR-0040` §3.1 / storage spec §5a human-approved table. Minimum field sets per spec §3.1 plus per-spec attribute sets:
  - `Credentials`: approved minimum column shape is `KdfMode`, `KdfVersion`, `PasswordHash`, `Salt`, `SaltBitLength`, `CreatedAtUtc`, `UpdatedAtUtc`, `PasswordChangedAtUtc` plus identifiers/foreign keys. KDF parameter-tuning payload/iteration metadata beyond this approved minimum is Sprint 5+ later-numbered `ALTER` migration scope.
  - `Sessions`: absolute/idle timeout-relevant timestamps per `ADR-0031`, no refresh tokens (P1 has no session refresh).
  - `AuditLog`: append-only with 365-day floor per `AC-052`/`NFR-011`; audit details continue through `AuditDetailsSanitizer`, diagnostics redaction remains the Sprint 3 sensitive assignment/key redaction boundary, and any broader raw path/stack redaction expectation must be verified explicitly rather than assumed.
  - `ConsumedRecoveryNonces`: per `ADR-0039` single-use 24h artifact nonces.
  - `CustomerDeploymentContext`: per `ADR-0052` minimum customer/deployment identity fields plus `default` tenant primitive.
  - `HostConfiguration`: persisted host config keys that escape `appsettings.json` (e.g., one-time bootstrap state).
  - `StorageMetadata`: schema version + provider-mode + migration history.
  - `DeviceIdentity`: protected `DobbyStableDeviceId` material plus `algorithmVersion`, `protectionVersion`, `createdAt` and redacted support-prefix metadata per the storage spec; if the approved Phase A table instead chooses an external protected-blob/reference shape, that divergence must be recorded as a human-approved decision.
  - `Permissions`: resolved by decision package 2026-05-14 as grant-row storage. Row presence represents grant, row absence represents no grant/default deny, revocation is row deletion, and there is no `GrantState` column or explicit deny storage.
- Pre-login startup may only touch these tables. Plugin runtime, plugin discovery, license import, settings UI hydration, shell composition queries are forbidden during pre-login per `ADR-0041`.

**Task group 4.5 — Post-login migration infrastructure**.
- Provide the migration framework (`IHostPersistenceMigrator`), `StorageMetadata` migration ledger, `NNNN_descriptive_step_name` numbered migration ID convention, and post-login migration ordering invariant.
- Post-login migrations (`0002_*` and later) only run after successful authentication / the authenticated-session seam. They must never run during pre-login bootstrap and must never run merely because `LoginVisible` was emitted.
- Sprint 4 does NOT materialize license state, plugin install/cache state, host preferences, per-user shell UI state/favorites, recovery state, audit-retention fields, storage metadata extensions, or plugin shared-domain/user-scoped storage compartments. Consumer sprints 5/6/7/8/9 materialize their own post-login schemas as numbered migrations through evidence-layer schema reviews owned by their respective sprints.

**Task group 4.6 — SQLite connection guardrails (`AC-038`)**.
- Every connection opened from the host adapter enables `foreign_keys=ON` and sets a bounded `busy_timeout` (default `Dobby:Storage:BusyTimeoutMilliseconds=5000`, validated by `StorageOptions`).
- WAL/journal/temp side-files inherit DB-at-rest protection (provider responsibility — verified by integration test inspecting filesystem after a forced WAL checkpoint).
- Connection pool/factory enforces guardrails uniformly; guardrail-less connection paths fail release verification.

**Task group 4.7 — Transactional integrity and crash recovery (`AC-042`)**.
- All multi-statement writes go through the host-owned `IHostPersistenceTransactionScope`; rollback on failure; no auto-commit of partial state.
- Crash-recovery smoke: kill the host between begin/commit, restart, verify partial-write does not survive (atomic) and DB opens without unrecoverable corruption (durable). Sprint 4 runs this against a local publish output or equivalent isolated smoke artifact as mechanism verification only; Sprint 11 owns final `AC-041` release-candidate publish/installer evidence.

**Task group 4.8 — Audit log durable backing**.
- Replace the consumer-facing wiring of `InMemoryAuditTrail` with the durable `IAuditTrail` backed by the encrypted `AuditLog` table. `InMemoryAuditTrail` stays as a unit-test seam.
- 365-day floor enforced by retention sweeper; sweep does not run on the login-first first-paint path (`NFR-021`/`AC-052`).
- Audit event shape, sanitization (`AuditDetailsSanitizer`) and 37-entry `AuditEventName` registry are not changed; durable backing only.

**Task group 4.9 — Runtime folder protected-material probe**.
- Replace `RuntimeFolderPreparer`'s injected `protectedMaterialPrerequisiteAvailable` stub (Sprint 3) with a real probe that allows a clean first run to generate and persist Dobby application-level entropy plus `DobbyStableDeviceId`, then returns `true` only when both are available/loadable. The approved initialized-state marker is metadata-only `protected-material.initialized.json` under the protected-material folder; it contains no secret material and records only `protectionVersion`, `algorithmVersion`, and `createdAtUtc`. If protected material exists without the marker, or the marker exists without required material, the probe fails closed. After the approved initialized-state marker exists, missing or malformed protected material fails closed and must not silently reissue identity/key-adjacent material.

**Task group 4.10 — Configuration alignment**.
- Transition `Dobby:Storage:ProviderMode` default from `ProviderNeutralSkeleton` to the human-approved production provider mode (e.g., approved enum value once `ADR-0054` materialization decision is recorded). `StorageOptions` validator updates to reject `ProviderNeutralSkeleton` in production builds under release-candidate gate (`AC-042`).
- `appsettings.json` `Dobby:Storage:ProviderMode` value updated; `appsettings.Test.json` (if added) may retain `ProviderNeutralSkeleton` only for unit-test seams that do not load native cipher binaries.

**Task group 4.11 — Architecture-map edges**.
- Add `Dobby.Audit -> Dobby.Infrastructure` `ProjectReference` (already allowed by Cədvəl 2 Row `Dobby.Audit`; currently absent → architecture test will flip from "allowed but not present" to present) only after the approved Phase B D-01 `PrivateAssets="all"`-based project-graph isolation strategy is recorded and applied to the temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary `ProjectReference`.
- Add `Dobby.Security -> Dobby.Infrastructure` `ProjectReference` (already allowed by Cədvəl 2 Row `Dobby.Security`; currently absent) under the same approved transitive dependency guardrail. Sprint 4 must verify the guardrail with two complementary tests before adding consumer edges: a metadata-aware transitive dependency check that treats `PrivateAssets="all"` project references as non-propagating edges, and a build-output smoke that locates the repository root from `AppContext.BaseDirectory` by walking up to `Dobby.sln`, then scans `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for absence of `Dobby.Application.dll`. If either check cannot prove isolation, Sprint 4 stops for a human architecture-map decision before adding the consumer edges.
- Conditionally add `Dobby.Infrastructure -> Dobby.Contracts` `ProjectReference` only if storage abstractions reference `Dobby.Contracts` types (allowed by Cədvəl 2 Row `Dobby.Infrastructure`; currently absent — Sprint 1 recorded the absence as deferred drift, not required fix; Sprint 4 may close this drift when the approved table/code shape requires it).
- `Dobby.Configuration -> Dobby.Infrastructure` edge is NOT in Cədvəl 2; if needed, Sprint 4 must request an ADR/architecture-map amendment first, otherwise place protected-storage adapter ownership on `Dobby.Infrastructure` and consume it from `Dobby.Host` composition root.
- `Dobby.PluginFramework -> Dobby.Infrastructure` edge stays absent (plugin loader must remain unaware of host persistence).

**Dependencies on prior sprints**:
- Sprint 1 — ErrorCode registry (Sprint 4 keeps the 17-entry Core registry unchanged per `storage-materialization.md` and uses local `PersistenceError`); `Directory.Packages.props` central versions discipline; `ArchitectureDependencyBoundaryTests`/`PackageGovernanceTests` baseline; `SPRINT1-INFRA-APP-PRINT-BOUNDARY` Sprint 9 sunset note (Sprint 4 must NOT touch `Dobby.Infrastructure -> Dobby.Application` print edge).
- Sprint 2 — `Result<T>`/`Result` Core surface; plugin storage contracts (`ISharedDomainStorage`/`IUserScopedStorage`) unchanged; `ContractsCoreSurfaceTests` allowed list — Sprint 4 must not widen.
- Sprint 3 — `RuntimeFolderLayout`/`RuntimeFolderPreparer` (replace stub); `IAuditTrail`/`AuditEvent`/`AuditDetailsSanitizer`/37-entry `AuditEventName` registry (Sprint 4 backs durably, does not extend); 10 `*Options` POCOs + `DobbyOptionsValidator` (Sprint 4 updates `StorageOptions.ProviderMode` validation only); `HostBootstrapper`/`HostConfigurationSources` (Sprint 4 adds pre-login DB open + provider-enabled probe + protected-material loading after `ConfigLoaded` and before `LoginVisible`).

**Linked ACs**:
- `AC-037` `release-blocker` — Production DB-at-rest provider active for host and plugin persistence; unprotected production DB / WAL/journal/temp / provider-disabled configuration fails release verification. Plugin sees no provider/key/backing-path detail.
- `AC-038` `release-blocker` — `foreign_keys=ON` per connection, bounded `busy_timeout`, WAL/journal/temp inside protection boundary. Guardrail-less or missing-pragma configuration fails release verification.
- `AC-042` `release-blocker` — Release-candidate native storage proof verifies provider packaged, loaded, active; fails when provider-disabled, provider-bypass, unprotected WAL/journal/temp or missing connection guardrail detected. (Final smoke owned by Sprint 11; Sprint 4 establishes the deterministic mechanism.)
- `AC-052` `release-blocker` — Audit log durable persistence keeps the 365-day floor; Sprint 4 contributes durable backing.
- `AC-051` is intentionally not claimed by Sprint 4. KDF runtime selection/floors/salt evidence and the baseline-latency artifact are Sprint 5 + Sprint 11 territory; Sprint 4's protected-storage/DEK-wrapping evidence is covered by `ADR-0055` and the Sprint 4 storage ACs above.

**Verification strategy**: unit tests for `StorageOptions.ProviderMode` validator transition, DPAPI scope-mapping wrappers, retention sweeper boundaries; integration tests for provider-enabled probe, encryption proof, pragma assertions, WAL/journal/temp protection, transactional rollback, durable `IAuditTrail` round-trip, pre-login closed table set discovery, plugin no-`Dobby.Infrastructure` static scan, no-plaintext-secret content scan over committed files and dev-time runtime data root; smoke test for crash recovery; architecture test updates for new dependency edges.

**Risks / blockers** (master plan): "This sprint is blocked until documented human decisions are available." Decision package 2026-05-14 closes the prior §13.HD-1 and §13.HD-2 schema questions by aligning `Permissions` with grant-only storage and amending the master plan to post-login migration infrastructure. Implementation proceeds only after the no-code preflight verifies:
- `toolchain-pin-gate.md` still approves the selected Sprint 4 storage package/native rows and still resolves `TOOLCHAIN-PIN-APPROVAL-MISSING` / `TOOLCHAIN-PIN-REPRESENTATION-MISSING`.
- `storage-materialization.md` still approves the provider-neutral primitive surface, local `PersistenceError` surface, unchanged Core `ErrorCode` stance, pre-login table/migration authority, grant-row `Permissions` shape, reserved post-login migration ID, DB-at-rest cipher/KDF profile, native supply-chain policy/evidence, provider package or asset decision, and provider-proof assertion list.
- Both ledgers remain unchanged within the freeze window; otherwise Phase A reopens for a separate approval pass before implementation continues.

## 4. Prior Sprint Continuity Check

**Sprint 1 outputs (Toolchain and Architecture Baseline)** — verified present and unchanged:

- 18 production projects (`Dobby.Core`, `Dobby.Contracts`, `Dobby.SDK`, `Dobby.PluginFramework`, `Dobby.Security`, `Dobby.Licensing`, `Dobby.Configuration`, `Dobby.Localization`, `Dobby.Navigation`, `Dobby.Messaging`, `Dobby.Application`, `Dobby.Diagnostics`, `Dobby.Audit`, `Dobby.UI`, `Dobby.Shell`, `Dobby.Infrastructure`, `Dobby.Host`, `Dobby.Admin`) + 3 tests + 1 sample plugin in `Dobby.sln` (`@c:\Users\Evaks\Desktop\dobby\Dobby.sln`). Sprint 4 adds zero new projects.
- `Directory.Packages.props` central package management = `true`; `global.json` SDK pinned `10.0.202`/`latestPatch`; `Directory.Build.props` `Nullable=enable`, `LangVersion=14.0`, `TreatWarningsAsErrors=true`. Sprint 4 will add package versions only after the Phase A preflight verifies the approved `toolchain-pin-gate.md` rows remain unchanged.
- `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` enforces Cədvəl 2 + Sprint 1 temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary allowance; Sprint 4 must not weaken this edge — Sprint 9 owns sunset.
- `PackageGovernanceTests` Tier 0 BCL exclusion (`System.Text.Json`, `System.IO.Compression`, `System.IO.Compression.ZipFile`, `System.Threading.Channels`) + Tier R rejected list (`SQLitePCLRaw.bundle_*` + commercial-license/`Moq`/`MediatR`/`AutoMapper`/`EPPlus`/`FluentAssertions`/`Microsoft.Extensions.Resilience`-family) + test-only-package containment. Sprint 4 confirms additions are NOT in Tier R; if a candidate cipher provider package is in Tier R, Sprint 4 escalates back to architecture rather than opt-in.
- `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` UTF-8/no-BOM scan over `docs/`, `src/`, `tests/`, `samples/`, `artifacts/`. Sprint 4 new files must be UTF-8 no-BOM.
- `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs` validates `artifacts/release-evidence/skeleton-local/` structure. Sprint 4 does NOT regenerate Sprint 11 final release evidence.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` now records Sprint 4 Phase A package/native asset approval and resolves `TOOLCHAIN-PIN-APPROVAL-MISSING` + `TOOLCHAIN-PIN-REPRESENTATION-MISSING` for the selected storage rows; `architecture-baseline.md` records `SPRINT1-INFRA-APP-PRINT-BOUNDARY` Sprint 9 sunset. Sprint 4 inherits and respects both.

**Sprint 2 outputs (Core Contracts and Plugin Framework Boundary)** — verified present and unchanged:

- `Dobby.Core` primitives: `Result`/`Result<T>` + `ErrorCode` 17-entry registry + `CorrelationId`/`ICorrelationIdGenerator`/`ICorrelationContextAccessor` + `HostVersion`/`IHostVersionProvider` + `TimeProvider` reference. Sprint 4 must not widen the ADR-0001 Core public surface (enforced by `ContractsCoreSurfaceTests`). New storage error codes only enter the registry under `ADR-0040` §3.1 human-approved table.
- `Dobby.Contracts` plugin-facing surface: ordering/messaging/license/policy/plugin-framework/plugin-page/plugin-storage contracts unchanged. Sprint 4 adds nothing here.
- `Dobby.SDK` allowed surface: `PluginBase` + storage helpers; ADR-0021 SDK-only marker. Unchanged.
- `Dobby.PluginFramework` manifest validation + dependency sort + plugin exception boundary. Unchanged.
- `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests` enforces Contracts → only 8 allowed Core types, Contracts → no WPF/UI assembly reference. Sprint 4 maintains. `PluginAndSdkBoundaryTests` enforces plugin → only Contracts/SDK; Sprint 4 confirms `Dobby.Sample.Plugin` and any future plugin asset never reference `Dobby.Infrastructure`.
- Sprint 2 Reconfirmation 2026-05-12 path (a) renewed in `architecture-baseline.md`. Sprint 4 will require its own Reconfirmation entry when implementation completes.

**Sprint 3 outputs (Configuration and Diagnostics Baseline)** — verified present and unchanged on the in-memory side; Sprint 4 backs durably:

- `Dobby.Configuration` 10 option POCOs (one public type per file): `RuntimeOptions`, `StorageOptions`, `AuthOptions`, `SessionOptions`, `DiagnosticsOptions`, `AuditOptions`, `LocalizationOptions`, `PluginsOptions`, `LicensingOptions`, `BranchSharedOptions`, plus `DobbyOptions` aggregate. `DobbyOptionsValidator` enforces ADR-0027 §6a per-key floors. Sprint 4 updates only `StorageOptions.ProviderMode` enum range + production-mode rejection of `ProviderNeutralSkeleton`.
- `src/Dobby.Host/HostConfigurationSources.cs` source order: packaged `appsettings.json` → runtime override under `%LocalAppData%/Dobby/config/` → environment variables (`DOBBY__SECTION__KEY` deterministic mapping) → user secrets (dev only). Sprint 4 does not change source order.
- `src/Dobby.Host/appsettings.json` ADR-0027 §6a canonical schema + top-level `Serilog:MinimumLevel=Information`. Sprint 4 updates `Dobby:Storage:ProviderMode` only.
- `src/Dobby.Host/Startup/HostBootstrapper.cs` emits `HostBuilt`/`ConfigLoaded`/`LoginVisible` milestones; `RuntimeFolderPreparer` produces the 8 folder layout (`data`, `config`, `logs`, `plugins`, `licenses`, `recovery`, `backup`, `temp`) with sentinel + fail-closed protected-material gate. Sprint 4 inserts a real protected-material probe + pre-login DB open between `ConfigLoaded` and `LoginVisible`.
- `Dobby.Audit`: `IAuditTrail`/`InMemoryAuditTrail`/`AuditEvent`/`AuditEventName` (37 names)/`AuditResult`/`AuditResultExtensions`/`AuditDetailsSanitizer`. Sprint 4 adds a durable backing implementation; in-memory implementation stays for unit tests.
- `Dobby.Diagnostics`: `DiagnosticRedactor` currently redacts sensitive assignment/key vocabulary to `<redacted>` (including password/token/private-key/signature/session/remember-me/recovery/license-style fields), but does not currently implement separate `<redacted:path>`/`<redacted:guid>`/`<redacted:base64>`/`<redacted:email>`/`<redacted:long>` replacement families; `DiagnosticsExportPackageBuilder`, `LocalizationMissingKeyEvent` `localization.missing-key`, `StartupMilestone`/`StartupMilestoneNames`. Sprint 4 does not expand this vocabulary unless a documented follow-up explicitly does so.
- Tests baseline at Sprint 3 exit: ~145-175 tests across unit + integration + smoke (Audit 4, Configuration 11, Diagnostics 4, plus Sprint 1+2 baselines; integration adds Architecture 6, Configuration 4, Diagnostics 2, Runtime 2, Startup 1; smoke `ShellSmokeTests`). Sprint 4 will add storage-area unit tests + provider/encryption integration tests + crash-recovery smoke.

**Deferred drift carried into Sprint 4**:

- `SPRINT1-INFRA-APP-PRINT-BOUNDARY` (`Dobby.Infrastructure -> Dobby.Application` print-target carry-over) — Sprint 9 sunset; Sprint 4 leaves untouched.
- `Dobby.SDK -> Dobby.Localization` missing edge (ADR-0021 allowed) — not Sprint 4 work; documentation-only entry in `architecture-baseline.md`.
- `Dobby.Configuration -> Dobby.Contracts` missing edge (Cədvəl 2 allowed) — not Sprint 4 work unless storage primitive abstractions placed in `Dobby.Configuration` explicitly require it; default placement is `Dobby.Infrastructure`.
- `Dobby.Infrastructure -> Dobby.Contracts` missing edge (Cədvəl 2 allowed) — Sprint 4 may close this drift when storage primitives reference `Dobby.Contracts` types (e.g., `Dobby.Contracts.PluginStorage.*`).

**Approved Phase A decisions carried into Sprint 4**:

- `TOOLCHAIN-PIN-APPROVAL-MISSING` and `TOOLCHAIN-PIN-REPRESENTATION-MISSING` are resolved for Phase A in `toolchain-pin-gate.md`. Sprint 4 may add `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, the approved `SQLite3MultipleCiphers v2.3.4` native asset, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, and `Dapper` `2.1.72` only after the preflight confirms the ledger is unchanged and the table/JSON consistency test is added.
- `storage-materialization.md` approves the storage primitive surface under `Dobby.Infrastructure.Persistence`, keeps the Core 17-entry `ErrorCode` registry unchanged, approves the `PersistenceError` local sub-code set, records the DB-at-rest profile and provider proof, and allows Phase B to start. Sprint 4 implementation consumes this file; it does not recreate the decision ledger.

**Master plan dependency on Sprint 4 outputs** (forward visibility):

- Sprint 5 — auth/session/recovery/Owner bootstrap consumes `Users`/`Credentials`/`Sessions`/`RememberMeSlot`/`ConsumedRecoveryNonces` tables + durable `IAuditTrail`.
- Sprint 6 — permissions/users/settings consumes `Permissions`/`HostConfiguration` tables.
- Sprint 8 — plugin install/cache consumes post-login plugin install/cache tables; plugin contracts unchanged.
- Sprint 9 — license state consumes license-state tables; print boundary sunset.
- Sprint 11 — release evidence regenerates `AC-037`/`AC-038`/`AC-042` proof against publish artifact + `AC-014`/`AC-051` baseline-latency artifact.

## 5. Current Codebase State

**`Dobby.Infrastructure`** (target of most Sprint 4 work):
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Infrastructure\Dobby.Infrastructure.csproj` — TFM `net10.0`, references `Dobby.Application` (print-boundary edge, Sprint 9 sunset) + `Dobby.Core`; `System.Security.Cryptography.ProtectedData` `PackageReference` already present (Tier 1, version `10.0.7` in `Directory.Packages.props`). NO `Dobby.Contracts` reference.
- Platform note: because current `Dobby.Infrastructure` is `net10.0`, any DPAPI/Win32/native-provider concrete adapter added by Sprint 4 must first follow the approved `net10.0;net10.0-windows` multi-target/conditional Windows adapter-slice strategy inside the existing project; do not place Windows-only code in the plain `net10.0` compile surface, and do not add a separate platform project in Sprint 4.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Infrastructure\Printing\WindowsPrintTargetClassifier.cs` — Sprint 1 print-boundary carry-over (only file). Sprint 4 leaves untouched.
- No `Persistence/`, no `Storage/`, no `ProtectedData/`, no DPAPI wrappers, no SQLite adapter, no `IHostPersistenceStore`-style types yet.

**`Dobby.Audit`** (consumer of durable backing):
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Audit\IAuditTrail.cs` (interface), `AuditEvent.cs` (record), `AuditEventName.cs` (37 typed names), `AuditResult.cs` (Success/Failed/Denied), `AuditResultExtensions.cs` (`ToPayloadValue`), `AuditDetailsSanitizer.cs`, `InMemoryAuditTrail.cs` (`Append`/`Snapshot`).
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Audit\Dobby.Audit.csproj` — TFM `net10.0`, references `Dobby.Core` only. Sprint 4 will add `Dobby.Infrastructure` `ProjectReference`.

**`Dobby.Configuration`** (option floors source):
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Configuration\StorageOptions.cs` — current `ProviderMode` is `string` accepting `ProviderNeutralSkeleton`; `RequireDataAtRestProtection` (bool, must be true), `BusyTimeoutMilliseconds` (`100..30_000`).
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Configuration\DobbyOptionsValidator.cs` — enforces ADR-0027 §6a floors including the `ProviderNeutralSkeleton` allowance until materialization pass.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Configuration\Dobby.Configuration.csproj` — TFM `net10.0`, references `Dobby.Core` only. Sprint 4 leaves project references alone.

**`Dobby.Security`** (consumer of protected-storage wrappers + storage primitives):
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Security\Authentication\Pbkdf2PasswordHasher.cs` — `KdfPolicy` record (algorithm/iterations/salt-bytes/hash-bytes), `KdfPolicy.SkeletonPbkdf2Fallback` (PBKDF2-HMAC-SHA256, 600_000 iter, 16-byte salt, 32-byte hash), `PasswordHash` record, `Pbkdf2PasswordHasher` (`Hash`/`Verify`). Sprint 5 owns KDF runtime wiring; Sprint 4 leaves the policy schema untouched but the `Credentials` table backs it.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Security\Authorization\OperationKey.cs` — unchanged.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Security\Dobby.Security.csproj` — TFM `net10.0`, references `Dobby.Core`+`Dobby.Contracts`. Sprint 4 will add `Dobby.Infrastructure` `ProjectReference`.

**`Dobby.Host`** (composition root):
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\Startup\HostBootstrapper.cs` — `Bootstrap(HostBootstrapOptions)` emits `HostBuilt` → loads config via `HostConfigurationSources` → validates via `DobbyOptionsValidator` → emits `ConfigLoaded` → calls `RuntimeFolderPreparer.Prepare` → opens nothing yet → emits `LoginVisible`. Sprint 4 inserts persistence open + protected-material probe + provider-enabled probe between `ConfigLoaded` and `LoginVisible`.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\RuntimeFolderPreparer.cs` — constructor overloads include parameterless default (uses `() => true`), `Func<bool>`, and `Func<bool>` + logger. The nullable optional bootstrap seam lives on `HostBootstrapOptions.ProtectedMaterialPrerequisiteAvailable`; Sprint 4 wires that seam/composition path to a real probe from `Dobby.Infrastructure`.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\RuntimeFolderLayout.cs` — 8 folder names. Unchanged.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\HostConfigurationSources.cs` — source order locked. Unchanged.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\LocalDiagnosticsConfigurator.cs` — Serilog file sink + `RedactingTextFormatter` (`DiagnosticRedactor`). Unchanged.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\appsettings.json` — ADR-0027 §6a canonical schema; `Dobby:Storage:ProviderMode=ProviderNeutralSkeleton` currently. Sprint 4 transitions to approved value.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\Trust\README.md` — Sprint 4 leaves untouched (Sprint 8/9 trust scope).

**`Dobby.Diagnostics`** (redaction floor):
- `DiagnosticRedactor` with current sensitive assignment/key redaction to `<redacted>` only; Sprint 4 reuses unchanged unless a documented follow-up explicitly expands that boundary.

**Test baseline at Sprint 4 entry**:
- Unit tests (`tests/Dobby.UnitTests`): Audit 4, Configuration 11, Diagnostics 4, Security 1, Contracts/Core/Licensing/Messaging/Navigation/PluginFramework/SDK/Shell (Sprint 1-2 baselines).
- Integration tests (`tests/Dobby.IntegrationTests`): Architecture 6 (`ArchitectureDependencyBoundaryTests`, `ContractsCoreSurfaceTests`, `EncodingBoundaryTests`, `PackageGovernanceTests`, `PluginAndSdkBoundaryTests`, `ReleaseEvidenceTests`), Configuration 4 (`DobbyOptionsBindingTests`, `ConfigurationSourceOrderTests`, `RetentionFloorIntegrationTests`, `NoTelemetryDefaultTests`), Diagnostics 2 (`LocalDiagnosticsSinkTests`, `DiagnosticsExportBoundaryTests`), Runtime 2 (`RuntimeFolderLayoutTests`, `RuntimeFolderRecoveryTests`), Startup 1 (`StartupBootstrapWiringTests`), `ProjectBoundaryTests`.
- Smoke (`tests/Dobby.SmokeTests`): `ShellSmokeTests` (1 test).
- All Sprint 1-3 tests pass at Sprint 4 entry (assumption to verify before Sprint 4 starts).

**Evidence baseline**:
- `@c:\Users\Evaks\Desktop\dobby\artifacts\release-evidence\skeleton-local\architecture-baseline.md` — Sprint 1 baseline + Sprint 2 2026-05-12 Reconfirmation (path (a) renewed). Sprint 3 Reconfirmation is a separate planned entry; Sprint 4 will require its own.
- `@c:\Users\Evaks\Desktop\dobby\artifacts\release-evidence\skeleton-local\decision-log.md` — Phase A 2026-05-14 approval anchor plus Phase B D-01..D-12 approval anchor by `n4hkro`.
- `@c:\Users\Evaks\Desktop\dobby\artifacts\release-evidence\skeleton-local\toolchain-pin-gate.md` — Sprint 4 storage package/native asset approval `APPROVED`; `TOOLCHAIN-PIN-APPROVAL-MISSING` + `TOOLCHAIN-PIN-REPRESENTATION-MISSING` resolved for Phase A.
- `@c:\Users\Evaks\Desktop\dobby\artifacts\release-evidence\skeleton-local\storage-materialization.md` — Sprint 4 Phase A storage materialization decisions `APPROVED`; Phase B may start after preflight.
- `@c:\Users\Evaks\Desktop\dobby\artifacts\release-evidence\skeleton-local\auth-kdf-selection.md` — PBKDF2 selected with skeleton-local 97.99 ms; Sprint 11 regenerates against publish artifact for `AC-014`.

## 6. Documentation and ADR Alignment

**Documents fully aligned for Sprint 4 implementation**:
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-001 pre-login allowed work (credential store + audit storage open) maps to Task group 4.4; FR-007/FR-016/FR-017 plugin install/license rely on durable cache (Task group 4.5); FR-022 throttle floor relies on `Sessions`/auth state (Task group 4.4).
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — NFR-007 local data protection drives Task groups 4.2 + 4.3; NFR-020 privacy default-local stays unchanged; NFR-011 audit append-only drives Task group 4.8; NFR-021 background work boundary drives crash-recovery + retention sweep off the UI thread.
- `docs/07_ACCEPTANCE_CRITERIA.md` — AC-037/AC-038/AC-042/AC-052 closed by Sprint 4 + Sprint 11. AC-051 KDF runtime is Sprint 5.
- `docs/03_TECH_STACK_AND_VERSIONS.md` Storage row (SQLite + Dapper, data-at-rest protection) + Secure Storage row (DPAPI + Dobby local storage protection layer) + Section 8 Pin Tier table scope Sprint 4 package additions under approved Phase A pin evidence. `Dapper` is selected for Sprint 4 by `storage-materialization.md` and `toolchain-pin-gate.md`.
- `docs/02_ARCHITECTURE_MAP.md` Cədvəl 1 Rows 87-89 (`Dobby.Infrastructure` host-internal Win32/OS adapter, Hardware Identity, Host Persistence / Consumed Nonces) + Cədvəl 2 edges (`Dobby.Audit -> Dobby.Infrastructure`, `Dobby.Security -> Dobby.Infrastructure`) + Cədvəl 3 conventions (`Dobby.Infrastructure Boundary`, `Secure Storage Boundary`, `Local Data Protection`) authoritative for Sprint 4 placement decisions.

**ADRs authoritative for Sprint 4**:
- `ADR-0030` Host persistence ownership + 3-bucket taxonomy + plugin storage compartments — no contract surface change.
- `ADR-0031` Session state shape (P1 no refresh) — Sprint 4 carries shape only.
- `ADR-0038` Local-first authentication + DPAPI scope for session/remember-me material — Sprint 4 implements storage; Sprint 5 implements runtime.
- `ADR-0039` Owner password recovery `ConsumedRecoveryNonces` persistence under `Dobby.Infrastructure` ownership.
- `ADR-0040` Internal host adapters boundary + §3.1 anti-name-invention stop condition.
- `ADR-0041` Login-first startup policy — pre-login closed work set.
- `ADR-0051` License lifecycle v2 state shape — Sprint 4 schema; Sprint 9 runtime.
- `ADR-0052` Tenant/customer context v2 — `CustomerDeploymentContext` minimum fields + `default` tenant primitive.
- `ADR-0054` DB-at-rest protection provider — Option A SQLite full-DB encryption, P1 Persistence Materialization Pass.
- `ADR-0055` Protected storage scope and key wrapping — per-secret scope mapping, application-level entropy, `DobbyStableDeviceId` separation.
- `ADR-0056`/`ADR-0057` Branch Shared deployment modes — runtime docs-only; Sprint 4 does not implement BranchShared runtime.

**Specs authoritative for Sprint 4**:
- `docs/specs/storage-and-data-protection.md` — primary spec (§1 protection, §2 key source boundary, §3 taxonomy, §3.1 runtime layout with 11 pre-login tables, §4 plugin compartments, §5 implementation notes, §5a P1 Persistence Materialization Pass, §6 verification minimums).
- `docs/specs/audit-and-diagnostics.md` — durable audit + 365-day floor + retention sweeper.
- `docs/specs/session-lifecycle.md` — Sprint 5 consumer; Sprint 4 carries schema only.
- `docs/specs/user-management.md` — Sprint 5/6 consumers.

**Phase A approved decision evidence consumed before Phase B** (pre-implementation gate artifacts):
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` — contains approved Phase A closure evidence for `TOOLCHAIN-PIN-APPROVAL-MISSING`/`TOOLCHAIN-PIN-REPRESENTATION-MISSING`. Before any package-bearing file is changed, Sprint 4 must verify the ledger remains `APPROVED`, the selected rows remain unchanged, the freeze window has not been broken, and the required table/JSON consistency test is added as the first package-governance task.
- `artifacts/release-evidence/skeleton-local/storage-materialization.md` — already exists and captures the `ADR-0040` §3.1 / storage spec §5a approved table plus the `ADR-0054` materialization decision. Required approved content is present: primitive surface, table/column/constraint/index ownership, migration ledger and migration ID convention, exact DB-at-rest cipher/KDF profile, `DeviceIdentity` storage shape, native supply-chain/provider package or asset stance, provider-proof assertion list, unchanged Core `ErrorCode` decision, and ADR/spec cross-reference invariant.
- `artifacts/release-evidence/skeleton-local/decision-log.md` — contains approved Phase B D-01..D-12 implementation-detail decisions that do not amend the frozen Phase A storage/toolchain ledgers. In particular, D-01 approves the `PrivateAssets="all"` project-graph isolation strategy for the temporary print-boundary edge, and D-11 records the already-applied placeholder cleanup as resolved.

**Documents requiring updates after Sprint 4 implementation**:
- `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 may need to record any newly-added `ProjectReference` edges (`Dobby.Audit -> Dobby.Infrastructure`, `Dobby.Security -> Dobby.Infrastructure`, and conditionally `Dobby.Infrastructure -> Dobby.Contracts` only if storage primitives consume `Dobby.Contracts` types). Allowed edges are already present in the table — Sprint 4 only flips status from "allowed but not present" to "present" for edges actually added.
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md` — unchanged for Sprint 4 because `storage-materialization.md` explicitly keeps the 17-entry Core `ErrorCode` registry unchanged and uses local `PersistenceError` sub-codes.
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` — Sprint 4 Reconfirmation entry recording new test count, new dependency edges, deferred drift updates.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` — final Sprint 4 implementation note appended after package pins are applied under the Phase A closure evidence.
- `artifacts/release-evidence/skeleton-local/storage-materialization.md` — final Sprint 4 implementation note appended with implemented provider/probe/schema evidence; Phase A decision content is not created at the end of implementation.

**Documents NOT affected by Sprint 4**:
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md` — unchanged because Phase A approved no Core storage `ErrorCode` additions; the `Result<T>` contract remains unchanged.
- `docs/08_CODING_STANDARDS.md` — banned segments, one-public-type-per-file, UTF-8 no-BOM, namespace == folder. Sprint 4 follows.
- `docs/09_BUILD_RUN_AND_RELEASE.md` — repository layout + evidence-split tests unchanged.

## 7. Files to Create

**General rule**: approved names and schema decisions come from `artifacts/release-evidence/skeleton-local/storage-materialization.md` plus the master-plan amendment in `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 `Post-login Migration Infrastructure`. The primitive names listed without `« »` below are approved Sprint 4 decisions. Any remaining `«placeholder»` names show intended responsibilities where exact implementation names or method signatures remain implementation-phase details constrained by the approved ledger, ADR-0040, and the storage spec. Implementers must not invent names or sub-codes outside that ledger.

**Storage primitive abstractions** (approved placement: `src/Dobby.Infrastructure/Persistence/`):
- `IHostPersistenceConnectionFactory.cs` — approved provider-neutral connection factory under `Dobby.Infrastructure.Persistence`; opens host-controlled encrypted SQLite connections through the approved provider path and applies connection guardrails.
- `IHostPersistenceTransactionScope.cs` — approved transaction primitive under `Dobby.Infrastructure.Persistence`; owns explicit connection/transaction/cancellation scope for multi-step writes and rollback semantics.
- `IHostPersistenceMigrator.cs` — approved schema migrator under `Dobby.Infrastructure.Persistence`; applies approved migration IDs idempotently and records migration checksums in `StorageMetadata`. The surface distinguishes pre-login and post-login application paths, e.g. `ApplyPreLoginMigrationsAsync(...)` for Sprint 4 bootstrap callers and a neutral `ApplyPostLoginMigrationsAsync(...)` API for Sprint 5+ host composition after the authenticated-session seam. The post-login API must not take `Dobby.Security` types, and Sprint 4 does not call it in production.
- `IHostPersistenceCommandExecutor.cs` — approved parameterized command/query primitive under `Dobby.Infrastructure.Persistence`; not an owner-project repository or plugin-visible contract.
- `PersistenceError.cs` — approved local error type under `Dobby.Infrastructure.Persistence` with closed sub-codes: `ConstraintViolation`, `TransactionConflict`, `SchemaMismatch`, `MigrationFailed`, `ProviderUnavailable`, `IoFailed`, `SerializationFailed`, `QueryFailed`, `Unknown`.

**Core/ErrorCode path**:
- `storage-materialization.md` explicitly keeps the Core 17-entry `ErrorCode` registry unchanged. Do not modify `src/Dobby.Core/Result.cs`, `docs/04_RESULT_VS_EXCEPTION_POLICY.md`, or the hard-coded `ArchitectureDependencyBoundaryTests` ErrorCode baseline for storage error additions in Sprint 4.
- Storage-specific failures remain in the approved local `PersistenceError` / approved `Result<T>` payload shapes. If implementation discovers a need for new Core `ErrorCode` values, stop and reopen Phase A instead of adding codes speculatively.

**Encrypted SQLite provider adapter** (placement: `src/Dobby.Infrastructure/Persistence/Sqlite/`):
- `«EncryptedSqliteConnectionFactory».cs` — wires `Microsoft.Data.Sqlite` + native cipher + provider key from DPAPI scope; applies pragmas on every open; verifies provider-enabled probe.
- `«SqliteProviderEnabledProbe».cs` — startup probe verifying native cipher binary loaded; fails closed on provider-disabled/bypass.
- `«SqliteWalProtectionVerifier».cs` — inspects WAL/journal/temp side-files for protection-boundary residence (integration-test target).

**DPAPI scope wrappers** (placement: platform-neutral surface under `src/Dobby.Infrastructure/ProtectedStorage/`; concrete DPAPI/Win32 implementations under the approved Windows-only adapter slice):
- Placement split: interfaces/contracts and consumer-facing types stay platform-neutral; concrete `ProtectedData`/Win32 code lives behind the approved `net10.0;net10.0-windows` multi-target/conditional Windows adapter slice inside the existing `Dobby.Infrastructure` project. No Windows-only implementation type leaks to `Dobby.Audit` or `Dobby.Security`.
- `«IDpapiScopeProtector».cs` — abstracts the scope-mapped DPAPI calls (Protect/Unprotect with `DataProtectionScope` + per-scope additional entropy).
- `«DpapiLocalMachineProtector».cs` — `DataProtectionScope.LocalMachine` adapter consuming Dobby application-level entropy.
- `«DpapiCurrentUserProtector».cs` — `DataProtectionScope.CurrentUser` adapter consuming per-scope user/slot/session entropy.
- `«DobbyApplicationLevelEntropyStore».cs` — generates/loads/persists the Dobby application-level entropy blob under `%LocalAppData%/Dobby/data/`; uses `System.Security.Cryptography.RandomNumberGenerator` for first-run material; never logged/exported.
- `«DobbyStableDeviceIdStore».cs` — generates/loads/persists `DobbyStableDeviceId` as a separate host-installation-bound random identity; NEVER used as key source.
- `«ProtectedMaterialAvailabilityProbe».cs` — on clean first run, coordinates first-run generation/persistence of both entropy + stable device id material and metadata-only `protected-material.initialized.json`, then returns `true` after successful creation. The marker contains only `protectionVersion`, `algorithmVersion`, and `createdAtUtc`; it never contains raw entropy, DB open secret, stable device id, protected blob content, key material, user identifiers, license identifiers, or paths. After the approved initialized-state marker exists, the probe returns `true` only when all required materials are loadable and fails closed on missing/malformed material or marker/material mismatch. Consumed by `RuntimeFolderPreparer`.

**Pre-login schema, migrations, and owner-scoped records**:
- Infrastructure migration/DDL placement: `src/Dobby.Infrastructure/Persistence/Schema/PreLogin/` contains `«PreLoginMigrationV1».cs` and schema/migration descriptors only. It may include Sprint-4-consumed Infrastructure-owned records such as `«StorageMetadataRecord».cs` under `Dobby.Infrastructure.Persistence` and `«DeviceIdentityRecord».cs` under `Dobby.Infrastructure.HardwareIdentity`.
- Owner-project record placement: Sprint 4 creates C# record types only for pre-login tables that have a real Sprint 4 consumer (production code, migration helper, or integration-test fixture). Any created record type lives in its approved owner project/namespace from `storage-materialization.md`: `Dobby.Security.Authentication` for `Users`/`Credentials`/`RememberMeSlot`/`Sessions`, `Dobby.Security.Authorization` for `Permissions`, `Dobby.Audit` for `AuditLog`, `Dobby.Security.Recovery` for `ConsumedRecoveryNonces`, `Dobby.Licensing.Context` for `CustomerDeploymentContext`, and `Dobby.Configuration` for `HostConfiguration`. Pre-login record types with no Sprint 4 consumer are deferred to their consumer sprint.
- `«PreLoginMigrationV1».cs` — CREATE TABLE + indexes + foreign keys + constraints per approved table, without making `Dobby.Infrastructure.Persistence` the owner of feature repositories or owner-project behavior. Migration body uses embedded SQL constants inside the migration class; Sprint 4 does not add external production `.sql` files or runtime-loaded SQL assets.
- `«PermissionRecord».cs` — grant-row model only: no `GrantState` column, no explicit deny storage, row presence represents grant, and revocation is row deletion.

**Post-login production schema files not created**:
- No production `src/Dobby.Infrastructure/Persistence/Schema/PostLogin/` files are created in Sprint 4. `0002_postlogin_bootstrap` is reserved for the first consumer sprint that runs a post-login migration after the authenticated-session seam; consumer sprints own their post-login schema evidence and migrations.

**Audit durable adapter** (placement: `src/Dobby.Audit/Persistence/` unless an explicit architecture-map amendment approves another direction):
- `«DurableAuditTrail».cs` — implements `IAuditTrail` against `AuditLog` table while consuming provider-neutral `Dobby.Infrastructure` persistence primitives; sanitization via existing `AuditDetailsSanitizer`. Do not place an `IAuditTrail` implementation in `Dobby.Infrastructure` unless the architecture map explicitly authorizes `Dobby.Infrastructure -> Dobby.Audit`.
- `«AuditRetentionSweeper».cs` — 365-day floor enforcer; cleanup off the login-first first-paint path.

**Runtime probe seam** (placement: `src/Dobby.Infrastructure/Persistence/` or `src/Dobby.Host/Startup/` per approved table):
- `«PreLoginPersistenceOpener».cs` — opens the encrypted DB during bootstrap between `ConfigLoaded` and `LoginVisible`; validates pragmas, runs provider-enabled probe, applies pending pre-login migrations; emits the approved new `StartupMilestoneName.PersistenceOpened` value `startup.persistence-opened`.

**Test files** (placement: `tests/Dobby.UnitTests/Persistence/` + `tests/Dobby.IntegrationTests/Persistence/` + `tests/Dobby.SmokeTests/`):
- `tests/Dobby.UnitTests/Persistence/StorageOptionsProviderModeTransitionTests.cs` — validator rejects `ProviderNeutralSkeleton` in production mode once Sprint 4 lands.
- `tests/Dobby.UnitTests/Persistence/«EncryptedSqliteConnectionFactoryTests».cs` — pragma application unit test (using in-process minimal SQLite open, NOT loading native cipher; or skipped if approved table requires integration-only).
- `tests/Dobby.UnitTests/ProtectedStorage/«DpapiScopeProtectorTests».cs` — scope routing + entropy combination + malformed-blob fail-closed behavior.
- `tests/Dobby.UnitTests/Audit/«DurableAuditTrailTests».cs` — record/snapshot round-trip against in-memory SQLite seam (without native cipher); retention sweep boundaries.
- `tests/Dobby.IntegrationTests/Persistence/«EncryptedProviderProbeTests».cs` — provider-enabled probe + encryption proof (read raw file bytes after write, assert non-plaintext) + WAL/journal/temp protection.
- `tests/Dobby.IntegrationTests/Persistence/«ConnectionGuardrailTests».cs` — every connection has `foreign_keys=ON` + bounded `busy_timeout`.
- `tests/Dobby.IntegrationTests/Persistence/«TransactionalIntegrityTests».cs` — commit/rollback boundaries; partial writes do not survive.
- `tests/Dobby.IntegrationTests/Persistence/«PreLoginTableSetClosedSetTests».cs` — startup discovers only the 11 pre-login tables; any additional pre-login schema fails the test.
- `tests/Dobby.IntegrationTests/Persistence/PostLoginMigrationOrderingTests.cs` — migrator-side ordering test: `ApplyPreLoginMigrationsAsync` applies only `0001_*`; no `0002_*` or later migration is applied during pre-login bootstrap. A test-only fake post-login migration may live under integration-test helpers only and must not enter production assemblies or release artifacts.
- `tests/Dobby.IntegrationTests/Startup/PostLoginMigrationOrderingTests.cs` (or an extension to `StartupBootstrapWiringTests.cs`) — composition-side ordering test: `HostBootstrapper` does not call the post-login migration path between `ConfigLoaded` and `LoginVisible`; `LoginVisible` alone does not authorize post-login migration application.
- `tests/Dobby.IntegrationTests/Persistence/«NoPlaintextSecretContentScanTests».cs` — scans committed sources + test-runtime data root after a fresh run; asserts no detected plaintext password/token/PEM/private-key shape.
- `tests/Dobby.IntegrationTests/Architecture/«NoTelemetryReferenceTests».cs` — or extend existing `NoTelemetryDefaultTests`; scans `Dobby.Audit` and `Dobby.Infrastructure` for external-upload/network client API references, at minimum `HttpClient` and `HttpListener`.
- `tests/Dobby.IntegrationTests/Persistence/«PluginNoInfrastructureReferenceTests».cs` — static scan of `samples/Dobby.Sample.Plugin/*.csproj` (and any future plugin csproj) for absence of `Dobby.Infrastructure` `ProjectReference`.
- `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` — UPDATE to record new edges as present (not new file, see §8).
- `tests/Dobby.IntegrationTests/Architecture/«PrintBoundaryBuildOutputIsolationTests».cs` — or an additional method in `ArchitectureDependencyBoundaryTests`; locates repo root from `AppContext.BaseDirectory` by walking up to `Dobby.sln`, then scans `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for absence of `Dobby.Application.dll`.
- `tests/Dobby.IntegrationTests/Startup/«PreLoginPersistenceOpenerTests».cs` — bootstrap milestone order with `PersistenceOpened` between `ConfigLoaded` and `LoginVisible`; fail-closed when provider-enabled probe fails; fail-closed when protected-material probe fails.
- `tests/Dobby.SmokeTests/«CrashRecoverySmokeTests».cs` — kill-between-begin-and-commit; restart; verify atomic + durable per `AC-042` Sprint-4-side mechanism (Sprint 11 wraps the publish-artifact run).

**Evidence artifacts** (placement: `artifacts/release-evidence/skeleton-local/`):
- `storage-materialization.md` — already exists and is approved Phase A input. Sprint 4 implementation must verify it remains `APPROVED` and unchanged before Phase B, then only append final provider/probe/schema implementation evidence in Step 9.14.
- `architecture-baseline.md` — Sprint 4 Reconfirmation entry appended (not a new file).
- `toolchain-pin-gate.md` — already contains approved Phase A closure evidence for both blockers. Sprint 4 implementation must add the required consistency test before package-bearing changes, then only append the final package-application note in Step 9.14 (not a new file).
- `decision-log.md` — contains the approved Phase B D-01..D-12 decision anchor (not a new file).

## 8. Files to Modify

**Project files**:
- `@c:\Users\Evaks\Desktop\dobby\Directory.Packages.props` — add approved `PackageVersion` entries for `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, and `Dapper` `2.1.72`; record/copy the approved `SQLite3MultipleCiphers v2.3.4` native asset into the deterministic repo-local path `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll` and copy it to `native/win-x64/sqlite3mc_x64.dll` under test/publish output. Versions/assets, license stance, vulnerability scan, hashes and approver come from the approved Phase A ledgers. Do not add `Konscious.Security.Cryptography.Argon2` in Sprint 4 unless a separate human-approved scope pull amends this plan.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Infrastructure\Dobby.Infrastructure.csproj` — add `PackageReference` for the new storage packages; apply the approved Windows-only adapter/TFM change for DPAPI/native code using the existing project (`net10.0;net10.0-windows` multi-target plus conditional Windows slice); add `ProjectReference` to `Dobby.Contracts` (closes deferred drift) if storage primitives consume `Dobby.Contracts.PluginStorage.*` types; KEEP existing `Dobby.Application` reference unchanged as the Sprint 9 print-boundary sunset work but apply the approved Phase B D-01 `PrivateAssets="all"`-based isolation to that temporary print-boundary `ProjectReference` so it does not flow transitively into `Dobby.Audit` or `Dobby.Security`. The plain `net10.0` consumer surface must remain platform-neutral and `CA1416` warnings must stay green under `TreatWarningsAsErrors=true`; Sprint 4 must not add a separate `Dobby.Infrastructure.Windows` project.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Audit\Dobby.Audit.csproj` — add `ProjectReference` to `Dobby.Infrastructure` (Cədvəl 2 allowed; closes drift) only after the approved print-boundary transitive-reference mitigation is recorded, applied, and covered by the metadata-aware transitive architecture test plus build-output smoke.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Security\Dobby.Security.csproj` — add `ProjectReference` to `Dobby.Infrastructure` (Cədvəl 2 allowed; closes drift) only after the same approved project-graph isolation strategy/test coverage prevents forbidden transitive `Dobby.Application` exposure.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\Dobby.Host.csproj` — no `ProjectReference` changes required (Host already references `Dobby.Infrastructure`); confirm composition root wires new persistence opener + protected-material probe.

**Configuration files**:
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\appsettings.json` — transition `Dobby:Storage:ProviderMode` from `ProviderNeutralSkeleton` to the approved production value `SQLite3MultipleCiphers`. KEEP `RequireDataAtRestProtection=true`, `BusyTimeoutMilliseconds=5000`.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Configuration\StorageOptions.cs` — update `ProviderMode` enum range + validator to accept the approved production mode (and continue to allow `ProviderNeutralSkeleton` only outside `Production` environment).
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Configuration\DobbyOptionsValidator.cs` — reject `ProviderNeutralSkeleton` when `RuntimeOptions.EnvironmentName == "Production"`; preserve all other ADR-0027 floors.

**Host bootstrap**:
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\Startup\HostBootstrapper.cs` — insert pre-login persistence opener call between `ConfigLoaded` and `LoginVisible`; emit approved `PersistenceOpened` milestone value `startup.persistence-opened`; inject protected-material probe instead of stub.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Host\RuntimeFolderPreparer.cs` — keep the existing constructor test seam; wire `HostBootstrapOptions.ProtectedMaterialPrerequisiteAvailable` / composition default to a real probe from `Dobby.Infrastructure.ProtectedStorage.«ProtectedMaterialAvailabilityProbe».IsAvailable`. Tests may continue to inject `Func<bool>`.
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Diagnostics\StartupMilestoneNames.cs` — append `PersistenceOpened` constant with value `startup.persistence-opened` and update Sprint 3 `StartupMilestoneNamesTests` accordingly.

**Audit consumer wiring**:
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Audit\InMemoryAuditTrail.cs` — UNCHANGED. Stays as unit-test seam.
- New `«DurableAuditTrail»` lives in `Dobby.Audit` and is registered as the default `IAuditTrail` implementation in `Dobby.Host` composition root while consuming `Dobby.Infrastructure` primitives through the approved provider-neutral surface.

**Existing tests** (small updates only; no test deletions or weakening):
- `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` — record new actually-present edges (`Dobby.Audit -> Dobby.Infrastructure`, `Dobby.Security -> Dobby.Infrastructure`, optionally `Dobby.Infrastructure -> Dobby.Contracts`) and add/extend transitive Dobby-owned dependency checks proving `Dobby.Audit` and `Dobby.Security` do not receive forbidden `Dobby.Application` exposure through the temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary edge. The transitive check must parse `ProjectReference` metadata and treat `PrivateAssets="all"` edges as non-propagating. Do not weaken the Cədvəl 2 upper-bound check.
- `tests/Dobby.IntegrationTests/Architecture/«PrintBoundaryBuildOutputIsolationTests».cs` — or an equivalent method in `ArchitectureDependencyBoundaryTests`; locate repo root from `AppContext.BaseDirectory` by walking up to `Dobby.sln`, then scan `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for absence of `Dobby.Application.dll`.
- `tests/Dobby.UnitTests/Diagnostics/StartupMilestoneNamesTests.cs` — extend to cover the new `PersistenceOpened` milestone if added.
- `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs` — extend the milestone-order test to assert `PersistenceOpened` appears between `ConfigLoaded` and `LoginVisible`; update test fixture to provide a usable temporary protected-material environment or inject a test probe.
- `tests/Dobby.IntegrationTests/Runtime/RuntimeFolderRecoveryTests.cs` — the injectable `protectedMaterialPrerequisiteAvailable=false` fail-closed contract stays; add complementary real-probe tests for clean first-run generation success and for fail-closed behavior when entropy/stable-device-id material is missing or malformed after the approved initialized-state marker exists.
- `tests/Dobby.UnitTests/Configuration/StorageOptionsTests.cs` — extend to cover production-mode rejection of `ProviderNeutralSkeleton`.

**Core/ErrorCode registry files**:
- `@c:\Users\Evaks\Desktop\dobby\src\Dobby.Core\Result.cs` — unchanged. `storage-materialization.md` approved no new Core storage `ErrorCode` values.
- `@c:\Users\Evaks\Desktop\dobby\docs\04_RESULT_VS_EXCEPTION_POLICY.md` — unchanged for storage errors; storage-specific failures stay in local `PersistenceError`.
- `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` — preserve the hard-coded 17-entry `ErrorCode` baseline and continue proving no speculative registry growth.

## 9. Code Work Plan

Each step is a single coherent checkpoint; in a Git worktree, each checkpoint may be committed separately. Order is strictly sequential: later steps depend on earlier ones.

**Step 9.1 — Toolchain pin preflight and package-governance guard**.
- Confirm Phase A resolved `TOOLCHAIN-PIN-APPROVAL-MISSING` and `TOOLCHAIN-PIN-REPRESENTATION-MISSING` in `toolchain-pin-gate.md` and that `storage-materialization.md` still approves the same selected package/native asset set. Confirm the ledgers remain unchanged within the freeze window.
- Before adding package-bearing changes, extend `PackageGovernanceTests` or an equivalent release-evidence governance test to parse the `toolchain-pin-gate.md` Markdown package/asset table and fenced JSON block, normalize Markdown code formatting, and verify field-by-field equality. Mismatches, unapproved selected rows, or drift from the approved pin evidence fail CI before package-bearing implementation is accepted.
- Add approved `PackageVersion` entries to `Directory.Packages.props` with central management discipline: `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, and `Dapper` `2.1.72`; wire the approved `SQLite3MultipleCiphers v2.3.4` native asset/package according to the approved packaging path. Keep `Konscious.Security.Cryptography.Argon2` absent.
- Verification: `dotnet restore` succeeds; the new toolchain table/JSON consistency check passes; `PackageGovernanceTests.Package_references_use_central_versions_only`, `Tier0_bcl_packages_are_not_referenced_or_pinned`, and `Rejected_or_deferred_packages_are_absent_without_human_opt_in` all pass.

**Step 9.2 — Storage abstraction surface**.
- Consume the approved `storage-materialization.md` primitive surface: `IHostPersistenceConnectionFactory`, `IHostPersistenceTransactionScope`, `IHostPersistenceMigrator`, `IHostPersistenceCommandExecutor`, and local `PersistenceError` under `Dobby.Infrastructure.Persistence`. Exact method signatures remain implementation-phase details constrained by ADR-0040 and the approved ledger.
- If the approved abstraction surface consumes `Dobby.Contracts` types, add `Dobby.Infrastructure -> Dobby.Contracts` `ProjectReference` before creating those abstractions. If it does not, leave the deferred drift open and do not add the reference speculatively.
- Create the abstraction surface in approved placement. No invented names. The Core 17-entry `ErrorCode` registry remains unchanged; storage-specific failures stay inside approved local `PersistenceError` / approved `Result<T>` payload shapes.
- Verification: `Dobby.Infrastructure` builds; `ContractsCoreSurfaceTests` still pass (Sprint 4 must not widen `Dobby.Core`/`Dobby.Contracts` surfaces); `ArchitectureDependencyBoundaryTests` pass if the conditional `Dobby.Infrastructure -> Dobby.Contracts` edge was added and the 17-entry `ErrorCode` baseline remains unchanged.

**Step 9.3 — DPAPI scope wrappers + entropy + stable device id**.
- Before implementing concrete DPAPI/native code, apply the approved Windows-only adapter/TFM strategy from Phase A: platform-neutral abstractions compile for the plain `net10.0` surface, concrete `ProtectedData`/Win32 calls compile only in the approved `net10.0-windows`/conditional Windows slice inside the existing `Dobby.Infrastructure` project, and platform analyzer warnings (`CA1416`) remain zero under `TreatWarningsAsErrors=true`.
- Implement `«DpapiLocalMachineProtector»`/`«DpapiCurrentUserProtector»` via `System.Security.Cryptography.ProtectedData.Protect`/`Unprotect`. Each call accepts per-scope additional entropy.
- Implement `«DobbyApplicationLevelEntropyStore»`: generate 32 random bytes via `RandomNumberGenerator` on clean first run; persist as a `LocalMachine`-protected blob under `%LocalAppData%/Dobby/data/protected/app-entropy.v1.dpapi` with restricted ACL; never written to logs or diagnostics export. After the approved initialized-state marker exists, missing or malformed entropy fails closed and is not silently regenerated.
- Implement `«DobbyStableDeviceIdStore»`: separate approved-length random identity generated on clean first run; persist as `%LocalAppData%/Dobby/data/protected/stable-device-id.v1.dpapi`; emphasize identity-not-key invariant via comments and tests. The file-backed protected stable device id is canonical before DB open; `DeviceIdentity.ProtectedStableDeviceIdBlob` mirrors it after migration/open. If the DB row is missing, migration/open inserts the mirror; if an existing DB row's protected blob differs from the canonical file-backed blob, startup fails closed. After the approved initialized-state marker exists, missing or malformed stable-device-id material fails closed and is not silently reissued.
- Implement DB open secret protected material as `%LocalAppData%/Dobby/data/protected/db-open-secret.v1.dpapi`.
- Implement `«ProtectedMaterialAvailabilityProbe»`: on clean first run, creates all required protected stores plus `%LocalAppData%/Dobby/data/protected/protected-material.initialized.json` and returns `true` after successful persistence. The initialized marker stores only `protectionVersion`, `algorithmVersion`, and `createdAtUtc`. After the approved initialized-state marker exists, returns `true` only when all required stores load successfully and fails closed on missing/malformed material or marker/material mismatch.
- Verification: unit tests cover first-run generation, post-initialization malformed/missing-blob fail-closed behavior, scope-routing, and no-mutation-on-failed-load.

**Step 9.4 — Encrypted SQLite provider materialization**.
- Consume the approved `ADR-0054` P1 Persistence Materialization Pass from `storage-materialization.md`: provider mode `SQLite3MultipleCiphers`, cipher scheme `sqlcipher_v4`, PBKDF2-HMAC-SHA512 with 256000 iterations, 4096-byte pages, HMAC enabled, plaintext header size 0, install-directory native DLL loading, SHA-256 verification before provider registration, explicit `SQLitePCLRaw.provider.dynamic_cdecl` initialization, and fail-closed PRAGMA/probe handling. DB-at-rest KDF evidence is not Sprint 5 login KDF selection or Argon2 scope.
- Implement `«EncryptedSqliteConnectionFactory»`: approved provider initialization with approved native cipher; key sourced from `LocalMachine`-scoped DPAPI wrap consuming application-level entropy; pragmas applied on every open (`PRAGMA foreign_keys=ON;`, `PRAGMA busy_timeout=«BusyTimeoutMilliseconds»;`).
- Implement `«SqliteProviderEnabledProbe»`: verify cipher loaded and active; fail-closed otherwise.
- Verification: integration tests use a test-only sentinel marker written by test setup, never production code, and assert no-key/wrong-key opens fail closed, correct-key open succeeds while the raw DB header is not `SQLite format 3\0`, DB/WAL/journal/temp plaintext scans do not contain the sentinel, and wrong-provider startup fails closed without writing the `«PersistenceOpened»` milestone.

**Step 9.5 — Pre-login schema + owner-scoped records + migration V1**.
- Create Infrastructure-owned migration/DDL artifacts plus only the owner-project record types that have a real Sprint 4 consumer (production code, migration helper, or integration-test fixture) in the approved owner namespaces from `storage-materialization.md`; record types without Sprint 4 consumers are deferred to their consumer sprint. `«PreLoginMigrationV1»` creates exactly the 11 approved tables with indexes/foreign keys/constraints from the approved table.
- Implement `«PreLoginMigrationV1»` with embedded SQL constants inside the migration class. Do not add production `.sql` files, embedded resources, or runtime-loaded SQL assets in Sprint 4.
- Implement `Permissions` using the resolved grant-row shape: no `GrantState` column, no explicit deny storage, row presence represents grant, row absence represents no grant/default deny, and revocation is row deletion.
- `IHostPersistenceMigrator` applies V1 idempotently and writes a row into `StorageMetadata` (schema version + provider-mode + migration history).
- Verification: pre-login table set closed-set integration test (lists tables after open, asserts equality with the 11 approved names).

**Post-login migration ordering invariant (no production post-login schema step)**.
- Do not create production post-login record types, production post-login schema directories, or `«PostLoginMigrationV1»` in Sprint 4. `0002_postlogin_bootstrap` is reserved for the first consumer sprint that supplies an approved post-login migration after the authenticated-session seam.
- Implement/shape `IHostPersistenceMigrator` so pre-login and post-login migration application paths are distinct. Sprint 4 production callers use only the pre-login path. The post-login path stays neutral and must not reference `Dobby.Security` types; Sprint 5+ host composition calls it only after successful authentication / the authenticated-session seam.
- Verification: `PostLoginMigrationOrderingTests` prove that pre-login bootstrap applies only `0001_*`, no `0002_*` or later migration runs during `ConfigLoaded` -> `LoginVisible`, and `LoginVisible` alone does not authorize post-login migration application. Any fake post-login migration used for this proof is test-only and excluded from production assemblies/release artifacts.

**Step 9.6 — Connection guardrails + host persistence transaction scope**.
- Implement `IHostPersistenceTransactionScope`: open, begin, commit, rollback. Partial-write rollback test asserts atomic. Multi-statement writes use this approved transaction primitive.
- Verification: integration `«ConnectionGuardrailTests»` and `«TransactionalIntegrityTests»`.

**Step 9.7 — Durable audit trail**.
- Before adding durable audit consumer code, record and apply the approved Phase B D-01 `PrivateAssets="all"`-based project-graph isolation strategy for the temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary edge, then add `Dobby.Audit -> Dobby.Infrastructure` `ProjectReference` (`@c:\Users\Evaks\Desktop\dobby\src\Dobby.Audit\Dobby.Audit.csproj`). Add `Dobby.Security -> Dobby.Infrastructure` (`@c:\Users\Evaks\Desktop\dobby\src\Dobby.Security\Dobby.Security.csproj`) before any Sprint 4 `Dobby.Security` consumer code touches protected-storage wrappers or storage primitives. Do not defer first-use consumer edges to Step 9.10, but do not add them before the approved transitive-reference guardrail is implemented and tested with both the metadata-aware transitive dependency check and build-output smoke; if either test cannot prove isolation, stop per §13.9.
- Implement `«DurableAuditTrail»` against `AuditLog`. Reuse `AuditDetailsSanitizer` for redaction. Implement `«AuditRetentionSweeper»` with 365-day floor; sweeper exposes a `SweepAsync` method invoked off the login-first first-paint path.
- Wire `«DurableAuditTrail»` as default `IAuditTrail` in `Dobby.Host` composition root. Schedule `«AuditRetentionSweeper»` as a one-shot, non-blocking post-`LoginVisible` maintenance operation for Sprint 4; do not add a periodic timer in Sprint 4. The operation must not be raw unobserved fire-and-forget: report status/failure through the existing `BackgroundOperationReporter` surface or an equivalent observed wrapper, and record support-safe diagnostics for exceptions.
- Verification: `ArchitectureDependencyBoundaryTests` pass after the first-use consumer edge is added; unit tests + integration round-trip test + retention sweep tests pass; existing `InMemoryAuditTrail` + tests stay unchanged.

**Step 9.8 — Configuration alignment**.
- Update `StorageOptions.ProviderMode` to accept the approved production mode. Update `DobbyOptionsValidator` to reject `ProviderNeutralSkeleton` when `RuntimeOptions.EnvironmentName=="Production"`.
- Update `src/Dobby.Host/appsettings.json` `Dobby:Storage:ProviderMode` value.
- Update `tests/Dobby.UnitTests/Configuration/StorageOptionsTests.cs` + `tests/Dobby.IntegrationTests/Configuration/DobbyOptionsBindingTests.cs` + `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs` `WriteBootstrapAppSettings` helper to use the approved value.
- Verification: Sprint 3 configuration tests all green; new tests cover the transition.

**Step 9.9 — Bootstrap milestone insertion**.
- Add `StartupMilestoneName.PersistenceOpened` constant with value `startup.persistence-opened` in `Dobby.Diagnostics/StartupMilestoneNames.cs`; update `StartupMilestoneNamesTests`.
- Update `HostBootstrapper.Bootstrap` to call `«PreLoginPersistenceOpener»` between `ConfigLoaded` and `LoginVisible`; emit milestone; route provider-enabled probe failure as fail-closed exception that prevents `LoginVisible` from being emitted.
- Wire `HostBootstrapOptions.ProtectedMaterialPrerequisiteAvailable` / composition default to `«ProtectedMaterialAvailabilityProbe».IsAvailable`; keep `RuntimeFolderPreparer`'s existing `Func<bool>` injection seam for tests.
- Verification: extended `StartupBootstrapWiringTests` asserts milestone order + fail-closed paths.

**Step 9.10 — Architecture-map edge verification**.
- Confirm first-use project references were added in their owning steps: `Dobby.Audit -> Dobby.Infrastructure` before durable audit implementation; `Dobby.Security -> Dobby.Infrastructure` before any Security consumer implementation; `Dobby.Infrastructure -> Dobby.Contracts` during Step 9.2 only if storage primitives actually reference `Dobby.Contracts` types.
- Update `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` to keep the direct Cədvəl 2 upper-bound check and add/extend transitive Dobby-owned dependency closure checks proving `Dobby.Audit` and `Dobby.Security` do not receive forbidden `Dobby.Application` exposure through `Dobby.Infrastructure`. The transitive closure must read `ProjectReference` metadata and treat `PrivateAssets="all"` edges as non-propagating.
- Add the build-output smoke check for the same boundary: locate repo root from `AppContext.BaseDirectory` by walking up to `Dobby.sln`, then scan `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for absence of `Dobby.Application.dll`. If either the metadata-aware graph check or build-output smoke cannot prove the approved isolation strategy, stop for a human architecture-map decision rather than adding the consumer edges.
- Verification: `ArchitectureDependencyBoundaryTests` still passes (new direct edges are within Cədvəl 2 allowed set, forbidden transitive edges are blocked), and no consumer edge was deferred past its first use.

**Step 9.11 — Plugin no-Infrastructure boundary test**.
- Add `«PluginNoInfrastructureReferenceTests»`: scan `samples/**/*.csproj` and any future plugin csproj for the absence of `Dobby.Infrastructure` `ProjectReference` or `Dobby.Infrastructure` `PackageReference`.
- Verification: tests green; existing `PluginAndSdkBoundaryTests` continues to enforce plugin → Contracts/SDK only.

**Step 9.12 — No-plaintext-secret and no-telemetry scans**.
- Implement `«NoPlaintextSecretContentScanTests»`: regex over committed `src/`, `tests/`, `samples/`, `docs/`, `artifacts/release-evidence/skeleton-local/` for `-----BEGIN PRIVATE KEY-----`, `password\s*=`, `Bearer\s+[A-Za-z0-9._-]{20,}`, `dpapi`/`entropy` blob-like patterns. Excludes documentation that explicitly discusses the boundary.
- Implement runtime-data-root scan: after a fresh `HostBootstrapper.Bootstrap` against a temporary data root, assert no on-disk plaintext password/token/PEM/private-key signature.
- Add or extend a no-telemetry/static network-upload reference regression for `Dobby.Audit` and `Dobby.Infrastructure`, at minimum covering `HttpClient` and `HttpListener`.
- Verification: tests green; no plaintext leak and no external-upload/network client API reference in the Sprint 4 persistence/audit areas.

**Step 9.13 — Crash recovery smoke**.
- Implement `«CrashRecoverySmokeTests»` in `tests/Dobby.SmokeTests`: launch the local publish output or equivalent isolated smoke artifact from the same build (Sprint 4 mechanism verification, not final `AC-041` release-candidate evidence), begin a transactional write, simulate process kill, relaunch, assert atomic (no partial state) + durable (DB opens, integrity check passes).
- Verification: smoke green against local publish output or equivalent isolated smoke artifact; Sprint 11 owns final `AC-041` publish/installer release-candidate evidence.

**Step 9.14 — Evidence and reconfirmation**.
- Append Sprint 4 implementation-complete evidence to the already-existing `artifacts/release-evidence/skeleton-local/storage-materialization.md`, including the implemented approved table reference, materialization decision, provider-proof assertion results and provider/probe/schema verification summary.
- Append Sprint 4 Reconfirmation entry to `architecture-baseline.md` with: new test count, new dependency edges present, deferred drift updates (close `Dobby.Infrastructure -> Dobby.Contracts` only if the edge was actually added because storage primitives consume `Dobby.Contracts` types; otherwise leave that observed drift open, and leave `SPRINT1-INFRA-APP-PRINT-BOUNDARY` open for Sprint 9), reconfirmation path (a) or (b).
- Append Sprint 4 package-application evidence to `toolchain-pin-gate.md`; the blocker closure entries themselves must have been present before Phase B.
- Verification: `ReleaseEvidenceTests` passes; `EncodingBoundaryTests` passes (new files UTF-8 no-BOM).

## 10. Test Plan

Each test below specifies the test type, target file (placeholder names gated by approved table), assertions, and verifying ACs/NFRs/ADRs.

**Unit tests** (`tests/Dobby.UnitTests`):

- `Persistence/StorageOptionsProviderModeTransitionTests`:
  - Asserts `DobbyOptionsValidator` rejects `Dobby:Storage:ProviderMode=ProviderNeutralSkeleton` when `RuntimeOptions.EnvironmentName=="Production"`.
  - Asserts validator accepts approved production provider mode value.
  - Asserts `RequireDataAtRestProtection=false` still fails closed.
  - Sources: ADR-0027 §6a, ADR-0054, AC-037.
- `Persistence/«EncryptedSqliteConnectionFactoryTests»` (unit-level surface):
  - Pragmas `foreign_keys=ON` + `busy_timeout=<configured>` are applied per connection (verified against a non-encrypted in-process SQLite if approved as unit-level seam; otherwise this test moves to integration only).
  - Connection factory exposes provider-neutral handle types only (compile-time + reflection check).
  - Sources: AC-038, ADR-0040 §3.1.
- `ProtectedStorage/«DpapiScopeProtectorTests»`:
  - `LocalMachine` scope routes through `DataProtectionScope.LocalMachine`; `CurrentUser` scope routes through `DataProtectionScope.CurrentUser`.
  - Wrong-scope unprotect fails closed (typed error, no plaintext leak).
  - Malformed protected blob fails closed (no silent re-creation).
  - Application-level entropy invariant: combining entropy yields different ciphertext than without entropy (sanity check, NOT a cryptographic strength test).
  - Sources: ADR-0055, NFR-007.
- `ProtectedStorage/«DobbyApplicationLevelEntropyStoreTests»`:
  - Clean first-run generates 32 random bytes via `RandomNumberGenerator` and persists the protected blob.
  - Subsequent run loads the same entropy (idempotent).
  - Missing/malformed blob after the approved initialized-state marker exists fails closed; store does NOT silently regenerate.
  - Entropy material never returned across plugin boundary (compile-time + reflection check that no public method returns the raw bytes).
  - Sources: ADR-0055 §application-level-entropy.
- `ProtectedStorage/«DobbyStableDeviceIdStoreTests»`:
- Clean first-run generation produces an approved-length random identity distinct from the application-level entropy bytes and persists the protected blob.
  - Subsequent run loads the same stable device id (idempotent).
  - Missing/malformed blob after the approved initialized-state marker exists fails closed; store does NOT silently reissue the stable device id.
  - Identity-not-key invariant: store exposes the identity as an opaque token, NOT as a `byte[]` key material parameter to any DPAPI Protect/Unprotect call.
  - Persistence is host-installation-bound (not user-account-bound).
  - Sources: ADR-0055 §DobbyStableDeviceId, NFR-007, Cədvəl 3 `Secure Storage Boundary`.
- `Audit/«DurableAuditTrailTests»` (unit-level seam — uses in-memory SQLite without native cipher):
  - `Append(AuditEvent)` round-trips via `Snapshot()`.
  - `AuditDetailsSanitizer` is applied to details before write.
  - Append-only invariant: no public `Delete`/`Update` method; the only mutation is via the retention sweeper.
  - 37-entry `AuditEventName` registry round-trips (each known event name persists and reads back).
  - Sources: ADR-0019, AC-052, NFR-011, audit-and-diagnostics spec.
- `Audit/«AuditRetentionSweeperTests»`:
  - Sweep removes only entries older than the 365-day floor (verified with `FakeTimeProvider`).
  - Sweep never runs on the login-first first-paint path (the sweeper exposes only an explicit method, not an auto-trigger).
  - Sources: AC-052, NFR-021.
- `Diagnostics/StartupMilestoneNamesTests` (existing, extended):
  - Asserts the new `«PersistenceOpened»` constant matches the approved milestone name.
  - Sources: Sprint 3 baseline.
- `Configuration/StorageOptionsTests` (existing, extended):
  - Adds production-mode rejection of `ProviderNeutralSkeleton`.
  - Sources: ADR-0027 §6a, AC-037.

**Integration tests** (`tests/Dobby.IntegrationTests`):

- `Persistence/«EncryptedProviderProbeTests»`:
  - Provider-enabled probe returns true when native cipher binary loaded; fails closed with typed `PersistenceError.ProviderUnavailable` when missing/wrong native binary.
  - Named provider-proof assertions: open without key fails closed; open with wrong key fails closed; open with correct key succeeds while the raw DB header is not `SQLite format 3\0`.
  - Encryption proof: test setup writes the test-only sentinel marker `__dobby_storage_marker_v1__` into a row, production code never writes that marker, a checkpoint is forced, raw DB bytes are read from disk, and the sentinel is NOT present in plaintext.
  - WAL/journal/temp protection: trigger WAL/journal/temp side-file creation, read raw bytes from each, assert the test-only sentinel is not present in plaintext.
  - Wrong-provider startup fails closed and does not emit the `«PersistenceOpened»` milestone.
  - Sources: AC-037, AC-038, AC-042, ADR-0054.
- `Persistence/«ConnectionGuardrailTests»`:
  - Open a connection through `«EncryptedSqliteConnectionFactory»`; assert `PRAGMA foreign_keys` returns `1`.
  - Assert `PRAGMA busy_timeout` returns the configured value (`Dobby:Storage:BusyTimeoutMilliseconds`).
  - Repeat for multiple connection opens; assert pragmas applied every time.
  - Sources: AC-038, ADR-0027 §6a.
- `Persistence/«TransactionalIntegrityTests»`:
  - Begin transaction, write two rows, simulate exception, assert rollback removes both writes (atomic).
  - Begin transaction, write one row, commit, assert presence (durable).
  - Concurrent writer hits busy timeout but does NOT corrupt state.
  - Sources: AC-042, ADR-0054.
- `Persistence/«PreLoginTableSetClosedSetTests»`:
  - After fresh bootstrap, query `sqlite_master` and assert table set equals exactly the 11 approved pre-login table names.
  - Any additional table at pre-login phase fails the test (closed set), including post-login feature tables.
  - Sources: storage spec §3.1, ADR-0041.
- `Persistence/PostLoginMigrationOrderingTests`:
  - Migrator-side invariant: `ApplyPreLoginMigrationsAsync` applies only `0001_*`; no `0002_*` or later post-login migration is applied during pre-login bootstrap.
  - A test-only fake post-login migration may be injected from integration-test helpers to prove ordering, but it must not live under production `src/`, production `Schema/PostLogin/`, or release artifacts.
  - Sources: master plan Sprint 4 `Post-login Migration Infrastructure`, `storage-materialization.md` D4-11, ADR-0040 §3.1.
- `Startup/PostLoginMigrationOrderingTests` (or existing `StartupBootstrapWiringTests` extended):
  - Composition-side invariant: `HostBootstrapper` does not call the post-login migration path between `ConfigLoaded` and `LoginVisible`.
  - `LoginVisible` emission alone does not authorize post-login migration application; post-login migrations are only for successful authentication / the authenticated-session seam in Sprint 5+ host composition.
  - Sources: ADR-0041 login-first startup policy, storage-materialization D4-11.
- `Persistence/«NoPlaintextSecretContentScanTests»`:
  - Static scan over `src/`, `tests/`, `samples/`, `docs/specs/`, `artifacts/release-evidence/skeleton-local/`: no `-----BEGIN PRIVATE KEY-----`, no `Bearer ` followed by long base64, no `password = "<value>"` etc.
  - Excludes test fixtures and spec documents that explicitly discuss the boundary.
  - Runtime scan: after a fresh `HostBootstrapper.Bootstrap` against a temp data root, scan all files under that root; assert no plaintext password/token/PEM/private-key signature appears outside the encrypted DB.
  - Sources: NFR-007, NFR-020, AC-037.
- `Architecture/«NoTelemetryReferenceTests»` (or existing `NoTelemetryDefaultTests` extended):
  - Static scan over `Dobby.Audit` and `Dobby.Infrastructure` source/compiled references confirms no external-upload/network client API references are introduced for durable audit or persistence work, at minimum `HttpClient` and `HttpListener`.
  - Sources: NFR-020, AC-033.
- `Persistence/«PluginNoInfrastructureReferenceTests»`:
  - Loads all `*.csproj` under `samples/` and any future `plugins/` folder.
  - Asserts no `ProjectReference` to `Dobby.Infrastructure`.
  - Asserts no `PackageReference` to `Microsoft.Data.Sqlite*`, `SQLitePCLRaw.*`, `Dapper`, or the approved native cipher package.
  - Sources: ADR-0001, ADR-0030, NFR-007, NFR-008.
- `Architecture/PackageGovernanceTests` (existing, extended before package-bearing changes):
  - Parses `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`.
  - Extracts the Sprint 4 package/asset Markdown table and fenced JSON representation.
  - Normalizes Markdown code formatting and compares fields one-for-one across both representations.
  - Fails if a selected Sprint 4 row is not `APPROVED`, if Argon2 is not `DEFERRED_TO_SPRINT_5`, or if any table/JSON field drifts before package-bearing implementation is accepted.
  - Sources: `toolchain-pin-gate.md` automated consistency invariant, Sprint 4 Phase A closure.
- `Architecture/ArchitectureDependencyBoundaryTests` (existing, extended):
  - Asserts new `Dobby.Audit -> Dobby.Infrastructure` and `Dobby.Security -> Dobby.Infrastructure` edges are present and within Cədvəl 2 allowed set.
  - If `Dobby.Infrastructure -> Dobby.Contracts` was added because storage primitives consume `Dobby.Contracts` types, asserts that edge is present and within Cədvəl 2 allowed set; otherwise records that the Sprint 1 observed drift remains open and does not add the reference speculatively.
  - Preserves the current 17-entry Core `ErrorCode` baseline and fails speculative registry growth because Phase A approved no Core storage `ErrorCode` expansion.
  - Asserts `Dobby.Infrastructure -> Dobby.Application` print-boundary edge is still present (Sprint 9 sunset).
  - Asserts the print-boundary edge does not flow transitively into `Dobby.Audit` or `Dobby.Security`; the transitive closure reads `ProjectReference` metadata and treats `PrivateAssets="all"` edges as non-propagating. Any forbidden transitive `Dobby.Application` exposure fails until a human architecture-map decision explicitly changes the boundary.
  - Sources: Cədvəl 2, `SPRINT1-INFRA-APP-PRINT-BOUNDARY`.
- `Architecture/«PrintBoundaryBuildOutputIsolationTests»` (new class or equivalent method in `ArchitectureDependencyBoundaryTests`):
  - Locates repo root from `AppContext.BaseDirectory` by walking up to `Dobby.sln`.
  - Scans `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for `Dobby.Application.dll`.
  - Fails if the temporary print-boundary edge appears in either consumer's build output.
  - Sources: Phase B D-01 project-graph isolation decision, `SPRINT1-INFRA-APP-PRINT-BOUNDARY`.
- `Startup/«PreLoginPersistenceOpenerTests»`:
  - `HostBootstrapper.Bootstrap` emits `HostBuilt` → `ConfigLoaded` → `«PersistenceOpened»` → `LoginVisible` in order.
  - Bootstrap fails closed when provider-enabled probe fails (no `LoginVisible` milestone emitted).
  - Bootstrap fails closed when protected-material probe returns false.
  - Bootstrap fails closed when pre-login migration fails.
  - Sources: ADR-0041, NFR-007, AC-037.
- `Startup/StartupBootstrapWiringTests` (existing, extended):
  - Extends milestone-order assertion to include `«PersistenceOpened»`.
  - Sources: Sprint 3 baseline + Sprint 4 milestone insertion.
- `Runtime/RuntimeFolderRecoveryTests` (existing, extended):
  - Existing fail-closed test (`protectedMaterialPrerequisiteAvailable=false`) preserved.
  - New tests: clean first bootstrap without entropy/stable-device-id material on disk generates and persists both materials, while missing/malformed material after the approved initialized-state marker exists makes the real probe return false and startup fail closed.
  - Sources: ADR-0055, Sprint 3 protected-material seam.
- `Configuration/DobbyOptionsBindingTests` (existing, extended):
  - Confirms the new `Dobby:Storage:ProviderMode` value binds correctly.
  - Sources: ADR-0027 §6a.
- `Configuration/ConfigurationSourceOrderTests` (existing, unchanged):
  - Confirms source order is preserved.
  - Sources: ADR-0027 §2.

**Smoke tests** (`tests/Dobby.SmokeTests`):

- `«CrashRecoverySmokeTests»`:
  - Launches the local publish output or equivalent isolated smoke artifact from the same build; this is Sprint 4 mechanism verification, not final `AC-041` release-candidate evidence.
  - Issues a transactional write through a host-exposed test hook (or via running a known startup-time write); kills the process before commit.
  - Restarts; asserts the write does NOT appear (atomic).
  - Asserts the DB opens cleanly (no recovery error in diagnostics log).
  - Issues a second transactional write + commit; restart; asserts the write IS present (durable).
  - Sources: AC-042 mechanism (Sprint 4 side); Sprint 11 final smoke owns the artifact-level evidence.

**Test count delta**:
- Sprint 3 exit baseline: ~145-175 tests (Sprint 1+2+3 sum).
- Sprint 4 adds:
  - Unit: ~10-15 new tests across `Persistence/`, `ProtectedStorage/`, `Audit/` (durable + sweeper), Configuration extensions.
  - Integration: ~12-17 new tests across `Persistence/`, `Startup/`, `Runtime/`, and package-governance extensions.
  - Smoke: 1 new test (`CrashRecoverySmokeTests`).
- Sprint 4 exit baseline: approximately 170-205 tests (final count depends on approved table granularity; record the exact number in `architecture-baseline.md` Reconfirmation).

**Test execution discipline** (per `AC-041`):
- Unit tests run without touching `%LocalAppData%` or any real DPAPI state — must inject mock entropy/stable-device-id stores.
- Integration tests run against isolated temporary data roots (already standard via `TemporaryDirectory` helper from Sprint 3).
- Smoke tests execute against local publish output or equivalent isolated smoke artifact from the same CI run; Sprint 11 owns final `AC-041` publish/installer release-candidate evidence.
- No test deletions or weakening of existing assertions (Sprint 3 retention floors, Sprint 2 Contracts surface, Sprint 1 architecture boundary).

## 11. Configuration, Build, and Tooling Changes

**`Directory.Packages.props`** — approved `PackageVersion` additions after the no-code preflight and toolchain consistency test:
- `Microsoft.Data.Sqlite.Core` — `10.0.8`. Tier 3 per tech-stack §8.
- `SQLitePCLRaw.core` — `3.0.3`. Tier 3.
- `SQLitePCLRaw.provider.dynamic_cdecl` — `3.0.3`. Required for explicit provider wiring.
- `Dapper` — `2.1.72`. Selected for Sprint 4 storage implementation by `storage-materialization.md` and `toolchain-pin-gate.md`.
- Approved native encrypted SQLite provider asset — `SQLite3MultipleCiphers v2.3.4` / `sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip`, SHA-256 `f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15`, packaged according to the approved implementation path. `SQLitePCLRaw.bundle_*` packages remain `Tier R` rejected.

**Package-governance test change required before package-bearing edits**:
- Extend `PackageGovernanceTests` or equivalent release-evidence governance tests to parse `toolchain-pin-gate.md`, compare the Markdown table to the fenced JSON representation field-by-field after Markdown normalization, and fail on mismatches, unapproved selected rows, or Argon2 scope drift.

**Package pins explicitly unchanged by Sprint 4**:
- `Konscious.Security.Cryptography.Argon2` — Sprint 5 owns KDF package/runtime selection unless a separate human-approved scope pull amends this plan.

**`src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`** — `PackageReference` additions for the storage stack; no inline `Version` attributes (central package management enforced). Also records the approved Windows-only adapter/TFM treatment for DPAPI/native-provider code using the existing project (`net10.0;net10.0-windows` target/slice and conditional Windows implementation folder); the plain `net10.0` surface remains platform-neutral for `Dobby.Audit`/`Dobby.Security`, and `CA1416` remains zero under `TreatWarningsAsErrors=true`. Sprint 4 adds zero new projects.

**`src/Dobby.Audit/Dobby.Audit.csproj`** — add `<ProjectReference Include="..\Dobby.Infrastructure\Dobby.Infrastructure.csproj" />` only after the approved Phase B D-01 `PrivateAssets="all"` project-graph isolation strategy from §13.9 is recorded, applied, and covered by architecture tests.

**`src/Dobby.Security/Dobby.Security.csproj`** — add `<ProjectReference Include="..\Dobby.Infrastructure\Dobby.Infrastructure.csproj" />` only after the same approved Phase B D-01 strategy prevents forbidden transitive `Dobby.Application` exposure.

**`src/Dobby.Host/appsettings.json`** — `Dobby:Storage:ProviderMode` value transitioned from `ProviderNeutralSkeleton` to the approved production mode.

**`global.json`** — UNCHANGED (`10.0.202`, `latestPatch`). Sprint 4 does not bump the SDK.

**`Directory.Build.props`** — UNCHANGED (`Nullable=enable`, `LangVersion=14.0`, `TreatWarningsAsErrors=true`). Sprint 4 maintains zero-warning builds.

**`.editorconfig`** — UNCHANGED. UTF-8 no-BOM, namespace == folder, one-public-type-per-file enforced for new files.

**`Dobby.sln`** — UNCHANGED. Sprint 4 adds zero new projects.

**No tooling additions**: Sprint 4 does NOT add new tools under `tools/`. Sprint 4 does NOT add an installer asset (Sprint 11 owns release artifact). Sprint 4 does NOT add a CI workflow file.

**Build invariants Sprint 4 must keep green** (run before commit):
- `dotnet restore` — succeeds without warnings about missing/floating versions.
- `dotnet build -warnaserror` — zero warnings, zero errors.
- `dotnet test tests/Dobby.UnitTests` — all green.
- `dotnet test tests/Dobby.IntegrationTests` — all green, including `PackageGovernanceTests` with the toolchain table/JSON consistency check, `ArchitectureDependencyBoundaryTests`, `EncodingBoundaryTests`, `ContractsCoreSurfaceTests`, `PluginAndSdkBoundaryTests`, `ReleaseEvidenceTests`.
- `dotnet test tests/Dobby.SmokeTests` — all green (Sprint 4 adds crash-recovery smoke).

## 12. Implementation Order

Sprint 4 implementation is strictly sequential due to layered dependencies. Each phase must complete (with green tests) before the next begins.

**Phase A — Approved-ledger preflight (NO CODE CHANGES)**:
1. Verify `toolchain-pin-gate.md` remains `Status: APPROVED`, still records Phase A closure by `n4hkro` on 2026-05-14, still resolves both toolchain stop conditions, and still contains the approved selected rows for `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, `Dapper` `2.1.72`, and `SQLite3MultipleCiphers v2.3.4`.
2. Verify `storage-materialization.md` remains `Status: APPROVED`, still records Phase A closure by `n4hkro` on 2026-05-14, and still says `Phase B implementation may start: YES`.
3. Verify `decision-log.md` contains `phase-b-decisions-2026-05-14` with `Status: APPROVED` by `n4hkro` and D-01..D-12 decision rows.
4. Verify the approved storage ledger still records: primitive surface under `Dobby.Infrastructure.Persistence`, local `PersistenceError` sub-codes, unchanged Core 17-entry `ErrorCode` registry, DB-at-rest profile, native provider proof, pre-login schema/migration authority, grant-row `Permissions` shape, reserved post-login migration ID, Dapper selection, TFM/platform strategy, and Argon2 deferral.
5. Verify the master plan Sprint 4 section has been amended to `Post-login Migration Infrastructure` with the authenticated-session seam ordering invariant from D4-11.
6. Verify the freeze-window rule was not violated. If either Phase A ledger is missing, no longer approved, inconsistent, or changed during the freeze window, STOP and reopen Phase A for human approval before implementation continues. If the Phase B decision anchor is missing or no longer approved, STOP before Phase B implementation-detail work.

**Phase B — Foundation (Steps 9.1 → 9.3)**:
6. Step 9.1: add the toolchain table/JSON consistency test, then pin approved packages in `Directory.Packages.props` + add to `Dobby.Infrastructure.csproj`. Run `PackageGovernanceTests`.
7. Step 9.2: create storage abstraction surface (no SQLite implementation yet) — interfaces, error types, `Result<T>` shapes per approved table, and neutral pre-login/post-login migrator application paths. Run `ContractsCoreSurfaceTests`.
8. Step 9.3: apply approved Windows-only adapter/TFM strategy, then implement DPAPI scope wrappers + application-level entropy store + stable-device-id store + protected-material availability probe. Run unit tests for ProtectedStorage.

**Phase C — Encrypted provider (Step 9.4)**:
9. Step 9.4: implement `«EncryptedSqliteConnectionFactory»` + `«SqliteProviderEnabledProbe»` + `«SqliteWalProtectionVerifier»`. Run `«EncryptedProviderProbeTests»` integration tests.

**Phase D — Schema (Steps 9.5 → 9.6)**:
10. Step 9.5: implement Infrastructure-owned migration/DDL artifacts + Sprint-4-consumed owner-project record types + embedded-SQL `«PreLoginMigrationV1»` + `IHostPersistenceMigrator` with `StorageMetadata` tracking and grant-row `Permissions`. Run `«PreLoginTableSetClosedSetTests»` + `PostLoginMigrationOrderingTests`.
11. Step 9.6: implement `IHostPersistenceTransactionScope` + connection guardrails. Run `«ConnectionGuardrailTests»` + `«TransactionalIntegrityTests»`.

**Phase E — Consumers (Steps 9.7 → 9.10)**:
12. Step 9.7: apply the approved Phase B D-01 `PrivateAssets="all"`-based project-graph isolation strategy for the temporary print-boundary edge, add the first-use `Dobby.Audit -> Dobby.Infrastructure` consumer edge, implement `«DurableAuditTrail»` + one-shot post-`LoginVisible` `«AuditRetentionSweeper»`, and wire in composition root. Add `Dobby.Security -> Dobby.Infrastructure` before any Security consumer code in this or a later step, but only after the same approved transitive-reference guardrail is active and verified by both architecture tests. Run `ArchitectureDependencyBoundaryTests` + `«PrintBoundaryBuildOutputIsolationTests»`/equivalent + `«DurableAuditTrailTests»`.
13. Step 9.8: update `StorageOptions` validator + `appsettings.json` + Sprint 3 test fixtures. Run Sprint 3 configuration tests + new transition tests.
14. Step 9.9: update `StartupMilestoneNames` + `HostBootstrapper` + `HostBootstrapOptions`/`RuntimeFolderPreparer` protected-material probe wiring. Run extended `StartupBootstrapWiringTests` + `RuntimeFolderRecoveryTests`.
15. Step 9.10: verify first-use architecture-map edges are present when needed (`Dobby.Audit` → `Infrastructure`, `Dobby.Security` → `Infrastructure`, conditional `Dobby.Infrastructure` → `Contracts`), remain within Cədvəl 2, and do not introduce forbidden transitive `Dobby.Application` exposure into `Dobby.Audit`/`Dobby.Security`. Re-run `ArchitectureDependencyBoundaryTests`.

**Phase F — Boundary verification (Steps 9.11 → 9.12)**:
16. Step 9.11: add `«PluginNoInfrastructureReferenceTests»`. Run.
17. Step 9.12: add `«NoPlaintextSecretContentScanTests»` (committed-file + runtime-data-root) and the no-telemetry/static network-upload reference regression for `Dobby.Audit`/`Dobby.Infrastructure`. Run.

**Phase G — Smoke + evidence (Steps 9.13 → 9.14)**:
18. Step 9.13: add `«CrashRecoverySmokeTests»`. Run against local publish output or equivalent isolated smoke artifact; Sprint 11 owns final `AC-041` release-candidate evidence.
19. Step 9.14: append Sprint 4 Reconfirmation to `architecture-baseline.md`, append package-application evidence to `toolchain-pin-gate.md`, and append implementation-complete evidence to the already-existing `storage-materialization.md`. Run `ReleaseEvidenceTests` + `EncodingBoundaryTests`.

**Total verification (after every phase)**:
- `dotnet build -warnaserror` green.
- All existing tests green (no test deletions, no weakening).
- New tests green.
- Test count strictly increased.

## 13. Risks, Blockers, and Human Decisions

**CLOSED PHASE A DECISIONS AND PREFLIGHT GUARDS** (Sprint 4 implementation may start only after these approvals are re-verified unchanged):

**13.1 Toolchain pin approval and representation (CLOSED / PREFLIGHT REQUIRED)**.
- Source: `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`, `SPRINT_1_EXECUTION_PLAN.md`.
- Current state: `toolchain-pin-gate.md` records Sprint 4 storage package/native asset scope as `APPROVED` on 2026-05-14 by `n4hkro` and resolves `TOOLCHAIN-PIN-APPROVAL-MISSING` plus `TOOLCHAIN-PIN-REPRESENTATION-MISSING` for Phase A.
- Approved selected rows: `Microsoft.Data.Sqlite.Core` `10.0.8`; `SQLitePCLRaw.core` `3.0.3`; `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`; `Dapper` `2.1.72`; `SQLite3MultipleCiphers v2.3.4` / `sqlite3mc-2.3.4-sqlite-3.53.1-win64.zip` SHA-256 `f685ab7aed02f79985c6876b7dfa463bb81ebc1f6acd3ce536382cc276467b15`. `Konscious.Security.Cryptography.Argon2` is `DEFERRED_TO_SPRINT_5`.
- Guard: before package-bearing changes, add the automated table/JSON consistency test required by the ledger. If the ledger is missing, no longer approved, inconsistent, or modified during the freeze window, stop and reopen Phase A.

**13.2 Storage primitive/pre-login schema/materialization approval (CLOSED / PREFLIGHT REQUIRED)**.
- Source: `artifacts/release-evidence/skeleton-local/storage-materialization.md`, `docs/adr/ADR-0040-internal-host-adapters-boundary.md` §3.1, `docs/specs/storage-and-data-protection.md` §5a.
- Current state: `storage-materialization.md` records `Status: APPROVED`, Phase A closure approver `n4hkro`, Phase A closure date `2026-05-14`, and `Phase B implementation may start: YES`.
- Approved primitive surface: `Dobby.Infrastructure.Persistence.IHostPersistenceConnectionFactory`, `IHostPersistenceTransactionScope`, `IHostPersistenceMigrator`, `IHostPersistenceCommandExecutor`, and local `PersistenceError`.
- Approved error stance: Core 17-entry `ErrorCode` registry remains unchanged; storage-specific failures use the closed local `PersistenceError` sub-code set.
- Guard: method signatures remain implementation-phase details constrained by the approved ledger and ADR-0040. Adding, removing, renaming, or widening primitives/sub-codes reopens Phase A. The decision package 2026-05-14 resolves the prior schema questions by using grant-row `Permissions` storage and by reserving post-login schema materialization for consumer sprints after the authenticated-session seam.

**13.3 DB-at-rest provider materialization approval (CLOSED / PREFLIGHT REQUIRED)**.
- Source: `storage-materialization.md`, `docs/adr/ADR-0054-db-at-rest-protection-provider.md`, `docs/specs/storage-and-data-protection.md` §5a.
- Current state: approved provider mode `SQLite3MultipleCiphers`, cipher scheme `sqlcipher_v4`, PBKDF2-HMAC-SHA512, 256000 iterations, 4096-byte pages, HMAC enabled, plaintext header size 0, install-directory native DLL loading, startup SHA-256 verification, explicit `SQLitePCLRaw.provider.dynamic_cdecl` initialization, and fail-closed PRAGMA/probe handling.
- Approved provider-proof assertions: no-key open fails closed, wrong-key open fails closed, correct-key open succeeds with encrypted header, test-only sentinel absent from DB/WAL/journal/temp plaintext scans, wrong-provider/provider-disabled startup fails closed without `PersistenceOpened`.
- Guard: DB-at-rest KDF evidence does not approve Sprint 5 login KDF selection, Argon2 package pinning, password KDF floors, or `AC-051` latency evidence.

**13.4 Phase A freeze-window guard**.
- Source: `storage-materialization.md`, `toolchain-pin-gate.md`.
- Current state: both ledgers state that after Phase A closure and before the first Sprint 4 implementation commit/task, ledger changes reopen Phase A.
- Impact on Sprint 4: implementation starts with preflight verification, not new decision-making. Any required ledger change during the freeze window stops implementation until a separate approval pass is recorded.
- Mitigation: Step 9.1 adds automated consistency coverage for the toolchain ledger before package-bearing changes; Step 9.14 appends implementation evidence only after implementation is complete.

**RESOLVED HUMAN DECISIONS** (decision package 2026-05-14):

**13.HD-1 Permissions grant-only/schema conflict (RESOLVED)**.
- Source: `artifacts/release-evidence/skeleton-local/storage-materialization.md` `Permissions` row; `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-018; `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` NFR-017; `docs/specs/user-management.md`.
- Decision: amend `storage-materialization.md` to grant-row `Permissions` storage. `GrantState` is removed. Row presence represents grant, row absence represents no grant/default deny, revocation is row deletion, and explicit deny storage remains structurally impossible.
- Decision link: `artifacts/release-evidence/skeleton-local/storage-materialization.md` Phase A closure 2026-05-14 (`n4hkro`) plus amended source paths in this decision package (`storage-materialization.md` `Permissions` row and this execution plan §3/§7/§9/§13).

**13.HD-2 Post-login schema approval gap (RESOLVED)**.
- Source: `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 post-login P1 host state scope; `artifacts/release-evidence/skeleton-local/storage-materialization.md` minimum pre-login schema draft and post-login/later-migration notes; `docs/adr/ADR-0040-internal-host-adapters-boundary.md` §3.1 anti-name-invention rule.
- Decision: amend `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 `Post-login P1 Host State` to `Post-login Migration Infrastructure`. Sprint 4 provides the migrator framework, `StorageMetadata` ledger, numbered migration convention, and authenticated-session-seam ordering invariant. Sprint 4 does not materialize production post-login feature tables or extra columns.
- Decision link: `artifacts/release-evidence/skeleton-local/storage-materialization.md` D4-11 and Phase A closure 2026-05-14 (`n4hkro`) plus amended source paths in this decision package (`SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 `Post-login Migration Infrastructure`, `storage-materialization.md` `Post-login migration ID` reinterpretation, and this execution plan §3/§7/§9/§10/§12/§13).

**13.HD-3 Phase B implementation-detail decision set (RESOLVED)**.
- Source: `artifacts/release-evidence/skeleton-local/decision-log.md` `phase-b-decisions-2026-05-14`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md` Sprint 4 Phase B Decision Notes.
- Decision: D-01..D-12 are approved by `n4hkro` for Sprint 4 Phase B. This includes the `PrivateAssets="all"` print-boundary isolation strategy, deterministic native DLL layout, protected-material marker invariants, conditional `Dobby.Infrastructure -> Dobby.Contracts`, `PersistenceOpened` / `startup.persistence-opened`, already-approved D4-07 TFM strategy, unit/integration provider-proof split, `PreLoginPersistenceOpener` placement, one-shot observed audit-retention sweep, owner-scoped record placement, resolved primitive placeholder cleanup, and embedded-SQL migration body shape.
- Decision link: `decision-log.md` `phase-b-decisions-2026-05-14`; the D-01 architecture note in `architecture-baseline.md` records only the strategy decision, not Sprint 4 implementation evidence or a Sprint 4 Reconfirmation.

**SOFT RISKS AND PHASE GATES**:

Sprint 4 foundation/provider/schema work may proceed after Phase A preflight. Items marked as phase gates block only the named later phase until the decision or guard is satisfied.

**13.5 `Dobby.Configuration -> Dobby.Infrastructure` edge absence**.
- Source: `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2.
- Impact: if Sprint 4 places any storage primitive abstractions in `Dobby.Configuration` (which is unlikely under the default placement on `Dobby.Infrastructure`), this edge would need an architecture-map amendment. Default placement avoids this.
- Mitigation: confirm placement on `Dobby.Infrastructure` per Cədvəl 1 Row 87.

**13.6 ErrorCode registry expansion under `docs/04_RESULT_VS_EXCEPTION_POLICY.md`**.
- Source: ADR-0004-like policy, Sprint 1 17-entry registry baseline.
- Impact: `storage-materialization.md` explicitly approves no Core registry expansion. If implementation discovers a need for new Core storage codes, that is a Phase A decision change, not an implementation detail.
- Mitigation: keep storage-specific failures in approved local `PersistenceError` / approved `Result<T>` payload shapes and leave the Core registry plus 17-entry baseline unchanged.

**13.7 Native cipher binary distribution**.
- Source: `docs/03_TECH_STACK_AND_VERSIONS.md` §Pin Tier table + ADR-0054.
- Impact: native binary distribution may require additional MSBuild / install layout work (e.g., copying native DLLs to publish output). Sprint 4 must verify the install layout from §13.4 decision works for both `dotnet test` and local publish-output smoke; Sprint 11 performs final `AC-041` publish/installer release-candidate verification.
- Mitigation: include explicit `<Content>` or `<None>` entries for `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll`, copied to `native/win-x64/sqlite3mc_x64.dll` under test/publish output; verify Sprint 4 smoke runs against local publish output or equivalent isolated smoke artifact and record that final artifact-level evidence remains Sprint 11 scope.

**13.8 BranchShared mode runtime stays docs-only**.
- Source: ADR-0056/ADR-0057.
- Impact: `Dobby:BranchShared:RuntimeEnabled=false` floor must be preserved. Sprint 4 does NOT implement BranchShared persistence runtime.
- Mitigation: Sprint 4 only implements Standalone mode persistence; BranchShared protected-storage scope (DPAPI `LocalMachine`) is implemented but BranchShared-mode-specific code paths are gated by `RuntimeEnabled=false` (existing Sprint 3 floor).

**13.9 Print boundary carry-over (PHASE E GATE / APPROVED PHASE B DECISION SATISFIED BEFORE CONSUMER EDGES)**.
- Source: `SPRINT1-INFRA-APP-PRINT-BOUNDARY` (Sprint 9 sunset).
- Impact: `Dobby.Infrastructure -> Dobby.Application` print edge must remain present and untouched for Sprint 9, but adding `Dobby.Audit -> Dobby.Infrastructure` and `Dobby.Security -> Dobby.Infrastructure` can otherwise expose `Dobby.Application` transitively to consumers that Cədvəl 2 does not allow to reference it.
- Mitigation: before adding the Sprint 4 consumer edges, record and apply the approved Phase B D-01 `PrivateAssets="all"`-based project-graph isolation strategy for the temporary print edge, extend `ArchitectureDependencyBoundaryTests` to fail forbidden transitive `Dobby.Application` exposure into `Dobby.Audit` or `Dobby.Security` while respecting `PrivateAssets="all"` metadata, and add the build-output smoke that scans `src/Dobby.Audit/bin/**` and `src/Dobby.Security/bin/**` for absence of `Dobby.Application.dll`. If either check cannot prove isolation, stop for a human architecture-map decision. This gate does not block Steps 9.1-9.6, but Phase E consumer-edge work must not start until satisfied.

**13.10 KDF runtime selection scope drift**.
- Source: master plan Sprint 5; `AC-051` split.
- Impact: Sprint 4 must NOT decide `Dobby:Auth:PasswordKdfMode` runtime value, wire Argon2/PBKDF2 selection, or add `Konscious.Security.Cryptography.Argon2` package pins unless a separate human-approved scope pull amends this plan. The KDF package/runtime decision belongs to Sprint 5.
- Mitigation: Sprint 4 leaves the current `AuthOptions.PasswordKdfMode` value (`Pbkdf2Sha256`) and `Pbkdf2PasswordHasher` wiring unchanged.

**13.11 No telemetry default invariant**.
- Source: NFR-020, AC-033.
- Impact: durable audit log must NOT enable any external upload. Audit retention sweeper must NOT emit anything beyond local diagnostics events.
- Mitigation: Sprint 4 includes a regression test confirming no `HttpClient`/`HttpListener` reference in `Dobby.Audit`/`Dobby.Infrastructure` (or extends `NoTelemetryDefaultTests` from Sprint 3).

**13.12 Master plan readiness assertion**.
- Source: `SPRINT_IMPLEMENTATION_PLAN.md` §3 Sprint 4 risks/blockers — verbatim quote: "This sprint is blocked until documented human decisions are available."
- Impact: current Phase A artifacts plus decision package 2026-05-14 provide the documented decisions needed for Sprint 4 implementation. Sprint 4 implementation agent must verify §13.1-§13.4 approved-ledger state and the amended source paths before beginning Phase B.
- Mitigation: this plan converts the original blocker into a no-code preflight and freeze-window guard.

## 14. Definition of Done

Sprint 4 is DONE when ALL of the following hold:

**Phase A approved evidence verified**:
- [ ] `toolchain-pin-gate.md` is present, `Status: APPROVED`, records 2026-05-14 Phase A closure by `n4hkro`, and resolves `TOOLCHAIN-PIN-APPROVAL-MISSING` plus `TOOLCHAIN-PIN-REPRESENTATION-MISSING` for Phase A.
- [ ] `toolchain-pin-gate.md` still contains the approved selected rows and values for `Microsoft.Data.Sqlite.Core` `10.0.8`, `SQLitePCLRaw.core` `3.0.3`, `SQLitePCLRaw.provider.dynamic_cdecl` `3.0.3`, `Dapper` `2.1.72`, and `SQLite3MultipleCiphers v2.3.4` native asset evidence; Argon2 remains `DEFERRED_TO_SPRINT_5`.
- [ ] `storage-materialization.md` is present, `Status: APPROVED`, records 2026-05-14 Phase A closure by `n4hkro`, and says `Phase B implementation may start: YES`.
- [ ] `storage-materialization.md` still records the approved primitive surface, local `PersistenceError` sub-code set, unchanged Core `ErrorCode` registry, pre-login schema/migration authority, grant-row `Permissions` shape, reserved post-login migration ID, DB-at-rest cipher/KDF profile, native supply-chain/provider package or asset path, provider-proof assertion list, Dapper selection, Argon2 deferral, ADR/spec cross-reference invariant, and freeze-window rule.
- [ ] `decision-log.md` is present, contains `phase-b-decisions-2026-05-14`, records `Status: APPROVED` by `n4hkro`, and lists D-01..D-12 with D-11 marked resolved.
- [ ] `architecture-baseline.md` contains the Sprint 4 Phase B Decision Notes entry for D-01 and keeps Sprint 4 Reconfirmation as a later Step 9.14 implementation-evidence entry.
- [ ] `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 still contains the amended `Post-login Migration Infrastructure` task group with the authenticated-session seam ordering invariant.
- [ ] Freeze-window preflight passed: neither ledger changed between Phase A closure and the first Sprint 4 implementation task. Any required ledger change reopened Phase A and received a separate approval pass before implementation continued.
- [ ] Approved Windows-only adapter/TFM strategy recorded for DPAPI/Win32/native-provider concrete code; the strategy keeps `Dobby.Audit`/`Dobby.Security` on platform-neutral abstractions and keeps `CA1416` green under `TreatWarningsAsErrors=true`.

**Implementation complete**:
- [ ] Storage abstraction surface implemented in `Dobby.Infrastructure` (provider-neutral types, no raw `Microsoft.Data.Sqlite` types past adapter boundary).
- [ ] Core `ErrorCode` no-expansion decision honored: the 17-entry registry/baseline remains unchanged and storage-specific failures use approved local `PersistenceError`.
- [ ] Encrypted SQLite provider materialized with approved cipher + native binary; provider-enabled startup probe wired.
- [ ] DPAPI scope wrappers (`LocalMachine`/`CurrentUser`) implemented; per-scope additional entropy applied.
- [ ] Concrete DPAPI/Win32/native-provider code isolated in the approved Windows-only adapter slice/TFM; plain `net10.0` consumer surface remains platform-neutral.
- [ ] Dobby application-level entropy store generates + persists + loads `protected/app-entropy.v1.dpapi` on clean first run; never logs/exports raw entropy; after metadata-only `protected-material.initialized.json` exists, missing/malformed entropy fails closed without silent regeneration.
- [ ] `DobbyStableDeviceId` store generates + persists separate identity as `protected/stable-device-id.v1.dpapi` on clean first run; identity-not-key invariant enforced; the file-backed protected stable device id is canonical and `DeviceIdentity.ProtectedStableDeviceIdBlob` is a DB mirror; mismatch fails closed. After metadata-only `protected-material.initialized.json` exists, missing/malformed stable-device-id material fails closed without silent reissue.
- [ ] Protected-material availability probe replaces Sprint 3 stub in `RuntimeFolderPreparer`, succeeds on clean first-run generation, and fails closed only for missing/malformed post-initialization material or explicit injected false probes.
- [ ] Pre-login closed table set materialized (exactly the 11 approved tables; no extras), with grant-row `Permissions` and no `GrantState` column.
- [ ] Post-login migration infrastructure implemented without production post-login feature tables or extra columns; post-login migrations are not applied during pre-login bootstrap and are not authorized merely by `LoginVisible`.
- [ ] Connection guardrails (`foreign_keys=ON`, `busy_timeout`) applied on every connection.
- [ ] WAL/journal/temp side-files protected inside DB-at-rest boundary.
- [ ] `IHostPersistenceTransactionScope` implemented; multi-statement writes go through this approved transaction primitive; commit/rollback atomic + durable.
- [ ] Durable `IAuditTrail` implementation wired as default in `Dobby.Host` composition root.
- [ ] Audit retention sweeper enforces 365-day floor; sweep does not run on login-first first-paint path.
- [ ] `Dobby:Storage:ProviderMode` transitioned from `ProviderNeutralSkeleton` to approved production value in `appsettings.json` + `StorageOptions` validator.
- [ ] `StartupMilestoneNames` extended with `PersistenceOpened` value `startup.persistence-opened`; `HostBootstrapper` emits it between `ConfigLoaded` and `LoginVisible`.
- [ ] `Dobby.Audit -> Dobby.Infrastructure` and `Dobby.Security -> Dobby.Infrastructure` `ProjectReference` edges added only after the approved Phase B D-01 `PrivateAssets="all"`-based project-graph isolation strategy for the temporary print-boundary edge is recorded, applied, and verified by both the metadata-aware transitive graph test and build-output smoke; `Dobby.Infrastructure -> Dobby.Contracts` added only if storage primitives consume `Dobby.Contracts` types, otherwise the Sprint 1 observed drift remains open and documented.
- [ ] `Dobby.Infrastructure -> Dobby.Application` print edge UNCHANGED (Sprint 9 sunset preserved) and verified not to flow transitively into `Dobby.Audit` or `Dobby.Security`.

**Tests green**:
- [ ] `dotnet restore` succeeds; central package management enforced.
- [ ] `dotnet build -warnaserror` zero warnings, zero errors.
- [ ] All Sprint 1 tests green (architecture/package/encoding/contracts-core/plugin/release-evidence baselines).
- [ ] All Sprint 2 tests green (Core primitives, Contracts surface, plugin framework).
- [ ] All Sprint 3 tests green (configuration floors, runtime folder layout, diagnostics, startup, audit in-memory).
- [ ] New Sprint 4 unit tests green: `StorageOptionsProviderModeTransitionTests`, `«EncryptedSqliteConnectionFactoryTests»` (unit-level), `«DpapiScopeProtectorTests»`, `«DobbyApplicationLevelEntropyStoreTests»`, `«DobbyStableDeviceIdStoreTests»`, `«DurableAuditTrailTests»`, `«AuditRetentionSweeperTests»`.
- [ ] New Sprint 4 integration tests green: `«EncryptedProviderProbeTests»`, `«ConnectionGuardrailTests»`, `«TransactionalIntegrityTests»`, `«PreLoginTableSetClosedSetTests»`, `PostLoginMigrationOrderingTests`, `«NoPlaintextSecretContentScanTests»`, `«NoTelemetryReferenceTests»` (or equivalent `NoTelemetryDefaultTests` extension), `«PluginNoInfrastructureReferenceTests»`, `«PreLoginPersistenceOpenerTests»`.
- [ ] Package governance/release-evidence consistency test green: parses `toolchain-pin-gate.md` Markdown package/asset table and fenced JSON, normalizes Markdown code formatting, verifies field-by-field equality, and fails on unapproved selected rows or Argon2 scope drift.
- [ ] Extended existing integration tests green: `ArchitectureDependencyBoundaryTests` (including forbidden transitive dependency checks), `StartupBootstrapWiringTests`, `RuntimeFolderRecoveryTests`, `DobbyOptionsBindingTests`.
- [ ] New Sprint 4 smoke test green: `«CrashRecoverySmokeTests»` against local publish output or equivalent isolated smoke artifact; final `AC-041` publish/installer release-candidate evidence remains Sprint 11 scope.
- [ ] Test count strictly increased relative to Sprint 3 exit baseline; no test deletions; no weakened assertions.

**Evidence and reconfirmation**:
- [ ] `artifacts/release-evidence/skeleton-local/storage-materialization.md` exists as approved Phase A input and has a Sprint 4 implementation-complete note appended with implemented provider/probe/schema evidence.
- [ ] `artifacts/release-evidence/skeleton-local/architecture-baseline.md` appended with Sprint 4 Reconfirmation entry (test count, new edges present, deferred drift updates, reconfirmation path).
- [ ] `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` contains approved Phase A closure evidence for both resolved blockers, passes the table/JSON consistency test, and has a Sprint 4 package-application note appended after implementation.
- [ ] `ReleaseEvidenceTests` passes.
- [ ] `EncodingBoundaryTests` passes (all new files UTF-8 no-BOM).

**Boundary invariants verified**:
- [ ] No plaintext secret in committed sources or runtime data root after fresh bootstrap.
- [ ] No `Dobby.Infrastructure` reference from any plugin csproj.
- [ ] No `HttpClient`/`HttpListener` or equivalent external-upload/network client API reference introduced in `Dobby.Audit` or `Dobby.Infrastructure` (no-telemetry default).
- [ ] `Dobby.Contracts` plugin-facing surface unchanged (`ContractsCoreSurfaceTests` green).
- [ ] `Dobby.Core` public surface unchanged at ADR-0001 level.
- [ ] Plugin storage contracts (`ISharedDomainStorage`/`IUserScopedStorage`) unchanged.
- [ ] BranchShared runtime stays docs-only (`Dobby:BranchShared:RuntimeEnabled=false` preserved).

## 15. Out of Scope

Sprint 4 explicitly does NOT do the following work (each item is owned by a later sprint or a deferred phase):

- **KDF runtime selection and parameter-tuning payload** (Sprint 5+) — `Dobby:Auth:PasswordKdfMode` decision (`Argon2id` vs `Pbkdf2Sha256`), runtime hashing wiring, KDF parameter tuning fields beyond the approved minimum `Credentials` columns, and baseline-latency artifact regeneration. Sprint 4 only carries the approved minimum `Credentials` column shape.
- **Owner bootstrap / first-login flow** (Sprint 5) — first successful login becomes `Owner` per `FR-002`. Sprint 4 only persists `Users`/`Credentials` tables.
- **Session creation / refresh / lock** (Sprint 5) — Sprint 4 carries `Sessions` schema only; absolute (8h) / idle (30m) timeout runtime is Sprint 5.
- **Remember-me semantics** (Sprint 5) — Sprint 4 carries `RememberMeSlot` schema only.
- **Owner password recovery flow** (Sprint 5) — Sprint 4 carries `ConsumedRecoveryNonces` schema only.
- **Permission registry / authorization decisions** (Sprint 6) — Sprint 4 carries `Permissions` schema only; `OperationPermissionCapability` runtime is Sprint 6.
- **User management UI / user deactivation** (Sprint 6) — Sprint 4 carries `Users` schema only.
- **Settings / About screens** (Sprint 6) — Sprint 4 carries `HostConfiguration` schema only.
- **Shell composition / UI / navigation** (Sprint 7) — Sprint 4 carries only the post-login migration infrastructure; per-user UI state schema is consumer-sprint owned.
- **Plugin install / cache / lifecycle** (Sprint 8) — Sprint 4 carries only the post-login migration infrastructure; plugin install/cache schema, `.dobbyplugin` signature verification and activation are Sprint 8.
- **Plugin manager / plugin signature trust** (Sprint 8/10) — Sprint 4 leaves `Dobby.Host/Trust/README.md` untouched.
- **License state runtime / `.dobbylicense` import flow** (Sprint 9) — Sprint 4 carries only the post-login migration infrastructure; license state schema and runtime are Sprint 9-owned.
- **Print boundary sunset** (Sprint 9) — Sprint 4 keeps `Dobby.Infrastructure -> Dobby.Application` print edge present.
- **Sample plugin Main Content hosting / capability runtime** (Sprint 10) — Sprint 4 leaves sample plugin untouched.
- **Release evidence regeneration against publish artifact** (Sprint 11) — Sprint 4 establishes the deterministic mechanism for `AC-037`/`AC-038`/`AC-042` but Sprint 11 runs final proof against publish artifact, including `AC-014`/`AC-051` baseline-latency.
- **BranchShared runtime** (P1-if-time / P2) — `Dobby:BranchShared:RuntimeEnabled=false` floor preserved; `Dobby.BranchSetup` / `Dobby.Infrastructure.Postgres` projects NOT added per ADR-0056/ADR-0057.
- **Host installer / MSIX/MSI / portable artifact** (Sprint 11) — Sprint 4 makes no changes under `installer/`.
- **Admin tool** (P2; sample/recovery signer only in P1) — Sprint 4 does not touch `Dobby.Admin`.
- **Audit viewer UI** (P2 per `ADR-0019` extension).
- **Plugin marketplace / catalog** (P3).
- **Cross-device migration / backup/restore** (P2).
- **Auto online update** (P2 per `ADR-0047`).
- **Plugin update / repair / downgrade** (P2 per `ADR-0037` extension).
- **Out-of-process plugin worker / hardened process isolation** (P2 per `ADR-0007` triggers).
- **Field-level encryption of business data** (`Dobby.Storage` boundary rule: business data stays unencrypted at field level; only password/token/key/remember-me/recovery/session secrets get protected-storage). Sprint 4 maintains this floor.
- **Telemetry / metrics / traces / observability layer** (P2 per ADR-0017/ADR-0018).
- **Runtime language switching / multi-language UI / RTL** (P2/P3 per ADR-0043/ADR-0044).

## 16. Final Execution Checklist

Concise, copy-paste-friendly checklist for the Sprint 4 implementation agent. Items are ordered. Tick boxes only when verified.

**Phase A — Approved-ledger preflight (NO CODE CHANGES)**:
- [ ] Read `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 + cross-sprint dependency table.
- [ ] Read `SPRINT_4_EXECUTION_PLAN.md` (this file).
- [ ] Read `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` — confirm `Status: APPROVED`, 2026-05-14 closure by `n4hkro`, resolved stop conditions, approved selected rows, and freeze-window rule.
- [ ] Read `artifacts/release-evidence/skeleton-local/storage-materialization.md` — confirm `Status: APPROVED`, 2026-05-14 closure by `n4hkro`, `Phase B implementation may start: YES`, approved primitive surface, DB-at-rest profile, pre-login schema/migration authority, grant-row `Permissions`, reserved post-login migration ID, no Core `ErrorCode` expansion, Dapper selection, Argon2 deferral, and freeze-window rule.
- [ ] Read `artifacts/release-evidence/skeleton-local/decision-log.md` — confirm `phase-b-decisions-2026-05-14` is `APPROVED` by `n4hkro`, D-01..D-12 are recorded, and D-11 is marked resolved rather than open.
- [ ] Read `docs/specs/storage-and-data-protection.md` §5a and `docs/adr/ADR-0054-db-at-rest-protection-provider.md` as authority context; use the approved ledgers as the Phase A decision surface unless a ledger/ADR/spec conflict is found.
- [ ] Read `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 amended `Post-login Migration Infrastructure`; confirm post-login migrations are gated on successful authentication / authenticated-session seam, not `LoginVisible`.
- [ ] Confirm §13.HD-1 and §13.HD-2 are marked `RESOLVED` in this plan and that no production post-login schema files are listed for Sprint 4.
- [ ] If either Phase A approved ledger is missing, no longer `APPROVED`, inconsistent with its ADR/spec authority, or changed during the freeze window, STOP and reopen Phase A. If the Phase B decision anchor is missing or no longer `APPROVED`, STOP before implementation-detail work. Do not proceed to package-bearing Phase B work until these preflight checks pass.

**Phase B — Foundation**:
- [ ] If this checkout is a Git worktree, create `sprint-4-protected-storage-materialization` (or per repo convention). If this is still the non-git skeleton described in `README.md`, skip branch/commit commands and record per-phase checkpoint notes in the implementation summary/evidence.
- [ ] Run `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror` + `dotnet test .\Dobby.sln -c Debug --no-build` at Sprint 3 exit baseline. Record passing test count.
- [ ] Step 9.1: add the toolchain table/JSON consistency test first; run it; then pin approved packages in `Directory.Packages.props` and add `PackageReference`s to `Dobby.Infrastructure.csproj`. Run `dotnet restore` + `PackageGovernanceTests`. Checkpoint.
- [ ] Step 9.2: create storage abstraction surface per approved table; keep the Core 17-entry `ErrorCode` registry unchanged. Run `ContractsCoreSurfaceTests` + `ArchitectureDependencyBoundaryTests`. Checkpoint.
- [ ] Step 9.3: apply approved Windows-only adapter/TFM strategy, then implement DPAPI scope wrappers + entropy store + stable-device-id store + protected-material probe. Run new unit tests. Checkpoint.

**Phase C — Encrypted provider**:
- [ ] Step 9.4: implement `«EncryptedSqliteConnectionFactory»` + probe + WAL verifier. Run `«EncryptedProviderProbeTests»`. Checkpoint.

**Phase D — Schema**:
- [ ] Step 9.5: implement Infrastructure-owned migration/DDL artifacts + Sprint-4-consumed owner-project records + embedded-SQL `«PreLoginMigrationV1»` + `IHostPersistenceMigrator` with grant-row `Permissions` and no `GrantState`. Run `«PreLoginTableSetClosedSetTests»` + `PostLoginMigrationOrderingTests`. Checkpoint.
- [ ] Step 9.6: implement `IHostPersistenceTransactionScope` + connection guardrails. Run `«ConnectionGuardrailTests»` + `«TransactionalIntegrityTests»`. Checkpoint.

**Phase E — Consumers**:
- [ ] Step 9.7: apply the approved Phase B D-01 `PrivateAssets="all"`-based project-graph isolation strategy for the temporary print-boundary edge, add the first-use `Dobby.Audit -> Dobby.Infrastructure` consumer edge, implement `«DurableAuditTrail»` + one-shot post-`LoginVisible` `«AuditRetentionSweeper»`, and wire in `Dobby.Host`. Add `Dobby.Security -> Dobby.Infrastructure` before any Security consumer code only after the same approved guardrail is active and verified by both architecture tests. Run architecture + audit tests. Checkpoint.
- [ ] Step 9.8: update `StorageOptions` validator + `appsettings.json` + Sprint 3 test fixtures. Run configuration tests. Checkpoint.
- [ ] Step 9.9: update `StartupMilestoneNames` + `HostBootstrapper` + `HostBootstrapOptions`/`RuntimeFolderPreparer` protected-material probe wiring. Run startup/runtime tests. Checkpoint.
- [ ] Step 9.10: verify first-use architecture-map edges are present when needed, still within Cədvəl 2, and do not introduce forbidden transitive `Dobby.Application` exposure into `Dobby.Audit`/`Dobby.Security`. Run `ArchitectureDependencyBoundaryTests`. Checkpoint.

**Phase F — Boundary verification**:
- [ ] Step 9.11: add `«PluginNoInfrastructureReferenceTests»`. Run. Checkpoint.
- [ ] Step 9.12: add `«NoPlaintextSecretContentScanTests»` (committed-file + runtime) and the no-telemetry/static network-upload reference regression for `Dobby.Audit`/`Dobby.Infrastructure`. Run. Checkpoint.

**Phase G — Smoke + evidence**:
- [ ] Step 9.13: add `«CrashRecoverySmokeTests»`. Run against local publish output or equivalent isolated smoke artifact; leave final `AC-041` publish/installer evidence to Sprint 11. Checkpoint.
- [ ] Step 9.14: append Sprint 4 Reconfirmation to `architecture-baseline.md`; append Sprint 4 package-application evidence to `toolchain-pin-gate.md`; append implementation-complete evidence to the already-existing `storage-materialization.md`. Run `ReleaseEvidenceTests` + `EncodingBoundaryTests`. Checkpoint.

**Final verification**:
- [ ] `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror` zero warnings.
- [ ] `dotnet test .\Dobby.sln -c Debug --no-build` all green.
- [ ] Test count strictly greater than Sprint 3 exit baseline.
- [ ] All `## 14. Definition of Done` boxes ticked.
- [ ] Sprint 4 branch/checkpoint ready for review.

## 17. Planning Coverage and Confidence

**Areas covered with high confidence**:
- Master sprint plan Sprint 4 task groups, dependencies, ACs, verification, risks (re-read at `deep` level; quoted directly).
- Sprint 1 + Sprint 2 + Sprint 3 outputs (verified against current code via direct file reads of `csproj`, `appsettings.json`, option POCOs, `RuntimeFolderPreparer`, `HostBootstrapper`, `IAuditTrail`/`InMemoryAuditTrail`/`AuditEventName` 37-entry registry, `DiagnosticRedactor`, test files).
- ADR-0027 §6a 18-key schema (full table re-read; current `appsettings.json` confirmed to bind it).
- ADR-0040 §3.1 anti-name-invention requirement + Cədvəl 2 dependency table (confirmed approved primitive surface in `storage-materialization.md`; confirmed `Dobby.Audit -> Dobby.Infrastructure`, `Dobby.Security -> Dobby.Infrastructure`, `Dobby.Infrastructure -> Dobby.Contracts` allowed; confirmed `Dobby.Configuration -> Dobby.Infrastructure` NOT allowed without amendment).
- ADR-0054 + ADR-0055 (full re-read; approved cipher/KDF/native binary profile captured from `storage-materialization.md`; per-secret scope mapping captured).
- Pre-login closed table set (11 tables enumerated from storage spec §3.1).
- AC-037/AC-038/AC-042/AC-052 text (full re-read from `docs/07_ACCEPTANCE_CRITERIA.md`).
- NFR-007 text (full re-read; `deviceId`-not-key invariant captured).
- Package governance rules (Tier R rejected list, Tier 0 BCL exclusion, central package management discipline verified against existing `PackageGovernanceTests`).
- Toolchain pin gate state (`TOOLCHAIN-PIN-APPROVAL-MISSING` + `TOOLCHAIN-PIN-REPRESENTATION-MISSING` resolved for Phase A; approved table/JSON consistency test requirement captured via `toolchain-pin-gate.md` re-read).
- Storage materialization decision state (`storage-materialization.md` `APPROVED`, Phase A closure by `n4hkro` on 2026-05-14, `Phase B implementation may start: YES`, approved primitive surface, no Core `ErrorCode` expansion, DB-at-rest profile, pre-login schema authority, grant-row `Permissions`, reserved post-login migration ID, Dapper selection, Argon2 deferral).
- Print boundary carry-over (`SPRINT1-INFRA-APP-PRINT-BOUNDARY` Sprint 9 sunset confirmed via `architecture-baseline.md` re-read and Sprint 1 ADR list).
- Sprint 3 runtime folder protected-material seam (confirmed via `RuntimeFolderPreparer.cs` direct read and `RuntimeFolderRecoveryTests.cs` direct read).

**Areas approved by Phase A ledgers and now consumed by this plan**:
- Storage primitive type/namespace names, local error surface, pre-login schema/migration authority, grant-row `Permissions`, reserved post-login migration ID, Windows-only adapter/TFM strategy, DB-at-rest cipher/KDF profile, native supply-chain path/evidence, provider package/asset stance, provider-proof assertion list, exact package versions/assets, Dapper selection, and Argon2 deferral.
- Core `ErrorCode` registry stance: unchanged 17-entry registry; storage-specific failures remain in local `PersistenceError`.
- Package-governance stance: `toolchain-pin-gate.md` table/JSON consistency test must be added before package-bearing implementation.

**Resolved human decisions**:
- `Permissions` schema authority is settled by decision package 2026-05-14: grant-row storage, no `GrantState`, no explicit deny storage.
- Post-login schema authority is settled by decision package 2026-05-14: Sprint 4 provides post-login migration infrastructure only; consumer sprints own their post-login schema evidence and migrations after the authenticated-session seam.
- Phase B D-01..D-12 implementation-detail decisions are approved by `n4hkro` in `decision-log.md` `phase-b-decisions-2026-05-14`; D-11 is already resolved in this plan text and is not an open architecture decision.

**Areas where this plan asserts implementation assumptions** (the implementer must verify before proceeding):
- Default placement of storage abstraction primitives on `Dobby.Infrastructure` per Cədvəl 1 Row 87 (confirm against approved table; alternative placements may exist).
- Storage primitive abstractions reference `Dobby.Contracts` types (drives the `Dobby.Infrastructure -> Dobby.Contracts` deferred-drift closure; if not, leave drift open).
- `«DurableAuditTrail»` final placement (`Dobby.Audit/Persistence/` by default because it implements `IAuditTrail`; any `Dobby.Infrastructure` implementation placement requires an explicit architecture-map amendment).

**Areas covered at `boundary/context` level only** (not load-bearing for Sprint 4 implementation):
- Branch Shared deployment mode runtime (ADR-0056/ADR-0057 — Sprint 4 keeps `RuntimeEnabled=false`).
- Plugin manifest schema (`docs/schemas/dobby-plugin-manifest.schema.json` — Sprint 8 scope).
- License import flow (`docs/specs/license-import.md` — Sprint 9 scope).
- Sample plugin Main Content hosting (`docs/specs/sample-plugin.md` — Sprint 10 scope).
- Release artifact / publish layout (`docs/09_BUILD_RUN_AND_RELEASE.md` — Sprint 11 scope).

**Reviewer focus**:
- Phase A preflight verification — the implementer MUST verify §13.1-§13.4 approved-ledger state before Phase B; reviewer should re-check both approved ledgers and the freeze-window rule BEFORE approving Sprint 4 code changes.
- `ADR-0040` §3.1 / storage spec §5a name-table fidelity — reviewer should compare every concrete type name, namespace, method signature, local `PersistenceError` sub-code and table/column name in the Sprint 4 implementation against the approved table verbatim. No invented names, no Core `ErrorCode` additions.
- Encryption proof test — reviewer should run `«EncryptedProviderProbeTests»` locally and inspect the raw DB bytes to confirm no plaintext marker leak.
- No-plaintext-secret content scan — reviewer should verify the scan covers committed files AND the runtime data root after fresh bootstrap.
- Print boundary preservation — reviewer should confirm `Dobby.Infrastructure -> Dobby.Application` edge is still present and `SPRINT1-INFRA-APP-PRINT-BOUNDARY` Sprint 9 sunset is referenced in the Reconfirmation entry.
- KDF runtime selection scope drift — reviewer should confirm Sprint 4 has NOT touched `AuthOptions.PasswordKdfMode` runtime value or `Pbkdf2PasswordHasher` wiring (Sprint 5 territory).
- Test count delta — reviewer should record exact pre/post test counts in the Sprint 4 Reconfirmation entry.

**Confidence rating**: HIGH for plan completeness against the amended `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 4 scope. HIGH for grounding against existing codebase state, prior sprint outputs, ADRs, specs, acceptance criteria, approved Phase A ledgers, and the decision package 2026-05-14. HIGH for approved primitive names, package versions/assets, Core ErrorCode no-expansion stance, grant-row `Permissions`, reserved post-login migration ID, and DB-at-rest profile. NEUTRAL only on implementation-phase method signatures and internal class names that `storage-materialization.md` deliberately leaves constrained but not pre-named.
