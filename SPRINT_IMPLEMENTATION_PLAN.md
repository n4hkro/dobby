# 1. Sprint Plan Overview

- Implementation scope: P1 Dobby Windows desktop host foundation, including login-first local authentication, Owner/user/session controls, shell/navigation/theme/localization, plugin package trust and activation, detached licensing, audit/diagnostics, storage/data protection, sample plugin validation, admin artifact tooling, installer, and release evidence.
- Total sprint count: 11.
- Sprint ordering: establish documented boundaries first, then persistence/security foundations, then authenticated shell behavior, then plugin/licensing runtime, then release evidence. Work marked P1-if-time, P2, P3, Branch Shared/Tier-B, or human-decision gated is kept out of the critical P1 path unless explicitly approved.

# 2. Sprint List

| Sprint | Title | Main goal | Main deliverable | Dependencies | Ready / blocked status |
|---|---|---|---|---|---|
| 1 | Toolchain and Architecture Baseline | Verify repo, project, build, and architecture guardrails | Existing solution/build baseline verified against documented boundaries, static checks, and approved pin-list evidence | Current `global.json`, `Directory.Packages.props`, docs toolchain governance, and human-approved pin list or documented stop condition | Ready only after current pins are verified against the approved pin list; package-bearing work remains blocked if no approval record exists |
| 2 | Core Contracts and Shared Primitives | Implement the stable Core/Contracts/SDK surfaces allowed by ADRs | Result/error/correlation/versioning contracts, plugin-facing storage, policy, navigation, operation, event, and page contracts | Sprint 1 | Ready after Sprint 1 |
| 3 | Configuration, Runtime Folders, Diagnostics, and Audit | Establish host configuration floors, runtime layout, local logging, diagnostics, and append-only audit boundaries | Config model, runtime folder handling, diagnostics sink, audit service boundary, redaction and retention floors | Sprints 1-2 | Ready after dependencies |
| 4 | Protected Storage and Persistence Materialization | Materialize protected local persistence required for P1 release | Protected storage, local encrypted DB proof, schema/bootstrap/migration, storage tests | Human-approved storage abstraction table and ADR-0054 provider materialization decision | Blocked by documented storage decisions |
| 5 | Login-First Auth, Sessions, and Owner Recovery | Build login-first startup, local auth, session lifecycle, first Owner bootstrap, and host-side recovery flow | Login/lock/logout/password-change flows, Owner bootstrap, recovery request/import against the Sprint 5 TEST-ONLY fixture authority ledger | Sprints 2-4; approved Sprint 5 TEST-ONLY fixture authority; production vendor key handover deferred to Sprint 11 | Ready after storage foundation exists; production/Admin-generated AC-007 evidence depends on Sprint 10/11 |
| 6 | Operation Authorization, Users, and Settings | Implement permission model, User Management, Settings, Account, Security/Session, and About surfaces | Operation registry, grant-only permissions, Owner guards, Settings tree | Sprints 2-5 | Ready after dependencies |
| 7 | Shell, Navigation, Theme, Localization, and Work Sessions | Build the authenticated WPF shell and documented UX surfaces | TopToolbar/Sidebar/Third Bar/Main Content/status strip, theme/localization, in-memory work sessions | Sprints 2-6 | Ready after dependencies |
| 8 | Plugin Package Trust and Activation Runtime | Implement signed plugin install, package validation, ALC loading, dependency ordering, DI registration, and post-login activation | Trusted local .dobbyplugin runtime foundation | Sprints 2-7 | Ready after dependencies |
| 9 | Licensing and Runtime Mode Enforcement | Implement detached .dobbylicense lifecycle and host-enforced runtime modes | License import/status, binding checks, FullAccess/ReadOnlyExpired/Denied enforcement | Sprints 4, 6, 8 | Ready after dependencies |
| 10 | Plugins Manager, Sample Plugin, and Admin Artifact Tools | Complete P1 plugin management workflows and validation artifacts | Plugins UI, sample plugin, signing/license/recovery tools, dev/QA trust exclusion | Sprints 5, 8-9; admin option naming decision | Partly blocked by tool option decision |
| 11 | Release Gates, Installer, Evidence, and Smoke | Prove P1 readiness against documented acceptance criteria | Installer artifacts, smoke/integration/unit evidence, performance baseline, release gate checklist | Sprints 1-10 | Ready after implementation dependencies |

# 3. Detailed Sprint Breakdown

## Sprint 1 — Toolchain and Architecture Baseline

### Goal
Verify the existing solution structure, build conventions, and architecture guardrails without crossing package-version decision gates.

### Scope
- Existing repo-root build and documentation alignment.
- P1 project ownership boundaries from the architecture map.
- Dependency rules, naming conventions, encoding rules, and static architecture checks.
- Existing solution readiness against the current `global.json` and `Directory.Packages.props` pins.

### Subsections / Task Groups
- **Documentation Baseline**
  - Concrete tasks: map P1-core, P1-if-time, P2/future, Branch Shared/Tier-B, and initial-skeleton exclusions from the docs.
  - Expected result: future agents can identify release-blocking work and deferred work without reinterpreting scope.
- **Solution Shape**
  - Concrete tasks: verify current projects and build files against `docs/09_BUILD_RUN_AND_RELEASE.md` and `docs/02_ARCHITECTURE_MAP.md`.
  - Expected result: project/module ownership is explicit before implementation continues.
- **Toolchain Gate**
  - Concrete tasks: map current `global.json`, `Directory.Packages.props`, and any existing package references to the human-approved toolchain/package pin list; stop before adding any package/version not covered by that approval record.
  - Expected result: existing pins are treated as usable only after approval evidence is verified; no invented package versions or unsupported dependencies are added.
- **Architecture Checks**
  - Concrete tasks: define checks for plugin reference boundaries, SDK limits, Contracts dependencies, UTF-8/no-BOM, and forbidden namespace patterns.
  - Expected result: implementation can be verified against documented architecture rules.

