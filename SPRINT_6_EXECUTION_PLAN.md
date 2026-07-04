# Sprint 6 Execution Plan — Operation Authorization, Users, and Settings

## 1. Sprint Summary

- Sprint number and title: Sprint 6 — Operation Authorization, Users, and Settings, from `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6.
- Main goal: "Implement host authorization, user management, and the documented Settings/About surfaces." (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
- Expected deliverable: a host-owned operation authorization registry/policy, managed-user CRUD and grant/revoke service behavior, Owner/self-grant protections, and implementation-source Settings/Account/Security-Session/User Management/About surfaces without pulling Sprint 7 rendered Settings snapshots or Sprint 8 plugin activation seeding into this sprint (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
- Why this sprint exists: Sprint 6 consumes the Sprint 2-5 authentication, storage, session, recovery, audit, and pre-login persistence foundation listed as dependencies in `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 and verified in current release evidence at `artifacts/release-evidence/skeleton-local/architecture-baseline.md`, `artifacts/release-evidence/skeleton-local/storage-materialization.md`, and `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`.
- Downstream consumers: Sprint 7 consumes the Settings/User Management source behavior for rendered Settings routes and snapshots; Sprint 8 consumes the Sprint 6 permission registry for plugin `operationTargets` seeding before activation; later license and plugin-management flows consume the built-in `plugins.license` and `plugins.management` targets (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10; `docs/adr/ADR-0048-operation-based-authorization.md`).
- Readiness: READY FOR IMPLEMENTATION. Closed gates are recorded for Owner bootstrap/all-grants (`D-17`), Host-mediated audit composition (`D-16`), Sprint 5 package governance (`D-14`), no-new-package recovery JSON handling (`D-19`), the Sprint 6 built-in User Management target id (`D-20`), and the Sprint 6 Settings namespace/preference authority (`D-21`) in `artifacts/release-evidence/skeleton-local/decision-log.md`. The built-in User Management operation target id is `users.management`; supported verbs are `view`, `create`, `update`, `delete`, `execute`, and `grant` (`D-20`). `D-21` authorizes `Dobby.Host.Settings` only for Settings/About source orchestration and keeps durable Settings preference ownership in `Dobby.Configuration` with concrete `HostConfiguration` access supplied by Host-wired persistence.
- Strategy:
  1. Use the recorded User Management target id `users.management` from `D-20` and the recorded Settings namespace/preference authority from `D-21` before implementation.
  2. Add tests for the documented operation verb set, target registry, grant-only/default-deny semantics, Owner/self-grant guards, and User Management service behavior before implementation code (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 Verification).
  3. Extend the current authorization primitives and the existing narrow plugin-manifest validator verb path to match the documented closed verb set; do not expand Sprint 6 into full plugin manifest schema/package validation (`src/Dobby.Security/Authorization/OperationKey.cs`; `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs`; `docs/schemas/dobby-plugin-manifest.schema.json`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
  4. Implement Security-owned authorization and user-management services without adding a `Dobby.Security -> Dobby.Audit` reference (`src/Dobby.Security/Dobby.Security.csproj`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `artifacts/release-evidence/skeleton-local/decision-log.md` `D-16`).
  5. Wire Host-owned Settings/About orchestration/source behavior, `Dobby.Configuration`-owned typed Settings preference surface/abstractions, Host-wired `HostConfiguration` persistence, and Host-mediated audit coordination, leaving rendered Settings tree snapshots to Sprint 7 (`src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7; `D-21`).
- Dependency summary: consumes `Users`, `Credentials`, `Sessions`, `Permissions`, `AuditLog`, and `HostConfiguration` schema from Sprint 4, session/login/recovery runtime from Sprint 5, and policy contracts from `Dobby.Contracts`; unblocks Sprint 7 Settings rendering, Sprint 8 plugin target seeding, and later permission-guarded license/plugin flows (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `src/Dobby.Contracts/Policies/PolicyContracts.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`).

## 2. Sources Read

| Group | Source | Depth | Why it mattered / plan effect |
|---|---|---:|---|
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 | Deep | Defines Sprint 6 goal, task groups, dependencies, AC ownership, verification, risks, and exit condition. |
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 5 | Targeted | Establishes predecessor authentication/session/recovery scope consumed by Sprint 6. |
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7 | Boundary | Keeps rendered Settings tree, theme/localization polish, and snapshots out of Sprint 6. |
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8 | Boundary | Keeps plugin activation and plugin `operationTargets` seeding out of Sprint 6 while preserving the registry dependency. |
| Master sprint plan | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9 and §Sprint 10 | Boundary | Confirms `plugins.license` and `plugins.management` consumers are downstream, not Sprint 6 license/plugin implementations. |
| Previous sprint execution plans | `SPRINT_1_EXECUTION_PLAN.md` | Targeted | Dependency and architecture baseline context only. |
| Previous sprint execution plans | `SPRINT_2_EXECUTION_PLAN.md` | Targeted | Dependency and release-evidence continuity context only. |
| Previous sprint execution plans | `SPRINT_3_EXECUTION_PLAN.md` | Targeted | Dependency and continuity context only. |
| Previous sprint execution plans | `SPRINT_4_EXECUTION_PLAN.md` | Targeted | Storage and permission-table continuity context. |
| Previous sprint execution plans | `SPRINT_5_EXECUTION_PLAN.md` | Deep | Immediate predecessor plan; identifies Sprint 6 as owner for runtime authorization, managed-user permission rows, and Settings/User Management. |
| Later sprint references | Repository-root discovery for `SPRINT_*_EXECUTION_PLAN.md` files | Deep | Found no later sprint execution plan files; later boundaries come from `SPRINT_IMPLEMENTATION_PLAN.md`. |
| Repo-root files | `.editorconfig` | Targeted | Encoding/style boundary with `docs/08_CODING_STANDARDS.md`. |
| Repo-root files | `.gitignore` | Targeted | Confirms ignored build/test artifacts are not planning outputs. |
| Repo-root files | `Directory.Build.props` | Deep | Warnings-as-errors, nullable, C# language, deterministic build rules for Sprint 6. |
| Repo-root files | `Directory.Packages.props` | Deep | Central package pins and no-new-package planning boundary. |
| Repo-root files | `global.json` | Deep | SDK pin consumed by Sprint 6 implementation. |
| Repo-root files | `Dobby.sln` | Targeted | Solution boundary for expected build/test verification. |
| Repo-root files | `README.md` | Targeted | Repository orientation only; no scope expansion. |
| Product / architecture / requirements docs | `docs/01_PRODUCT_SCOPE.md` | Targeted | Settings/About and license-surface boundary context. |
| Product / architecture / requirements docs | `docs/02_ARCHITECTURE_MAP.md` | Deep | Project ownership, operation authorization form, User Management guard, Settings/About surface, and route authorization boundary. |
| Product / architecture / requirements docs | `docs/03_TECH_STACK_AND_VERSIONS.md` | Deep | TFM, package-governance, test-stack, WPF, and host version metadata rules. |
| Product / architecture / requirements docs | `docs/04_RESULT_VS_EXCEPTION_POLICY.md` | Deep | Result-vs-exception split and fixed Core `ErrorCode` registry. |
| Product / architecture / requirements docs | `docs/05_FUNCTIONAL_REQUIREMENTS.md` | Targeted | FR-003, FR-018, FR-028, FR-029, and FR-032 behavior anchors. |
| Product / architecture / requirements docs | `docs/07_ACCEPTANCE_CRITERIA.md` | Deep | AC-022, AC-030, AC-035, AC-039, and license/About boundaries. |
| Product / architecture / requirements docs | `docs/08_CODING_STANDARDS.md` | Deep | Namespace, ViewModel, auth, encoding, no-dumping-ground rules, and the `D-21` `Dobby.Host.Settings` placement row. |
| Product / architecture / requirements docs | `docs/09_BUILD_RUN_AND_RELEASE.md` | Deep | Build/test/evidence gate and absent deferred project boundaries. |
| ADRs | `docs/adr/ADR-0004-capability-enforcement-model.md` | Deep | `ICapabilityPolicy<TCapability>` and `PolicyDecision` enforcement model. |
| ADRs | `docs/adr/ADR-0028-shell-composition-ordering-render.md` | Deep | Shell/Profile/Settings surface boundary and Sprint 7 rendering context. |
| ADRs | `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` | Deep | Session invalidation, logout permission-cache clearing, password reset session effects. |
| ADRs | `docs/adr/ADR-0036-plugin-manifest-schema.md` | Deep | Manifest `operationTargets` shape and plugin target seeding source for later Sprint 8. |
| ADRs | `docs/adr/ADR-0038-local-first-authentication.md` | Deep | Local Owner bootstrap and no-cloud-identity boundary. |
| ADRs | `docs/adr/ADR-0040-internal-host-adapters-boundary.md` | Deep | Confirms user/app settings behavior belongs to `Dobby.Configuration`, Host wires persistence by default, and exact storage-facing abstraction names/namespaces require approved authority. |
| ADRs | `docs/adr/ADR-0041-login-first-startup-policy.md` | Deep | No plugin scan/activation/license verification before login. |
| ADRs | `docs/adr/ADR-0048-operation-based-authorization.md` | Deep | Canonical verbs, operation-key parsing, Owner/managed semantics, built-in license/plugin ids. |
| ADRs | `docs/adr/ADR-0051-license-lifecycle-v2.md` | Targeted | License import permission target and About-not-license boundary. |
| ADRs | `docs/adr/ADR-0052-tenant-customer-context-v2.md` | Deep | License import does not create customer/deployment context; no Sprint 6 schema widening. |
| ADRs | `docs/adr/ADR-0053-capability-surface-runtime-mode.md` | Deep | License runtime and print boundary remain later work. |
| Specs | `docs/specs/user-management.md` | Deep | User Management verbs, Owner guard, grant-only/default-deny, audit events, validation, CRUD/reset/reactivate. |
| Specs | `docs/specs/settings-and-about.md` | Deep | Settings tree, Account/Security/About fields, About privacy, license import exclusion. |
| Specs | `docs/specs/license-import.md` | Deep | `plugins.license` target and license import out-of-scope boundary. |
| Specs | `docs/specs/audit-and-diagnostics.md` | Targeted | Audit taxonomy and diagnostics-vs-audit distinction. |
| Specs | `docs/specs/session-lifecycle.md` | Deep | Deactivated user session and password reset behavior. |
| Specs | `docs/specs/sample-plugin.md` | Targeted | Plugin target example remains Sprint 8/sample scope, not Sprint 6 activation. |
| Specs | `docs/specs/plugins-manager.md` | Targeted | `plugins.management` target id for built-in registry boundary. |
| Schemas | `docs/schemas/dobby-plugin-manifest.schema.json` | Deep | Confirms schema allows all canonical verbs including `configure` and `grant`. |
| Source code | `src/Dobby.Security/Authorization/OperationKey.cs` | Deep | Existing parser splits on last dot but verb enum currently omits `configure` and `grant`. |
| Source code | `src/Dobby.Contracts/Policies/PolicyContracts.cs` | Deep | Existing policy contract and `OperationPermissionCapability`. |
| Source code | `src/Dobby.PluginFramework/Manifest/PluginManifest.cs` | Deep | Existing typed manifest `PluginOperationTarget`. |
| Source code | `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs` | Deep | Current validation flows through the incomplete `OperationVerbParser`. |
| Source code | `src/Dobby.Audit/AuditEventName.cs` | Deep | Existing user, permission, owner-protection, license, and plugin audit event constants. |
| Source code | `src/Dobby.Audit/Persistence/DurableAuditTrail.cs` | Targeted | Durable audit append path for Host-mediated audit coordination. |
| Source code | `src/Dobby.Audit/IHostTransactionAuditTrail.cs` | Targeted | Host transaction audit seam used to avoid `Dobby.Security -> Dobby.Audit`. |
| Source code | `src/Dobby.Audit/AuditEvent.cs` | Targeted | Audit payload and result model. |
| Source code | `src/Dobby.Audit/AuditResult.cs` | Targeted | Audit result values for denied/success/failed mapping. |
| Source code | `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs` | Deep | Existing `Users`, `Credentials`, `Sessions`, `Permissions`, and `AuditLog` schema. |
| Source code | `src/Dobby.Security/Authentication/SessionStore.cs` | Deep | Current active-user/session operations consumed by AC-039. |
| Source code | `src/Dobby.Security/Authentication/LoginService.cs` | Deep | Current future-login-deny behavior for inactive users. |
| Source code | `src/Dobby.Security/Authentication/OwnerBootstrapService.cs` | Deep | Current Owner `Users.IsOwner=1` bootstrap behavior. |
| Source code | `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` | Deep | Current Host composition root for persistence/auth/session/recovery services. |
| Source code | `src/Dobby.Shell/ShellViewModel.cs` | Deep | Current shell exposes Settings route without Sprint 6 permission-gated Settings source behavior. |
| Source code | `src/Dobby.Shell/ShellRouteHost.cs` | Deep | Current shell route activation boundary. |
| Source code | `src/Dobby.Host/MainWindow.xaml` | Deep | Current post-login placeholder/account UI boundary. |
| Source code | `src/Dobby.Host/MainWindow.xaml.cs` | Deep | Current lock/password/remember-me wiring boundary. |
| Source code | `src/Dobby.Security/Dobby.Security.csproj` | Deep | Current Security project references include Infrastructure/Configuration but not Audit. |
| Source code | `src/Dobby.Host/Dobby.Host.csproj` | Deep | Host reference surface can coordinate Security, Audit, Shell, Localization. |
| Source code | `src/Dobby.Configuration/Dobby.Configuration.csproj` | Deep | Current Configuration project has no Infrastructure reference; supports `D-21`'s Host-wired persistence boundary without a direct `Dobby.Configuration -> Dobby.Infrastructure` edge. |
| Source code | `src/Dobby.Shell/Dobby.Shell.csproj` | Deep | Current Shell does not reference Security; preserve that absence by default, while recognizing `docs/02_ARCHITECTURE_MAP.md` allows `Dobby.Shell -> Dobby.Security` if route-gating requires it and tests cover the edge. |
| Source code | `src/Dobby.Localization/HostStringCatalog.cs` | Deep | Current host resource-key pattern and missing About/User Management keys. |
| Tests | `tests/Dobby.UnitTests/Security/SecurityTests.cs` | Targeted | Existing parser behavior basis; Sprint 6 adds dedicated `OperationVerbParserTests.cs` coverage for canonical verbs. |
| Tests | `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` | Targeted | Existing manifest validation tests to extend for `configure`/`grant`. |
| Tests | `tests/Dobby.UnitTests/Audit/AuditEventNameTests.cs` | Targeted | Existing audit-event closed-set test boundary. |
| Tests | `tests/Dobby.UnitTests/Localization/HostLoginRecoveryStringCatalogTests.cs` | Targeted | Current resource-key coverage pattern. |
| Tests | `tests/Dobby.UnitTests/Core/HostVersionTests.cs` | Targeted | Existing `IHostVersionProvider` and `HostVersion` behavior for About source. |
| Tests | `tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs` | Targeted | Existing inactive-user login-deny baseline. |
| Tests | `tests/Dobby.IntegrationTests/Auth/SelfServicePasswordChangeTests.cs` | Targeted | Existing session invalidation/password-change context. |
| Tests | `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapTransactionTests.cs` | Targeted | Owner bootstrap transaction context. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` | Deep | Project-reference, no `Dobby.Security -> Dobby.Audit`, ErrorCode, and deferred-project guardrails. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | Deep | Package pin and approval evidence guardrails. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs` | Targeted | Plugin-visible boundary guard. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs` | Targeted | UTF-8/no-BOM/final-newline gate. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs` | Targeted | Existing release-evidence convention. |
| Tests | `tests/Dobby.IntegrationTests/Architecture/Sprint5ReleaseEvidenceTests.cs` | Targeted | Sprint-specific evidence-test pattern. |
| Project / config / build files | `tests/Dobby.UnitTests/Dobby.UnitTests.csproj` | Targeted | Unit test project references already include Host/Security surfaces. |
| Project / config / build files | `tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj` | Targeted | Integration test project references already include Host/Security surfaces. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/decision-log.md` | Deep | Closed decisions `D-13` through `D-21`, including the Sprint 6 User Management target id `users.management` and Settings namespace/preference authority. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` | Deep | Approved package/native-asset pins and package governance boundary. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/storage-materialization.md` | Deep | Schema, permission table, storage decisions, and Sprint 4 verification evidence. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md` | Deep | Sprint 5 authentication/KDF/session closure and test evidence. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/test-fixture-authority.md` | Targeted | Test-only recovery fixture boundary; no Sprint 6 production key work. |
| Release / evidence artifacts | `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | Deep | Current project graph, Sprint 5 verification baseline, and remaining deferred work. |

Total inspected source entries counted for this plan: 90.

## 3. Sprint Scope From Master Sprint Plan

- Master-plan goal quote: "Implement host authorization, user management, and the documented Settings/About surfaces." (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
- Master-plan task groups:
  1. Operation Registry: built-in targets, canonical verbs, key parsing, Owner all-grants, managed default-deny, no explicit deny, and non-disclosing failure (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
  2. User Management: create/update/deactivate/reactivate/reset-password/grant-revoke with service validation and DB constraints (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
  3. Owner / Self-Grant Guards: no Owner mutation by managed users, no self-grant, no grant of permissions absent from actor (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
  4. Settings and Account: Settings tree source behavior for Appearance, Account, Security/Session, User Management, About; Account password change controls; About version/device/support/plugin summary; license import under Plugins > License, not About (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `docs/specs/settings-and-about.md`).
- Acceptance / exit expectations and AC ownership:
  - Primary Sprint 6 AC ownership: `AC-022`, `AC-030`, and `AC-039` (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `docs/07_ACCEPTANCE_CRITERIA.md`).
  - `AC-022` Sprint 6 contribution boundary: built-in targets, canonical verbs, grant-only authorization, Owner/self-grant guards, unknown target/verb deny behavior, and service-boundary enforcement; Sprint 8 owns seeding plugin-declared targets from validated signed manifest `operationTargets` before activation (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8; `docs/adr/ADR-0036-plugin-manifest-schema.md`).
  - `AC-035` Sprint 6 contribution: Settings/About source behavior and service/view-model tests; rendered Settings tree/snapshots/manual visual evidence are Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
  - Exit condition: Settings/User Management operations work through the permission model and cannot bypass Owner protections (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
- Dependencies:
  - Sprint dependencies: Sprints 2-5 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
  - ADR dependencies: `ADR-0004`, `ADR-0028`, `ADR-0031`, and `ADR-0048` (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
  - Spec dependencies: `docs/specs/settings-and-about.md`, `docs/specs/user-management.md`, and `docs/specs/license-import.md` (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6).
- Later sprints depending on this sprint:
  - Sprint 7 depends on Settings/User Management source behavior for route/render/snapshot closure (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
  - Sprint 8 depends on the permission registry for plugin `operationTargets` seeding before activation (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
  - Later plugin manager/license/print flows consume the same operation authorization model but do not move their implementation into Sprint 6 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10; `docs/adr/ADR-0053-capability-surface-runtime-mode.md`).
- Master-plan explicit out-of-scope items relevant here:
  - Sprint 7 rendered Settings tree, screenshots, and theme/localization final polish are not Sprint 6 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
  - Sprint 8 plugin install/activation/runtime and plugin `operationTargets` seeding are not Sprint 6 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
  - License import implementation is later work; Sprint 6 only consumes the documented built-in target boundary where needed (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `docs/specs/license-import.md`).

## 4. Prior Sprint Continuity Check

| Prior sprint | Verified present in current repo | Missing / carry-over visible to Sprint 6 | Readiness effect |
|---|---|---|---|
| Sprint 1 | Architecture and test guardrails exist through `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`, `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`, and `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. | Temporary print-boundary evidence remains deferred and must not be widened in Sprint 6 (`artifacts/release-evidence/skeleton-local/architecture-baseline.md`; `docs/02_ARCHITECTURE_MAP.md`). | No blocker for Sprint 6 if project-reference boundaries remain unchanged. |
| Sprint 2 | Core/Contracts/SDK policy primitives exist in `src/Dobby.Contracts/Policies/PolicyContracts.cs`, including `ICapabilityPolicy<TCapability>`, `PolicyContext`, `PolicyDecision`, and `OperationPermissionCapability`. | Shell, navigation, localization, full Settings rendering, and snapshots are later Sprint 7 boundary context, not Sprint 2 continuity (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 2; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7). | Sprint 6 can build authorization on the established policy vocabulary while leaving rendered Settings and shell visual completion to Sprint 7. |
| Sprint 3 | Configuration/diagnostics/runtime folder tests exist under `tests/Dobby.IntegrationTests/Configuration/`, `tests/Dobby.IntegrationTests/Diagnostics/`, and `tests/Dobby.IntegrationTests/Runtime/`. | No Sprint 6-specific carry-over found; no telemetry or online behavior should be introduced (`docs/03_TECH_STACK_AND_VERSIONS.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`). | No blocker. |
| Sprint 4 | Durable pre-login schema is present in `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; evidence verifies `Users`, `Credentials`, `Sessions`, `Permissions`, `HostConfiguration`, and `AuditLog` materialization in `artifacts/release-evidence/skeleton-local/storage-materialization.md`. | Sprint 4 intentionally left managed-user permission rows, registry/verb inventory, runtime authorization, and Settings preference source behavior to Sprint 6 (`artifacts/release-evidence/skeleton-local/storage-materialization.md`; `SPRINT_5_EXECUTION_PLAN.md`). | Sprint 6 consumes the schema and implements runtime/source behavior; it does not redo storage materialization. |
| Sprint 5 | Login/session/recovery code exists in `src/Dobby.Security/Authentication/`, Host runtime wiring exists in `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`, and Sprint 5 closure evidence is recorded in `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md` and `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. | Sprint 5 explicitly kept authorization runtime, managed-user CRUD, and Settings/User Management outside Sprint 5 (`SPRINT_5_EXECUTION_PLAN.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6). | Sprint 6 can proceed because the User Management target id authority gate is closed by `D-20` and the Settings namespace/preference authority gate is closed by `D-21`. |

Carry-over blockers and risks:

- The `Permissions` table exists with `UNIQUE(UserId, PermissionKey)` and no explicit deny column, which supports Sprint 6 grant-only semantics (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`).
- The `HostConfiguration` table exists and is the planned backing store for Sprint 6 Settings preference source behavior, including Appearance theme preference and Security/Session auto-lock source state, without schema widening; durable Settings preference ownership remains in `Dobby.Configuration` and concrete table access is supplied by Host-wired persistence (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `D-21`; Assumption A6).
- Owner all-grants is represented by `Users.IsOwner=1`, not an Owner `Permissions` row, per `D-17` (`artifacts/release-evidence/skeleton-local/decision-log.md`).
- `Dobby.Security` must remain audit-neutral and must not reference `Dobby.Audit`; Host coordination is the existing approved audit pattern (`src/Dobby.Security/Dobby.Security.csproj`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `artifacts/release-evidence/skeleton-local/decision-log.md` `D-16`).

Readiness conclusion: predecessor outputs required for planning are present and verified against current code/evidence, and implementation may start using the built-in User Management target id `users.management` recorded in `D-20` and the Settings namespace/preference authority recorded in `D-21`.

## 5. Current Codebase State

| Area | Current files / namespaces / surfaces | Sprint 6 relevance |
|---|---|---|
| Policy contract | `src/Dobby.Contracts/Policies/PolicyContracts.cs` defines `Dobby.Contracts.Policies.ICapabilityPolicy<TCapability>`, `PolicyContext`, `PolicyDecision`, and `OperationPermissionCapability`. | Sprint 6 policy must implement the ADR-0004 typed capability pattern without introducing a god-interface (`docs/adr/ADR-0004-capability-enforcement-model.md`). |
| Operation key parser | `src/Dobby.Security/Authorization/OperationKey.cs` parses operation keys by the last dot and has `Dobby.Security.Authorization.OperationVerb`. | Last-dot parsing aligns with `docs/adr/ADR-0048-operation-based-authorization.md`, but the enum/parser currently omit `configure` and `grant`, which conflicts with `docs/05_FUNCTIONAL_REQUIREMENTS.md`, `docs/schemas/dobby-plugin-manifest.schema.json`, and `docs/adr/ADR-0048-operation-based-authorization.md`. |
| Plugin manifest target model | `src/Dobby.PluginFramework/Manifest/PluginManifest.cs` has `PluginOperationTarget`; `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs` validates verbs through `OperationVerbParser`. | Updating the parser affects manifest validation; Sprint 6 should align the validator with the closed verb set without implementing Sprint 8 plugin seeding (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8). |
| Storage schema | `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs` creates `Users`, `Credentials`, `Sessions`, `Permissions`, `HostConfiguration`, and `AuditLog`. | Supports managed users, credentials, active flags, session invalidation, grant rows, Settings preference source state, and audit entries required by `docs/specs/user-management.md` and `docs/specs/settings-and-about.md`. |
| Authentication/session services | `src/Dobby.Security/Authentication/LoginService.cs`, `src/Dobby.Security/Authentication/SessionStore.cs`, `src/Dobby.Security/Authentication/OwnerBootstrapService.cs`, and related stores already exist. | Sprint 6 should integrate deactivation and reset behavior with existing stores rather than introducing a parallel authentication surface (`docs/specs/session-lifecycle.md`). |
| Audit surface | `src/Dobby.Audit/AuditEventName.cs`, `src/Dobby.Audit/IHostTransactionAuditTrail.cs`, and `src/Dobby.Audit/Persistence/DurableAuditTrail.cs` exist. | Required events already exist; Sprint 6 must not invent audit event names and must use Host-mediated audit coordination because `Dobby.Security` cannot reference `Dobby.Audit` (`artifacts/release-evidence/skeleton-local/decision-log.md` `D-16`). |
| Host composition | `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` wires persistence, audit, owner bootstrap, login, session, remember-me, recovery, and self-service password change. | Natural integration point for Sprint 6 Host-facing user management/settings services; modifications must preserve pre-login policy from `docs/adr/ADR-0041-login-first-startup-policy.md`. |
| Shell and Settings route | `src/Dobby.Shell/ShellViewModel.cs` exposes Settings navigation; `src/Dobby.Shell/ShellRouteHost.cs` activates simple routes; `src/Dobby.Shell/Dobby.Shell.csproj` does not reference `Dobby.Security`. | Sprint 6 default posture is Host-owned Settings source behavior with the current Shell-to-Security absence preserved. `Dobby.Shell -> Dobby.Security` is allowed by `docs/02_ARCHITECTURE_MAP.md` if real route-gating needs it, but that edge should be added only deliberately and covered by architecture/dependency tests. |
| Host UI placeholder | `src/Dobby.Host/MainWindow.xaml` and `src/Dobby.Host/MainWindow.xaml.cs` contain current post-login lock/password/remember-me placeholder behavior. | Sprint 6 may wire source/view-model behavior for Settings/About but must leave Sprint 7 rendered Settings tree/snapshot closure to Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7). |
| Settings preference source | No dedicated typed Settings preference surface exists yet; the existing `HostConfiguration` table is available through Host-wired persistence, and `src/Dobby.Configuration/Dobby.Configuration.csproj` currently has no direct Infrastructure reference. | Sprint 6 must make Appearance, Account, and Security/Session source behavior testable against the same durable preference source Sprint 7 will consume, without adding a new table/schema column or a direct `Dobby.Configuration -> Dobby.Infrastructure` edge (`docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `D-21`; Assumption A6). |
| Localization | `src/Dobby.Localization/HostStringCatalog.cs` contains current `HostResourceKeys` and az-AZ strings. | Sprint 6 needs new resource keys such as neutral `About.OwnerStatus.Registered`, User Management labels, and non-disclosing denial text without leaking internal reasons (`docs/specs/settings-and-about.md`; `docs/specs/user-management.md`). |

Mismatches the sprint must address or avoid:

- Address: documented verb set includes `configure` and `grant`, but current parser/manifest validator do not accept them (`src/Dobby.Security/Authorization/OperationKey.cs`; `docs/schemas/dobby-plugin-manifest.schema.json`; `docs/adr/ADR-0048-operation-based-authorization.md`).
- Address using `D-20`: User Management permission keys, registry entries, grants, and tests must use the built-in target id `users.management` (`artifacts/release-evidence/skeleton-local/decision-log.md`).
- Avoid: do not add new Core `ErrorCode` entries for authorization detail; the registry is fixed in `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` and `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.
- Avoid: do not add `Dobby.Security -> Dobby.Audit`; current architecture test and `D-16` forbid that edge (`src/Dobby.Security/Dobby.Security.csproj`; `artifacts/release-evidence/skeleton-local/decision-log.md`).

## 6. Documentation and ADR Alignment

Documents fully aligned for the target sprint and requiring no planning-time edits:

- `docs/specs/user-management.md`: aligns with Sprint 6 CRUD, grant-only/default-deny, Owner/self-grant, audit, username/password validation, and deactivation requirements.
- `docs/specs/settings-and-about.md`: aligns with Sprint 6 Settings/About source behavior and Sprint 7 rendered evidence split.
- `docs/specs/license-import.md`: aligns with About-not-license and `plugins.license` target boundary.
- `docs/specs/session-lifecycle.md`: aligns with active deactivation and reset session invalidation behavior.
- `docs/07_ACCEPTANCE_CRITERIA.md`: aligns with `AC-022`, `AC-030`, `AC-035`, and `AC-039` coverage.

Documents requiring implementation-time evidence appends only:

- `artifacts/release-evidence/skeleton-local/architecture-baseline.md`: append Sprint 6 architecture/test/AC evidence after implementation, following Sprint 5 evidence style.
- `artifacts/release-evidence/skeleton-local/decision-log.md`: cites `D-20`, the human-approved User Management target id decision, and `D-21`, the human-approved Settings namespace/preference authority; implementation must not invent or vary the id in code or tests, and must not create Host-owned durable Settings preference storage.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`: no append expected if Sprint 6 uses existing pins only; append only if a human-approved package/native asset decision is made.

Documents that may need an update note after implementation:

- No ADR/spec/schema content edit is planned. If implementation discovers the existing docs are insufficient beyond the User Management target id decision, stop and record a human decision in `artifacts/release-evidence/skeleton-local/decision-log.md` before changing docs (`docs/08_CODING_STANDARDS.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`).

ADR alignment classification:

| ADR | Classification | Sprint 6 action |
|---|---|---|
| `docs/adr/ADR-0004-capability-enforcement-model.md` | Aligned | Implement typed `ICapabilityPolicy<OperationPermissionCapability>` behavior using `src/Dobby.Contracts/Policies/PolicyContracts.cs`. |
| `docs/adr/ADR-0028-shell-composition-ordering-render.md` | Aligned with boundary | Use Host/Shell Settings ownership but leave rendered ordering/snapshot closure to Sprint 7. |
| `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` | Aligned | Enforce deactivation/reset invalidation and permission-cache/session clearing semantics. |
| `docs/adr/ADR-0036-plugin-manifest-schema.md` | Aligned with boundary | Keep plugin `operationTargets` typed model compatible with the canonical verb set through the existing validator path; do not implement Sprint 8 package/schema validation or activation seeding. |
| `docs/adr/ADR-0038-local-first-authentication.md` | Aligned via `D-17` | Owner all-grants source is `Users.IsOwner=1`, not an Owner permission row. |
| `docs/adr/ADR-0040-internal-host-adapters-boundary.md` | Aligned via `D-21` | `Dobby.Configuration` owns durable Settings preference behavior; `Dobby.Host.Settings` is authorized only for authenticated Settings/About orchestration/source models/wiring, with concrete `HostConfiguration` access supplied by Host-wired persistence and no direct `Dobby.Configuration -> Dobby.Infrastructure` edge. |
| `docs/adr/ADR-0041-login-first-startup-policy.md` | Aligned | No plugin scan/activation/license verification or Settings runtime before authenticated host flow. |
| `docs/adr/ADR-0048-operation-based-authorization.md` | Aligned via `D-20` | Canonical verbs and built-in license/plugin ids are aligned; built-in User Management target id is supplied by the approved decision-log anchor `D-20`. |
| `docs/adr/ADR-0052-tenant-customer-context-v2.md` | No action | Sprint 6 must not create/change customer/deployment context. |
| `docs/adr/ADR-0053-capability-surface-runtime-mode.md` | Boundary only | License runtime/print behavior remains later work. |

Specs and schemas alignment:

- `docs/schemas/dobby-plugin-manifest.schema.json` allows `configure` and `grant`, so Sprint 6 must update `OperationVerbParser` and verify the current validator path accepts those canonical verbs while still rejecting unknown verbs. Full target-id pattern enforcement, duplicate verb item validation, package/signature validation, and activation/seeding remain Sprint 8 work (`src/Dobby.Security/Authorization/OperationKey.cs`; `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
- `docs/specs/plugins-manager.md` and `docs/specs/license-import.md` provide exact built-in ids `plugins.management` and `plugins.license`; `D-20` provides the exact User Management id `users.management`.

Net documentation footprint summary: no docs, ADRs, specs, schemas, or master sprint plan edits are part of Sprint 6 implementation, except appending release evidence that cites the recorded `D-20` and `D-21` decisions.

## 7. Files to Create

Naming rule: every new file must map to a documented role in `SPRINT_IMPLEMENTATION_PLAN.md`, an ADR/spec/schema, current architecture map row, current code namespace, or an accepted implementation assumption in §7a. If implementation discovers an unavoidable new name not covered here, stop and record it as an implementation assumption or human decision before creating the file.

Create Sprint 6 source/test files using the built-in User Management target id `users.management` recorded in `D-20`; do not introduce aliases or placeholder ids in test names, permission keys, registry fixtures, or audit assertions (`docs/specs/user-management.md`; `docs/adr/ADR-0048-operation-based-authorization.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`). Create Settings/About source files under the `D-21` boundary: `Dobby.Host.Settings` is for orchestration/source models/wiring only, while durable Settings preference behavior belongs to `Dobby.Configuration` and concrete `HostConfiguration` access remains Host-wired.

| Placement | Filename | Purpose | Source basis | Associated tests | Boundary / architecture restrictions |
|---|---|---|---|---|---|
| `src/Dobby.Security/Authorization/` | `OperationTarget.cs` | Represent operation target id, supported verbs, label key, and built-in/plugin target origin. | `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/adr/ADR-0036-plugin-manifest-schema.md`; current `src/Dobby.Security/Authorization/OperationKey.cs`. | `tests/Dobby.UnitTests/Security/OperationTargetRegistryTests.cs`. | Must not reference Host, Shell, UI, or Audit (`src/Dobby.Security/Dobby.Security.csproj`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`). |
| `src/Dobby.Security/Authorization/` | `OperationTargetRegistry.cs` | Register built-in targets, support validated plugin target registration inputs, reject unknown target/verb fail-closed. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `docs/adr/ADR-0048-operation-based-authorization.md`; Assumption A1. | `tests/Dobby.UnitTests/Security/OperationTargetRegistryTests.cs`; `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs`. | Must not implement Sprint 8 plugin activation/seeding (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8). |
| `src/Dobby.Security/Authorization/` | `BuiltInOperationTargets.cs` | Centralize built-in `plugins.license`, `plugins.management`, and `users.management` target registration. | `docs/specs/license-import.md`; `docs/specs/plugins-manager.md`; `docs/adr/ADR-0048-operation-based-authorization.md`; `D-20`. | `tests/Dobby.UnitTests/Security/OperationTargetRegistryTests.cs`. | User Management id must be exactly `users.management` from `D-20`, not an alias or route-derived string. |
| `src/Dobby.Security/Authorization/` | `PermissionGrantStore.cs` | Read/write grant-only permission rows from the existing `Permissions` table. | `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; Assumption A1. | `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs`; `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs`. | Must use existing table shape; schema widening requires human decision. |
| `src/Dobby.Security/Authorization/` | `OperationAuthorizationPolicy.cs` | Implement `ICapabilityPolicy<OperationPermissionCapability>` with Owner all-grants, managed default-deny, and non-disclosing deny. | `docs/adr/ADR-0004-capability-enforcement-model.md`; `docs/adr/ADR-0048-operation-based-authorization.md`; `src/Dobby.Contracts/Policies/PolicyContracts.cs`; `D-17`. | `tests/Dobby.UnitTests/Security/OperationAuthorizationPolicyTests.cs`; `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs`. | Must return `Result<PolicyDecision>` and avoid new Core `ErrorCode` values (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`). |
| `src/Dobby.Security/Authorization/` | `UserManagementAuthorizationGuard.cs` | Enforce Owner protection, self-grant prohibition, and "cannot grant what actor lacks" checks. | `docs/specs/user-management.md`; `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/07_ACCEPTANCE_CRITERIA.md` `AC-022`. | `tests/Dobby.UnitTests/Security/UserManagementAuthorizationGuardTests.cs`; `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs`. | Must not rely on UI hiding as authorization (`docs/08_CODING_STANDARDS.md`). |
| `src/Dobby.Security/Authentication/` | `ManagedUserService.cs` | List/detail managed users for the documented `view` operation, and create/update/deactivate/reactivate/reset password and grant/revoke managed users using existing users/credentials/sessions/permissions stores. | `docs/specs/user-management.md`; `src/Dobby.Security/Authentication/CredentialStore.cs`; `src/Dobby.Security/Authentication/SessionStore.cs`; Assumption A2. | `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs`. | Must not expose password hashes, protected credential material, or Owner-mutation controls through list/detail results; must not implement Owner recovery; managed reset is separate from Owner recovery (`docs/specs/user-management.md`); stop for architecture review if implementation cannot keep the service persistence-adjacent without expanding Authentication responsibilities. |
| `src/Dobby.Host/Settings/` | `UserManagementAuditCoordinator.cs` | Host-owned audit wrapper for User Management actions and deny outcomes. | `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Audit/AuditEventName.cs`; `src/Dobby.Audit/IHostTransactionAuditTrail.cs`; `D-16`; Assumption A3. | `tests/Dobby.UnitTests/Host/UserManagementAuditCoordinatorTests.cs`; `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs`. | Keeps `Dobby.Security` audit-neutral; Host may reference both Security and Audit (`src/Dobby.Host/Dobby.Host.csproj`). |
| `src/Dobby.Host/Settings/` | `SettingsNavigationModel.cs` | Build authenticated Settings categories, permission-gated User Management entry state, and source availability for User Management list/detail behavior backed by `view`. | `docs/specs/settings-and-about.md`; `docs/specs/user-management.md`; `docs/02_ARCHITECTURE_MAP.md` Settings/About Surface; Assumption A3. | `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`. | No full rendered tree/snapshot closure; Sprint 7 owns render (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7). |
| `src/Dobby.Configuration/` | Typed Settings preference surface files; exact durable preference abstraction class name is not pre-approved by this plan. | Define typed Settings preference behavior for Appearance theme preference and Security/Session source state without implying Host storage ownership. | `docs/02_ARCHITECTURE_MAP.md` row 46; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `D-21`; Assumption A6. | `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | Must not introduce direct `Dobby.Configuration -> Dobby.Infrastructure` access; concrete `HostConfiguration` table access is supplied by Host-wired persistence outside `Dobby.Configuration`; no plugin-facing settings access or public Contracts/SDK settings surface. |
| `src/Dobby.Host/Settings/` | `AppearanceSettingsModel.cs` | Represent `Settings > Appearance` theme choice source behavior and read/write the same `Dobby.Configuration` typed Settings preference surface that the Sprint 7 TopToolbar quick switch must consume. | `docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6/7 split; `D-21`; Assumption A3; Assumption A6. | `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`; `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | No rendered TopToolbar interaction, resource-dictionary theme switching, snapshots, or manual visual evidence in Sprint 6. |
| `src/Dobby.Host/Settings/` | `AboutInfoProvider.cs` | Provide support-safe About fields from build/project metadata, redacted device code, plugin summary placeholders, support/legal copy. | `docs/specs/settings-and-about.md`; `docs/03_TECH_STACK_AND_VERSIONS.md` Build Metadata And Version Source; Assumption A4. | `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs`. | Must not expose raw hardware fingerprint, protected key material, token, session, raw license, or plugin secret (`docs/specs/settings-and-about.md`). |
| `src/Dobby.Host/Settings/` | `AccountSettingsModel.cs` | Represent Account password-change controls as shortcuts to existing Account/session behavior without duplicating manual lock/logout. | `docs/specs/settings-and-about.md`; existing `src/Dobby.Host/MainWindow.xaml.cs` account placeholder behavior; `src/Dobby.Host/Login/SelfServicePasswordChangeCoordinator.cs`. | `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`. | No direct OS APIs from ViewModels; Settings must not add manual lock/logout actions (`docs/08_CODING_STANDARDS.md`; `docs/specs/settings-and-about.md`). |
| `src/Dobby.Host/Settings/` | `SecuritySessionSettingsModel.cs` | Represent default-passive auto-lock toggle/minute input source behavior backed by the `Dobby.Configuration` typed Settings preference surface and Host-wired persistence. | `docs/specs/settings-and-about.md`; `docs/adr/ADR-0031-session-lifecycle-and-refresh.md`; `D-21`; Assumption A6. | `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`; `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | Must not weaken session-security floors or change runtime session lifecycle semantics without source evidence (`docs/adr/ADR-0031-session-lifecycle-and-refresh.md`). |
| `tests/Dobby.UnitTests/Security/` | `OperationVerbParserTests.cs` | Cover all canonical verbs, last-dot parsing, unknown/unsupported verb failure. | `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-018; current `tests/Dobby.UnitTests/Security/SecurityTests.cs`. | Self. | Create this dedicated parser test file for Sprint 6 canonical verb coverage. |
| `tests/Dobby.UnitTests/Security/` | `OperationTargetRegistryTests.cs` | Cover built-in targets, supported verb toggles, no explicit deny surface, unknown target behavior, and the User Management `delete` verb as the supported lifecycle deactivate/reactivate permission rather than hard delete. | `docs/07_ACCEPTANCE_CRITERIA.md` `AC-022`; `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/specs/user-management.md`; `D-20`. | Self. | User Management target id test must assert `users.management` exactly and must not introduce a hard-delete target or operation. |
| `tests/Dobby.UnitTests/Security/` | `OperationAuthorizationPolicyTests.cs` | Cover Owner all-grants, managed default-deny, grant rows, non-disclosing deny. | `docs/adr/ADR-0004-capability-enforcement-model.md`; `D-17`; `docs/specs/user-management.md`. | Self. | No new Core `ErrorCode`. |
| `tests/Dobby.UnitTests/Security/` | `UserManagementAuthorizationGuardTests.cs` | Cover Owner protection, self-grant, and cannot-grant-what-actor-lacks. | `docs/specs/user-management.md`; `docs/07_ACCEPTANCE_CRITERIA.md` `AC-022`. | Self. | Must assert service-boundary enforcement, not UI-only hiding. |
| `tests/Dobby.IntegrationTests/Security/` | `OperationAuthorizationIntegrationTests.cs` | Exercise durable permissions, policy evaluation, unsupported target/verb deny, audit coordination boundary. | `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `docs/specs/audit-and-diagnostics.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 Verification. | Self. | Must not require plugin activation. |
| `tests/Dobby.IntegrationTests/Auth/` | `UserManagementServiceTests.cs` | Exercise list/detail under `view`; create/update/deactivate/reactivate/reset/grant/revoke; DB constraints; session invalidation; future-login deny; and the rule that the `delete` permission gates deactivate/reactivate lifecycle changes rather than physical hard delete. | `docs/specs/user-management.md`; `docs/specs/session-lifecycle.md`; `docs/07_ACCEPTANCE_CRITERIA.md` `AC-030` and `AC-039`. | Self. | Must use existing test fixture patterns under `tests/Dobby.IntegrationTests/Auth/`, must assert list/detail results do not expose password hashes or protected credential material, and must not create or exercise a hard-delete code path. |
| `tests/Dobby.UnitTests/Host/` | `UserManagementAuditCoordinatorTests.cs` | Cover Host-owned User Management audit wrapping for success, denial, and Owner-protection outcomes. | `docs/specs/audit-and-diagnostics.md`; `docs/specs/user-management.md`; `src/Dobby.Audit/AuditEventName.cs`; `D-16`; Assumption A3. | Self; `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs`. | Must use existing audit event names only and keep `Dobby.Security` audit-neutral. |
| `tests/Dobby.UnitTests/Host/` | `SettingsAndAboutViewModelTests.cs` | Exercise Settings category source behavior, Appearance shared theme source behavior through the `Dobby.Configuration` preference surface, Account password-change shortcut, absence of manual lock/logout duplication, Security/Session default-passive auto-lock toggle/minute input state, User Management permission-gating plus list/detail source availability through `view`, and About no-license boundary. | `docs/specs/settings-and-about.md`; `docs/specs/user-management.md`; `docs/07_ACCEPTANCE_CRITERIA.md` `AC-035`; `D-21`; Assumption A3; Assumption A6. | Self; `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`. | No snapshot/rendered-tree assertions; Sprint 7 owns them. |
| `tests/Dobby.UnitTests/Host/` | `AboutInfoProviderTests.cs` | Exercise support-safe About fields and no raw secret/license/session exposure. | `docs/specs/settings-and-about.md`; `docs/03_TECH_STACK_AND_VERSIONS.md`. | Self. | Must use existing `Dobby.Core.Versioning` primitives when possible. |
| `tests/Dobby.UnitTests/Localization/` | `SettingsLocalizationStringCatalogTests.cs` | Exercise new Settings/About/User Management resource keys, including neutral `About.OwnerStatus.Registered`, without overloading the Sprint 5 login/recovery localization test. | `docs/specs/settings-and-about.md`; `docs/specs/user-management.md`; `docs/07_ACCEPTANCE_CRITERIA.md` `AC-035`. | Self. | Must assert non-disclosing user-facing strings and avoid exposing internal authorization deny reasons. |
| `tests/Dobby.UnitTests/Configuration/` | `SettingsPreferenceSurfaceTests.cs` | Prove the `Dobby.Configuration` typed Settings preference surface exposes Appearance theme preference and Security/Session source-state semantics without concrete storage access. | `docs/02_ARCHITECTURE_MAP.md` row 46; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `docs/specs/settings-and-about.md`; `D-21`; Assumption A6. | Self; `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`. | Must not require `Dobby.Configuration` to reference `Dobby.Infrastructure`; no exact durable preference storage class name is approved. |
| `tests/Dobby.IntegrationTests/Host/` | `SettingsPreferencePersistenceTests.cs` | Prove Settings preference source values persist through Host-wired persistence backed by the existing `HostConfiguration` table for theme preference and Security/Session auto-lock state without a new schema/table. | `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `D-21`; Assumption A6. | Self; `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`. | Must use isolated persistence fixture patterns, must not mutate config/build files, and must not add a direct `Dobby.Configuration -> Dobby.Infrastructure` edge. |
| `tests/Dobby.IntegrationTests/Architecture/` | `Sprint6ReleaseEvidenceTests.cs` | Assert required Sprint 6 release-evidence blocks after implementation. | Existing `tests/Dobby.IntegrationTests/Architecture/Sprint5ReleaseEvidenceTests.cs`; `docs/09_BUILD_RUN_AND_RELEASE.md`. | Self. | Test should fail until implementation appends evidence. |

## 7a. Implementation assumptions for new files

### Assumption A1 — Authorization files stay under existing `Dobby.Security.Authorization`

- Assumption: Sprint 6 authorization registry/policy files should be placed under `src/Dobby.Security/Authorization/` and namespace `Dobby.Security.Authorization`.
- Cited source(s): current `src/Dobby.Security/Authorization/OperationKey.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md` names `Permissions` ownership as `Dobby.Security.Authorization`; `docs/02_ARCHITECTURE_MAP.md` row 37 names `Dobby.Security` Permissions / Policy.
- Risk if wrong: `docs/adr/ADR-0004-capability-enforcement-model.md` and `docs/adr/ADR-0048-operation-based-authorization.md` refer to `Permissions / Policy`, so a reviewer may require a different namespace/folder.
- Fallback: stop before creating the first authorization file and get a human architecture decision if current-code placement is rejected.

### Assumption A2 — Managed-user persistence operations live beside authentication stores

- Assumption: `ManagedUserService.cs` should be placed under `src/Dobby.Security/Authentication/` because it coordinates `Users`, `Credentials`, and `Sessions` with existing authentication stores.
- Cited source(s): `src/Dobby.Security/Authentication/CredentialStore.cs`; `src/Dobby.Security/Authentication/SessionStore.cs`; `src/Dobby.Security/Authentication/LoginService.cs`; `docs/specs/user-management.md`.
- Risk if wrong: user-management service placement may be expected under authorization/policy rather than authentication because grants and operation guards are central to the feature.
- Fallback: stop and get a human architecture decision before adding a new namespace such as `Dobby.Security.UserManagement`, co-locating the service with authorization guards, or broadening `Dobby.Security.Authentication` beyond credential/session/login-adjacent persistence responsibilities.

### Assumption A3 — Host Settings source orchestration belongs under `src/Dobby.Host/Settings/`

- Assumption: Sprint 6 Settings/About orchestration, bindable UI-facing source models, Host wiring, and Host-mediated Security/Audit coordination files belong under `src/Dobby.Host/Settings/`, with Shell rendering and route polish left to Sprint 7. `D-21` authorizes this namespace placement only and does not authorize Host-owned durable preference storage.
- Cited source(s): `docs/02_ARCHITECTURE_MAP.md` Settings/About Surface; `docs/08_CODING_STANDARDS.md` `Dobby.Host.Settings` table row; `src/Dobby.Host/Dobby.Host.csproj` references Security, Audit, Shell, Localization; `src/Dobby.Shell/Dobby.Shell.csproj` does not reference Security; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7; `D-21`.
- Risk if wrong: adding Host Settings files may duplicate future Shell ownership, create a UI split, or be misread as Host ownership of durable Settings preference storage.
- Fallback: preserve the current absence of `Dobby.Shell -> Dobby.Security` by default. If implementation proves route-gating must call Security from Shell, add that allowed edge deliberately and cover it with architecture/dependency tests; request a human decision only if the required edge or placement falls outside `docs/02_ARCHITECTURE_MAP.md`.

### Assumption A4 — About uses existing version primitives plus assembly metadata

- Assumption: About version/build source should use `Dobby.Core.Versioning.IHostVersionProvider` and assembly/build metadata, not `appsettings.json`.
- Cited source(s): `docs/specs/settings-and-about.md` About Version Source; `docs/03_TECH_STACK_AND_VERSIONS.md` Build Metadata And Version Source; `src/Dobby.Core/Versioning/HostVersion.cs`; `tests/Dobby.UnitTests/Core/HostVersionTests.cs`.
- Risk if wrong: build number/date may need a separately approved metadata provider or build pipeline input.
- Fallback: expose only approved metadata fields and record a product/build decision before inventing a new build-date source.

### Assumption A5 — Existing Sprint 4 schema is sufficient for Sprint 6 durable state

- Assumption: Sprint 6 can implement managed users and permission grants using existing `Users`, `Credentials`, `Sessions`, and `Permissions` tables without a new migration.
- Cited source(s): `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `docs/specs/user-management.md`.
- Risk if wrong: implementation may require an additional durable column or table, which would widen the schema beyond this plan.
- Fallback: stop and request a human schema/migration decision before changing `PreLoginMigrationV1.cs` or adding a new migration.

### Assumption A6 — Settings preferences use `Dobby.Configuration` ownership with Host-wired `HostConfiguration` backing

- Assumption: Sprint 6 Settings preference source behavior uses a typed `Dobby.Configuration` Settings preference surface and abstractions for internal preference behavior such as Appearance theme preference and Security/Session auto-lock source state, backed by the existing `HostConfiguration` table through Host-wired persistence. The `Dobby.Configuration` surface may define typed behavior/abstractions, but concrete `HostConfiguration` table access remains supplied outside `Dobby.Configuration`.
- Cited source(s): `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `src/Dobby.Configuration/Dobby.Configuration.csproj`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `docs/02_ARCHITECTURE_MAP.md` row 46 and Settings/About Surface note; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `SPRINT_4_EXECUTION_PLAN.md` Sprint 6 consumes `Permissions`/`HostConfiguration` tables; `D-21`.
- Risk if wrong: implementation may accidentally create Host-owned durable preference storage, imply an exact unapproved durable preference abstraction name, or add a direct `Dobby.Configuration -> Dobby.Infrastructure` edge without architecture-map authority.
- Fallback: keep the source behavior testable but stop before adding a new table, schema column, public Contracts/SDK surface, plugin-facing settings access, non-durable-only source, direct `Dobby.Configuration -> Dobby.Infrastructure` edge, or Host-owned durable preference store; request a human schema/architecture decision if Host-wired `HostConfiguration` backing cannot carry the required preference values.

Implementation assumption count: 6.

## 8. Files to Modify

| Path | Exact change scope | Reason | Boundary impact | Required test updates |
|---|---|---|---|---|
| `src/Dobby.Security/Authorization/OperationKey.cs` | Add `OperationVerb.Configure` and `OperationVerb.Grant`; map wire values `configure` and `grant`; preserve last-dot parse behavior and invalid-input failure for malformed keys. | Align current parser with `docs/adr/ADR-0048-operation-based-authorization.md`, `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-018, and `docs/schemas/dobby-plugin-manifest.schema.json`. | No project-reference change. | Add `tests/Dobby.UnitTests/Security/OperationVerbParserTests.cs`. |
| `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs` | Wire Sprint 6 authorization, user-management, Settings/About orchestration, the `Dobby.Configuration` typed Settings preference surface, Host-wired `HostConfiguration` persistence, and Host audit-coordinator services after login/runtime setup. | Existing Host composition root already creates persistence, audit, login, session, recovery, and account services, and `D-21` keeps concrete preference persistence Host-wired. | Must preserve login-first and pre-login workload rules from `docs/adr/ADR-0041-login-first-startup-policy.md`; must not add a direct `Dobby.Configuration -> Dobby.Infrastructure` edge. | Extend `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs`, `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`, and Sprint 6 integration tests. |
| `src/Dobby.Host/MainWindow.xaml` | Minimal wiring only if needed to expose Settings source behavior for tests; do not implement Sprint 7 rendered Settings tree or snapshot-polished layout. | Current post-login placeholder holds account/lock controls. | UI changes must avoid manual lock/logout duplication in Settings (`docs/specs/settings-and-about.md`). | `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`; rendered snapshots deferred to Sprint 7. |
| `src/Dobby.Host/MainWindow.xaml.cs` | Hook Settings/About source/view-model behavior only as required by Sprint 6 tests; preserve existing lock/password/remember-me workflows. | Current code contains post-login account behavior that Settings/Account should share rather than duplicate. | No direct OS API calls from ViewModels; adapters stay in Host code-behind/services (`docs/08_CODING_STANDARDS.md`). | `tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs` and new Settings tests. |
| `src/Dobby.Localization/HostStringCatalog.cs` | Add resource keys/az-AZ strings for Settings categories, User Management labels, neutral `About.OwnerStatus.Registered`, and non-disclosing deny text. | Required by `docs/specs/settings-and-about.md` and `docs/specs/user-management.md`; current catalog lacks these keys. | Do not expose internal authorization reasons in UI strings. | Add `tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs`. |
| `src/Dobby.Localization/Resources/Host.az-AZ.resx` | Add matching resource entries only if the implementation keeps `.resx` synchronized with `HostStringCatalog`. | The repo contains both `HostStringCatalog.cs` and `Resources/Host.az-AZ.resx`; implementation must preserve local localization convention. | UTF-8/no-BOM/final-newline gate applies. | `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`. |
| `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` | Modify only if implementation adds a new project/reference edge. | Current test enforces architecture map and no `Dobby.Security -> Dobby.Audit`; `Dobby.Shell -> Dobby.Security` is allowed by the architecture map only if route-gating requires it. | Preserve current Shell-to-Security absence by default; if added, cover the edge with dependency/architecture assertions. | Existing test should remain green unless an allowed edge is deliberately added. |
| `artifacts/release-evidence/skeleton-local/architecture-baseline.md` | Append Sprint 6 implementation evidence after build/test completion. | Existing release-evidence convention records sprint architecture and verification closure. | Evidence append only; no source-code effect. | `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs`. |
| `artifacts/release-evidence/skeleton-local/decision-log.md` | Already contains `D-20`, the human-approved built-in User Management target id, and `D-21`, the human-approved Settings namespace/preference authority. | Required authority anchor for registry, permission keys, grant/revoke rows, Settings namespace placement, durable preference ownership, and tests. | Decision-log append only; implementation must cite but not rewrite `D-20` or `D-21`. | `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs` should inspect the decision evidence. |

### Existing Test Files Expected to Change

These existing test files are part of the Sprint 6 modification inventory and cross-reference the detailed scenarios in Section 14.

| Path | Expected Sprint 6 update | Source / scenario link | Boundary guardrail |
|---|---|---|---|
| `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` | Extend existing manifest-validator coverage so `configure` and `grant` are accepted through the shared `OperationVerbParser` path, while unknown verbs still fail. | Section 14 `PluginFrameworkTests`; `docs/schemas/dobby-plugin-manifest.schema.json`; `docs/adr/ADR-0048-operation-based-authorization.md`. | Do not add Sprint 8 package/signature/activation/seeding validation or broaden validator scope beyond the current narrow invalid-target behavior. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs` | Extend Host composition coverage for Sprint 6 authorization, user-management, Settings/About orchestration, typed Settings preference surface, and Host-wired persistence. | Section 14 `HostLoginRuntimeFactoryTests`; `docs/adr/ADR-0041-login-first-startup-policy.md`; `D-21`; Assumption A6. | Do not introduce pre-login plugin/license work or a direct `Dobby.Configuration -> Dobby.Infrastructure` edge. |
| `tests/Dobby.UnitTests/Host/PostLoginSessionRuntimeTests.cs` | Extend only if Sprint 6 changes post-login route/source hooks or shared account behavior that this existing test owns. | Section 8 `MainWindow.xaml.cs` required test updates; Section 14 Settings/About source behavior. | Preserve existing lock, password-change, and remember-me workflows; do not implement Sprint 7 rendered Settings tree assertions. |
| `tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs` | Extend only if current inactive-login coverage does not satisfy the AC-039 future-login portion after managed-user deactivation/reactivation behavior is implemented; otherwise keep as a preserved baseline cited by implementation evidence. | Section 16 `AC-039`; `docs/specs/session-lifecycle.md`; `src/Dobby.Security/Authentication/LoginService.cs`. | Do not move User Management lifecycle coverage out of `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs` unless an existing login-service assertion must be expanded. |

### Files to Verify Without Expected Code Edits

| Path | Verification scope | Reason | Boundary impact | Required test updates |
|---|---|---|---|---|
| `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs` | Verify schema-approved verbs pass through the updated `OperationVerbParser`; no validator body edit is expected unless tests expose a mismatch in the existing verb path. Continue rejecting unknown verbs and the validator's existing narrow duplicate/invalid target cases only. | Current validator already uses `OperationVerbParser`, so the parser update should carry the closed verb-set alignment without turning Sprint 6 into full manifest schema validation. | Does not implement Sprint 8 package/schema validation, plugin activation, or plugin target seeding. | Extend `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` only for canonical verb propagation, unknown-verb rejection, and current narrow validator behavior. |
| `tests/Dobby.UnitTests/Audit/AuditEventNameTests.cs` | Verify Sprint 6 consumes existing `user.*`, `permission.*`, and `owner.protection.denied` names; no test edit is expected unless verification exposes a missing closed-set assertion. | `src/Dobby.Audit/AuditEventName.cs` already contains required events; user-management spec names them. | Audit event closed-set must not widen without decision. | Existing test boundary should remain green. |
| `src/Dobby.Configuration/Dobby.Configuration.csproj` | Verify the project does not add a `Dobby.Infrastructure` reference for Sprint 6 Settings preference behavior. | `D-21` allows typed preference behavior/abstractions in `Dobby.Configuration` but requires concrete `HostConfiguration` access to remain Host-wired outside `Dobby.Configuration`. | No direct `Dobby.Configuration -> Dobby.Infrastructure` edge without separate architecture-map amendment. | `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`. |

## 9. Files Not Touched

| Surface | Exclusion | Source basis |
|---|---|---|
| `SPRINT_IMPLEMENTATION_PLAN.md` | Do not edit master plan. | User request; Sprint 6 scope comes from `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6. |
| `SPRINT_1_EXECUTION_PLAN.md` through `SPRINT_5_EXECUTION_PLAN.md` | Do not edit predecessor execution plans. | User request; predecessor plans are continuity context only. |
| `docs/adr/*.md` | Do not edit ADRs during Sprint 6 implementation unless a separate human-approved documentation task is created. | `docs/08_CODING_STANDARDS.md`; user request; ADR alignment in §6. |
| `docs/specs/*.md` | Do not edit specs for Sprint 6; record authority gaps in decision log. | `docs/specs/user-management.md`; `docs/specs/settings-and-about.md`; user request. |
| `docs/schemas/*.json` | Do not edit schemas; current plugin manifest schema already includes canonical verbs. | `docs/schemas/dobby-plugin-manifest.schema.json`; `docs/adr/ADR-0036-plugin-manifest-schema.md`. |
| `Directory.Packages.props` | Do not add package pins unless a new human-approved pin exists. | `docs/03_TECH_STACK_AND_VERSIONS.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`. |
| `global.json` and `Directory.Build.props` | Do not change SDK/build policy. | `global.json`; `Directory.Build.props`; `docs/09_BUILD_RUN_AND_RELEASE.md`. |
| `installer/` and `tools/` | Do not modify installer/tooling. | Sprint 6 scope in `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 does not include installer/tooling. |
| `src/Dobby.PluginFramework/Activation/` | Do not implement plugin activation or operation target seeding. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8; `docs/adr/ADR-0036-plugin-manifest-schema.md`. |
| `src/Dobby.Licensing/` | Do not implement license import/runtime behavior. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `docs/specs/license-import.md`. |
| `src/Dobby.Host/Trust/README.md` and production trust material | Do not add production vendor keys or trust handover content. | `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `artifacts/release-evidence/skeleton-local/decision-log.md` `D-13`. |
| `src/Dobby.BranchSetup`, `src/Dobby.Infrastructure.Postgres`, `src/Dobby.Update` | Do not create deferred Branch Shared/update/Postgres runtime projects. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`. |
| `src/Dobby.Contracts/` public plugin-visible surfaces | Do not widen Contracts/SDK beyond existing `OperationPermissionCapability` unless an ADR/decision requires it. | `docs/adr/ADR-0004-capability-enforcement-model.md`; `tests/Dobby.IntegrationTests/Architecture/PluginAndSdkBoundaryTests.cs`. |
| `src/Dobby.Core/Result.cs` and Core `ErrorCode` registry | Do not add Core error codes for Sprint 6 detail. | `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`. |

## 10. Error / Result / Policy Mapping

Sprint 6 does apply to Result/Error/policy mapping because it introduces service-boundary authorization, managed-user operations, and permission-deny behavior (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6; `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `docs/adr/ADR-0048-operation-based-authorization.md`).

| Surface | Expected outcome type | Existing `ErrorCode` / policy | Audit event(s) | UI/plugin-facing behavior |
|---|---|---|---|---|
| Operation key parsing | `Result<OperationKey>` | Existing `ErrorCode.InvalidInput` for malformed key or unknown verb; no new Core code (`src/Dobby.Security/Authorization/OperationKey.cs`; `docs/04_RESULT_VS_EXCEPTION_POLICY.md`). | None unless parse is part of an authorization attempt. | Validation failure remains non-sensitive. |
| Policy evaluation with no active user/session | `Result<PolicyDecision>` | Existing `ErrorCode.Unauthorized` may be used for missing unauthenticated context; expected denial returns `PolicyDecision.Deny` per `docs/adr/ADR-0004-capability-enforcement-model.md`. | `permission.denied` if an authorization-required operation is attempted (`src/Dobby.Audit/AuditEventName.cs`). | Non-disclosing denial. |
| Unknown target / unsupported verb | `Result<PolicyDecision>` | Fail closed with `PolicyDecision.Deny`; do not add a new `ErrorCode`. | `permission.denied`; host-side audit/diagnostic details may record semantic category only (`docs/adr/ADR-0048-operation-based-authorization.md`; `docs/specs/audit-and-diagnostics.md`). | Non-disclosing reason. |
| Missing managed-user grant | `Result<PolicyDecision>` | Managed default deny; row absence means no grant. | `permission.denied`. | Non-disclosing reason. |
| Owner all-grants | `Result<PolicyDecision>` | `Users.IsOwner=1` short-circuits allow; no Owner permission row required (`D-17`; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`). | Action-specific success event if operation completes. | Owner sees allowed operation. |
| User list/detail | `Result<T>` | Requires `view` for managed users; Owner is allowed through all-grants. Do not expose password hashes or protected credential material. | `permission.denied` for denied read attempts. | Non-disclosing denial; source data remains safe for Settings/User Management list/detail. |
| Managed user attempts Owner mutation | `Result<T>` from user-management service plus policy deny | Existing `ErrorCode.Forbidden` or `PolicyDecision.Deny`; no new Core code. | `owner.protection.denied`. | Non-disclosing denial. |
| Managed user self-grant or grant absent from actor | `Result<T>` from user-management service plus policy deny | Existing `ErrorCode.Forbidden` or `PolicyDecision.Deny`; no explicit deny row. | `permission.denied` or `owner.protection.denied` when Owner target involved. | Non-disclosing denial. |
| User create/update/deactivate/reactivate/reset | `Result<T>` | Validation uses existing `ErrorCode.ValidationFailed`, duplicates use `ErrorCode.AlreadyExists`, not-found uses `ErrorCode.NotFound`, forbidden uses `ErrorCode.Forbidden` (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `docs/specs/user-management.md`). | `user.create`, `user.update`, `user.password.reset`, `user.activate`, `user.deactivate`. | User-facing text must not reveal internal policy details. |
| Grant/revoke | `Result<T>` | Existing `ErrorCode.Forbidden`, `ValidationFailed`, or `NotFound`; no explicit deny entry. | `permission.grant`, `permission.revoke`, `permission.denied`. | Toggle is grant-only/on-off; no deny toggle. |
| Deactivated active user next protected operation | `Result<PolicyDecision>` and session invalidation | Deny protected operation and invalidate session through existing session lifecycle; future login denied by existing login inactive check. | `session.invalidated` and authorization/user-management audit as applicable (`docs/specs/session-lifecycle.md`; `src/Dobby.Audit/AuditEventName.cs`). | User returns to login; no sensitive reason detail. |

Local sub-code semantics inside owner namespace:

- Internal audit/diagnostic detail may distinguish semantic categories such as malformed operation key, unknown target, unsupported verb for target, missing grant, owner protection, self-grant, grant not held by actor, and inactive managed user session.
- These categories are not new Core `ErrorCode` values, not new audit event names, and not plugin/user-facing reason strings (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `docs/specs/audit-and-diagnostics.md`; `src/Dobby.Audit/AuditEventName.cs`).

Plugin-host boundary statement:

- Sprint 6 implements host-owned built-in registry and policy behavior. It may accept validated plugin target metadata as an input model, but actual plugin package validation, activation, and seeding are Sprint 8 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8; `docs/adr/ADR-0036-plugin-manifest-schema.md`).

Result-vs-exception split discipline:

- Expected authorization denials, validation failures, not-found rows, duplicate usernames, inactive users, and unsupported operations return `Result`/`PolicyDecision` outcomes.
- Programmer invariants and impossible enum states may throw, following `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; raw plugin/runtime exceptions remain sealed at host/plugin boundaries.

## 11. Technology Stack and Versions

Pinned versions consumed:

| Item | Version / setting | Source |
|---|---|---|
| .NET SDK | `10.0.202`, `rollForward=latestPatch` | `global.json` |
| C# | `LangVersion` `14.0`, nullable enabled, warnings as errors, latest analysis, deterministic build | `Directory.Build.props` |
| Security TFM | `net10.0` | `src/Dobby.Security/Dobby.Security.csproj`; `docs/03_TECH_STACK_AND_VERSIONS.md` |
| Host/Shell TFM | `net10.0-windows`, WPF enabled | `src/Dobby.Host/Dobby.Host.csproj`; `src/Dobby.Shell/Dobby.Shell.csproj` |
| `Microsoft.AspNetCore.Authorization` | `10.0.7` available but plugin-visible enforcement remains Dobby-owned contracts | `Directory.Packages.props`; `docs/03_TECH_STACK_AND_VERSIONS.md`; `docs/adr/ADR-0004-capability-enforcement-model.md` |
| `Dapper` | `2.1.72` | `Directory.Packages.props`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` |
| `Microsoft.Data.Sqlite.Core` | `10.0.8` | `Directory.Packages.props`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` |
| `SQLitePCLRaw.core` / `SQLitePCLRaw.provider.dynamic_cdecl` | `3.0.3` | `Directory.Packages.props`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` |
| `CommunityToolkit.Mvvm` | `8.4.2` | `Directory.Packages.props`; `src/Dobby.Shell/Dobby.Shell.csproj` |
| `WPF-UI` / `WPF-UI.DependencyInjection` | `4.3.0` | `Directory.Packages.props`; `src/Dobby.Host/Dobby.Host.csproj` |
| Unit/integration test packages | `xunit` `2.9.3`, `Shouldly` `4.3.0`, `NSubstitute` `5.3.0`, `Verify.Xunit` `31.12.5`, `Microsoft.NET.Test.Sdk` `18.5.1` | `Directory.Packages.props`; `docs/03_TECH_STACK_AND_VERSIONS.md` |

Package pin impact:

- Allowed without new pins: use existing project references, BCL/.NET runtime APIs, existing Dapper/SQLite packages, existing test packages, existing WPF/MVVM packages, and existing `Dobby.Contracts.Policies` types (`Directory.Packages.props`; `src/Dobby.Contracts/Policies/PolicyContracts.cs`).
- Requires new approved pin: any package not already in `Directory.Packages.props`, any new native asset, any new commercial dependency, or any package version/source change (`docs/03_TECH_STACK_AND_VERSIONS.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`).
- Human decision required if unresolved: no package decision is currently required for the planned Sprint 6 work; if implementation proposes a package, stop for approval before editing `Directory.Packages.props`.

Native assets:

- Sprint 6 should not add or modify native assets. Existing approved native SQLite material is tracked in `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`; no Sprint 6 native asset scope appears in `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6.

Test / runtime / TFM / conditioned-reference consequences:

- `Dobby.Security` remains `net10.0` and must not use WPF-only APIs (`src/Dobby.Security/Dobby.Security.csproj`).
- `Dobby.Configuration` remains `net10.0` and must not add a direct `Dobby.Infrastructure` reference for Sprint 6 Settings preference behavior (`src/Dobby.Configuration/Dobby.Configuration.csproj`; `D-21`).
- `Dobby.Host` and `Dobby.Shell` remain `net10.0-windows`; Host may coordinate WPF/Login/Settings behavior (`src/Dobby.Host/Dobby.Host.csproj`; `src/Dobby.Shell/Dobby.Shell.csproj`).
- No `System.Management` expansion is needed; its approved Sprint 5 use is conditioned to hardware identity in Infrastructure only (`artifacts/release-evidence/skeleton-local/decision-log.md` `D-14`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).

Zero-warning / format / encoding constraints:

- Build must remain warnings-as-errors and deterministic (`Directory.Build.props`).
- Source files must use UTF-8 no BOM and final newline (`docs/08_CODING_STANDARDS.md`; `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`).
- No inline package versions in `.csproj` files (`docs/03_TECH_STACK_AND_VERSIONS.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).

## 12. Code Work Plan

### Pre-flight

1. Confirm the built-in User Management target id and Settings namespace/preference authority anchors.
   - Action: verify `D-20` and `D-21` in `artifacts/release-evidence/skeleton-local/decision-log.md` before any source/test implementation.
   - Success criteria: tests and registry fixtures use `users.management` exactly and cite `D-20`; Settings source/persistence tests cite `D-21`, keep durable preference ownership in `Dobby.Configuration`, and keep concrete `HostConfiguration` access Host-wired.
   - Out-of-scope guardrail: do not infer an alias from labels, routes, file names, or planning-agent preference (`docs/specs/user-management.md`; `docs/adr/ADR-0048-operation-based-authorization.md`).
2. Reconfirm current parser/schema mismatch.
   - Action: inspect `src/Dobby.Security/Authorization/OperationKey.cs`, `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs`, and `docs/schemas/dobby-plugin-manifest.schema.json`.
   - Success criteria: failing tests prove `configure`/`grant` behavior before code changes.
   - Out-of-scope guardrail: do not add full manifest schema/package/signature validation or change plugin activation/seeding (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
3. Add failing tests for Sprint 6 authorization and user-management behavior.
   - Action: create/extend tests listed in §7 and §14 for canonical verbs, registry targets, Owner all-grants, managed default deny, unknown target/verb, Owner/self-grant guards, CRUD/reset/deactivate/reactivate, delete-as-lifecycle behavior with no hard-delete path, session invalidation, and Settings/About source behavior.
   - Success criteria: tests fail only for missing Sprint 6 behavior and cite `AC-022`, `AC-030`, `AC-035`, and `AC-039`.
   - Out-of-scope guardrail: no rendered Settings snapshot tests in Sprint 6; those belong to Sprint 7.

### Implementation

4. Complete canonical verb support.
   - Action: update `src/Dobby.Security/Authorization/OperationKey.cs` and verify the existing validator verb path so all canonical verbs from `docs/adr/ADR-0048-operation-based-authorization.md` are accepted and unknown verbs fail closed.
   - Success criteria: `OperationVerbParserTests` and `PluginFrameworkTests` pass for `view`, `create`, `update`, `delete`, `execute`, `import`, `export`, `print`, `configure`, and `grant`.
   - Out-of-scope guardrail: do not add non-canonical verbs, package pins, or Sprint 8 manifest schema/package validation.
5. Implement operation target registry.
   - Action: create registry/model files under the approved placement, register built-in license/plugin-management targets from source docs, and register the approved User Management target `users.management` from `D-20`.
   - Success criteria: registry tests prove supported verbs only, no explicit deny toggle, unknown target deny, and plugin-target input validation without activation.
   - Out-of-scope guardrail: do not seed targets from plugin packages; Sprint 8 owns validated signed manifest seeding.
6. Implement permission grant store and operation authorization policy.
   - Action: use existing `Permissions` table rows for grants, `Users.IsOwner=1` for Owner all-grants, and `ICapabilityPolicy<OperationPermissionCapability>` for policy evaluation.
   - Success criteria: policy/unit/integration tests prove grant-only/default-deny, non-disclosing deny, and no Owner permission rows.
   - Out-of-scope guardrail: no new Core `ErrorCode`; no `Dobby.Security -> Dobby.Audit` reference.
7. Implement User Management service behavior.
   - Action: implement list/detail for the documented `view` operation plus create/update/deactivate/reactivate/reset-password/grant/revoke using existing credential/session/permission schema and validators.
   - Success criteria: integration tests cover `view`-authorized list/detail, denial without the `view` grant for managed users, no password hashes or protected credential material in list/detail results, username/display/password validation, DB unique constraints, active flag changes, reset session invalidation, future-login deny, and absence of any physical hard-delete behavior.
   - Out-of-scope guardrail: do not implement Owner recovery or owner transfer; managed reset remains separate from Owner recovery.
8. Implement Owner/self-grant service guards.
   - Action: enforce no managed mutation of Owner, no self-grant, and no grant of permissions the actor lacks at service boundary.
   - Success criteria: tests verify UI hiding is not the only enforcement and denied operations audit through Host coordinator.
   - Out-of-scope guardrail: do not add explicit deny rows or deny toggles.
9. Implement Host-mediated audit coordination.
   - Action: coordinate user/permission/owner-protection audit events through Host using `IHostTransactionAuditTrail` and existing `AuditEventName` constants.
   - Success criteria: integration tests see `user.create`, `user.update`, `user.password.reset`, `user.activate`, `user.deactivate`, `permission.grant`, `permission.revoke`, `permission.denied`, and `owner.protection.denied` as applicable.
   - Out-of-scope guardrail: do not invent audit event names and do not add `Dobby.Security -> Dobby.Audit`.
10. Implement Settings/About source behavior.
    - Action: create Host Settings orchestration/source models for Appearance, Account, Security/Session, User Management, and About; create a typed `Dobby.Configuration` Settings preference surface and abstractions for Appearance and Security/Session preference behavior; make `AppearanceSettingsModel` or an equivalent source model read/write the same theme preference source that Sprint 7 TopToolbar quick switch will consume; back Appearance and Security/Session source state with Host-wired persistence over the existing `HostConfiguration` table; gate User Management by policy; source About fields from approved metadata/redacted providers.
    - Success criteria: service/view-model and persistence tests verify category presence, Appearance shared theme preference source behavior, Host-wired `HostConfiguration`-backed theme preference persistence, Account password-change shortcut to existing behavior, no manual lock/logout duplication in Settings, Security/Session default-passive auto-lock toggle and minute input source behavior, User Management entry gating plus list/detail source availability through `view`, About no license import/status, neutral Owner identity key, and no raw sensitive fields.
    - Out-of-scope guardrail: no Sprint 7 rendered tree/snapshot/manual visual closure; no Host-owned durable preference store; no exact durable preference abstraction class name pre-approved by the plan; no direct `Dobby.Configuration -> Dobby.Infrastructure` edge.
11. Wire composition and localization.
    - Action: wire Sprint 6 services through `HostLoginRuntimeFactory`, add required host resource keys, and preserve existing login/session/recovery behavior.
    - Success criteria: existing Sprint 5 tests still pass; new Settings/User Management tests pass; architecture tests still accept project references.
    - Out-of-scope guardrail: no direct OS APIs in ViewModels; preserve the current `Dobby.Shell -> Dobby.Security` absence unless real route-gating requires the allowed edge and architecture/dependency tests cover it.

### Evidence / closure

12. Run verification in the repository's normal order.
    - Action: run format/build/unit/integration/smoke gates according to `docs/09_BUILD_RUN_AND_RELEASE.md`.
    - Success criteria: zero warnings/errors; Sprint 6 test set green; existing Sprint 5 evidence baseline is not regressed.
    - Out-of-scope guardrail: do not update package pins or generate unrelated artifacts.
13. Append release evidence.
    - Action: append Sprint 6 implementation summary, AC matrix, test commands/results, architecture graph notes, package-governance statement, and open/closed decisions to `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
    - Success criteria: `Sprint6ReleaseEvidenceTests` passes and cites the appended evidence.
    - Out-of-scope guardrail: do not rewrite prior sprint evidence.
14. Final boundary audit.
    - Action: inspect diff for forbidden files/surfaces, unapproved names, new packages, new Core errors, new audit events, and later-sprint work.
    - Success criteria: final diff touches only Sprint 6 source/tests/evidence and cites the recorded `D-20` and `D-21` human decisions.
    - Out-of-scope guardrail: do not close implementation with unresolved human authority gates.

## 13. Decisions and Risks

Closed authority gates:

| Decision | Disposition | Evidence |
|---|---|---|
| `D-16` | Host-coordinated Owner bootstrap audit through `IHostTransactionAuditTrail`; no `Dobby.Security -> Dobby.Audit`. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `src/Dobby.Audit/IHostTransactionAuditTrail.cs`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`. |
| `D-17` | Owner all-grants/protected-principal state is `Users.IsOwner=1`, not an Owner `Permissions` row. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`. |
| `D-14` | `System.Management` `10.0.8` approved only for net10.0-windows hardware identity adapter slice. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`. |
| `D-19` | No new package for internal JCS writer in Sprint 5 recovery work; supports no-new-package discipline. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`. |
| `D-20` | Built-in User Management operation target id is `users.management`; supported verbs are `view`, `create`, `update`, `delete`, `execute`, and `grant`. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `docs/specs/user-management.md`; `docs/adr/ADR-0048-operation-based-authorization.md`. |
| `D-21` | `Dobby.Host.Settings` is approved only for Settings/About orchestration/source models/wiring; durable Settings preference ownership remains `Dobby.Configuration`, with concrete `HostConfiguration` access supplied by Host-wired persistence and no direct `Dobby.Configuration -> Dobby.Infrastructure` edge. | `artifacts/release-evidence/skeleton-local/decision-log.md`; `docs/08_CODING_STANDARDS.md`; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `docs/02_ARCHITECTURE_MAP.md` row 46. |

Approved implementation decisions / source-grounded implementation choices:

- Use existing `ICapabilityPolicy<OperationPermissionCapability>` rather than a new authorization contract (`src/Dobby.Contracts/Policies/PolicyContracts.cs`; `docs/adr/ADR-0004-capability-enforcement-model.md`).
- Use existing `Permissions` row presence as grant and absence as deny; no explicit deny row/toggle (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `docs/specs/user-management.md`).
- Use existing audit event names in `src/Dobby.Audit/AuditEventName.cs`; no new audit event names.
- Use `plugins.license` and `plugins.management` as exact built-in ids where those targets are registered (`docs/specs/license-import.md`; `docs/specs/plugins-manager.md`; `docs/adr/ADR-0048-operation-based-authorization.md`).
- Use `users.management` as the exact built-in User Management target id (`D-20`).
- Use a typed `Dobby.Configuration` Settings preference surface and abstractions for Sprint 6 Appearance and Security/Session preference behavior, backed by the existing `HostConfiguration` table through Host-wired persistence; do not add schema columns/tables, public Contracts/SDK settings surfaces, plugin-facing settings access, Host-owned durable preference storage, a direct `Dobby.Configuration -> Dobby.Infrastructure` edge, or non-durable-only preference state without human authority (`D-21`; Assumption A6).

Risks:

| Risk | Mitigation | Evidence / tests |
|---|---|---|
| User Management target id drift could corrupt registry and permission-key tests. | Use `users.management` exactly and cite `D-20`; reject aliases/placeholders. | `artifacts/release-evidence/skeleton-local/decision-log.md`; §12 step 1. |
| Authorization implementation may accidentally rely on UI hiding. | Service-boundary integration tests for denied operations; coding-standard review. | `docs/08_CODING_STANDARDS.md`; `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs`. |
| `Dobby.Security` could acquire a forbidden audit dependency. | Keep audit in Host coordinator and run architecture tests. | `src/Dobby.Security/Dobby.Security.csproj`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`; `D-16`. |
| Current schema may be insufficient for implementation detail. | Treat schema change as human decision, not Sprint 6 assumption drift. | `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; Assumption A5. |
| Settings source behavior may drift into Sprint 7 rendering, under-specify Account/Security/User Management view behavior, use a non-durable preference source, or imply Host ownership of durable preference storage. | Keep tests at service/view-model level; explicitly test Appearance shared theme preference source behavior through the `Dobby.Configuration` preference surface, Account password-change shortcut, absence of manual lock/logout duplication, Security/Session default-passive auto-lock toggle/minute input source behavior, User Management entry gating plus list/detail source availability through `view`, and Host-wired persistence through `HostConfiguration`; defer snapshot/manual rendered evidence. | `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 Verification; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7; `docs/specs/settings-and-about.md`; `docs/specs/user-management.md`; `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`; `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`; `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`; `D-21`; Assumption A6. |
| Verb parser update may unintentionally broaden plugin runtime behavior. | Limit change to canonical parser/validator tests; no activation/seeding code. | `docs/schemas/dobby-plugin-manifest.schema.json`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8. |

Carry-over from previous sprints requiring no new decision:

- Owner bootstrap and active-user login behavior are already implemented and evidenced (`src/Dobby.Security/Authentication/OwnerBootstrapService.cs`; `src/Dobby.Security/Authentication/LoginService.cs`; `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`).
- Durable schema and approved package/native assets are already implemented/evidenced (`src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `artifacts/release-evidence/skeleton-local/storage-materialization.md`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`).

Human decision required items:

- None. The built-in User Management operation target id gate is closed by `D-20`, and the Settings namespace/preference authority gate is closed by `D-21`.

## 14. Test Plan

Unit tests:

| Test file | Scenarios | AC / source coverage |
|---|---|---|
| `tests/Dobby.UnitTests/Security/OperationVerbParserTests.cs` | All 10 canonical verbs parse and serialize; last-dot target parsing; unknown/malformed key returns `InvalidInput`. | `AC-022`; `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-018. |
| `tests/Dobby.UnitTests/Security/OperationTargetRegistryTests.cs` | Built-in targets register supported verbs only, including exact `users.management` verbs from `D-20`; User Management `delete` is asserted as the deactivate/reactivate lifecycle permission, not hard delete; no explicit deny toggle; unknown target/verb fail closed; plugin target input accepted only as validated metadata input. | `AC-022`; `docs/adr/ADR-0048-operation-based-authorization.md`; `docs/adr/ADR-0036-plugin-manifest-schema.md`; `docs/specs/user-management.md`; `D-20`. |
| `tests/Dobby.UnitTests/Security/OperationAuthorizationPolicyTests.cs` | Owner all-grants; managed default deny; grant row allows exact operation; missing grant denies non-disclosing; no Owner permission row needed. | `AC-022`; `D-17`; `docs/specs/user-management.md`. |
| `tests/Dobby.UnitTests/Security/UserManagementAuthorizationGuardTests.cs` | Managed user cannot mutate Owner, self-grant, or grant absent permission; `grant` permission does not bypass Owner protection. | `AC-022`; `AC-030`; `docs/specs/user-management.md`. |
| `tests/Dobby.UnitTests/PluginFramework/PluginFrameworkTests.cs` | Manifest validator accepts schema-approved `configure` and `grant` through the shared parser path; still rejects unknown verbs and only the validator's existing narrow invalid-target cases. Do not add full target-id pattern, duplicate verb item, package/signature, activation, or seeding validation in Sprint 6. | `AC-022` contribution; `docs/schemas/dobby-plugin-manifest.schema.json`; `docs/adr/ADR-0036-plugin-manifest-schema.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8. |
| `tests/Dobby.UnitTests/Audit/AuditEventNameTests.cs` | Required Sprint 6 events already exist; no new audit event names required. | `docs/specs/user-management.md`; `docs/specs/audit-and-diagnostics.md`. |
| `tests/Dobby.UnitTests/Host/UserManagementAuditCoordinatorTests.cs` | Host audit coordinator uses existing User Management audit event names for success, deny, and Owner-protection outcomes while keeping Security audit-neutral. | `AC-022`; `AC-030`; `docs/specs/user-management.md`; `docs/specs/audit-and-diagnostics.md`; `D-16`. |
| `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs` | Minimum Settings categories; Appearance model reads/writes the same `Dobby.Configuration` typed Settings preference surface reserved for the Sprint 7 TopToolbar quick switch; Account password-change shortcut binds to existing behavior without manual lock/logout duplication; Security/Session default-passive auto-lock toggle and minute input expose source behavior; User Management entry gating plus list/detail source availability through `view`; About no license import/status; neutral Owner identity key. | `AC-035`; `docs/specs/settings-and-about.md`; `docs/specs/user-management.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `D-21`; Assumption A6. |
| `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs` | Typed Settings preference surface exposes Appearance theme preference and Security/Session source-state behavior without concrete table access or an Infrastructure project reference. | `AC-035`; `docs/02_ARCHITECTURE_MAP.md` row 46; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `D-21`; Assumption A6. |
| `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs` | Version/build source from approved metadata; redacted device code; no raw hardware fingerprint/protected key/session/raw license/plugin secret. | `AC-035`; `docs/specs/settings-and-about.md`; `docs/03_TECH_STACK_AND_VERSIONS.md`. |
| `tests/Dobby.UnitTests/Localization/SettingsLocalizationStringCatalogTests.cs` | New Settings/About/User Management resource keys resolve, including `About.OwnerStatus.Registered`, without expanding the Sprint 5 login/recovery localization test. | `AC-035`; `docs/specs/settings-and-about.md`; `docs/specs/user-management.md`. |

Integration tests:

| Test file | Scenarios | AC / source coverage |
|---|---|---|
| `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs` | Durable permission rows, Owner all-grants, managed view-only denial for other verbs, unknown target/verb deny, non-disclosing result, audit trail. | `AC-022`; `docs/specs/user-management.md`; `docs/specs/audit-and-diagnostics.md`. |
| `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs` | Owner and `view`-granted managed user list/detail; denial without `view`; no password hashes or protected credential material in list/detail results; Owner create/update/deactivate/reactivate/reset/grant/revoke; managed user operations require matching grant; DB unique constraints; service validation; `delete` permission maps to deactivate/reactivate lifecycle behavior and no hard-delete path exists. | `AC-030`; `docs/specs/user-management.md`; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`. |
| `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs` | Deactivating active managed user invalidates the next protected operation and blocks future login until reactivated. | `AC-039`; `docs/specs/session-lifecycle.md`; `src/Dobby.Security/Authentication/LoginService.cs`. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs` | Host runtime composes authorization/user-management/settings orchestration, the `Dobby.Configuration` typed Settings preference surface, and Host-wired persistence without pre-login plugin/license work. | `docs/adr/ADR-0041-login-first-startup-policy.md`; `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`; `D-21`; Assumption A6. |
| `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs` | Appearance theme preference and Security/Session auto-lock state persist through Host-wired persistence backed by the existing `HostConfiguration` table and do not require a new schema/table, direct `Dobby.Configuration -> Dobby.Infrastructure` edge, or non-durable-only source. | `AC-035`; `docs/specs/settings-and-about.md`; `docs/05_FUNCTIONAL_REQUIREMENTS.md` FR-010; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`; `D-21`; Assumption A6. |
| `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` | Project references stay within architecture map; no `Dobby.Security -> Dobby.Audit`; Core ErrorCode registry unchanged. | `docs/02_ARCHITECTURE_MAP.md`; `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `D-16`. |
| `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` | No new package pin, no inline package version, existing approved pins remain valid. | `Directory.Packages.props`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`. |
| `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs` | Sprint 6 evidence block exists after implementation and references AC/test/package/decision closure. | `docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. |

Manual / out-of-band notes:

- No Sprint 6 rendered Settings snapshot/manual visual evidence is required; rendered Settings tree and snapshots are Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
- Manual evidence is acceptable only for fields that cannot be safely automated, such as confirming support-safe copy behavior, and must be recorded in `artifacts/release-evidence/skeleton-local/architecture-baseline.md` if used (`docs/specs/settings-and-about.md`; `docs/09_BUILD_RUN_AND_RELEASE.md`).

Test count expectation vs previous sprint baseline:

- Sprint 5 release evidence records 365 total tests, 0 failures, 0 skipped, 0 warnings in `artifacts/release-evidence/skeleton-local/architecture-baseline.md`. Sprint 6 should add the named tests above and increase the total; do not hard-code a target count until implementation creates the exact test cases.

## 15. Out of Scope

- Earlier sprint work is consumed, not redone: storage materialization, login/session/recovery, KDF selection, Owner bootstrap, and pre-login startup rules remain as implemented/evidenced in `artifacts/release-evidence/skeleton-local/storage-materialization.md`, `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md`, and `artifacts/release-evidence/skeleton-local/architecture-baseline.md`.
- Sprint 7 work is out of scope: rendered Settings tree, theme/localization visual polish, screenshots/snapshots, shell route visual closure, and manual visual evidence (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).
- Sprint 8 work is out of scope: full plugin manifest schema/package/signature validation beyond the current narrow validator checks, plugin install/activation/runtime, plugin page hosting, event bus runtime subscriptions, and seeding plugin `operationTargets` from signed plugin packages before activation (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
- Later license/plugin manager work is out of scope: `.dobbylicense` import implementation and license signature/binding/revision checks are Sprint 9; plugin status/license UI and plugin management lifecycle operations are Sprint 10 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 9; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 10; `docs/specs/license-import.md`; `docs/specs/plugins-manager.md`).
- Print flow, Branch Shared runtime, update runtime, Postgres deployment, installer, tooling, and production trust/key custody remain out of scope (`docs/09_BUILD_RUN_AND_RELEASE.md`; `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; `docs/adr/ADR-0053-capability-surface-runtime-mode.md`).
- No new package pins, package sources, native assets, commercial packages, telemetry, online calls, Core `ErrorCode` values, audit event names, plugin-visible Contracts/SDK surfaces, or schema widening unless a human decision explicitly approves them (`Directory.Packages.props`; `docs/03_TECH_STACK_AND_VERSIONS.md`; `docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).
- No Host-owned durable Settings preference store, direct `Dobby.Configuration -> Dobby.Infrastructure` edge, plugin-facing settings access, or durable preference abstraction name that implies Host storage ownership or exceeds the `D-21` authority is in scope.
- UI hiding/disabling cannot be the only authorization control; service-boundary checks are required (`docs/08_CODING_STANDARDS.md`; `docs/adr/ADR-0048-operation-based-authorization.md`).

## 16. Definition of Done

Build / test / format expectations:

- Normal repository verification from `docs/09_BUILD_RUN_AND_RELEASE.md` passes with zero warnings under `Directory.Build.props`.
- Unit, integration, architecture, and smoke tests relevant to Sprint 6 pass; existing Sprint 5 authentication/session/recovery tests remain green.
- Encoding boundary remains UTF-8 no BOM/final newline (`docs/08_CODING_STANDARDS.md`; `tests/Dobby.IntegrationTests/Architecture/EncodingBoundaryTests.cs`).

Architecture / boundary expectations:

- `Dobby.Security` still does not reference `Dobby.Audit`, `Dobby.Host`, `Dobby.Shell`, or WPF-only surfaces (`src/Dobby.Security/Dobby.Security.csproj`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`).
- Host coordinates audit through existing audit seams (`src/Dobby.Audit/IHostTransactionAuditTrail.cs`; `D-16`).
- Durable Settings preference ownership remains in `Dobby.Configuration`; concrete `HostConfiguration` table access remains Host-wired outside `Dobby.Configuration`, with no direct `Dobby.Configuration -> Dobby.Infrastructure` edge (`D-21`; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `src/Dobby.Configuration/Dobby.Configuration.csproj`).
- No new package pins or inline versions (`Directory.Packages.props`; `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs`).
- No new Core `ErrorCode` entries (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`).

Per-AC behavior expectations:

| AC | Required Sprint 6 evidence |
|---|---|
| `AC-022` | `OperationVerbParserTests`, `OperationTargetRegistryTests`, `OperationAuthorizationPolicyTests`, and `OperationAuthorizationIntegrationTests` prove canonical verbs, built-in targets, grant-only/default-deny, Owner/self-grant guards, unknown target/verb deny, and non-disclosing audit behavior. |
| `AC-030` | `UserManagementServiceTests` prove list/detail under `view`, denial without `view`, no password hashes or protected credential material in list/detail results, and create/update/deactivate/reactivate/reset-password/grant/revoke with service validation, DB constraints, permission enforcement, delete-as-lifecycle mapping, and no hard-delete path. |
| `AC-039` | `UserManagementServiceTests` plus existing/extended `LoginServiceTests` prove deactivated active managed user cannot continue protected operations and cannot login until reactivated. |
| `AC-035` contribution | `SettingsAndAboutViewModelTests`, `SettingsPreferenceSurfaceTests`, `SettingsPreferencePersistenceTests`, `AboutInfoProviderTests`, and localization tests prove source behavior for minimum category tree, `Dobby.Configuration`-owned Appearance shared theme preference behavior, Host-wired `HostConfiguration`-backed persistence, Account password-change shortcut without manual lock/logout duplication, Security/Session default-passive auto-lock toggle/minute input source behavior, User Management entry gating plus list/detail source availability through `view`, About fields, no license import/status, and neutral Owner identity key. |

Evidence expectations:

- `artifacts/release-evidence/skeleton-local/decision-log.md` contains `D-20`, the human-approved built-in User Management target id, and `D-21`, the human-approved Settings namespace/preference authority, before source implementation.
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` contains a Sprint 6 block with implementation summary, AC matrix, test command results, architecture/boundary statement, package-governance statement, and remaining deferred work.
- `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs` verifies the Sprint 6 evidence block after implementation.
- `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` remains unchanged unless a new package/native asset was human-approved.

Boundary preservation expectations:

- License import remains under Plugins > License and not About/Settings (`docs/specs/license-import.md`; `docs/specs/settings-and-about.md`).
- Full plugin manifest schema/package validation and plugin `operationTargets` seeding from signed packages remain Sprint 8 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 8).
- Rendered Settings snapshots remain Sprint 7 (`SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7).

Open items allowed at close:

- Deferred later-sprint responsibilities may remain open if documented as Sprint 7/Sprint 8/later work.
- No unresolved Sprint 6 authority gate may remain; `D-20` must remain the cited User Management target id authority and `D-21` must remain the cited Settings namespace/preference authority at implementation completion.

## 17. Reviewer Focus

1. Confirm `D-20` records `users.management` and `D-21` records Settings namespace/preference authority before source implementation in `artifacts/release-evidence/skeleton-local/decision-log.md`.
2. Inspect `src/Dobby.Security/Authorization/OperationKey.cs` and `tests/Dobby.UnitTests/Security/OperationVerbParserTests.cs` for full canonical verb coverage.
3. Inspect `src/Dobby.Security/Authorization/OperationTargetRegistry.cs`, `BuiltInOperationTargets.cs`, and `tests/Dobby.UnitTests/Security/OperationTargetRegistryTests.cs` for exact built-in target ids, supported verb toggles, and no explicit deny surface.
4. Inspect `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs` and `tests/Dobby.IntegrationTests/Security/OperationAuthorizationIntegrationTests.cs` for Owner all-grants, managed default deny, unknown target/verb deny, and non-disclosing audit behavior.
5. Inspect `src/Dobby.Security/Authentication/ManagedUserService.cs`, `src/Dobby.Security/Authorization/UserManagementAuthorizationGuard.cs`, and `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs` for list/detail `view` behavior, CRUD/reset/reactivate/grant/revoke behavior, Owner/self-grant protections, and no exposure of password hashes or protected credential material.
6. Inspect `src/Dobby.Host/Settings/UserManagementAuditCoordinator.cs`, `tests/Dobby.UnitTests/Host/UserManagementAuditCoordinatorTests.cs`, and `src/Dobby.Audit/AuditEventName.cs` for existing audit event names only.
7. Run or inspect `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs` to verify no `Dobby.Security -> Dobby.Audit` edge and unchanged Core `ErrorCode` registry.
8. Inspect `src/Dobby.Host/Settings/SettingsNavigationModel.cs`, `src/Dobby.Host/Settings/AppearanceSettingsModel.cs`, `src/Dobby.Host/Settings/AccountSettingsModel.cs`, `src/Dobby.Host/Settings/SecuritySessionSettingsModel.cs`, `src/Dobby.Host/Settings/AboutInfoProvider.cs`, the `Dobby.Configuration` typed Settings preference surface files chosen during implementation, `tests/Dobby.UnitTests/Host/SettingsAndAboutViewModelTests.cs`, `tests/Dobby.UnitTests/Configuration/SettingsPreferenceSurfaceTests.cs`, `tests/Dobby.IntegrationTests/Host/SettingsPreferencePersistenceTests.cs`, and `tests/Dobby.UnitTests/Host/AboutInfoProviderTests.cs` for Settings/About source behavior, Host-wired `HostConfiguration`-backed Appearance preference persistence, Account password-change shortcut without manual lock/logout duplication, Security/Session auto-lock source behavior, User Management entry gating plus list/detail source availability through `view`, no Host-owned durable preference store, no direct `Dobby.Configuration -> Dobby.Infrastructure` edge, and privacy boundaries.
9. Inspect `src/Dobby.Localization/HostStringCatalog.cs` and localization tests for `About.OwnerStatus.Registered` and non-disclosing text.
10. Inspect `Directory.Packages.props`, all modified `.csproj` files, and `tests/Dobby.IntegrationTests/Architecture/PackageGovernanceTests.cs` for package-governance compliance.
11. Inspect `artifacts/release-evidence/skeleton-local/architecture-baseline.md` and `tests/Dobby.IntegrationTests/Architecture/Sprint6ReleaseEvidenceTests.cs` for Sprint 6 AC/test/evidence closure.
12. Verify the diff excludes docs/ADRs/specs/schemas, earlier sprint plans, plugin activation, license import implementation, rendered Settings snapshots, installer, tools, and deferred runtime projects listed in §9 and §15.

## 18. Final Execution Checklist

Before-starting checks:

- Confirm `artifacts/release-evidence/skeleton-local/decision-log.md` records `D-20` with exact built-in User Management target id `users.management` and `D-21` with Settings namespace/preference authority.
- Re-open `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6, `docs/specs/user-management.md`, `docs/specs/settings-and-about.md`, `docs/adr/ADR-0048-operation-based-authorization.md`, and `docs/07_ACCEPTANCE_CRITERIA.md` before coding.
- Confirm `Directory.Packages.props`, `global.json`, and `Directory.Build.props` do not need changes.
- Confirm the existing `HostConfiguration` table remains the Host-wired backing store for Sprint 6 Appearance and Security/Session preference state, durable Settings preference ownership remains in `Dobby.Configuration`, and no schema widening, public settings surface, or direct `Dobby.Configuration -> Dobby.Infrastructure` edge is needed.
- Confirm no existing branch/worktree changes require reverting; do not revert unrelated changes.

During-implementation rules:

- Write failing tests before implementation for each Sprint 6 behavior group.
- Preserve service-boundary authorization; UI hiding/disabling is never sufficient.
- Keep User Management `view` behavior explicit: list/detail service and source-model data must require `view` for managed users and must not expose password hashes or protected credential material.
- Keep Appearance at source-behavior level: shared `Dobby.Configuration` preference surface and Host-wired `HostConfiguration` theme preference persistence tests are Sprint 6, rendered TopToolbar interaction and snapshots are Sprint 7.
- Keep Account Settings as shortcut to existing password-change behavior; do not add manual lock/logout duplication in Settings.
- Keep Security/Session Settings at source-behavior level: default-passive auto-lock toggle and minute input state are Sprint 6, runtime session lifecycle changes remain out of scope unless existing authority requires them.
- Use the `Dobby.Configuration` typed Settings preference surface and abstractions with Host-wired `HostConfiguration` backing for Settings source state; stop before adding schema columns/tables, public Contracts/SDK settings surfaces, plugin-facing settings access, Host-owned durable preference stores, durable preference abstraction names that imply Host storage ownership or exceed `D-21`, a direct `Dobby.Configuration -> Dobby.Infrastructure` edge, or non-durable-only preference state.
- Keep `Dobby.Security` audit-neutral; Host coordinates audit.
- Use existing `Result`/`PolicyDecision`/`ErrorCode` policy; do not add Core errors.
- Use existing audit event constants; do not invent audit events.
- Keep package-governance rule: no new package/version/source without human approval.
- Keep no-direct-OS-API-from-ViewModel rule.
- Stop before creating any unsupported namespace, file, target id alias, sub-code closed set, schema column, package pin, or public contract.

Before-finishing checks:

- Named tests in §14 are present and green.
- Architecture, package-governance, encoding, release-evidence, unit, integration, and smoke gates required by `docs/09_BUILD_RUN_AND_RELEASE.md` are green.
- `artifacts/release-evidence/skeleton-local/architecture-baseline.md` contains Sprint 6 evidence and `Sprint6ReleaseEvidenceTests` passes.
- No unresolved Sprint 6 authority gate remains.
- Later-sprint boundaries from §15 are still untouched.

## 19. Planning Coverage and Confidence

| Topic | Coverage | Justification |
|---|---|---|
| Sprint scope and sequencing | High | Sprint 6, adjacent Sprint 5/Sprint 7/Sprint 8 sections and previous execution plans were inspected (`SPRINT_IMPLEMENTATION_PLAN.md`; `SPRINT_5_EXECUTION_PLAN.md`). |
| Prior sprint continuity | High | Current code/tests/evidence verify storage, authentication, session, recovery, package, and architecture foundations (`artifacts/release-evidence/skeleton-local/*.md`; `src/Dobby.Security/Authentication/`; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`). |
| Operation authorization behavior | High | ADR/spec/code/schema evidence is strong for verbs, policy shape, grant-only/default-deny, built-in license/plugin ids, and User Management target id via `D-20` (`docs/adr/ADR-0048-operation-based-authorization.md`; `docs/specs/user-management.md`; `artifacts/release-evidence/skeleton-local/decision-log.md`). |
| User Management implementation | Medium | Required behavior and schema are documented; exact service/file placement remains captured as implementation assumptions in §7a (`docs/specs/user-management.md`; `src/Dobby.Infrastructure/Persistence/PreLoginMigrationV1.cs`). |
| Settings/About source behavior | Medium-high | Spec and architecture are clear, Appearance shared theme preference source behavior, Account shortcuts, Security/Session source state, `Dobby.Configuration` durable preference ownership, and Host-wired `HostConfiguration` persistence are explicitly planned through `D-21`; rendered UI belongs to Sprint 7, and exact internal preference-key naming plus implementation class names remain implementation assumptions within the `D-21` boundary (`docs/specs/settings-and-about.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 7; `docs/adr/ADR-0040-internal-host-adapters-boundary.md`; `D-21`; Assumption A6). |
| Error/result/audit policy | High | Existing Result policy, audit constants, and architecture tests define the allowed surface (`docs/04_RESULT_VS_EXCEPTION_POLICY.md`; `src/Dobby.Audit/AuditEventName.cs`; `tests/Dobby.IntegrationTests/Architecture/ArchitectureDependencyBoundaryTests.cs`). |
| Technology/package plan | High | Current SDK/build/package pins and evidence were inspected; no new package is needed (`global.json`; `Directory.Build.props`; `Directory.Packages.props`; `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`). |
| Test and evidence plan | High | Existing test layout, prior evidence tests, and Sprint 6 verification expectations map directly to named tests and evidence appends (`docs/09_BUILD_RUN_AND_RELEASE.md`; `SPRINT_IMPLEMENTATION_PLAN.md` §Sprint 6 Verification). |

Confidence limits:

- Implementation must use the recorded `D-20` target id `users.management` exactly; aliases/placeholders are not allowed.
- Exact file placement for authorization/user-management/settings is source-grounded but still captured as implementation assumptions because current docs and code use overlapping placement language.
- Settings preference source behavior must use the `Dobby.Configuration` typed Settings preference surface with Host-wired `HostConfiguration` backing from `D-21` and Assumption A6; if implementation evidence shows that path cannot safely carry Appearance/Security preferences, a human schema/architecture decision is required before choosing a non-durable source, Host-owned durable store, direct `Dobby.Configuration -> Dobby.Infrastructure` edge, or schema widening.
- Rendered Settings visual evidence remains Sprint 7 responsibility.
- Plugin package activation/seeding remains Sprint 8 responsibility.
- License import/runtime behavior remains later-sprint responsibility.

Planning stance: READY FOR IMPLEMENTATION under the assumptions and guardrails above.
