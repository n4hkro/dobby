# Sprint 2 Execution Plan — Core Contracts and Shared Primitives

## 1. Sprint Summary

Sprint 2 stabilizes and verifies the shared surfaces that host, SDK, and plugins are allowed to depend on: `Dobby.Core`, `Dobby.Contracts`, and `Dobby.SDK`. The Sprint 1 master plan defines the goal as "stable shared surfaces that host, SDK, and plugins consume," with explicit task groups for Core Result/Errors, Correlation/Versioning, Plugin Storage Contracts, Policy/Operation/Navigation/Events/Pages, and SDK Surface.

This is primarily a **verification, guardrail-tightening, scoped Core behavior correction, and Sprint 1 carry-over resolution** sprint. Most contract shapes already exist in the Sprint 1 skeleton; Sprint 2 closes the deferred ADR-0001 type-level enforcement gap, resolves the deferred ADR-0021 `PluginBase` drift, corrects the ADR-0004 `PolicyDecision` and ADR-0048 `OperationPermissionCapability` contract drift, corrects ADR-0029 correlation behavior and HostVersion SemVer validation, adds the missing correlation/contract default/error-mapping test coverage required by the master plan, and consumes the recorded path (a) renewal for the `SPRINT1-INFRA-APP-PRINT-BOUNDARY` blocker.

### Expected Deliverables

- **Static guardrail**: type-level Contracts/Core surface enforcement (ADR-0001) as a new integration architecture test.
- **SDK shape correction**: `PluginBase` aligned with ADR-0021 §3a — `RegisterServices` changed from `virtual` to `abstract`, `OnActivated` body trimmed to the ADR-specified no-op (empty body), with the dependent unit tests updated.
- **Core behavior closure**: scoped `CorrelationContextAccessor` no-overwrite/scope-cleanup behavior per ADR-0029 and `HostVersion.TryParse` SemVer numeric-component validation per ADR-0037/schema.
- **Test coverage closure**: correlation flow tests (ADR-0029 behavioral rules), contract default tests for plugin pages / policy / background operations / licensing runtime mode / plugin storage compartment and prefix shape / host version, full plugin-boundary error-mapping tests, SDK storage JSON helper tests, and existing operation-key parsing verification confirmation.
- **Sprint 1 carry-over decision gate**: path (a) renewal has been selected by human-authorized reconciled decision and recorded in `artifacts/release-evidence/skeleton-local/architecture-baseline.md`; Sprint 2 final evidence must preserve that bounded renewal and record the final verification run.
- **Evidence refresh**: updated `## Sprint 2 Reconfirmation` section in `architecture-baseline.md` and recorded final `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` results.

### Why This Sprint Exists

Every Sprint 3+ deliverable consumes the contract vocabulary established here. If the canonical types (Result, ErrorCode, CorrelationId, HostVersion, IPluginStorage, ICapabilityPolicy, IEventBus, IBackgroundOperationReporter, PluginRuntimeMode, IUiOrderingComparer, NavigationRoute, IPluginPage, PluginBase) are not stable and not guardrailed, every later sprint must re-plan its boundary. Sprint 1 explicitly deferred two structural items into Sprint 2 ownership; closing them here unblocks Sprint 3+ without re-litigating shape.

### Sprint Readiness

- **Verification and static-guardrail work**: ready.
- **Core correlation/versioning behavior correction**: ready; mechanically scoped to two `Dobby.Core` files before adding the corresponding tests.
- **SDK PluginBase shape correction**: ready; mechanically scoped to one file plus a test update.
- **`SPRINT1-INFRA-APP-PRINT-BOUNDARY` resolution**: ready with path (a). Human-authorized reconciled decision recorded on 2026-05-12 (Asia/Baku) renews the temporary allowance for Sprint 2 only, explicitly not as architectural approval, with Sprint 9 as the hard sunset gate. The implementation agent verifies this record at §9 Step 2 and halts only if it is missing or superseded by a later human decision.
- **Toolchain pin gate**: Sprint 1 carry-over (`TOOLCHAIN-PIN-APPROVAL-MISSING`, `TOOLCHAIN-PIN-REPRESENTATION-MISSING`) remains open for package-bearing work. Sprint 2 does not introduce or change package pins, package references, project files, or toolchain pin files, and therefore is not blocked by it. The gate must close before the first package-bearing file change; the anticipated close target is Sprint 4 storage/provider materialization if no earlier package-bearing work is introduced.

### Sprint Dependency Summary

- **Depends on**: Sprint 1 baseline (`Dobby.sln` builds clean, 50 tests green as of 2026-05-11, central package management, BOM removed, architecture-baseline.md and toolchain-pin-gate.md and auth-kdf-selection.md present).
- **Does not depend on**: any sprint after Sprint 2.
- **Does not include**: new package references, new project additions, storage provider work, authentication runtime, licensing implementation, shell/UI surface, plugin runtime/activation orchestration, sample-plugin manifest schema fixes, operation verb taxonomy expansion (deferred to Sprint 6), plugin manifest validator drift (deferred to Sprint 8).

## 2. Sources Read

The plan is grounded in the following inspected files and ADR/spec docs.

### Plan and Sprint Boundary

- `SPRINT_IMPLEMENTATION_PLAN.md` — Sprint 2 task groups (Core Result/Errors, Correlation/Versioning, Plugin Storage Contracts, Policy/Operation/Navigation/Events/Pages, SDK Surface) and Sprint 2 verification (unit tests + static dependency check + plugin-facing API surface review).
- `SPRINT_1_EXECUTION_PLAN.md` — verified scope/out-of-scope boundaries and deferred drift items handed off to Sprint 2.

### Doctrine