### Dependencies
- Root `README.md`.
- Current `global.json`.
- Current `Directory.Packages.props`.
- `docs/01_PRODUCT_SCOPE.md`.
- `docs/02_ARCHITECTURE_MAP.md`.
- `docs/03_TECH_STACK_AND_VERSIONS.md`.
- `docs/05_FUNCTIONAL_REQUIREMENTS.md`.
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md`.
- `docs/08_CODING_STANDARDS.md`.
- `docs/09_BUILD_RUN_AND_RELEASE.md`, especially Section 9 (Static and Dynamic Scan Tooling).
- Human-approved pin list covering current `global.json`, `Directory.Packages.props`, and existing package references; if absent, package-bearing project work in Sprint 2+ is blocked.
- Human decision for any package/version not already present in the approved pin list.

### Acceptance Criteria
- Primary AC ownership: none; Sprint 1 supplies build, static-check, and toolchain guardrails consumed by later sprint evidence.
- P1-core and deferred scope are separated.
- No Branch Shared runtime, Branch Shared `ErrorCode` identifiers in the initial `Dobby.Core/ErrorCodes` registry, `Dobby.Infrastructure.Postgres`, Npgsql, BranchSetup, Dobby.Update, marketplace, update/repair/downgrade, backup/restore, or full Admin UI is included in the P1 critical path.
- Dependency direction rules are known before code work starts.
- Current pin files and existing package references are reconciled to the human-approved pin list; if no approval record exists, package-bearing project work is recorded as blocked before Sprint 2+ starts.
- Any new package/version choice stops for human approval.

### Verification
- Review against architecture map dependency table.
- Verify current `global.json`, `Directory.Packages.props`, and existing package references against the approved pin list fields required by `docs/03_TECH_STACK_AND_VERSIONS.md`.
- Run documented build/static checks once files exist.
- Confirm no P2/Tier-B project is accidentally added to P1 implementation.

### Risks / Blockers
- Missing approval evidence for current `global.json`, `Directory.Packages.props`, or existing package references is a documented stop condition for package-bearing project work in Sprint 2+.
- Any package/version not present in the human-approved pin list is a documented stop condition.
- Changing package-bearing files outside the approved pin set would violate the tech-stack gate.

### Exit Condition
Sprint finishes when the existing skeleton is verified against documented architecture/build boundaries and any unapproved package/version need is recorded as a stop condition.

## Sprint 2 — Core Contracts and Shared Primitives

### Goal
Implement the stable shared surfaces that host, SDK, and plugins are allowed to depend on.

### Scope
- Core primitives allowed by ADR-0001.
- Result and error code policy.
- Correlation ID ownership.
- Host version contract.
- Plugin-facing Contracts and SDK convenience surfaces.
- No host implementation leakage into plugin-facing APIs.

### Subsections / Task Groups
- **Core Result and Errors**
  - Concrete tasks: implement Result shape, expected failure mapping, cancellation mapping, and baseline error codes.
  - Expected result: expected/domain failures use Result and plugin boundary failures do not expose raw exceptions.
- **Correlation and Versioning**
  - Concrete tasks: implement `CorrelationId`, generator/context accessor, and `HostVersion` provider contract.
  - Expected result: diagnostics, audit, background operations, and plugin activation can carry stable correlation/version data.
- **Plugin Storage Contracts**
  - Concrete tasks: implement ADR-0003 plugin-visible `IPluginStorage`, `SharedDomainStorage`, and `UserScopedStorage` contract shape.
  - Expected result: plugins can target documented storage compartments without seeing backing storage topology.
- **Policy, Operation, Navigation, Events, and Pages**
  - Concrete tasks: implement capability policy contracts, operation target vocabulary, ordering keys, route/page descriptors, event bus contract, background operation snapshots, and ADR-0053 licensing runtime surface contracts (`PluginRuntimeMode`, `ICapabilityLicensing.GetCurrentMode()`).
  - Expected result: shell, security, plugins, and sample plugin share one documented contract vocabulary.
- **SDK Surface**
  - Concrete tasks: provide only documented SDK helpers and `PluginBase` behavior.
  - Expected result: SDK remains convenience-only and does not create new capabilities, logging, telemetry, WPF, or service-locator access.

### Dependencies
- Sprint 1.
- ADR-0001, ADR-0003, ADR-0004, ADR-0005, ADR-0010, ADR-0013, ADR-0015, ADR-0021, ADR-0029, ADR-0036, ADR-0048, ADR-0049, ADR-0053.
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`.

### Acceptance Criteria
- Primary AC ownership: none; Sprint 2 supplies shared contracts used by later AC-owning sprints.
- Contracts reference only allowed Core/BCL surfaces.
- SDK does not expose host internals.
- Plugin-facing APIs contain no WPF types.
- Error and Result handling match the documented policy.

### Verification
- Unit tests for Result, error mapping, correlation flow, ordering comparer, route identity, operation key parsing, and contract defaults.
- Static dependency check for Contracts and SDK references.

### Risks / Blockers
- Host-internal storage abstraction names remain blocked; only plugin-visible ADR-0003 storage contracts are safe in this sprint.

### Exit Condition
Sprint finishes when shared primitives compile, have focused tests, and do not violate dependency rules.

## Sprint 3 — Configuration, Runtime Folders, Diagnostics, and Audit

### Goal
Create the host infrastructure needed before authentication and shell startup can be safely implemented.

### Scope
- Configuration source order and security floors.
- Runtime folder layout under `%LocalAppData%/Dobby/`.
- Local-only logging and diagnostics.
- Audit boundary, event names, payload minimums, redaction, and retention floors.
- Diagnostics export rules without telemetry.

### Subsections / Task Groups
- **Configuration Model**
  - Concrete tasks: implement documented config keys, environment variable mapping, validation, and fail-closed security floors.
  - Expected result: startup cannot weaken storage, signing, KDF, protected-storage, or related security floors through config.
- **Runtime Folder Handling**
  - Concrete tasks: create required folders safely and fail closed for missing protected material or provider metadata.
  - Expected result: folder recovery follows the storage/data protection spec.
- **Diagnostics**
  - Concrete tasks: configure local-only structured diagnostics with redaction, 30-day default retention, and finite total size cap behavior.
  - Expected result: technical troubleshooting data stays local unless explicitly exported.
- **Audit**
  - Concrete tasks: implement append-only audit boundary, canonical event taxonomy, minimum payload, correlation, actor, target, result, redaction rules, and 365-day minimum retention floor.
  - Expected result: security/business-significant actions have a separate audit trail from diagnostics.

### Dependencies
- Sprints 1-2.
- ADR-0016, ADR-0019, ADR-0027, ADR-0040, ADR-0055.
- `docs/specs/audit-and-diagnostics.md`.
- `docs/specs/storage-and-data-protection.md`.

### Acceptance Criteria
- Primary AC ownership: AC-010, AC-033, AC-052.
- No telemetry or remote upload exists by default.
- Audit and diagnostics are separate.
- Audit log default minimum retention is 365 days and config cannot lower it.
- Technical diagnostics default retention is 30 days and release verification fails without a finite total size cap.
- Security floors are not configurable downward.
- Runtime folder behavior is support-safe and fail-closed where required.

### Verification
- Unit tests for config binding/validation and weaker-value rejection.
- Audit event shape tests.
- Redaction tests for logs and exports.
- Retention-floor tests for AC-052: audit retention cannot go below 365 days, diagnostics default to 30 days, and diagnostics require a finite total size cap.
- Runtime folder recovery/fail-closed tests.

### Risks / Blockers
- Durable audit persistence depends on Sprint 4 materialization.
- Exact host-internal protected-storage method surfaces may depend on the architecture-approved table.

### Exit Condition
Sprint finishes when config, local diagnostics, audit boundaries, and runtime folder behavior are ready for persistence integration.

## Sprint 4 — Protected Storage and Persistence Materialization

### Goal
Materialize the protected local persistence required for P1 release without exposing storage topology to plugins.

### Scope
- Provider-neutral storage abstraction only after approved architecture input.
- Encrypted local DB provider proof per ADR-0054.
- Protected-storage mapping per ADR-0055.
- Pre-login bootstrap closed table set.
- Post-login migration infrastructure for consumer-owned P1 persisted host state.
- Transactional write and crash-recovery behavior.

### Subsections / Task Groups
- **Decision Gate**
  - Concrete tasks: obtain human-approved provider-neutral abstraction table, owner project/namespace placements, method surfaces, Result behavior, and error-code surfaces.
  - Expected result: storage implementation can proceed without invented repository/session names.
- **DB-at-Rest Provider Materialization**
  - Concrete tasks: apply the accepted ADR-0054 provider direction, provider probe, encryption proof, WAL/journal/temp protection, and startup fail-closed checks.
  - Expected result: production release cannot run with an unprotected DB file.
- **Pre-login Bootstrap**
  - Concrete tasks: materialize only the closed pre-login set: Users, Credentials, RememberMeSlot, Sessions, Permissions, AuditLog, ConsumedRecoveryNonces, CustomerDeploymentContext, HostConfiguration, StorageMetadata, and DeviceIdentity.
  - Expected result: login UI can start without post-login feature tables or plugin I/O.
- **Post-login Migration Infrastructure**
  - Concrete tasks: provide the migration framework (`IHostPersistenceMigrator`, `StorageMetadata` migration ledger, `NNNN_descriptive_step_name` numbered migration ID convention) and the post-login migration ordering invariant: post-login migrations only run after successful authentication / the authenticated-session seam, never during pre-login bootstrap and never merely because `LoginVisible` was emitted.
  - Expected result: consumer sprints (5, 6, 7, 8, 9) materialize their own post-login schemas as numbered migrations through evidence-layer schema reviews owned by their respective sprints; the migration framework, ledger, and ordering invariant are ready for them.
  - Authority cross-reference: `artifacts/release-evidence/skeleton-local/storage-materialization.md` `D4-11` (post-login feature tables and extra columns may only be materialized through later numbered migrations after the authenticated-session seam).
- **Protection and Integrity**
  - Concrete tasks: implement DPAPI scope mapping, Dobby application-level entropy, Dobby stable device id separation, transaction boundaries, foreign key/busy timeout guardrails, and crash-recovery behavior.
  - Expected result: protected material is not logged, copied as plaintext, or used incorrectly as a key source.

### Dependencies
- Sprints 1-3.
- ADR-0030, ADR-0040, ADR-0054, ADR-0055.
- `docs/specs/storage-and-data-protection.md`.
- Human-approved storage abstraction and provider decisions.

### Acceptance Criteria
- Primary AC ownership: AC-037, AC-038, AC-042.
- Unprotected production DB is impossible for P1 release.
- Plugin storage topology remains hidden.
- Secret material gets field-level/protected-storage handling.
- Pre-login bootstrap remains closed and idempotent.
- Transactions rollback partial multi-step mutations.
- Crash/power-loss recovery does not require reinstall.

### Verification
- Provider probe and encryption proof tests.
- Protected-storage scope tests.
- Plaintext secret scan.
- Transaction commit/rollback tests.
- Crash-recovery smoke.
- Static scan proving plugins cannot reference host persistence or Infrastructure.

### Risks / Blockers
- This sprint is blocked until documented human decisions are available.
- Initial skeleton rules prohibit concrete migrations/entities/repositories/native encrypted SQLite bootstrap before this materialization pass.

### Exit Condition
Sprint finishes when protected persistence is release-gate ready and downstream auth/plugin/licensing flows can store required state.

## Sprint 5 — Login-First Auth, Sessions, and Owner Recovery

### Goal
Implement offline-first login, local credential handling, session lifecycle, first Owner bootstrap, and Owner recovery.

### Scope
- Login-first startup policy.
- Local username/password auth.
- Remember-me masked saved-login slot.
- Throttle and password/KDF floors.
- First-user Owner bootstrap.
- Single-instance host coordinator before login UI.
- Session creation, logout, lock, unlock, timeout, invalidation, and password change.
- Owner recovery request/response import.

### Subsections / Task Groups
- **Login-First Startup**
  - Concrete tasks: enforce allowed pre-login workload and prevent plugin discovery, activation, migrations beyond the closed set, online/update work, import/export, and background tasks before login.
  - Expected result: app always starts at login first and remains offline-capable.
- **Single-Instance Host Coordinator**
  - Concrete tasks: acquire the host single-instance coordinator before login UI, focus or hand off second-instance launch attempts to the active instance, and prevent a second writable host persistence/plugin/runtime session.
  - Expected result: process ownership is established before login and AC-044 has a concrete implementation owner.