- `docs/02_ARCHITECTURE_MAP.md` — Cədvəl 1 rows for Core, Contracts, SDK, PluginFramework, Application, Infrastructure; Cədvəl 2 dependency map; Cədvəl 3 risk register including `Dobby.Contracts` row, plugin storage row, plugin/Contracts/SDK rows, print boundary row.
- `docs/03_TECH_STACK_AND_VERSIONS.md` — toolchain/package governance context; Sprint 2 uses it only to confirm that package-bearing work remains gated and out of scope.
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md` (referenced through Sprint 1 baseline ErrorCode test) — Result/error policy and the 17-entry baseline error registry.
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-009`, `AC-015`, `AC-043` boundary references; Sprint 2 supports but does not own a P1 release-blocker acceptance.
- `docs/08_CODING_STANDARDS.md` — namespace-mapping table for `Dobby.Core.Versioning`, `Dobby.Contracts.PluginStorage`, `Dobby.Contracts.PluginPages`, `Dobby.SDK.PluginFramework`, `Dobby.Messaging.UiDispatch`, `Dobby.Application.Printing`, `Dobby.Infrastructure.Printing`; banned dumping-ground segments; XAML attribute rule (not Sprint 2).
- `docs/09_BUILD_RUN_AND_RELEASE.md` — package/build/release governance; confirms package-bearing edits require explicit approval and remain outside Sprint 2.
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — through architecture-baseline reference; the type-level allowed surface for Contracts referencing Core/BCL is the Sprint 2 deferred guardrail.
- `docs/adr/ADR-0003-plugin-storage-boundary-and-access-model.md` — plugin-visible storage contract origin; Sprint 2 uses ADR-0030 as the current host-persistence/storage-boundary refinement for the same surface.
- `docs/adr/ADR-0004-capability-enforcement-model.md` — capability policy contract source for `ICapabilityPolicy<TCapability>`, `PolicyDecision`, `PolicyContext`, and policy-default tests.
- `docs/adr/ADR-0005-ui-ordering-model.md` — plugin-visible ordering contract source for `UiOrderingKey` and `IUiOrderingComparer`.
- `docs/adr/ADR-0010-plugin-communication-policy.md` — plugin communication contract source for `PublicIntegrationEvent` and `IEventBus`.
- `docs/adr/ADR-0013-routing-and-deep-link-model.md` — route/page identity source for plugin page descriptor/context tests.
- `docs/adr/ADR-0015-background-operation-reporting.md` (referenced via existing `BackgroundOperationSnapshot`) — Phase 1 background operation snapshot/progress shape.
- `docs/adr/ADR-0021-sdk-surface-area.md` (referenced via Sprint 1 deferred drift) — `PluginBase` canonical shape with abstract `RegisterServices`.
- `docs/adr/ADR-0029-correlation-id-ownership.md` — behavioral rules: generator only in new scope, no overwrite, async-flow propagation, explicit pass to background tasks, scope cleanup.
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — plugin storage taxonomy (`SharedDomainStorage`, `UserScopedStorage`) and host persistence isolation rule.
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — background-only manifest version pattern source; Sprint 2 does not modify manifest validation, but HostVersion tests reuse the documented numeric version shape.
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — host/plugin version compatibility source for `HostVersion` parsing and ordering behavior.
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md` — Infrastructure dependency direction; print classifier placement context for the carry-over blocker.
- `docs/adr/ADR-0048-operation-based-authorization.md` (referenced via Sprint 1 schema work) — capability payload shape `OperationPermissionCapability(string OperationKey)`; canonical verb list is host-side and out of Sprint 2 scope.
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` — plugin page/main-content runtime contract shape for `PluginPageContext`, `PluginPageDescriptor`, and `IPluginPage`.
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` (referenced via existing `PluginRuntimeMode`) — non-disclosing runtime mode surface (`FullAccess`, `ReadOnlyExpired`, `Denied`).

### Schemas and Samples

- `docs/schemas/dobby-plugin-manifest.schema.json` — verb enum (10 values, host-side), capability scopes, extension targets; informs what NOT to change in Sprint 2.
- `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` and `samples/Dobby.Sample.Plugin/SamplePlugin.cs` — confirmed sample is plugin-facing API consumer; Sprint 2 leaves the sample manifest untouched, but modifies `SamplePlugin.cs` per §8.5 to remove the obsolete `base.RegisterServices` call after `PluginBase.RegisterServices` becomes abstract.

### Source

- `src/Dobby.Core` (Result, ErrorCode, CorrelationId, Versioning) — read through Sprint 1 test references and Sprint 2 review; ErrorCode registry currently 17 entries, locked by `Source_project_references_are_subset_of_architecture_map` and the ErrorCode equality test in `ArchitectureDependencyBoundaryTests.cs`. Sprint 2 modifies `CorrelationId.cs` for ADR-0029 no-overwrite/scope-cleanup behavior and `Versioning/HostVersion.cs` for SemVer numeric-component validation.
- `src/Dobby.Contracts/**` — plugin-facing surfaces (Events, PluginFramework, PluginPages, PluginStorage, Policies, Licensing, Ordering, BackgroundOperations) confirmed present in current Sprint 1 baseline, including `IPluginStorage` compartment properties, `IPluginStorageCompartment`, `PluginStorageKey`, `PluginStorageKeyPrefix`, and `PluginStorageValue`.
- `src/Dobby.SDK/PluginFramework/PluginBase.cs` — Sprint 1 deferred drift target (`RegisterServices` is `virtual`; ADR-0021 requires abstract).
- `src/Dobby.SDK/PluginStorage/PluginStorageJsonExtensions.cs` — public SDK storage contract convenience wrapper; Sprint 2 verifies the existing shape and error behavior without changing the helper unless tests reveal a defect in current documented behavior.
- `src/Dobby.PluginFramework/Dobby.PluginFramework.csproj`, `Manifest/PluginManifest.cs`, `Manifest/PluginManifestValidator.cs`, `Activation/PluginDependencySorter.cs`, `Activation/PluginExceptionBoundary.cs` — host-internal, not Sprint 2 work; identified as Sprint 6/Sprint 8 scope.
- `src/Dobby.Security/Authorization/OperationKey.cs` — host-side; current verb enum lacks `configure` and `grant`; explicit Sprint 6 carry-over per Sprint 1 deferred drift.
- `src/Dobby.Navigation/RouteRegistry.cs`, `DefaultUiOrderingComparer.cs` — host-side navigation; existing Sprint 1 tests already cover documented behavior.
- `src/Dobby.Messaging/InMemoryEventBus.cs`, `src/Dobby.Licensing/LicenseRuntimeModeMapper.cs`, `src/Dobby.Application/BackgroundOperationReporter.cs`, `src/Dobby.Application/Printing/PrintTargetClassifier.cs` — Sprint 1 implementation; touched only under a revised path (b) print-boundary migration scope where the Infrastructure→Application reference must be removed (this migration is OUT of Sprint 2 default scope).
- `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` — has reference to `Dobby.Application`; sole source of `SPRINT1-INFRA-APP-PRINT-BOUNDARY`.

### Tests and Evidence

- `tests/Dobby.UnitTests/**` (Core/ResultTests.cs, Contracts/PluginFrameworkContractTests.cs, Contracts/PluginStorageContractTests.cs, Navigation/NavigationTests.cs, Security/SecurityTests.cs, Messaging/InMemoryEventBusTests.cs, Licensing/LicensingTests.cs, PluginFramework/PluginFrameworkTests.cs, Diagnostics/DiagnosticsAndAuditTests.cs, Configuration/ConfigurationTests.cs, Shell/WorkSessionRegistryTests.cs) — existing 50-test baseline.
- `tests/Dobby.IntegrationTests/**` (ProjectBoundaryTests.cs, Architecture/ArchitectureDependencyBoundaryTests.cs, Architecture/EncodingBoundaryTests.cs, Architecture/PackageGovernanceTests.cs, Architecture/PluginAndSdkBoundaryTests.cs, Architecture/ReleaseEvidenceTests.cs).
- `tests/Dobby.SmokeTests/ShellSmokeTests.cs` and `tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj` — existing smoke baseline kept green by the full-solution verification gates.
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` — Sprint 1 reconfirmation contract for the print-boundary blocker.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` — Sprint 1 toolchain pin gate.
- `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md` — Sprint 1 KDF evidence (untouched in Sprint 2).

### Build Configuration

- `Directory.Build.props` — `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=14.0`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`.
- `Directory.Packages.props` — central package management; Sprint 2 does not add or modify pins.
- `global.json` — SDK 10.0.202 pinned with `rollForward=latestPatch`.
- `Dobby.sln` — confirmed project graph; Sprint 2 does not add or rename solution entries.

## 3. Sprint 2 Scope (Master Plan Verbatim)

From the master `SPRINT_IMPLEMENTATION_PLAN.md`, Sprint 2 owns:

- **Core Result and Errors**: implement `Result` shape, expected failure mapping, cancellation mapping, and baseline error codes per `04_RESULT_VS_EXCEPTION_POLICY.md`.
- **Correlation and Versioning**: implement `CorrelationId`, generator/context accessor, and `HostVersion` provider contract.
- **Plugin Storage Contracts**: implement `ADR-0003` plugin-visible `IPluginStorage`, `SharedDomainStorage`, and `UserScopedStorage` contract shape (no provider implementation).
- **Policy, Operation, Navigation, Events, and Pages**: implement capability policy contracts, operation target vocabulary, ordering keys, route/page descriptors, event bus contract, background operation snapshots, and `ADR-0053` licensing runtime surface contracts (`PluginRuntimeMode`, `ICapabilityLicensing.GetCurrentMode()`).
- **SDK Surface**: provide only documented SDK helpers and `PluginBase` behavior per `ADR-0021`; no host capability creation, no logging/audit/telemetry channels, no raw license surface, no service locator.

**Sprint 2 verification (master plan)**: unit tests for Result, error mapping, correlation flow, ordering comparer, route identity, operation key parsing, and contract defaults; static dependency check for Contracts and SDK references; review of plugin-facing API surface to confirm absence of WPF types.

**Sprint 2 acceptance support (master plan)**: Sprint 2 does not own a primary AC. Contracts reference only allowed Core/BCL surfaces; SDK does not expose host internals; plugin-facing APIs contain no WPF types; error and result handling matches documented policy.

## 4. Sprint 2 Boundary Application

### Already Present from Sprint 1 Baseline (Sprint 2 verifies, does not re-create)

- `Dobby.Core`: `Result`, `Result<T>`, `ErrorCode` (17-entry baseline, locked by integration test), `CorrelationId`, `ICorrelationIdGenerator`, `ICorrelationContextAccessor`, `CorrelationContextAccessor`, `Dobby.Core.Versioning.HostVersion`, `IHostVersionProvider`/`FixedHostVersionProvider`. The shapes are present; Sprint 2 corrects `CorrelationContextAccessor` behavior and `HostVersion.TryParse` validation before adding tests.
- `Dobby.Contracts`: `Dobby.Contracts.Events.PublicIntegrationEvent`, `IEventBus`; `Dobby.Contracts.PluginFramework.IPluginModule`, `IPluginServiceCollection`, `PluginActivationContext`, `PluginLifetime`; `Dobby.Contracts.PluginPages.IPluginPage`, `IPluginPageModel`, `PluginPageContext`, `PluginPageDescriptor`; `Dobby.Contracts.PluginStorage.IPluginStorage` with `SharedDomainStorage` and `UserScopedStorage` properties typed as `IPluginStorageCompartment`, plus `IPluginStorageCompartment`, `PluginStorageKey`, `PluginStorageKeyPrefix`, `PluginStorageValue`; `Dobby.Contracts.Policies.ICapabilityPolicy<TCapability>`, `PolicyDecision`, `PolicyContext`, `OperationPermissionCapability`; `Dobby.Contracts.Licensing.PluginRuntimeMode`, `ICapabilityLicensing`; `Dobby.Contracts.Ordering.UiOrderingKey`, `IUiOrderingComparer`; `Dobby.Contracts.BackgroundOperations.BackgroundOperationSnapshot`, `BackgroundOperationProgress`, `IBackgroundOperationReporter`, `IBackgroundOperationCancellation`. `PolicyDecision` and `OperationPermissionCapability` are present but Sprint 2 corrects their ADR-0004/ADR-0048 record shapes before adding default tests.
- `Dobby.SDK`: `Dobby.SDK.PluginFramework.PluginBase` (currently `virtual RegisterServices` — Sprint 2 corrects) and `Dobby.SDK.PluginStorage.PluginStorageJsonExtensions` (Sprint 2 verifies existing helper behavior; no shape change planned).

### Sprint 2 New / Modified Artifacts

| Item | Action | Reason |
|---|---|---|
| `src/Dobby.Core/CorrelationId.cs` | Modify (`CorrelationContextAccessor` no-overwrite/scope-cleanup behavior) | Sprint 2 owns correlation primitives; ADR-0029 behavioral rules must be implemented before adding tests. |
| `src/Dobby.Core/Versioning/HostVersion.cs` | Modify (SemVer numeric-component validation) | Sprint 2 owns HostVersion contract; schema/ADR-0037 reject leading-zero numeric segments. |
| `src/Dobby.Contracts/Policies/PolicyContracts.cs` | Modify (`PolicyDecision` ADR-0004 and `OperationPermissionCapability` ADR-0048 shape corrections) | Sprint 2 owns capability policy contracts; ADR-0004 defines `PolicyDecision` as `readonly record struct PolicyDecision(bool Allowed, string? DenyReason)`, and ADR-0048 defines `OperationPermissionCapability` as `readonly record struct OperationPermissionCapability(string OperationKey)`. |
| `src/Dobby.SDK/PluginFramework/PluginBase.cs` | Modify (`RegisterServices` `virtual` → `abstract`; `OnActivated` body trimmed to ADR-0021 §3a no-op) | ADR-0021 §3a `PluginBase` shape; Sprint 1 deferred `RegisterServices` drift plus the `OnActivated` body deviation observed during Sprint 2 planning. |
| `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs` | Modify (`MinimalPlugin` concrete override; add `OnActivated` default-no-op assertion) | Compile-time dependent on abstract change; verifies ADR-0021 §3a no-op shape after §8.3 trims the body. |
| `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` | Modify (complete plugin exception/error mapping coverage) | Master Sprint 2 verification requires error mapping; current test covers only `NotSupportedException`. |
| `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` | Modify (add route identity coverage for canonical built-in route keys) | Master Sprint 2 verification explicitly includes route identity; existing fallback coverage is not route identity verification. |
| `samples/Dobby.Sample.Plugin/SamplePlugin.cs` | Modify (remove `base.RegisterServices` call) | Compile-time dependent on abstract `PluginBase.RegisterServices`; sample manifest remains untouched. |
| `tests/Dobby.UnitTests/Core/CorrelationContextAccessorTests.cs` | Create | Sprint 2 verification: correlation flow tests required by master plan; ADR-0029 behavioral rules. |
| `tests/Dobby.UnitTests/Core/HostVersionTests.cs` | Create | Sprint 2 verification: contract defaults; complements existing single `ResultTests` host-version parse case. |
| `tests/Dobby.UnitTests/Contracts/PluginContractDefaultsTests.cs` | Create | Sprint 2 verification: contract defaults across PluginPages, Policies, BackgroundOperations, Licensing surface, and plugin storage compartment/prefix shape. |
| `tests/Dobby.UnitTests/SDK/PluginStorageJsonExtensionsTests.cs` | Create | Sprint 2 SDK surface verification: storage JSON helper round-trip and error behavior per ADR-0021 SDK convenience wrapper scope. |
| `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs` | Create | Sprint 2 owns the ADR-0001 type-level Contracts/Core surface guardrail deferred from Sprint 1. |
| `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | Modify after human decision (Sprint 2 Reconfirmation section + Static Guardrail Status verification line) | Reflect the selected human path for `SPRINT1-INFRA-APP-PRINT-BOUNDARY` and record final Sprint 2 verification run. |
| `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` | Conditional only | Path (a): no change; path (b): remove the temporary allowance only after a revised green migration scope removes the edge; path (c): update only with the human-authorized architecture-map/ADR resolution. |
| `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj` | Conditional only | Path (a): no change; path (b): remove `Dobby.Application` reference only under a revised green migration scope; path (c): no change only if the architecture map legally allows the edge. |

### Excluded from Sprint 2 (Out of Scope)

- Plugin manifest validator code or shape (`PluginManifest`, `PluginManifestValidator`) — Sprint 8 (Plugin Package Trust and Activation).
- `OperationVerb` enum expansion to include `configure` and `grant` — Sprint 6 (Operation Authorization).
- Sample plugin manifest schema-required fields (`vendorId`, `entryAssembly`, `displayName`, `description`, `files`, `signature` shape, `capabilities[].name`) — Sprint 8.
- Storage provider implementation, SQLite/DPAPI wiring, protected-storage primitive selection — Sprint 4 (Configuration and Local Storage), Sprint 5 (Auth and Session), Sprint 11 (Storage Protection).
- Licensing runtime enforcement, license import flow, signature verification — Sprint 9.
- Shell composition, WPF host, navigation runtime, settings UI — Sprint 7.
- Plugin discovery, ALC isolation, activation lifecycle, plugin install flow — Sprint 8.
- New package references in `Directory.Packages.props`.
- New projects, new solution entries.
- Any change to `Dobby.PluginFramework.csproj`, `Dobby.Security.csproj`, `Dobby.Application.csproj`, `Dobby.Licensing.csproj`, `Dobby.Messaging.csproj`, `Dobby.Audit.csproj`, `Dobby.Diagnostics.csproj`, `Dobby.Localization.csproj`, `Dobby.UI.csproj`, `Dobby.Shell.csproj`, `Dobby.Admin.csproj`, `Dobby.Host.csproj`, `Dobby.Navigation.csproj`, `Dobby.Configuration.csproj` (except under a revised, human-authorized print-boundary-resolution scope).
- Any change to `Directory.Build.props`, `global.json`, `Dobby.sln`.

## 5. Sprint 1 Carry-Over Items

### 5.1 `SPRINT1-INFRA-APP-PRINT-BOUNDARY` (architecture exception)

**Status from Sprint 1**: open evidence-bound blocker. The current `Dobby.Infrastructure → Dobby.Application` reference conflicts with `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 and ADR-0040. The architecture-test allowance for this edge is conditioned on `architecture-baseline.md` containing both the blocker token `SPRINT1-INFRA-APP-PRINT-BOUNDARY` and the heading `## Sprint 2 Reconfirmation`.

**Sprint 1 reconfirmation contract** (from `architecture-baseline.md` line 47-49): "At Sprint 2 start, the architecture-baseline pass must either (a) renew `SPRINT1-INFRA-APP-PRINT-BOUNDARY` by human confirmation, (b) remove the named architecture-test exception and let the `Dobby.Infrastructure` to `Dobby.Application` edge fail, or (c) record the human-authorized ADR-0040 or architecture-map amendment that changes the boundary."

**Sprint 2 disposition**: **Path (a) selected by human-authorized reconciled decision.** The Sprint 1 reconfirmation contract (`architecture-baseline.md` line 49) requires one of paths (a), (b), or (c) at Sprint 2 start; no fourth ("deferred / unresolved") state is permitted for implementation. The reviewable decision record now exists in `architecture-baseline.md` under `## Sprint 2 Reconfirmation`: path (a) renews the temporary allowance for Sprint 2 only, explicitly not as architectural approval, with Sprint 9 as the hard sunset gate. The Sprint 2 implementation agent must verify this record at §9 Step 2 and halt only if the record is missing, contradicted, or superseded by a later human decision. The agent must in particular confirm the recorded path before:

- Modifying `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`.
- Modifying the `## Sprint 2 Reconfirmation` section of `artifacts/release-evidence/skeleton-local/architecture-baseline.md` as renewed, consumed, resolved, or documented for a later sunset gate.
- Modifying `src/Dobby.Infrastructure/Dobby.Infrastructure.csproj`.
- Modifying `src/Dobby.Application/Printing/PrintTargetClassifier.cs`.

**Branch outcomes**:

- **Path (a) Renew — selected**: human-authorized reconciled decision confirms continued allowance for Sprint 2 only; Sprint 2 final evidence preserves the `## Sprint 2 Reconfirmation` section, records final verification status, and keeps the hard sunset gate at Sprint 9, where `AC-021`/`ADR-0053` print-classification deny test ownership lives. Sprint 9 must also complete `IPrintTargetClassifier` ADR-0053 host-enforcement/DI consumption alongside the named architecture-test allowance removals. This is not architectural approval of the `Dobby.Infrastructure → Dobby.Application` edge. No Sprint 2 test or csproj change.
- **Path (b) Sunset**: human authorizes removing the temporary allowance. Sprint 2 may not end with a red architecture gate. The implementation agent must either halt for a revised execution plan that expands Sprint 2 to include the required print classifier migration, or leave the blocker unchanged and defer sunset to a follow-up sprint. Removing the reference requires migrating `IPrintTargetClassifier`/`PrintTargetDescriptor`/`PrintTargetClass` per ADR-0040 row-table; this migration is OUT of Sprint 2 default scope and would convert Sprint 2 into a print-boundary-resolution sprint.
- **Path (c) Amendment**: human authorizes an architecture-map amendment to legally allow the edge; an ADR-0040 amendment may accompany it, but cannot replace the architecture-map change while `ArchitectureDependencyBoundaryTests.LoadArchitectureMapReferences` remains the checked authority. Sprint 2 updates `docs/02_ARCHITECTURE_MAP.md` Cədvəl 2 under human approval, then removes `IsTemporaryPrintBoundaryEdge`/`PrintBoundaryBlockerIsRecorded` because the edge is now natively permitted by the architecture-map driven parser. **Doc edits in Sprint 2 must be human-authorized; the agent does not invent architecture-map row changes.** If humans want ADR-0040 alone to be authoritative, Sprint 2 must first be revised to include the parser/test/documentation changes that make ADR-0040 a checked authority.

**Behavior if this decision is missing from implementation context**: Sprint 2 is blocked until the human-authorized path (a) choice is restored to the implementation context. The implementation agent does not invent a renewal, does not switch paths, does not modify the test allowance, and does not edit `architecture-baseline.md`'s reconfirmation section without the recorded human-authorized decision.

### 5.2 ADR-0001 Type-Level Contracts/Core Surface Enforcement

**Status from Sprint 1** (`architecture-baseline.md` line 27): "ADR-0001 type-level Contracts/Core surface enforcement remains Sprint 2-owned deferred guardrail work."

**Sprint 2 disposition**: implement `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs` (see §10.7).

### 5.3 PluginBase ADR-0021 Drift

**Status from Sprint 1** (`architecture-baseline.md` line 28): "`PluginBase.RegisterServices` current code shape differs from ADR-0021 and is recorded as deferred SDK drift."

**Additional drift observed during Sprint 2 planning**: `src/Dobby.SDK/PluginFramework/PluginBase.cs` `OnActivated` body throws via `ArgumentNullException.ThrowIfNull(context)` rather than the ADR-0021 §3a no-op (empty body). Sprint 1 deferred-drift did not capture this, but it is the same ADR section as the `RegisterServices` deviation, and master Sprint 2 ownership of "documented SDK helpers and `PluginBase` behavior per ADR-0021" (`SPRINT_IMPLEMENTATION_PLAN.md:112-114`) covers it.

**Sprint 2 disposition**: align both `RegisterServices` (`virtual` → `abstract`) and `OnActivated` (trim body to ADR-0021 §3a no-op) with ADR-0021 in the same atomic edit set, and update the dependent unit tests (see §9 Step 5, §8.3, §8.4, §10.6).

### 5.4 Plugin Manifest Schema/Code/Sample Drift

**Status from Sprint 1** (`architecture-baseline.md` line 29): "Plugin manifest schema/code/sample drift, including the schema-required `capabilities[].name` field, is deferred to the plugin/package implementation sprint."

**Sprint 2 disposition**: NOT Sprint 2 work. Sprint 8 owns this. Sprint 2 must not modify `PluginManifest.cs`, `PluginManifestValidator.cs`, the sample manifest, or the schema.

### 5.5 Operation Verb Taxonomy Drift

**Status from Sprint 1** (`architecture-baseline.md` line 30): "Operation verb taxonomy drift is deferred to the authorization/plugin-manifest implementation sprint."

**Sprint 2 disposition**: NOT Sprint 2 work. Sprint 6 owns this. Sprint 2 must not modify `OperationVerb`, `OperationVerbParser`, or `OperationKey`.

### 5.6 Toolchain Pin Gate

**Status from Sprint 1** (`toolchain-pin-gate.md`): two blockers (`TOOLCHAIN-PIN-APPROVAL-MISSING`, `TOOLCHAIN-PIN-REPRESENTATION-MISSING`).

**Sprint 2 disposition**: carry over with a first-change trigger. The master plan and gate evidence block package-bearing work until approval/representation are resolved, but this execution plan performs no package-bearing work: §15 excludes package references, project additions, `Directory.Packages.props`, `Directory.Build.props`, `global.json`, and `Dobby.sln` changes. The gate must close before the first package-bearing file change in any later sprint. Package-bearing file changes include `Directory.Packages.props`, `Directory.Build.props`, `global.json`, any `.csproj` `PackageReference` change, or a new toolchain pin-list / stop-condition representation file. The anticipated close target is Sprint 4 storage/provider materialization if no earlier package-bearing work is introduced; Sprint 2 final evidence notes the carry-over status as-is.

### 5.7 SDK Localization Reference Absence

**Status from Sprint 1** (`architecture-baseline.md` line 44): "`Dobby.SDK` currently lacks the allowed `Dobby.Localization` reference from ADR-0021 and the architecture dependency table; this is recorded only as observed current graph evidence, not as SDK completeness proof or a required Sprint 1 fix."

**Sprint 2 disposition**: Sprint 2 does NOT add the Localization reference. ADR-0021 allows it, but adding it without an SDK consumer is shape inflation. Sprint 2 keeps the SDK reference set minimal (Core, Contracts). Later sprints add `Dobby.Localization` to SDK only when an SDK helper requires it.

## 6. Task Decomposition

Sprint 2 work is organized into thirteen tasks. Tasks are ordered so each builds on confirmed state.

| # | Task | Risk Surface | Blocks |
|---|---|---|---|
| T1 | Confirm Sprint 1 baseline is green (restore/build/test/format) | None — read-only verification | All later tasks |
| T2 | Verify reviewable human-authorized path (a) record for `SPRINT1-INFRA-APP-PRINT-BOUNDARY` | Architecture-baseline test dependency; implementation is blocked only if the path (a) record is missing, contradicted, or superseded | All later tasks |
| T3 | Implement ADR-0029 `CorrelationContextAccessor` no-overwrite/scope-cleanup behavior | Core primitive behavior; must stay minimal and not create a new correlation namespace | T6 |
| T4 | Implement `HostVersion.TryParse` SemVer numeric-component validation | Core versioning primitive; rejects leading-zero segments per schema/ADR-0037 | T7 |
| T5 | Align `PluginBase` with ADR-0021 §3a (`RegisterServices` `virtual` → `abstract`; `OnActivated` body trimmed to no-op) and update dependent test/sample code | Compile dependency on test fixture and sample plugin | T13 |
| T6 | Add `CorrelationContextAccessorTests` for ADR-0029 behavioral rules | New unit test file over the corrected Core behavior | T13 |
| T7 | Add `HostVersionTests` for parser/comparison/`FixedHostVersionProvider` defaults | New unit test file over the corrected Core behavior | T13 |
| T8 | Align ADR-0004 `PolicyDecision` and ADR-0048 `OperationPermissionCapability` shapes and add `PluginContractDefaultsTests` for PluginPages/Policies/BackgroundOperations/Licensing defaults and plugin storage contract shape | Plugin-facing Contracts surface; source edit plus new unit test file | T13 |
| T9 | Add `PluginStorageJsonExtensionsTests` for SDK storage helper behavior | New unit test file over existing SDK helper; no shape change planned | T13 |
| T10 | Add `ContractsCoreSurfaceTests` (ADR-0001 type-level enforcement) | Reflection over `Dobby.Contracts.dll`; must not assert on transitively-loaded BCL types | T13 |
| T11 | Expand `PluginFrameworkTests` plugin exception/error mapping coverage | Existing unit test file; verifies all documented mappings from `docs/04_RESULT_VS_EXCEPTION_POLICY.md` | T13 |
| T12 | Update `NavigationTests` route identity coverage | Existing unit test file; verifies ADR-0013 route-key identity rather than only fallback behavior | T13 |
| T13 | Update `architecture-baseline.md` Sprint 2 evidence (Static Guardrail Status + Sprint 2 Reconfirmation outcome) | Evidence rendering only after T2 has a reviewable human path choice | None |

## 7. Files to Create

### 7.1 `tests/Dobby.UnitTests/Core/CorrelationContextAccessorTests.cs`

- **Project**: `Dobby.UnitTests`.
- **Namespace**: `Dobby.UnitTests.Core`.
- **Purpose**: Verify ADR-0029 behavioral rules for `CorrelationContextAccessor` and `ICorrelationIdGenerator`. Concrete behaviors covered:
  - A new correlation id is generated by `ICorrelationIdGenerator.New()` and round-trips through `CorrelationId.Equals`.
  - Setting `CorrelationContextAccessor.Current` then reading it on the same async-flow returns the same value.
  - When `Current` is already set, assigning a different non-null value through the setter is rejected with `InvalidOperationException` and the existing value remains unchanged.
  - When `Current` is already set, attempting to push a new scope does not silently overwrite the existing value; assertions target the scoped ADR-0029 behavior implemented in §8.1.
  - When a scope completes, `Current` is restored to the previous value or cleared to null when no previous value existed.
  - Async-flow propagation: setting `Current`, awaiting a `Task.Yield()` inside an `async` method, and reading `Current` returns the same value.
- **Dependencies**: `Dobby.Core` only.
- **Length**: ~80-120 lines.

### 7.2 `tests/Dobby.UnitTests/Core/HostVersionTests.cs`

- **Project**: `Dobby.UnitTests`.
- **Namespace**: `Dobby.UnitTests.Core`.
- **Purpose**: Cover `HostVersion` and `FixedHostVersionProvider`/`IHostVersionProvider` defaults beyond the single `ResultTests.Host_version_rejects_prerelease_and_build_metadata` case. Concrete behaviors:
  - `HostVersion.TryParse` accepts canonical `Major.Minor.Patch`; rejects four-segment versions, leading-zero segments (per the schema regex pattern), empty input, whitespace-only input, component/edge whitespace, signed components, leading or trailing dots.
  - `HostVersion.CompareTo` orders correctly across major, minor, and patch boundaries (e.g., `1.0.0 < 1.0.1 < 1.1.0 < 2.0.0`).
  - `HostVersion.Equals` is value-equality and is symmetric.
  - `FixedHostVersionProvider.Current` returns the constructor value.
- **Dependencies**: `Dobby.Core` only.
- **Length**: ~80 lines.

### 7.3 `tests/Dobby.UnitTests/Contracts/PluginContractDefaultsTests.cs`

- **Project**: `Dobby.UnitTests`.
- **Namespace**: `Dobby.UnitTests.Contracts`.
- **Purpose**: Cover contract default behavior referenced by master Sprint 2 verification but not currently tested:
  - `PluginPageDescriptor` and `PluginPageContext` instantiation with documented field shape (no implicit defaults, all required fields present).
  - `PolicyDecision` matches ADR-0004 shape (`readonly record struct PolicyDecision(bool Allowed, string? DenyReason)`); `PolicyDecision.Allow()` and `PolicyDecision.Deny(...)` factories produce the documented `Allowed` and `DenyReason` values; `OperationPermissionCapability` matches ADR-0048 shape (`readonly record struct OperationPermissionCapability(string OperationKey)`) and carries the key verbatim.
  - `IPluginStorage` exposes `SharedDomainStorage` and `UserScopedStorage` properties typed as `IPluginStorageCompartment`; no test treats those property names as standalone concrete types.
  - `PluginStorageKeyPrefix.Create` accepts the documented empty-prefix case and rejects invalid non-empty prefixes using the same validation boundary as `PluginStorageKey.Create`.
  - `BackgroundOperationProgress.Determinate(int percent)` accepts values in `[0, 100]` and rejects out-of-range; `Indeterminate` constructs without a percent; `BackgroundOperationSnapshot` carries id/owner/status/progress/correlationId/failure reason without mutation.
  - `PluginRuntimeMode` enum exposes exactly `FullAccess`, `ReadOnlyExpired`, `Denied` and no additional values (mirrors ADR-0053 closed surface). The test enumerates `Enum.GetValues<PluginRuntimeMode>()` and asserts equality with the documented set.
- **Dependencies**: `Dobby.Contracts`, `Dobby.Core` only.
- **Length**: ~120-160 lines.
- **Note on shape verification before adding tests**: The implementation agent must first read each Contracts file to confirm the exact present shape of factories/records. `PolicyDecision` and `OperationPermissionCapability` are known ADR drift targets and are corrected in §8.9 before these tests are added. Other assertions match the current documented shape; this task is not a broader contract-redesign task.

### 7.4 `tests/Dobby.UnitTests/SDK/PluginStorageJsonExtensionsTests.cs`

- **Project**: `Dobby.UnitTests`.
- **Namespace**: `Dobby.UnitTests.SDK`.
- **Purpose**: Verify the existing public SDK storage helper surface from `src/Dobby.SDK/PluginStorage/PluginStorageJsonExtensions.cs` without redesigning it:
  - `SetJsonAsync` serializes a simple DTO and delegates the resulting bytes to `IPluginStorageCompartment.SetAsync`.
  - `GetJsonAsync` deserializes previously stored JSON bytes and returns the expected value.
  - `GetJsonAsync` returns a failure for an unsuccessful `IPluginStorageCompartment.GetAsync`, preserving the upstream `ErrorCode`.
  - `GetJsonAsync` maps invalid JSON to `ErrorCode.SerializationFailed`.
- **Dependencies**: `Dobby.SDK`, `Dobby.Contracts`, `Dobby.Core`, BCL `System.Text.Json` only.
- **Length**: ~80-120 lines.
- **Out of scope**: no storage provider implementation, no new serializer abstraction, no new package reference, no change to the SDK helper shape unless the test exposes a current behavior defect against the documented helper contract. The defensive fallback branch for a missing upstream error code is not a test obligation because public `Result<T>` failures always carry an `ErrorCode`.

### 7.5 `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`

- **Project**: `Dobby.IntegrationTests`.
- **Namespace**: `Dobby.IntegrationTests.Architecture`.
- **Purpose**: ADR-0001 type-level enforcement plus plugin-facing UI isolation. Scan `Dobby.Contracts.dll` via `System.Reflection` and assert every reference from a `Dobby.Contracts.*` type to a `Dobby.Core.*` type targets a member of the documented allow set below. The same file also asserts the Contracts assembly exposes no WPF/UI-only dependency.
  - `Dobby.Core.Result`
  - `Dobby.Core.Result\`1` (generic `Result<T>`)
  - `Dobby.Core.ErrorCode`
  - `Dobby.Core.CorrelationId`
  - `Dobby.Core.ICorrelationIdGenerator`
  - `Dobby.Core.ICorrelationContextAccessor`
  - `Dobby.Core.Versioning.HostVersion`
  - `Dobby.Core.Versioning.IHostVersionProvider`
- **Implementation approach**:
  - Use `typeof(Dobby.Contracts.Events.IEventBus).Assembly` (or any anchor type in `Dobby.Contracts`) to load the assembly without an explicit project reference change (Dobby.Contracts is transitively referenced via Dobby.PluginFramework which is already a project reference in `Dobby.IntegrationTests.csproj`).
  - Enumerate `Assembly.GetTypes()` for the Contracts assembly. For each type, walk: base type, implemented interfaces, generic arguments, field types, property types, method parameter types, method return types, event handler types, custom attribute argument types, and constructor parameter types.
  - For each referenced type, recursively unwrap generic arguments, pointer/by-ref/array element types, and nullable value-type wrappers, then check whether the resolved type lives in any namespace under `Dobby.Core` (i.e., `Dobby.Core` or any subnamespace).
  - If yes, the resolved type's full name must be in the allow set. Otherwise add to the `violations` list as `"{Contracts type}.{member}: references {Core type} which is outside the ADR-0001 allowed surface"`.
  - Also scan assembly references and reflected member types for WPF/UI-only surface (`PresentationFramework`, `WindowsBase`, `System.Xaml`, and WPF-only namespaces/types). Any such reference fails `Contracts_assembly_has_no_wpf_or_ui_dependency`.
  - BCL types (`System.*`, `System.Collections.*`, `System.Threading.*`, `System.IO.*`, etc.) are allowed and skipped.
  - Compiler-generated types (`<>c__DisplayClass*`, anonymous types, async state machines) are skipped via `Type.IsSpecialName`/`<>` name check or `[CompilerGenerated]` attribute presence.
- **Dependencies**: BCL `System.Reflection` only. No new package.
- **Length**: ~150-200 lines.
- **Out of scope for this test**: SDK-to-Core surface enforcement (master plan asks for static dependency check on SDK references, which is already provided by `PluginAndSdkBoundaryTests.Sdk_project_references_stay_within_documented_boundary` at csproj-graph level for Sprint 2; type-level SDK surface enforcement is not in the Sprint 1 deferred list and is therefore not Sprint 2 work).

## 8. Files to Modify

### 8.1 `src/Dobby.Core/CorrelationId.cs`

- **Change**: extend the existing `CorrelationContextAccessor` surface minimally so ADR-0029 no-overwrite and scope-cleanup behavior is implementable and testable without adding a new project or namespace. The implementation should keep `ICorrelationContextAccessor.Current` as the plugin-visible/current-value contract and add a concrete `IDisposable PushScope(CorrelationId id)` member on `CorrelationContextAccessor`. `PushScope` sets the current id only when no current id is present, rejects an existing non-null current id instead of silently overwriting it, and restores the previous value when the returned scope is disposed. The `Current` setter remains for current-value compatibility, but attempts to set a different non-null value while another non-null value is already present must throw `InvalidOperationException` without changing the existing value; setting `null` remains the explicit cleanup path.
- **Reason**: ADR-0029 says existing correlation id is not overwritten and context is cleaned up when scope ends. Current Sprint 1 skeleton behavior is a plain settable `AsyncLocal<CorrelationId?>`; Sprint 2 owns correlation/versioning primitives and must close this behavior before adding `CorrelationContextAccessorTests`.
- **Order constraint**: apply before creating `tests/Dobby.UnitTests/Core/CorrelationContextAccessorTests.cs`.
- **Out of scope**: no new correlation namespace, no job/audit/device/setup id model, no background queue implementation, and no diagnostics/audit logging integration.

### 8.2 `src/Dobby.Core/Versioning/HostVersion.cs`

- **Change**: update `HostVersion.TryParse` so each numeric component follows the manifest/schema SemVer numeric-component rule: each component must match `^(0|[1-9][0-9]*)$`; `0` is allowed; non-zero segments must not start with `0`; components are digits-only with no whitespace or sign; exactly three dot-separated non-negative integer components are accepted; pre-release/build metadata and four-segment versions remain rejected. Keep `CompareTo`, `ToString`, `IHostVersionProvider`, and `FixedHostVersionProvider` shape unchanged.
- **Reason**: `docs/schemas/dobby-plugin-manifest.schema.json` and ADR-0037 require stable `MAJOR.MINOR.PATCH` SemVer numeric components. Current `int.TryParse`-based validation accepts leading-zero and whitespace-padded components such as `01.0.0` or `1. 2.3`, which contradicts the planned `HostVersionTests`.
- **Order constraint**: apply before creating `tests/Dobby.UnitTests/Core/HostVersionTests.cs`.
- **Out of scope**: no dependency on `NuGet.Versioning` in `Dobby.Core`, no host assembly metadata provider implementation, no plugin manifest validator redesign.

### 8.3 `src/Dobby.SDK/PluginFramework/PluginBase.cs`

- **Change**: `RegisterServices(IPluginServiceCollection services)` declaration goes from `public virtual void` to `public abstract void`; remove the current default implementation body (currently `ArgumentNullException.ThrowIfNull(services);`). `OnActivated(IPluginActivationContext context)` remains `virtual` but its body is trimmed to the ADR-0021 §3a no-op shape — remove the `ArgumentNullException.ThrowIfNull(context)` line so the body is `{ }` (empty), matching the code listing in `docs/adr/ADR-0021-sdk-surface-area.md` §3a.
- **Reason**: ADR-0021 §3a defines `PluginBase : IPluginModule` with abstract `RegisterServices` (so every plugin module must opt into service registration explicitly; the virtual default silently allows plugins that never register any service to compile, which masks integration errors) and a virtual no-op `OnActivated` (empty body). Sprint 1 deferred-drift recorded the `RegisterServices` shape divergence; review of the same ADR section during Sprint 2 planning also identified that the current `OnActivated` body is not a no-op (it throws on null) and the prior plan's justification — "ADR-0021 allows the activation hook to be optional" — conflated "plugin override is optional" with "base body may throw," which the ADR does not say. Sprint 2 closes both deviations as one atomic edit to keep `PluginBase` aligned with its specifying ADR before Sprint 3+ consumers fan out.
- **Compile blast radius**: any subclass of `PluginBase` that did not provide a `RegisterServices` override will fail to compile. The current codebase has two such subclasses:
  - `samples/Dobby.Sample.Plugin/SamplePlugin.cs` `SamplePluginModule` — already provides an override (calls `base.RegisterServices` then `AddExtensionPoint`). The `base.RegisterServices(services)` call must be removed because there is no longer a base implementation; the override body keeps the `AddExtensionPoint` call only.
  - `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs` `MinimalPlugin` — currently `private sealed class MinimalPlugin : PluginBase;` with no override. Sprint 2 modifies this fixture (see §8.4).
- **Order constraint**: this change must be sequenced with §8.4 and §8.5; apply the edit set together or the solution build breaks.

### 8.4 `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs`

- **Change**: replace the empty `MinimalPlugin` declaration with a concrete subclass that overrides `RegisterServices` with an empty body. The existing test contract — "plugin base defaults do not register extra lifecycle hooks" — is preserved by the empty override; what changes is only that the no-op is explicit at the plugin author level, not implicit.
- **Additional test for §8.3 `OnActivated` alignment**: add a new test method (e.g., `PluginBase_OnActivated_default_is_no_op`) that constructs a `MinimalPlugin`, invokes the base default with a null-forgiven activation context (`null!`) or an equivalent shape check, and asserts the call returns without throwing and produces no observable side effect. This test must fail against the current `ArgumentNullException.ThrowIfNull(context)` body and locks ADR-0021 §3a no-op shape so future SDK changes cannot silently re-introduce a throwing or behavior-bearing default.
- **Test rename consideration**: The existing test method name `Plugin_base_defaults_do_not_register_extra_lifecycle_hooks` remains accurate. Sprint 2 keeps the name and adds the new `OnActivated` no-op method alongside it.
- **Order constraint**: must compile against the updated `PluginBase`. Apply alongside §8.3.

### 8.5 `samples/Dobby.Sample.Plugin/SamplePlugin.cs`

- **Change**: remove the `base.RegisterServices(services);` line. The remaining body keeps the `services.AddExtensionPoint<IPluginPage, SampleHomePage>();` registration.
- **Reason**: `base.RegisterServices` no longer exists (abstract has no body).
- **Order constraint**: must compile against the updated `PluginBase`. Apply alongside §8.3.
- **Note**: this is a sample modification, not a sample manifest/schema fix. The dobby-plugin.manifest.json file remains untouched.

### 8.6 `artifacts/release-evidence/skeleton-local/architecture-baseline.md`

- **Change 1 — `## Static Guardrail Status` section**: add a Sprint 2 final-verification line recording the date the Sprint 2 run completed and that `dotnet restore Dobby.sln`, `dotnet build Dobby.sln --no-restore`, `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"`, and `dotnet format Dobby.sln --verify-no-changes` all passed. Include the test count (Sprint 1 baseline was 50; Sprint 2 adds approximately 15-25 new test cases; the agent records the actual final number).
- **Change 2 — Known Blockers section**: update the `SPRINT1-INFRA-APP-PRINT-BOUNDARY` line for the selected path (a) renewal:
  - Mark the allowance as renewed for Sprint 2 only, state that the renewal is not architectural approval, note the Sprint 9 hard sunset gate, reference `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 9's carry-over/DoD obligation and ADR-0053 `IPrintTargetClassifier` host-enforcement/DI consumption, and reference the reviewable human-authorized decision record in `## Sprint 2 Reconfirmation`.
  - Path (b): if Sprint 2 is revised to include the print classifier migration and all tests are green, replace the line with a note that the allowance has been removed. If Sprint 2 is not revised, leave the blocker line substantively unchanged and note that sunset is deferred.
  - Path (c): replace the line with a reference to the new architecture-map row that legalizes the edge; if ADR-0040 is also amended, reference it as accompanying documentation rather than the parser-affecting authority.
  - Path (b) or path (c) applies only if a later human decision explicitly supersedes the recorded path (a) choice and this execution plan is updated accordingly. If the reviewable path (a) decision is missing from implementation context, Sprint 2 halts at §9 Step 2 until the decision record is supplied, and §8.6 is not entered.
- **Change 3 — Known Blockers section**: remove the "ADR-0001 type-level Contracts/Core surface enforcement" line (now implemented) and the "`PluginBase.RegisterServices` current code shape" line (now corrected). Keep the manifest and operation verb blocker lines (deferred to Sprint 6/Sprint 8). Do not remove or move the `## Deferred Drift` section's Branch Shared / observed-graph evidence lines.
- **Change 4 — `## Sprint 2 Reconfirmation` section**: keep the literal `## Sprint 2 Reconfirmation` heading because `ArchitectureDependencyBoundaryTests.PrintBoundaryBlockerIsRecorded` currently gates the temporary allowance on that exact heading. Preserve the recorded path (a) decision and append the final Sprint 2 verification/evidence note inside the section, reiterating that the renewal applies to Sprint 2 only, it is not architectural approval, and the sunset gate is Sprint 9 per `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 9's carry-over/DoD obligation for `SPRINT1-INFRA-APP-PRINT-BOUNDARY`. If a later human decision switches to path (b) or path (c) before execution reaches this step, stop and revise this execution plan before replacing the section with a `## Resolution Outcome` note. The agent never edits this section without a recorded human-authorized path choice.
- **Order constraint**: only after T1-T12 and the full suite/format gate are green; this is the last Sprint 2 edit.

### 8.7 `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` (conditional)

- **Path (a)**: no change. This depends on §8.6 keeping the literal `## Sprint 2 Reconfirmation` heading in `architecture-baseline.md`; if a future plan renames the heading, that same plan must update `PrintBoundaryBlockerIsRecorded` atomically.
- **Path (b)**: do not apply under this execution plan unless a revised Sprint 2 plan also brings the print classifier migration into scope. If the revised plan is approved, remove the `PrintBoundaryBlocker` constant, the `printBoundaryBlockerRecorded` conditional in `Source_project_references_are_subset_of_architecture_map`, the `IsTemporaryPrintBoundaryEdge` helper, and the `PrintBoundaryBlockerIsRecorded` helper only after the `Dobby.Infrastructure → Dobby.Application` edge has been removed or legally replaced. Sprint 2 must not end with this test red.
- **Path (c)**: same removals as path (b), but the test passes because the architecture map's Cədvəl 2 has been amended to allow the edge.

### 8.8 `tests/Dobby.UnitTests/Navigation/NavigationTests.cs`

- **Change**: add a focused route identity test for the existing `RouteRegistry` built-in routes. The test should verify that known built-in route lookups return their canonical route keys (`RouteRegistry.HomeRouteKey`, `RouteRegistry.PluginsRouteKey`, `RouteRegistry.SettingsRouteKey`) and do not depend on localized labels or fallback behavior.
- **Reason**: master Sprint 2 verification explicitly includes route identity. The existing `Route_registry_falls_back_to_home_for_unknown_route` test verifies invalid-route fallback, not route identity as defined by ADR-0013.
- **Order constraint**: apply after the source-shape correction steps and before the full suite re-run.
- **Out of scope**: no route registry redesign, no shell runtime change, no hierarchical plugin navigation implementation, and no new route persistence behavior.

### 8.9 `src/Dobby.Contracts/Policies/PolicyContracts.cs`

- **Change**: align policy contract record shapes with accepted ADRs:
  - Change the current `sealed record PolicyDecision(bool IsAllowed, string? DenyReason)` shape to the ADR-0004-defined `readonly record struct PolicyDecision(bool Allowed, string? DenyReason)`. Keep the `Allow()` and `Deny(string reason)` factories, updated to populate `Allowed` and `DenyReason`.
  - Change the current `sealed record OperationPermissionCapability(string OperationKey)` shape to the ADR-0048-defined `readonly record struct OperationPermissionCapability(string OperationKey)`.
- **Reason**: Sprint 2 owns capability policy contracts, and ADR-0004/ADR-0048 are the accepted authorities for the plugin-visible policy contract shapes. Current code drift would be locked by Sprint 2 contract default tests unless corrected first.
- **Order constraint**: apply before creating `tests/Dobby.UnitTests/Contracts/PluginContractDefaultsTests.cs`.
- **Out of scope**: no authorization runtime implementation, no permission registry, no async policy API, no new capability model, no operation-key parser, no `OperationVerb` taxonomy, and no ADR-0004 or ADR-0048 amendment unless humans explicitly choose to preserve the current code shape instead.

### 8.10 `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs`

- **Change**: extend the existing plugin exception boundary tests so `PluginExceptionBoundary.InvokeAsync` verifies every documented mapping from `docs/04_RESULT_VS_EXCEPTION_POLICY.md`:
  - `OperationCanceledException` maps to `ErrorCode.Cancelled`, including a path where the supplied cancellation token is already cancelled.
  - `ArgumentException` and `ArgumentNullException` map to `ErrorCode.InvalidInput`.
  - `NotSupportedException` remains mapped to `ErrorCode.UnsupportedOperation`.
  - generic non-fatal `Exception` maps to `ErrorCode.PluginInternalError`.
- **Reason**: master Sprint 2 verification includes error mapping. The current test covers only `NotSupportedException`.
- **Order constraint**: apply after the source-shape correction steps and before the full suite re-run.
- **Out of scope**: no change to `PluginExceptionBoundary` unless a test exposes a defect against the documented mapping; no logging/audit/diagnostics implementation.

## 9. Code Work Plan (Step-by-Step)

Each step lists what to do and what the verification gate is. No code is reproduced here; the agent is expected to inspect the cited file first and produce the smallest possible edit.

### Step 1 — Confirm Sprint 1 baseline is intact

- Read `Dobby.sln` to confirm no project entries have been added since Sprint 1.
- Run `dotnet restore Dobby.sln`, `dotnet build Dobby.sln --no-restore`, `dotnet test Dobby.sln --no-build`, `dotnet format Dobby.sln --verify-no-changes`. All four must pass before any Sprint 2 edits begin.
- If any of the four fails, halt Sprint 2 and report the regression with `command_status` output for the failing command.

### Step 2 — Verify the recorded path (a) decision for `SPRINT1-INFRA-APP-PRINT-BOUNDARY`

- Confirm `artifacts/release-evidence/skeleton-local/architecture-baseline.md` contains the 2026-05-12 human-authorized reconciled decision selecting path (a): Sprint 2-only renewal, not architectural approval of the `Dobby.Infrastructure → Dobby.Application` edge, with Sprint 9 as the hard sunset gate.
- Preserve the recorded path in the Sprint 2 evidence update at Step 14, and include final verification status without weakening the bounded renewal language.
- Do not switch paths unless a later revised human decision explicitly supersedes the recorded choice and the execution plan is updated accordingly. Agent-driven deferral or amendment remains prohibited.
- If the reviewable path (a) decision is missing from implementation context, halt at this step and surface the missing decision record instead of inventing a renewal or proceeding with code/evidence edits.

### Step 3 — Apply the ADR-0029 `CorrelationContextAccessor` behavior correction

- Edit `src/Dobby.Core/CorrelationId.cs` per §8.1.
- Preserve the existing `CorrelationId`, `ICorrelationIdGenerator`, and `ICorrelationContextAccessor.Current` public contract; add only the concrete `CorrelationContextAccessor.PushScope(CorrelationId id)` scope-management surface described in §8.1 for no-overwrite and cleanup semantics.
- Verification gate: `dotnet build Dobby.sln --no-restore` passes (zero warnings, zero errors per `TreatWarningsAsErrors=true`).

### Step 4 — Apply the `HostVersion.TryParse` SemVer numeric-component correction

- Edit `src/Dobby.Core/Versioning/HostVersion.cs` per §8.2.
- Confirm `0.0.0`, `1.0.0`, and normal non-zero numeric segments still parse; confirm leading-zero segments, component/edge whitespace, signed components, four-segment versions, pre-release, and build metadata reject.
- Verification gate: `dotnet build Dobby.sln --no-restore` passes.

### Step 5 — Apply the `PluginBase` shape correction (atomic edit set)

- Edit `src/Dobby.SDK/PluginFramework/PluginBase.cs` per §8.3.
- Edit `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs` per §8.4.
- Edit `samples/Dobby.Sample.Plugin/SamplePlugin.cs` per §8.5.
- Verification gate: `dotnet build Dobby.sln --no-restore` passes (zero warnings, zero errors per `TreatWarningsAsErrors=true`).

### Step 6 — Add `CorrelationContextAccessorTests.cs`

- First read `src/Dobby.Core/CorrelationId.cs` to confirm the corrected shape of `CorrelationContextAccessor`, `ICorrelationContextAccessor`, `ICorrelationIdGenerator`, and the scope-management surface. Assertions must target the ADR-0029 behavior implemented in Step 3; do not extend the accessor surface while writing tests.
- Add tests per §7.1.
- Verification gate: `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~CorrelationContextAccessor"` passes.

### Step 7 — Add `HostVersionTests.cs`

- First read `src/Dobby.Core/Versioning` files to confirm the corrected `HostVersion` constructor signature, `TryParse` signature, and `IComparable<HostVersion>`/`IEquatable<HostVersion>` shape.
- Add tests per §7.2.
- Verification gate: `dotnet test --filter "FullyQualifiedName~HostVersion"` passes.

### Step 8 — Add `PluginContractDefaultsTests.cs`

- First edit `src/Dobby.Contracts/Policies/PolicyContracts.cs` per §8.9 so `PolicyDecision` matches ADR-0004 and `OperationPermissionCapability` matches ADR-0048. Verify `dotnet build Dobby.sln --no-restore` passes before adding tests.
- Then read each referenced Contracts file (PluginPages, Policies, BackgroundOperations, Licensing, PluginStorage) to confirm the exact present factory and field shape. Policy assertions target the ADR-0004-corrected `PolicyDecision` shape and ADR-0048-corrected `OperationPermissionCapability` shape; other assertions match the current documented shape.
- Add tests per §7.3.
- Verification gate: `dotnet test --filter "FullyQualifiedName~PluginContractDefaults"` passes.

### Step 9 — Add `PluginStorageJsonExtensionsTests.cs`

- First read `src/Dobby.SDK/PluginStorage/PluginStorageJsonExtensions.cs`, `src/Dobby.Contracts/PluginStorage/PluginStorageContracts.cs`, and the existing storage contract tests to confirm the helper's current result and error mapping behavior.
- Add tests per §7.4. Use a local fake `IPluginStorageCompartment` in the test file; do not add a storage provider or serializer abstraction.
- Verification gate: `dotnet test --filter "FullyQualifiedName~PluginStorageJsonExtensions"` passes.

### Step 10 — Add `ContractsCoreSurfaceTests.cs`

- Inspect every `.cs` file under `src/Dobby.Contracts` to enumerate which Core types are currently referenced (using `grep_search` for `Dobby.Core` qualifiers and using-directive). Confirm the actually-referenced set is a subset of the documented allow set. If `Contracts_assembly_references_only_allowed_core_types` reports a reference outside the allow set, follow §13.4's `ContractsCoreSurfaceTests` violation policy before editing; do not expand the allow set, amend ADR-0001, or redesign public contracts inside Sprint 2.
- Add test per §7.5. Use BCL `System.Reflection` only.
- Verification gate: `dotnet test --filter "FullyQualifiedName~ContractsCoreSurface"` passes. If detection proof is required, perform it only with a transient throwaway fixture or disposable copy that is deleted before continuing; do not mutate real Contracts source to prove the negative in this non-git workspace.

### Step 11 — Expand plugin exception/error mapping coverage

- First read `docs/04_RESULT_VS_EXCEPTION_POLICY.md` and `src/Dobby.PluginFramework/Activation/PluginExceptionBoundary.cs` to confirm the documented mapping table and current implementation.
- Update `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` per §8.10. The assertions target documented error-code mapping only; do not add logging/audit/diagnostics behavior here.
- Verification gate: `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Plugin_exception"` passes.

### Step 12 — Add route identity coverage to `NavigationTests.cs`

- First read `src/Dobby.Navigation/RouteRegistry.cs` and `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` to confirm the current built-in route constants and existing fallback test shape.
- Add the route identity test per §8.8. The assertions target known-route identity only; do not alter route keys or add route behavior while writing the test.
- Verification gate: `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Route_registry"` passes.

### Step 13 — Run the full test suite and format check

- `dotnet restore Dobby.sln`
- `dotnet build Dobby.sln --no-restore` — must show zero warnings and zero errors.
- `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` — must show all tests passing. Record total count.
- `dotnet format Dobby.sln --verify-no-changes` — must succeed.

### Step 14 — Update `architecture-baseline.md`

- Apply §8.6 changes for the selected human path. Step 14 is entered only after the reviewable decision is available in implementation context.
- Re-run `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` to confirm the evidence-based architecture allowance or resolution still resolves correctly under the selected path.
- Run `dotnet format Dobby.sln --verify-no-changes` to ensure the markdown edit and any conditional code edits keep the format clean.

### Step 15 — Final full suite and format re-run

- `dotnet restore Dobby.sln`
- `dotnet build Dobby.sln --no-restore` — must show zero warnings and zero errors.
- `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` — must show all tests passing. Record final total count.
- `dotnet format Dobby.sln --verify-no-changes` — must succeed.

## 10. Test Plan

### 10.1 Existing tests Sprint 2 must keep green

All 50 Sprint 1 tests must continue to pass:

- `Dobby.UnitTests.Core.ResultTests` — 4 cases.
- `Dobby.UnitTests.Contracts.PluginFrameworkContractTests` — 1 case (modified fixture in §8.4 keeps the same assertion).
- `Dobby.UnitTests.Contracts.PluginStorageContractTests` — 8 runtime cases (2 accepted-key theory rows, 5 rejected-key theory rows, 1 opaque-value fact).
- `Dobby.UnitTests.PluginFramework.PluginFrameworkTests` — 3 cases.
- `Dobby.UnitTests.Navigation.NavigationTests` — 2 cases.
- `Dobby.UnitTests.Security.SecurityTests` — 3 cases.
- `Dobby.UnitTests.Messaging.InMemoryEventBusTests` — 1 case.
- `Dobby.UnitTests.Licensing.LicensingTests` — 4 cases (theory).
- `Dobby.UnitTests.Diagnostics.DiagnosticsAndAuditTests` — 2 cases.
- `Dobby.UnitTests.Configuration.ConfigurationTests` — 1 case.
- `Dobby.UnitTests.Shell.WorkSessionRegistryTests` — 1 case.
- `Dobby.IntegrationTests.ProjectBoundaryTests` — 2 cases.
- `Dobby.IntegrationTests.Architecture.ArchitectureDependencyBoundaryTests` — 4 cases.
- `Dobby.IntegrationTests.Architecture.EncodingBoundaryTests` — 1 case.
- `Dobby.IntegrationTests.Architecture.PackageGovernanceTests` — 5 cases.
- `Dobby.IntegrationTests.Architecture.PluginAndSdkBoundaryTests` — 4 cases.
- `Dobby.IntegrationTests.Architecture.ReleaseEvidenceTests` — 2 cases.
- `Dobby.SmokeTests.ShellSmokeTests` — existing smoke baseline.

### 10.2 New unit tests in `Dobby.UnitTests/Core/CorrelationContextAccessorTests.cs`

| Test | What it verifies | Why |
|---|---|---|
| `Generator_returns_unique_correlation_ids` | `ICorrelationIdGenerator.New()` returns distinct ids on repeated calls. | ADR-0029 generator contract; covers master plan "correlation flow." |
| `Accessor_returns_current_value_within_same_async_flow` | Setting `Current`, then reading on the same execution context, returns the same value. | ADR-0029 async-flow propagation. |
| `Accessor_propagates_across_awaited_task_yield` | After `await Task.Yield()`, `Current` retains the set value. | ADR-0029 async-flow propagation across continuations. |
| `Accessor_setter_rejects_existing_value_instead_of_overwriting` | Setting a different non-null `Current` value when a non-null value already exists throws `InvalidOperationException` and leaves the existing id unchanged. | ADR-0029 no silent overwrite rule; locks the compatibility setter behavior from §8.1. |
| `Accessor_returns_null_when_no_value_set` | Default state is null (or documented sentinel). | ADR-0029 scope cleanup default. |
| `PushScope_restores_previous_value_on_dispose` | `PushScope(CorrelationId)` sets `Current` for the scope and restores the previous value when disposed. | ADR-0029 scope cleanup rule; targets the concrete scope-management API from §8.1. |
| `PushScope_rejects_existing_value_instead_of_overwriting` | Calling `PushScope` when a non-null current id already exists is rejected and the existing id remains unchanged. | ADR-0029 explicit pass to background tasks; no silent overwrite. |

### 10.3 New unit tests in `Dobby.UnitTests/Core/HostVersionTests.cs`

| Test | What it verifies | Why |
|---|---|---|
| `TryParse_accepts_canonical_version` (theory across `1.0.0`, `0.0.0`, `123.45.67`) | Canonical `Major.Minor.Patch` shape accepted. | ADR-0036/schema regex contract. |
| `TryParse_rejects_invalid_inputs` (theory across empty, whitespace, `1.2`, `1.2.3.4`, `01.0.0`, ` 1.0.0`, `1.0.0 `, `1. 2.3`, `1.2 .3`, `\t1.0.0`, `1.+2.3`, `1.-2.3`, `1.2.3-rc1`, `1.2.3+build`) | Invalid shapes rejected. | Plugin manifest version pattern reuse; schema regex requires each segment to match `^(0|[1-9][0-9]*)$`. |
| `CompareTo_orders_by_major_then_minor_then_patch` | Ordering correctness across boundaries. | Plugin compatibility check (ADR-0036). |
| `Equals_is_value_equality_and_symmetric` | Value-record equality. | Defensive verification of record semantics under nullable enable. |
| `FixedHostVersionProvider_returns_constructor_value` | Provider contract. | ADR-0001/architecture-map Row 90 versioning surface. |

### 10.4 New unit tests in `Dobby.UnitTests/Contracts/PluginContractDefaultsTests.cs`

| Test | What it verifies | Why |
|---|---|---|
| `PluginPageDescriptor_constructs_with_required_fields` | All declared fields present and round-trip. | ADR-0049 plugin page/main-content runtime contract. |
| `PluginPageContext_constructs_with_string_correlation_id` | `CorrelationId` field is string per ADR-0049. | ADR-0049 plugin page/main-content runtime contract. |
| `PolicyDecision_shape_matches_ADR_0004` | `PolicyDecision` is a `readonly record struct` with `Allowed` and `DenyReason` properties. | ADR-0004 plugin-visible policy contract shape; closes current code drift. |
| `PolicyDecision_Allow_factory_produces_allowed_decision` | `Allow()` factory returns `Allowed=true` with no deny reason. | Sprint 2 verification: contract defaults aligned to ADR-0004. |
| `PolicyDecision_Deny_factory_carries_documented_reason_shape` | `Deny(...)` factory returns `Allowed=false` and preserves `DenyReason`. | Sprint 2 verification: contract defaults aligned to ADR-0004. |
| `OperationPermissionCapability_shape_matches_ADR_0048_and_keeps_operation_key` | `typeof(OperationPermissionCapability).IsValueType` is true, the type carries the readonly record struct contract shape, and constructing it with a sample key round-trips `OperationKey` verbatim. | ADR-0048 capability payload contract; closes current code drift. |
| `IPluginStorage_exposes_two_compartment_properties` | `SharedDomainStorage` and `UserScopedStorage` are properties typed as `IPluginStorageCompartment`. | ADR-0030 plugin storage taxonomy; avoids treating property names as standalone types. |
| `PluginStorageKeyPrefix_Create_accepts_empty_and_rejects_invalid_prefixes` | Empty prefix is accepted; invalid non-empty prefixes reject with the documented validation result. | Plugin Storage Contracts shape from master Sprint 2; closes untested prefix record. |
| `PluginRuntimeMode_enum_has_exactly_three_documented_values` | Enum members are `FullAccess`, `ReadOnlyExpired`, `Denied` only. | ADR-0053 closed non-disclosing surface. |
| `BackgroundOperationProgress_Determinate_rejects_out_of_range` (theory) | Integer percent range `[0, 100]` enforced. | ADR-0015 progress shape. |
| `BackgroundOperationSnapshot_carries_documented_payload` | Construction round-trips operation id, owner, display/status/progress, correlation id, and failure reason. | ADR-0015 snapshot contract. |
| `ICapabilityLicensing_GetCurrentMode_returns_documented_runtime_mode` (against a stub) | Plugin-visible licensing surface returns one of three documented modes. | ADR-0053 non-disclosing licensing contract. |

### 10.5 New unit tests in `Dobby.UnitTests/SDK/PluginStorageJsonExtensionsTests.cs`

| Test | What it verifies | Why |
|---|---|---|
| `SetJsonAsync_serializes_payload_and_delegates_to_compartment` | The helper serializes a DTO and calls `IPluginStorageCompartment.SetAsync` with bytes. | ADR-0021 SDK convenience-wrapper surface; no provider implementation. |
| `GetJsonAsync_deserializes_payload_from_compartment` | The helper reads bytes and returns the expected DTO value. | Verifies existing public SDK helper behavior. |
| `GetJsonAsync_propagates_failed_get_result` | A failed `GetAsync` result is returned as a failure and the upstream error is preserved. | Result/error policy; avoids hiding storage failures. |
| `GetJsonAsync_returns_serialization_failed_for_invalid_json` | Invalid JSON maps to `ErrorCode.SerializationFailed`. | Existing helper error mapping must be covered before SDK surface is considered stable. |

### 10.6 Updates to existing tests

- `Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs` — modify `MinimalPlugin` fixture to provide concrete `RegisterServices` override (no behavioral change to the existing test method itself); add a new test `PluginBase_OnActivated_default_is_no_op` that invokes `MinimalPlugin.OnActivated(...)` with a null-forgiven activation context (`null!`) or equivalent empty-body shape check and asserts the call returns without throwing, verifying ADR-0021 §3a no-op shape after §8.3 trims the body.
- `Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` — expand plugin exception boundary tests so `OperationCanceledException`, `ArgumentException`/`ArgumentNullException`, `NotSupportedException`, and non-fatal generic `Exception` each verify the documented `ErrorCode` mapping from `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.
- `Dobby.UnitTests/Navigation/NavigationTests.cs` — add a focused route identity test for known built-in routes. The test verifies that Home, Plugins, and Settings route lookups return the canonical `RouteRegistry.*RouteKey` values and are not satisfied merely by fallback behavior or localized/display labels.

### 10.7 New integration tests in `Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs`

| Test | What it verifies | Why |
|---|---|---|
| `Contracts_assembly_references_only_allowed_core_types` | Type-level scan of `Dobby.Contracts.dll` confirms every Core reference is in the documented allow set. | ADR-0001 type-level enforcement deferred from Sprint 1. |
| `Contracts_assembly_has_no_wpf_or_ui_dependency` | Asserts the assembly does not reference `PresentationFramework`, `WindowsBase`, `System.Xaml`, or any WPF-only type. | Master plan: "plugin-facing APIs contain no WPF types." |

### 10.8 Expanded coverage clarification

The master plan's "operation key parsing" verification is already covered by `Dobby.UnitTests.Security.SecurityTests.Operation_key_parser_splits_on_last_dot` and `Operation_key_parser_rejects_unknown_verbs`. Sprint 2 does not duplicate those tests. Sprint 2 also does not extend operation key parsing to new verbs (Sprint 6 carry-over).

The master plan's "route identity" verification is Sprint 2 work and is covered by the new `Dobby.UnitTests.Navigation.NavigationTests` route identity test from §8.8 and §10.6. The existing `Route_registry_falls_back_to_home_for_unknown_route` test remains fallback coverage only and is not treated as route identity verification.

The master plan's "ordering comparer" verification is already covered by `Dobby.UnitTests.Navigation.NavigationTests.Ordering_comparer_uses_group_weight_plugin_and_declaration_index`. Sprint 2 does not add a Contracts default test for `DefaultUiOrderingComparer` null handling because that comparer is a `Dobby.Navigation` implementation detail, not a `Dobby.Contracts` default.

## 11. Verification Commands

The Sprint 2 implementation agent runs these commands (Windows PowerShell, repo root as cwd):

```pwsh
dotnet restore Dobby.sln
dotnet build Dobby.sln --no-restore
dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"
dotnet format Dobby.sln --verify-no-changes
```

Per-step verification commands:

```pwsh
# After §8.1:
dotnet build Dobby.sln --no-restore

# After §8.2:
dotnet build Dobby.sln --no-restore

# After §8.3/§8.4/§8.5 atomic edit set:
dotnet build Dobby.sln --no-restore

# After §7.1:
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~CorrelationContextAccessor"

# After §7.2:
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~HostVersionTests"

# After §7.3:
dotnet build Dobby.sln --no-restore
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~PluginContractDefaultsTests"

# After §7.4:
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~PluginStorageJsonExtensionsTests"

# After §7.5:
dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj --filter "FullyQualifiedName~ContractsCoreSurfaceTests"

# After §8.10:
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Plugin_exception"

# After §8.8:
dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj --filter "FullyQualifiedName~Route_registry"
```

If any per-step command fails, halt and resolve the failure before continuing. Do not edit additional files while a per-step command is red.

## 12. Implementation Order

The execution agent applies tasks in this order. Each gate must be green before moving forward.

1. **Baseline confirmation** (T1) — read-only verification that Sprint 1 baseline is intact. No edits.
2. **Print-boundary human decision record** (T2) — confirm the recorded path (a) decision for the evidence update at Step 14. If the reviewable decision is missing from implementation context, halt here and surface the missing decision record. Do not touch the test/csproj unless a later human-selected path explicitly authorizes that work.
3. **`CorrelationContextAccessor` behavior correction** (T3) — apply the scoped ADR-0029 Core behavior fix; verify `dotnet build`.
4. **`HostVersion.TryParse` SemVer numeric-component correction** (T4) — reject leading-zero numeric segments, component/edge whitespace, and signed components; verify `dotnet build`.
5. **`PluginBase` atomic shape correction** (T5 plus sample plugin) — three source edits applied together: `RegisterServices` `virtual` → `abstract` and `OnActivated` body trimmed to ADR-0021 §3a no-op in `PluginBase.cs`, `MinimalPlugin` concrete `RegisterServices` override plus new `OnActivated` no-op test in `PluginFrameworkContractTests.cs`, and `base.RegisterServices` call removed in `SamplePlugin.cs`; verify `dotnet build` passes.
6. **CorrelationContextAccessorTests** (T6) — verify corrected accessor shape, then add tests.
7. **HostVersionTests** (T7) — verify corrected parsing shape, then add tests.
8. **PolicyContracts ADR-0004/ADR-0048 corrections + PluginContractDefaultsTests** (T8) — align `PolicyDecision` with ADR-0004 and `OperationPermissionCapability` with ADR-0048, then include policy shape plus plugin storage compartment and prefix shape coverage.
9. **PluginStorageJsonExtensionsTests** (T9) — verify the existing SDK storage helper behavior without changing the helper shape unless a documented defect is exposed.
10. **ContractsCoreSurfaceTests** (T10) — first inspect Contracts source for actual Core type usage, then add the reflection-based test.
11. **Plugin exception/error mapping coverage** (T11) — update `PluginFrameworkTests.cs` with the missing documented mapping coverage from §8.10, then verify the plugin-exception test filter.
12. **Route identity coverage** (T12) — update `NavigationTests.cs` with the ADR-0013 route identity coverage from §8.8, then verify the route-registry test filter.
13. **Full suite re-run** — `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
14. **Evidence refresh** (T13) — `architecture-baseline.md` updated per chosen path; conditional `ArchitectureDependencyBoundaryTests.cs` change applied only if path (c) is human-authorized, or path (b) has a revised green migration scope.
15. **Final full suite re-run** — confirm post-evidence state is clean.

## 13. Risks and Mitigations

### 13.1 Sprint 1 carry-over: `SPRINT1-INFRA-APP-PRINT-BOUNDARY`

- **Risk**: Sprint 2 proceeds without verifying the recorded path (a) decision, or later evidence rewrites the renewal as a generic deferral instead of a bounded sunset.
- **Mitigation**: halt at §9 Step 2 if the recorded path (a) decision is missing or contradicted. Preserve the renewal as Sprint 2-only, explicitly state that it is not architectural approval, cross-reference the Sprint 9 sunset gate, and keep the cleanup enforceable by naming the `Dobby.Infrastructure → Dobby.Application` `ProjectReference` removal, ADR-0053/DI consumption of `IPrintTargetClassifier`, and `IsTemporaryPrintBoundaryEdge` removal.

### 13.2 Test reflection complexity (T10)

- **Risk**: `ContractsCoreSurfaceTests` may produce false positives if compiler-generated types (async state machines, anonymous types, generated record `Equality` helpers) reference Core types in synthesized members.
- **Mitigation**: filter by `[CompilerGenerated]` attribute and `<>`-prefixed names; only walk public-API surface members (public/protected); for fields, only walk declared (`BindingFlags.DeclaredOnly`) fields; for methods, exclude operator/equality-record synthesized methods. The test should fail with a precise message that names the offending type+member.

### 13.3 PluginBase abstract introduction breaks downstream code

- **Risk**: another `PluginBase` subclass somewhere in the repo (not enumerated above) becomes uncompilable.
- **Mitigation**: before applying §8.3, run `grep_search` for `: PluginBase` across the repo. As of the plan source-read pass, only two subclasses exist (`MinimalPlugin` in tests and `SamplePluginModule` in samples). If a third exists, the agent applies the same explicit-override fix to that file before the build gate.

### 13.4 Type-level Contracts/Core scan catches an actual violation

- **Risk**: the scan reveals a previously-undetected ADR-0001 violation in `Dobby.Contracts`.
- **Mitigation / `ContractsCoreSurfaceTests` violation policy**: if a `Dobby.Contracts → Dobby.Core` reference outside the ADR-0001 allow set is found, the agent may apply an in-place Sprint 2 fix only when all conditions hold:
  1. The initial violation report identifies exactly one offending Contracts type/member and exactly one non-allow-set Core type.
  2. The fix is a single source-file edit that replaces the non-allow-set Core type with an allow-set Core type, or removes an unnecessary import/reference.
  3. The fix does not alter any Contracts public method signature, parameter type, return type, property type, public field type, record positional parameter, or public constructor shape.
  4. Re-running `ContractsCoreSurfaceTests` after the fix shows zero remaining violations.
- If any condition fails, halt at §9 Step 10 and escalate with the offending Contracts type/member, the non-allow-set Core type, and the candidate ADR/contract decision path. Do not expand the allow set, amend ADR-0001, or redesign public contracts inside Sprint 2.
- Any in-sprint fix is recorded in Sprint 2 evidence as a one-line note: file, line/member, removed Core type, and replacement/removal.

### 13.5 Test infrastructure churn under `TreatWarningsAsErrors=true`

- **Risk**: new test files trigger analyzer warnings or nullable warnings that surface as build errors under `TreatWarningsAsErrors=true`.
- **Mitigation**: follow the existing test file style verbatim (file-scoped namespaces, `sealed` test classes, `using` order). Compile each new test file with `dotnet build` immediately after creation.

### 13.6 Markdown evidence file UTF-8/BOM regression

- **Risk**: re-saving `architecture-baseline.md` introduces a BOM.
- **Mitigation**: the existing `EncodingBoundaryTests.Repo_text_files_are_utf8_without_bom` test catches this. The agent confirms the test passes after every evidence edit.

### 13.7 Format gate (`dotnet format --verify-no-changes`)

- **Risk**: new files or edited files violate the project's coding-style rules and the gate fails.
- **Mitigation**: run `dotnet format Dobby.sln` (without `--verify-no-changes`) once locally to normalize formatting if needed; ensure the workspace has no unintended edits; then re-run with `--verify-no-changes`. This repository skeleton is intentionally non-git, so do not rely on version-control state to restore files.

### 13.8 Tooling pin gate still open

- **Risk**: a Sprint 2 agent may be tempted to "fix" the toolchain pin gate without human approval.
- **Mitigation**: explicit Sprint 2 boundary in §5.6: no package changes; no pin list creation. The toolchain pin gate is reopened by humans, not by the Sprint 2 agent. The gate must close before the first package-bearing file change (`Directory.Packages.props`, `Directory.Build.props`, `global.json`, any `.csproj` `PackageReference` change, or a new toolchain pin-list / stop-condition representation file). `Dobby.IntegrationTests.Architecture.PackageGovernanceTests` remains the active guard against accidental package-reference drift during Sprint 2.

## 14. Definition of Done

Sprint 2 is done when **all** of the following are true:

- `dotnet restore Dobby.sln` succeeds with no errors.
- `dotnet build Dobby.sln --no-restore` succeeds with zero warnings and zero errors (per `TreatWarningsAsErrors=true`).
- `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` reports all tests passing. Test count is at least the Sprint 1 baseline of 50 plus the new Sprint 2 cases (target: ~70-80 total).
- `dotnet format Dobby.sln --verify-no-changes` succeeds.
- The new `ContractsCoreSurfaceTests` passes and would fail if a Contracts member referenced a Core type outside the ADR-0001 allow set.
- `src/Dobby.Core/CorrelationId.cs` implements the scoped ADR-0029 no-overwrite/scope-cleanup behavior needed by `CorrelationContextAccessorTests`.
- `src/Dobby.Core/Versioning/HostVersion.cs` rejects leading-zero numeric segments, component/edge whitespace, and signed components while preserving accepted canonical `Major.Minor.Patch` versions.
- `PluginBase.RegisterServices` is `abstract` in `src/Dobby.SDK/PluginFramework/PluginBase.cs` and all `PluginBase` subclasses provide a concrete override; `PluginBase.OnActivated` body is trimmed to the ADR-0021 §3a no-op shape (empty body), and the new `PluginBase_OnActivated_default_is_no_op` test in `PluginFrameworkContractTests.cs` verifies the default is a true no-op that does not throw even for the current drift case (`ArgumentNullException.ThrowIfNull(context)`).
- `src/Dobby.Contracts/Policies/PolicyContracts.cs` aligns with ADR-0004: `PolicyDecision` is a `readonly record struct` with `Allowed` and `DenyReason`, and `PluginContractDefaultsTests` verifies the ADR shape and factory behavior.
- `src/Dobby.Contracts/Policies/PolicyContracts.cs` aligns with ADR-0048: `OperationPermissionCapability` is a `readonly record struct` with `OperationKey`, and `PluginContractDefaultsTests` verifies the ADR shape and key round-trip.
- `CorrelationContextAccessorTests`, `HostVersionTests`, `PluginContractDefaultsTests`, and `PluginStorageJsonExtensionsTests` exist under `tests/Dobby.UnitTests/` and pass.
- `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` includes route identity coverage for the canonical Home, Plugins, and Settings route keys while keeping the existing fallback and ordering tests green.
- `PluginContractDefaultsTests` includes `IPluginStorage` compartment-property shape coverage and `PluginStorageKeyPrefix.Create` accept/reject coverage.
- `PluginStorageJsonExtensionsTests` covers JSON round-trip, failed `GetAsync` result propagation with the upstream `ErrorCode` preserved, and invalid JSON mapping to `ErrorCode.SerializationFailed`.
- `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` covers every documented plugin-boundary exception mapping from `docs/04_RESULT_VS_EXCEPTION_POLICY.md`: cancellation, argument exceptions, unsupported operation, and generic non-fatal exception fallback.
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` reflects:
  - Removed "ADR-0001 type-level Contracts/Core surface enforcement" deferred-drift line.
  - Removed "`PluginBase.RegisterServices` current code shape" deferred-drift line.
  - Sprint 2 final-verification line under `## Static Guardrail Status` with the date and pass status.
  - Updated `SPRINT1-INFRA-APP-PRINT-BOUNDARY` outcome line under `## Known Blockers` according to the reviewable human-selected path.
  - Updated the `## Sprint 2 Reconfirmation` section according to the reviewable human-selected path. If path (a) is selected, keep the literal heading and append the bounded renewal / sunset note required by that decision.
- No new package references in `Directory.Packages.props`.
- No new project additions to `Dobby.sln`.
- No changes to `Directory.Build.props`, `global.json`.
- No changes to host plugin manifest validator, OperationVerb enum, sample manifest, or storage/auth/licensing/shell runtime code.
- `samples/Dobby.Sample.Plugin/SamplePlugin.cs` has the obsolete `base.RegisterServices` call removed, while `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.json` is unchanged.
- Sprint 2 does not close with a known failing test.
- The `SPRINT1-INFRA-APP-PRINT-BOUNDARY` reconfirmation gate is satisfied by a reviewable human path choice and matching evidence. Sprint 2's DoD cannot be marked complete if that record is missing from evidence; agent-driven replacement or deferral remains prohibited.

## 15. Out of Scope (Explicit)

These items are NOT Sprint 2 work. Each is mapped to its owning sprint per the master plan.

| Item | Owning Sprint |
|---|---|
| Plugin manifest schema-required field expansion (vendorId, entryAssembly, displayName, description, files, signature shape, capabilities[].name) | Sprint 8 (Plugin Package Trust and Activation) |
| `OperationVerb` enum expansion (`configure`, `grant`) | Sprint 6 (Operation Authorization) |
| Sample manifest file content updates | Sprint 8 |
| Plugin manifest validator updates | Sprint 8 |
| `OperationKey` parser updates | Sprint 6 |
| Storage provider implementation (SQLite, DPAPI, protected-storage primitives) | Sprint 4, Sprint 11 |
| Auth/session runtime | Sprint 5 |
| Licensing runtime, import flow, signature verification | Sprint 9 |
| Shell composition, WPF host, navigation runtime, settings UI | Sprint 7 |
| Plugin discovery, ALC isolation, activation orchestration | Sprint 8 |
| Print classifier migration to a clean ADR-0040 boundary | Sprint 9 if path (a) renewal is human-selected; otherwise only under a revised human-authorized path (b)/(c) plan |
| Branch Shared runtime, BranchSetup executable, PostgreSQL adapter | Tier B (post-Phase 1) |
| `Dobby.Update` project | Sprint 11+ (P2) |
| New package references or package pin changes in `Directory.Packages.props` | Human-approved package-bearing work only; first such change must close the toolchain pin gate first |
| Build/toolchain/project graph changes in `Directory.Build.props`, `global.json`, or `Dobby.sln` | Not Sprint 2; first package-bearing or toolchain-bearing change must close the toolchain pin gate first |
| Toolchain pin list approval | Human-driven, not Sprint 2; anticipated close target is Sprint 4 storage/provider materialization if no earlier package-bearing work is introduced |

## 16. Final Execution Checklist (for the implementation agent)

The Sprint 2 implementation agent should treat this checklist as the contract for completion.

- [ ] Read `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 2 section in full and confirm scope matches §3 of this plan.
- [ ] Read `SPRINT_1_EXECUTION_PLAN.md` Sprint 2-relevant deferrals (§13 of Sprint 1) and confirm they match §5 of this plan.
- [ ] Read each file enumerated in §2 to confirm current shape before editing.
- [ ] Run baseline verification (Step 1 in §9).
- [ ] Verify the recorded path (a) print-boundary decision with reviewable provenance (Step 2 in §9).
- [ ] Apply `src/Dobby.Core/CorrelationId.cs` ADR-0029 behavior correction (Step 3 in §9). Verify `dotnet build`.
- [ ] Apply `src/Dobby.Core/Versioning/HostVersion.cs` SemVer numeric-component correction (Step 4 in §9). Verify `dotnet build`.
- [ ] Apply atomic `PluginBase` shape correction (Step 5 in §9): `src/Dobby.SDK/PluginFramework/PluginBase.cs`, `tests/Dobby.UnitTests/Contracts/PluginFrameworkContractTests.cs`, `samples/Dobby.Sample.Plugin/SamplePlugin.cs`. Verify `dotnet build`.
- [ ] Add `tests/Dobby.UnitTests/Core/CorrelationContextAccessorTests.cs` (Step 6).
- [ ] Add `tests/Dobby.UnitTests/Core/HostVersionTests.cs` (Step 7).
- [ ] Apply `src/Dobby.Contracts/Policies/PolicyContracts.cs` ADR-0004 `PolicyDecision` and ADR-0048 `OperationPermissionCapability` corrections and add `tests/Dobby.UnitTests/Contracts/PluginContractDefaultsTests.cs` (Step 8).
- [ ] Add `tests/Dobby.UnitTests/SDK/PluginStorageJsonExtensionsTests.cs` (Step 9).
- [ ] Add `tests/Dobby.IntegrationTests/Architecture/ContractsCoreSurfaceTests.cs` (Step 10).
- [ ] Update `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` plugin exception/error mapping coverage (Step 11).
- [ ] Update `tests/Dobby.UnitTests/Navigation/NavigationTests.cs` route identity coverage (Step 12).
- [ ] Run full test suite + format check (Step 13).
- [ ] Update `artifacts/release-evidence/skeleton-local/architecture-baseline.md` (Step 14) according to the selected path. If the reviewable human decision is missing from implementation context, Sprint 2 halted before this checklist item.
- [ ] Final full test suite + format check (Step 15).
- [ ] Verify Definition of Done (§14) checklist is fully satisfied.
- [ ] Provide the human reviewer with the final test count, the chosen print-boundary path, and any remaining known-blocker tokens.

## 17. Planning Coverage and Confidence

This plan is based on direct inspection of the following actual repository state at the time of writing:

- **Architecture map** (`docs/02_ARCHITECTURE_MAP.md` lines 1-200): namespace mapping, dependency map, risk register — fully read for Sprint 2-affected rows (Core, Contracts, SDK, PluginFramework, Application, Infrastructure, plugin storage, plugin pages, navigation, messaging, licensing).
- **Acceptance criteria** (`docs/07_ACCEPTANCE_CRITERIA.md` lines 1-118): full stage matrix and release-blocker table read; confirmed Sprint 2 does not own a P1 release-blocker AC.
- **Coding standards** (`docs/08_CODING_STANDARDS.md` lines 1-200): naming, file layout, namespace mapping table, language idioms, common prohibitions, machine-readable formats — confirmed Sprint 2 work conforms.
- **Result/exception policy** (referenced via Sprint 1 baseline registry test): confirmed 17-entry error-code baseline is locked.
- **ADR-0001 / ADR-0003 / ADR-0004 / ADR-0005 / ADR-0010 / ADR-0013 / ADR-0015 / ADR-0021 / ADR-0029 / ADR-0030 / ADR-0036 / ADR-0037 / ADR-0040 / ADR-0048 / ADR-0049 / ADR-0053**: read where Sprint 2 work touches the surface; cross-referenced against current code. ADR-0036 remains background/schema evidence only; Sprint 2 does not modify manifest validation.
- **Source files**: every file Sprint 2 edits, creates, or verifies against has been opened during planning. `Dobby.Contracts/**` includes explicit plugin storage contract inventory (`IPluginStorage`, `IPluginStorageCompartment`, `PluginStorageKey`, `PluginStorageKeyPrefix`, `PluginStorageValue`), and `src/Dobby.SDK/PluginStorage/PluginStorageJsonExtensions.cs` is included as an SDK surface verification target.
- **Tests**: every existing test file enumerated in §10.1 has been read in full.
- **Build configuration and toolchain gate**: `docs/03_TECH_STACK_AND_VERSIONS.md`, `docs/09_BUILD_RUN_AND_RELEASE.md`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `Dobby.sln` opening lines, `Dobby.UnitTests.csproj`, `Dobby.IntegrationTests.csproj`, `Dobby.PluginFramework.csproj`, `Dobby.Infrastructure.csproj`, `Dobby.Security.csproj` — all read.
- **Evidence files**: `architecture-baseline.md`, `toolchain-pin-gate.md`, `auth-kdf-selection.md` — all read in full.

### Confidence Levels

- **High confidence**: the Core correlation/versioning corrections (§8.1-§8.2), the ADR-0004 `PolicyDecision` and ADR-0048 `OperationPermissionCapability` corrections (§8.9), the SDK PluginBase correction (§8.3-§8.5), the five new test files (§7.1-§7.5), the plugin exception/error mapping update (§8.10), the route identity update (§8.8), and the verification command sequence (§11). These have deterministic, bounded paths now that the print-boundary decision gate has a recorded path (a) outcome.
- **Medium confidence**: the print-boundary evidence update sections (§5.1, §8.6). The remaining implementation risk is preserving the recorded path (a) decision accurately while keeping the architecture-test allowance and final evidence consistent with the selected path.
- **Lower confidence (and explicitly out of Sprint 2 default scope)**: the print classifier migration that path (b) would require. The plan deliberately refuses to scope this into Sprint 2 default work without separate human authorization.

### Known Open Questions (for the implementation agent to surface)

None.

The agent does not invent a replacement decision. If the recorded path (a) choice is missing from implementation context, Sprint 2 halts at §9 Step 2 until the decision record is restored.

---

**End of Sprint 2 Execution Plan.**