- **Credential Flow**
  - Concrete tasks: implement ASCII canonical username validation, display name support, simple password policy, KDF floors, throttle behavior, and remember-me masking.
  - Expected result: local authentication follows user-management and tech-stack requirements.
- **First Owner Bootstrap**
  - Concrete tasks: create first Owner transactionally with Owner protected-principal/all-grants state and audit entry.
  - Expected result: clean install produces exactly one protected Owner without partial state.
- **Session Lifecycle**
  - Concrete tasks: implement single active user session, lock preserving session, logout cleanup, idle/absolute timeout, deactivation invalidation, and current-user password change behavior.
  - Expected result: session state is predictable and plugin runtime starts only after authentication.
- **Owner Recovery**
  - Concrete tasks: implement host-side `.dobbyrecoveryrequest` export, `.dobbyrecovery` import, signature/expiry/one-use/device-bound checks, nonce consumption, password reset, and audit.
  - Expected result: Owner recovery host behavior is offline, signed against the Sprint 5 TEST-ONLY fixture authority in `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`, bounded, and separate from managed user reset; production vendor-key handover and Admin-generated end-to-end evidence are explicitly deferred to Sprint 10/11.

### Dependencies
- Sprints 2-4.
- ADR-0031, ADR-0038, ADR-0039, ADR-0041, ADR-0055.
- `docs/specs/session-lifecycle.md`.
- `docs/specs/user-management.md`.
- Recovery fixture source for Sprint 5 host-side import evidence: the human-approved Sprint 5 TEST-ONLY fixture authority model in `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`. The ledger must remain `APPROVED`, every `PENDING_IMPLEMENTATION_EVIDENCE` field must be replaced with concrete evidence before Sprint 5 closure, and production vendor private/public key custody remains a Sprint 11 release-candidate handover gate.

### Acceptance Criteria
- Primary AC ownership: AC-001, AC-002, AC-007 host-side request/export/import checks, AC-025, AC-026, AC-028, AC-029, AC-044, AC-045, AC-050, AC-051 skeleton-time KDF selection/floors/salt/fail-closed checks.
- AC-007 end-to-end evidence using an Admin-generated signed `.dobbyrecovery` is owned by Sprint 10/Sprint 11 after `create-recovery-response` exists.
- AC-008 contribution: Sprint 5 owns offline login/auth evidence; Sprint 7 shell evidence and Sprint 8 local plugin-host foundation evidence complete the AC, with Sprint 11 consolidating release smoke.
- AC-051 split ownership: Sprint 5 owns skeleton-time KDF selection, minimum floors, salt requirements, and fail-closed floor enforcement; Sprint 11 owns ADR-0042 baseline-latency evidence used to justify PBKDF2 fallback.
- No plugin runtime work happens before successful login.
- Logout clears active session and runtime state but not remember-me metadata.
- Lock preserves session and active work sessions.
- Owner recovery cannot be replayed or used cross-device.

### Verification
- Unit and integration tests for login, throttle, KDF metadata, remember-me, Owner bootstrap transaction, logout, lock/unlock, timeout, password change, deactivation invalidation, and recovery import against the approved Sprint 5 TEST-ONLY fixture set.
- Integration/manual test for AC-044 proving second host launch focuses or hands off to the active instance and does not open a second writable persistence session.
- Startup instrumentation proves forbidden pre-login work does not run.
- Audit verification for auth/session/recovery events.

### Risks / Blockers
- Requires protected persistence from Sprint 4.
- Exact KDF implementation depends on approved package pins.
- Production end-to-end AC-007 evidence through an Admin-generated signed `.dobbyrecovery` depends on Sprint 10 `create-recovery-response` and Sprint 11 release evidence.
- Sprint 5 host-side recovery import evidence depends on the TEST-ONLY ledger being populated with public-key, fixture inventory, private-key custody, TEST-ONLY exclusion, and handover evidence before closure.

### Exit Condition
Sprint finishes when a clean install can create/login an Owner, maintain sessions correctly, and validate host-side Owner recovery request/export/import behavior against the approved Sprint 5 TEST-ONLY fixture set; documented production end-to-end recovery with an Admin-generated artifact and production vendor key custody is verified in Sprint 10/11.

## Sprint 6 — Operation Authorization, Users, and Settings

### Goal
Implement host authorization, user management, and the documented Settings/About surfaces.

### Scope
- Operation-based authorization.
- Built-in targets and verbs.
- Grant-only/default-deny permission model.
- Owner protection and managed-user guards.
- Settings tree: Appearance, Account, Security/Session, User Management, About.
- License import remains under Plugins > License, not About.

### Subsections / Task Groups
- **Operation Registry**
  - Concrete tasks: register built-in operation targets, canonical verbs, key parsing, Owner all-grants, managed default deny, no explicit deny, and non-disclosing failure messages.
  - Expected result: every protected operation is service-boundary authorized.
- **User Management**
  - Concrete tasks: implement create/update/deactivate/reactivate/reset-password/grant-revoke for managed users with service validation and DB constraints.
  - Expected result: User Management supports P1 CRUD without Owner bypass.
- **Owner and Self-Grant Guards**
  - Concrete tasks: block managed-user changes to Owner, self-grant, granting permissions the actor lacks, unknown targets, and unsupported verbs.
  - Expected result: permission escalation paths fail closed and are audited.
- **Settings and Account**
  - Concrete tasks: implement Settings navigation visibility, Appearance theme selection, Account password/saved-login controls, Security/Session auto-lock controls, User Management visibility, and About read-only fields.
  - Expected result: authenticated settings match documented surfaces.

### Dependencies
- Sprints 2-5.
- ADR-0004, ADR-0028, ADR-0031, ADR-0048.
- `docs/specs/settings-and-about.md`.
- `docs/specs/user-management.md`.
- `docs/specs/license-import.md`.

### Acceptance Criteria
- Primary AC ownership: AC-022, AC-030, AC-039.
- AC-022 contribution boundary: Sprint 6 owns built-in targets, canonical verbs, grant-only authorization, Owner/self-grant guards, unknown target/verb deny behavior, and service-boundary enforcement; Sprint 8 seeds plugin-declared targets from validated signed manifest `operationTargets` before activation.
- AC-035 contribution: Sprint 6 owns Settings data/permission/account/security/About source behavior; Sprint 7 owns rendered Settings tree, TopToolbar/Appearance theme interaction, snapshot evidence, and manual evidence.
- UI hiding/disabling is not the only enforcement layer.
- About does not become a license-management surface.
- Deactivated managed users cannot continue protected operations.

### Verification
- Unit tests for operation parsing, grant decisions, Owner guards, self-grant denial, and unsupported verb denial.
- Integration tests for service-boundary authorization and audit events.
- Service/view-model tests for Settings visibility decisions and About fields; rendered Settings and snapshot evidence are verified in Sprint 7.

### Risks / Blockers
- Durable permission and user state depends on Sprint 4.
- Some visual polish belongs to P1-if-time and should not block P1-core.

### Exit Condition
Sprint finishes when Settings/User Management operations work through the documented permission model and cannot bypass Owner protections.

## Sprint 7 — Shell, Navigation, Theme, Localization, and Work Sessions

### Goal
Build the authenticated WPF shell and user-facing host experience required for P1.

### Scope
- WPF/WPF-UI shell layout.
- TopToolbar, Sidebar, Third Bar, Main Content, bottom status, minimized work strip.
- Built-in Home, Plugins, Settings routes.
- Route registry, fallback behavior, and hierarchical plugin navigation slots.
- Theme, design-system tokens, default `az-AZ` localization, fallback chain.
- In-memory work sessions and background operation progress.

### Subsections / Task Groups
- **Shell Composition**
  - Concrete tasks: implement documented shell surfaces, profile menu, notifications, feedback states, minimum window sizing, status/progress areas, and authenticated user/Owner diagnostics export action entry point.
  - Expected result: authenticated users land in the documented shell after login.
- **Navigation**
  - Concrete tasks: implement stable route identities, Home fallback, TopToolbar/Sidebar/Third Bar hierarchy, ordering by group/weight/tiebreak, built-in fixed positions, and user override rules.
  - Expected result: host and plugin routes render predictably.
- **Theme and Design System**
  - Concrete tasks: implement light and dark themes, the single TopToolbar light/dark switch, high-contrast/reduced-motion handling, and documented spacing/radius/typography/icon rules.
  - Expected result: UI follows design-system docs and shell screenshots can be compared.
- **Localization**
  - Concrete tasks: implement centralized strings, default `az-AZ`, optional skeleton `en-US`/`tr-TR`, missing-key diagnostics, Azerbaijani diacritic handling across input/storage/audit/UI display, and no runtime language switching.
  - Expected result: host UI text follows localization ADRs without plugin hard-coded strings, and AC-048 round-trip evidence has UI ownership with Sprint 3 audit and Sprint 4 storage plumbing.
- **Work Sessions**
  - Concrete tasks: implement explicit minimize/restore, no silent auto-minimize, navigation warning choices, controlled close/cancel, restart boundary, logout/lock/plugin lifecycle behavior, and minimized item metadata.
  - Expected result: live work remains process-lifetime only and is never treated as persisted domain data.

### Dependencies
- Sprints 2-6.
- ADR-0005, ADR-0013, ADR-0015, ADR-0028, ADR-0043, ADR-0044, ADR-0046, ADR-0049, ADR-0050.
- `docs/specs/design-system-assets.md`.
- `docs/specs/shell-ui.md`.
- `docs/specs/work-sessions.md`.

### Acceptance Criteria
- Primary AC ownership: AC-003, AC-016, AC-017, AC-019, AC-023, AC-027, AC-035, AC-040, AC-048.
- AC-035 primary ownership covers the rendered Settings tree, TopToolbar/Appearance theme interaction, snapshot evidence, and manual evidence; Sprint 6 contributes the underlying Settings data/permission/account/security/About behavior.
- AC-033 contribution: Sprint 7 owns the authenticated shell action surface for the documented user/Owner explicit diagnostics export trigger; Sprint 3 remains the diagnostics package, redaction, audit, and no-telemetry boundary owner.
- AC-011, AC-012, and AC-013 remain P1-if-time/non-blocking and do not block P1-core.
- Work sessions are not restored after restart.
- Plugin page contracts do not expose WPF types.
- Runtime language switching remains out of P1.

### Verification
- UI snapshot tests at 1024x680 and 1920x1080 covering both light and dark themes.
- AC-035 Settings tree and About-field snapshots/manual checks cover rendered category visibility, TopToolbar/Appearance theme preference interaction, and managed-user neutral Owner identity display.
- Manual checks at documented resolutions and DPI values, including the single TopToolbar theme-switch flow.
- Integration/manual check that the authenticated user/Owner diagnostics export action invokes the Sprint 3 local support package boundary and audit event.
- Unit tests for ordering and route fallback.
- Localization key and UTF-8/no-BOM scans.
- Azerbaijani diacritics round-trip integration test covering UI input/display, Sprint 3 audit payloads, and Sprint 4 storage persistence.
- Work-session lifecycle integration tests.

### Risks / Blockers
- Richer Home/favorites polish and expanded shared UI controls are P1-if-time or later.
- Shell plugin regions depend on Sprint 8 plugin activation.

### Exit Condition
Sprint finishes when authenticated shell navigation, settings access, theme/localization, and work-session behavior meet P1 acceptance hooks.

## Sprint 8 — Plugin Package Trust and Activation Runtime

### Goal
Implement the signed, offline, post-login plugin runtime foundation.

### Scope
- `.dobbyplugin` package validation.
- Manifest schema validation.
- Signature, file hash, fingerprint, forbidden-content, and trust checks.
- Host/plugin compatibility.
- Dependency graph and deferred post-login activation.
- Per-plugin AssemblyLoadContext.
- Plugin DI registration and capability boundary.
- Plugin communication through contracts, event bus, extension points, and page model.

### Subsections / Task Groups
- **Package Validation**
  - Concrete tasks: validate extension, zip shape, canonical manifest payload, schema, file hashes, forbidden content, signature, vendor trust, key version, and package fingerprint.
  - Expected result: invalid or tampered packages are rejected before runtime load.
- **Compatibility and Dependencies**
  - Concrete tasks: enforce stable SemVer, `minHostVersion`, no pre-release P1, topological sort, missing/disabled/minVersion/circular/self-dependency behavior, and audit.
  - Expected result: activation order and skipped plugins are deterministic.
- **Activation Runtime**
  - Concrete tasks: run discovery only after login, verify before each activation, load per-plugin non-collectible ALC, register services, call activation hooks, and keep shell non-blocking.
  - Expected result: plugins activate safely without blocking login or shell startup.
- **Plugin Boundary**
  - Concrete tasks: enforce Contracts/SDK-only plugin access, no direct plugin references, no reflection/shared-static/filesystem backdoors, no IServiceProvider exposure, and non-disclosing Result wrapping.
  - Expected result: plugin faults and permissions stay isolated.
- **Extension Hosting**
  - Concrete tasks: host plugin navigation entries, plugin page models, event bus subscriptions, background operation reporting, and seed plugin operation targets from validated signed manifest `operationTargets` metadata into the Sprint 6 permission registry before activation.
  - Expected result: plugins can contribute documented UI/runtime behavior.

### Dependencies
- Sprints 2-7.
- ADR-0006 through ADR-0012, ADR-0032, ADR-0033, ADR-0036, ADR-0037, ADR-0045, ADR-0049.
- `docs/schemas/dobby-plugin-manifest.schema.json`.
- `docs/specs/plugins-manager.md`.

### Acceptance Criteria
- Primary AC ownership: AC-004, AC-005, AC-009, AC-015, AC-018, AC-032, AC-043, AC-049.
- AC-022 contribution: validated plugin manifest `operationTargets` metadata seeds plugin-declared permission targets before activation; Sprint 6 remains the authorization and permission-registry behavior owner.
- License denial is not treated as package install failure.
- Dev/QA trust is rejected from production builds.
- Plugin exceptions do not crash the host or leak internals.

### Verification
- Unit tests for schema, canonical paths, hashes, signature payload, trust profile, compatibility, and dependency graph.
- Integration tests for install, activation, plugin `operationTargets` registry seeding, missing dependencies, circular dependencies, activation failure, fault isolation, and event/page hosting.
- Static dependency scan for plugin boundaries.

### Risks / Blockers
- Exact signing key material must follow ADR-0033.
- Out-of-process worker and plugin unload are P2, not P1.

### Exit Condition
Sprint finishes when signed local plugins can be validated, installed, activated after login, contribute UI, and fail safely.

## Sprint 9 — Licensing and Runtime Mode Enforcement

### Goal
Implement detached plugin licensing and host-enforced runtime modes.

### Scope
- `.dobbylicense` v2 schema validation.
- ECDSA P-256 signature verification.
- Customer/deployment context.
- Device fingerprint v1.
- Package fingerprint binding.
- Revision and status handling.
- Runtime modes: FullAccess, ReadOnlyExpired, Denied.
- Read-only expired enforcement and non-disclosing license failures.

### Subsections / Task Groups
- **License Import**
  - Concrete tasks: implement `Plugins > License` import flow, permission target `plugins.license`, file extension/shape/schema/signature/binding/package/revision checks, and audit events.
  - Expected result: users can import licenses through the canonical surface.
- **Binding and State**
  - Concrete tasks: implement customerId/deploymentId default context, device fingerprint v1, package fingerprint lookup, license revision series, active/pending/grace/readonly/denied/no-license state, and stored attestation metadata.
  - Expected result: license state is deterministic and device/package bound.
- **Runtime Enforcement**
  - Concrete tasks: map license state to plugin-visible runtime mode and host-side enforcement for mutation, extraction, report/file output, print-to-file, background, and plugin import restrictions.
  - Expected result: expired/read-only/denied behavior is enforced by the host, not plugins.
- **Sprint 1 Print Boundary Sunset**
  - Concrete tasks: close `SPRINT1-INFRA-APP-PRINT-BOUNDARY` by removing the `Dobby.Infrastructure → Dobby.Application` `ProjectReference`, consuming `IPrintTargetClassifier` through the ADR-0053 host-enforcement/DI boundary, and removing the `IsTemporaryPrintBoundaryEdge` exception from `ArchitectureDependencyBoundaryTests`.
  - Expected result: print target classification participates in runtime-mode enforcement without preserving the temporary Sprint 1 architecture-test allowance.
- **Plugin Surface**
  - Concrete tasks: expose only `ICapabilityLicensing.GetCurrentMode()` and no feature entitlement details.
  - Expected result: plugins see runtime mode only and cannot bypass host policy.

### Dependencies
- Sprints 4, 6, and 8.
- ADR-0051, ADR-0052, ADR-0053.
- `docs/schemas/dobby-plugin-license.schema.json`.
- `docs/specs/license-import.md`.

### Acceptance Criteria
- Primary AC ownership: AC-006, AC-020, AC-021, AC-034.
- License artifacts are detached from plugin packages.
- Expired licenses allow documented read-only recovery behavior only.
- Package mismatch and device/customer/deployment mismatch fail safely.
- `SPRINT1-INFRA-APP-PRINT-BOUNDARY` is closed; Sprint 2's path (a) renewal is consumed and not carried past Sprint 9.

### Verification
- Unit tests for schema, signature, device fingerprint, package fingerprint, revision handling, and runtime mode mapping.
- Integration tests for import success/failure, permission denial, activation re-verification, read-only restrictions, and license audit events.
- Integration test for `Dobby.Application.Printing/IPrintTargetClassifier` proving physical print targets are allowed and virtual/file-output or unknown targets deny fail-closed in `ReadOnlyExpired` enforcement.
- Architecture dependency verification proves the `Dobby.Infrastructure → Dobby.Application` edge and `IsTemporaryPrintBoundaryEdge` exception are removed.
- Tests proving license import remains available in read-only expired mode.

### Risks / Blockers
- Requires protected storage and stable device identity from Sprint 4.
- BranchSetup may read license as setup proof only in future Branch Shared scope; it is not a P1 host runtime feature.

### Exit Condition
Sprint finishes when installed plugins receive correct runtime modes, all license-gated operations are host-enforced, and the Sprint 1 print-boundary blocker has been closed rather than renewed again.

## Sprint 10 — Plugins Manager, Sample Plugin, and Admin Artifact Tools

### Goal
Complete P1 plugin management workflows and the artifacts used to prove package, license, recovery, and plugin behavior.

### Scope
- Plugins manager UI and operation flow.
- Install, enable, disable, uninstall, pending deletion/restart, and fault states.
- Plugin details and status vocabulary.
- Sample plugin as dev/QA verification artifact.
- Admin executable CLI wrappers for signing, license generation, optional verification, and recovery response.
- Dev/QA trust exclusion in production.

### Subsections / Task Groups
- **Plugins Manager**
  - Concrete tasks: implement list/details/install/enable/disable/uninstall/status/fault handling, permission target `plugins.management`, user-facing rejection reasons, and lifecycle audit.
  - Expected result: local signed plugins can be managed through the documented UI.
- **Sample Plugin**
  - Concrete tasks: complete/finalize the existing dev/QA signed sample plugin scaffold covering manifest, extension hosting, Main Content page, operation target denial, Result boundary, fault behavior, progress, and work session behavior.
  - Expected result: sample plugin validates host plugin surfaces and is not a production feature.
- **Admin Artifact Tools**
  - Concrete tasks: provide scripted flows for `sign-plugin-package`, `create-plugin-license`, optional `verify-plugin-package`, and `create-recovery-response` through documented Admin boundaries; production recovery response generation follows ADR-0033 vendor-side signing custody and is not treated as dev/QA or Sprint 5 TEST-ONLY fixture material.
  - Expected result: operator flows can generate artifacts needed for P1 tests, including Admin-generated production signed `.dobbyrecovery` evidence for AC-007.
- **Trust Profile Guard**
  - Concrete tasks: ensure dev/QA `dobby.dev-qa` artifacts work only in dev/QA profile and are rejected by production builds.
  - Expected result: release evidence proves production cannot accept dev/QA trust.

### Dependencies
- Sprints 5, 8-9.
- ADR-0023, ADR-0033, ADR-0045.
- `docs/specs/admin-tool.md`.
- `docs/specs/plugins-manager.md`.
- `docs/specs/sample-plugin.md`.
- Sprint 5 recovery request/import contract and the Sprint 5 TEST-ONLY fixture evidence boundary, plus the Sprint 11 production handover requirement.

### Acceptance Criteria
- Primary AC ownership: AC-031, AC-036.
- Admin artifact tooling owns production/Admin-generated AC-007 recovery-response evidence and supports evidence for AC-018 and AC-034; Sprint 5 remains the host-side AC-007 owner and uses only the approved TEST-ONLY fixture authority for Sprint 5 closure.
- Plugin update, repair, downgrade, marketplace/catalog, and production sample-plugin feature behavior stay out of P1.
- Admin tool exact option names are not invented before approval.

### Verification
- Smoke tests installing and running the sample plugin.
- Integration tests for plugin lifecycle state changes and lifecycle audit.
- Tool tests for artifact generation and exit behavior after option names are approved, including `create-recovery-response` producing a production/Admin-signed `.dobbyrecovery` from documented request input.
- Production build check rejects dev/QA packages and licenses.

### Risks / Blockers
- Exact Admin tool option names are blocked pending human/tooling decision.
- Full Admin UI is P1-if-time/P2 and must not block P1 scripted flows.
- Dev/QA signing material applies to sample plugin and smoke license artifacts only; production recovery response generation uses vendor-side signing material governed by ADR-0033 and replaces the Sprint 5 TEST-ONLY fixture path at the Sprint 11 release gate.

### Exit Condition
Sprint finishes when plugin management is usable, sample plugin proves host contracts, and required operator artifacts can be generated for release testing.

## Sprint 11 — Release Gates, Installer, Evidence, and Smoke

### Goal
Prove P1 release readiness against documented acceptance criteria and release procedures.

### Scope
- Unit, integration, and smoke evidence split.
- Installer and upgrade behavior.
- Portable artifact support/dev-only behavior.
- Performance baseline.
- Accessibility and UI snapshot gates.
- Security/static scans.
- Release evidence artifacts.

### Subsections / Task Groups
- **Build and CI Gate**
  - Concrete tasks: run documented restore, build, test, format, architecture graph, UTF-8/encoding, plugin-boundary, and dev/QA trust exclusion checks.
  - Expected result: automated evidence covers core build and static release blockers.
- **Installer and Upgrade**
  - Concrete tasks: verify MSIX primary installer, MSI fallback if required, app data preservation, upgrade behavior, and portable support/dev-only constraints.
  - Expected result: release distribution matches installer policy.
- **Smoke and Acceptance**
  - Concrete tasks: run login-first, Owner bootstrap, user management, shell, plugin install/activation, license import/mode, work session, logout/lock, diagnostics/export, sample plugin smoke flows, and AC-046 UI dispatcher heartbeat smoke for long-running plugin install, license import, and storage operations.
  - Expected result: P1 release blockers have executable evidence and no documented long-running flow blocks the UI thread.
- **Performance and UI Evidence**
  - Concrete tasks: measure startup on ADR-0042 baseline hardware profile, collect P95 evidence, capture shell snapshots, verify responsiveness/accessibility and DPI/resolution behavior.
  - Expected result: performance and UI release gates are documented.
- **Release Evidence Package**
  - Concrete tasks: gather evidence under documented artifacts paths, including storage proof, KDF evidence, Sprint 5 TEST-ONLY recovery fixture closure, production vendor key handover/recovery-evidence replacement, smoke logs, screenshots, static-scan results, and installer verification.
  - Expected result: release decision can be made from local evidence without telemetry.

### Dependencies
- Sprints 1-10.
- ADR-0042, ADR-0047.
- `docs/07_ACCEPTANCE_CRITERIA.md`.
- `docs/09_BUILD_RUN_AND_RELEASE.md`.
- `docs/_perf/README.md`.

### Acceptance Criteria
- Primary AC ownership: AC-008, AC-014, AC-024, AC-041, AC-046, AC-047.
- AC-008 primary ownership covers release smoke for the no-mandatory-internet login, shell, and local plugin-host foundation behavior contributed by Sprints 5, 7, and 8.
- AC-051 contribution covers ADR-0042 baseline-latency evidence used to justify PBKDF2 fallback and completes release-candidate `auth-kdf-selection.md` evidence.
- All P1-release-gate acceptance criteria applicable to implemented P1 scope are passing.
- Sprint 11 consolidates evidence from all primary AC-owning sprints without taking ownership away from those sprints.
- Non-blocking P1 and P2/future work is clearly separated.
- Initial skeleton exclusions remain excluded unless separately approved.
- Branch Shared/Tier-B requirements remain documentation-only with no runtime code.

### Verification
- Documented command sequence succeeds.
- Unit/integration/smoke test evidence is collected.
- AC-008 release smoke proves no mandatory internet dependency for login, shell, and local plugin-host foundation by consolidating Sprint 5, Sprint 7, and Sprint 8 evidence.
- AC-007 evidence includes the Sprint 5 host-side TEST-ONLY recovery checks, the Sprint 10 Admin-generated signed `.dobbyrecovery` path, and Sprint 11 production vendor key handover evidence replacing the Sprint 5 TEST-ONLY fixture path for release-candidate validation.
- AC-046 smoke instrumentation proves UI dispatcher heartbeat continues during plugin install, license import, and storage operations.
- UI screenshots and performance measurements are stored as release evidence.
- Installer/upgrade checks pass.
- Static scans show no forbidden references, trust leaks, telemetry, or unprotected storage.

### Risks / Blockers
- Performance baseline artifacts are future until the Phase 1 measurement process exists and is accepted.
- Native encrypted SQLite proof and KDF timing evidence cannot pass without approved provider/package decisions.

### Exit Condition
Sprint finishes when release evidence is complete enough for a P1 release-gate review or the remaining gaps are explicitly blocked by documented human decisions.

# 4. Cross-Sprint Dependencies

- Sprint 1 verifies the existing skeleton against documented product scope, FR/NFR phase boundaries, architecture/build boundaries, and current pin files against the human-approved pin list; package-bearing work is blocked if approval evidence is absent.
- Sprint 2 provides the shared API vocabulary used by security, shell, plugins, licensing, and sample plugin work.
- Sprint 3 provides configuration, diagnostics, audit, diagnostics export package/redaction boundaries, and runtime folder foundations for all later host behavior; Sprint 7 provides the user/Owner diagnostics export action surface.
- Sprint 4 is the major release-blocking dependency for durable auth, sessions, permissions, licenses, plugin state, audit retention, and release storage proof.
- Sprint 5 depends on protected persistence and provides host-side AC-007 recovery request/import behavior plus AC-008 offline login/auth evidence using the approved TEST-ONLY fixture authority ledger; production/Admin-generated AC-007 recovery evidence remains dependent on Sprint 10/11.
- Sprint 6 depends on auth/session state and protects Settings, User Management, Plugins, License, and operation surfaces; plugin-declared target evidence depends on Sprint 8 manifest seeding.
- Sprint 7 depends on auth and authorization, owns rendered AC-035 shell/Settings evidence and the AC-033 diagnostics export action surface, and later hosts plugin navigation from Sprint 8.
- Sprint 8 depends on shared contracts, shell route/page hosting, and post-login activation rules; it seeds plugin `operationTargets` into the Sprint 6 permission registry before activation.
- Sprint 9 depends on plugin package state, authorization, protected device identity, and durable license storage.
- Sprint 10 depends on Sprint 5 recovery request/import contracts, plugin runtime, and licensing to validate lifecycle and artifact tooling, and provides Admin-generated production AC-007 recovery-response evidence.
- Sprint 11 depends on all P1 implementation sprints and produces release evidence.

# 5. Blocked or Decision-Dependent Work

- Current `global.json`, `Directory.Packages.props`, and existing package references must be verified against a human-approved pin list before package-bearing files are changed; if no approval record exists, package-bearing work is blocked.
- Any package/version not present in the approved pin list requires human-approved input before package-bearing files are changed.
- Host-internal provider-neutral storage abstraction names, owner project/namespace placements, method surfaces, Result behavior, and error-code surfaces require human/architecture-approved input.
- ADR-0054 persistence materialization requires accepted encrypted SQLite provider/package/tooling proof before P1 release.
- Sprint 5 host-side Owner recovery import evidence uses the approved TEST-ONLY fixture authority in `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`; production vendor key custody and production/Admin-generated recovery-response evidence remain Sprint 10/11 release gates.
- Exact Admin scripted tool option names are blocked pending human/tooling decision.
- Full Admin UI is P1-if-time/P2; P1 critical path uses scripted artifact flows only.
- Home/favorites polish, expanded shared UI controls, richer process reporting, and full audit viewer are not P1-core unless explicitly pulled in.
- Backup/restore/migration, shared data tools, plugin update/repair/downgrade, marketplace/catalog, host update automation, runtime language switching, owner transfer, out-of-process plugin worker, and extended retention tooling are P2/P3 or future.
- Branch Shared, BranchSetup, PostgreSQL runtime adapters, Npgsql, live PostgreSQL tests, shared credentials, device lifecycle, and Branch Shared schemas are Tier-B/docs-only until separately approved.
- Production must reject dev/QA trust artifacts; release cannot proceed if dev/QA trust is accepted in production.

# 6. Final Readiness Checklist

- [ ] P1-core scope is separated from P1-if-time, P2/P3, and Branch Shared/Tier-B scope.
- [ ] Human-decision gates are explicit and treated as stop conditions.
- [ ] Sprint order respects login-first, offline-first, local data, and post-login plugin activation requirements.
- [ ] Storage/data protection gates are not bypassed by downstream feature work.
- [ ] Plugin-facing contracts do not expose host internals, WPF types, storage topology, secrets, or license entitlement details.
- [ ] Security floors, signing verification, KDF floors, protected storage, audit, and diagnostics boundaries are release blockers.
- [ ] Settings, User Management, Plugins, License, shell, work sessions, and sample plugin work map to documented acceptance hooks.
- [ ] Release verification includes unit, integration, smoke, static, UI, performance, installer, storage proof, KDF, and trust-exclusion evidence.
- [ ] Future implementation agents can start with Sprint 1 and stop at documented decision gates without inventing requirements.
