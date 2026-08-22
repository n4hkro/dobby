# Traceability and Findings Register

**Audit date:** 2026-08-21  
**Code baseline:** `f87ab515b82df1fb605fb7a52a088bc06954bc7e`  
**Baseline verification:** Debug solution tests passed, 1170/1170

## 1. Classification

- **Implemented:** production behavior and proportionate executable evidence exist.
- **Implemented-Weak:** the main behavior exists, but resilience, security,
  maintainability or proof is below the controlling requirement.
- **Partial:** meaningful pieces exist, but the end-to-end behavior is incomplete.
- **Incorrect:** code exists but violates the controlling requirement.
- **Unimplemented:** no production implementation was found.
- **Deferred-by-design:** intentionally outside the current Standalone phase.
- **Evidence-only:** build/source evidence exists, but customer behavior has not
  been executed and proven.

Green tests are baseline evidence, not proof that a finding is closed. Several
integration/release tests assert source strings or absence of a call site; those
tests cannot establish runtime security or packaged-install behavior.

## 2. Priority Findings

| ID | Priority | Classification | Finding | Required closure |
|---|---|---|---|---|
| F-001 | Critical | Implemented | Remembered login is username-only; legacy password columns/files are removed idempotently and a restarted process has no password to replay. | Closed by `ID-02`; guarded by remember-account migration, restart and residue tests. |
| F-002 | Critical | Implemented | Pre-auth composition constructs only the login-visible runtime; authenticated shell/plugin/license/retention construction requires a committed session and is lifetime-owned. | Closed by `ID-04`; guarded by strict failed-login/recovery/success/disposal composition probes. |
| F-003 | High | Implemented | Login throttling uses encrypted durable per-account and explicit device buckets with atomic updates, restart persistence and account isolation. | Closed by `ID-03`; guarded by deterministic restart/isolation/concurrency tests. |
| F-004 | High | Implemented | Input-driven credential warm-up was removed; submit performs one verification through a non-queued bounded verifier gate. | Closed by `ID-03`; guarded by input-wiring, exactly-once and capacity tests. |
| F-005 | Critical | Implemented | Recovery v2 now uses a protected P-256 device key, strict possession proof, purpose-separated vendor key, exact stored request binding, expiry/replay checks and atomic credential/session/audit/outbox consumption. `SupportDeviceCodeV2` is a versioned keyed digest with a transcription checksum; fuzzy recovery code was removed. | `RecoveryRequestVerifierTests`, `RecoveryRequestExportTests`, `RecoveryResponseImportConfirmTests`, `SupportDeviceCodeV2Tests`, `ProtectedMaterialAvailabilityProbeTests`. |
| F-006 | High | Implemented | Self-service password change proves the current password through durable throttling, confirms the new password and atomically commits credential/session/remembered-account/audit/outbox state; managed-user and session mutations use the same rollback boundary. | Closed by `ID-05` + `PA-02`; guarded by per-write/commit fault matrices, concurrency and WPF coordinator tests. |
| F-007 | Critical | Implemented | Package ingestion is stream-backed and enforces validated startup/hard limits before and during reads; one shared Windows-safe canonical path policy, exact manifest/file/hash checks and create-new rehashing staging prevent traversal, collision, overwrite and validation-to-install races. | Closed by `PL-02`; guarded by package limit/path/corpus tests and real persistence staging escape/hash-race tests. |
| F-008 | Critical | Implemented | Current authority and product resources define plugins as reviewed, signed, fully trusted in-process code; ALC and compatibility preflight carry no hostile-code sandbox claim. | Closed by `PL-01`; guarded by trust-language and v2-contract architecture tests. |
| F-009 | High | Implemented | Plugin package, plugin license and Owner-recovery artifacts use non-interchangeable purposes, distinct profile-selected public keys/key IDs, bounded versioned verification rings, canonical domain-separated bytes, P1363 signatures and unpadded base64url. | Closed by `PL-03`; guarded by cross-purpose, encoding, rotation/retirement and packaged-resource tests. |
| F-010 | High | Implemented | Plugin installation uses a durable transaction journal and idempotent startup recovery to complete or roll back staging, promotion, persistence and cache projections. Recovery also removes orphaned staging/cache artifacts and incomplete temporary outputs. | Closed by `PL-04` + `PA-04`; guarded by install-state persistence, runtime recovery and startup-order tests. |
| F-011 | High | Implemented | Manifest schema/package/host API compatibility is table-driven; lifecycle v2 and page commands are async, cancellable and host-timeout-owned, while the bounded v1 module adapter emits an upgrade warning. | Closed by `PL-05`; guarded by compatibility, lifecycle ordering/timeout/cleanup, disable/uninstall and command boundary tests. |
| F-012 | High | Implemented | Plugin pages use bounded declarative form/table/list/validation models and opaque command IDs; host WPF rendering resolves three-culture bundles and dispatches commands only after runtime/license/operation policy checks. | Closed by `PL-06`; guarded by contract, renderer, dispatcher, signed-package integration and smoke tests. |
| F-013 | Medium | Implemented | Runtime invocation outcomes distinguish expected validation/policy results, cancellation, timeout, plugin-declared failure, thrown exceptions, contract breaches, and host infrastructure failures. Only documented plugin-defect categories increment the bounded quarantine policy. | Closed by `PL-07`; guarded across activation, route, event-handler and command boundaries, including repeated rejection and quarantine cleanup tests. |
| F-014 | Critical | Implemented | License v3 signs only granted/revoked intent; lifecycle state is derived deterministically from bounded trusted inputs and a signed Pending status cannot become authority. | Closed by `LC-01`/`LC-02`; guarded by v3 contract, payload and lifecycle truth-table tests. |
| F-015 | High | Implemented | License parsing is bounded and checked; revision/LKG state, attestation, audit and refresh outbox commit atomically, with restart-safe idempotent post-commit retry. | Closed by `LC-02`/`LC-03`; guarded by parser, migration and per-write/commit/outbox fault tests. |
| F-016 | High | Implemented | Minimum host support diagnostics is license-state independent in Pending, Active, Grace, ReadOnlyExpired and Denied states, while plugin/domain output remains policy-gated. | Closed by `LC-04`/`PA-03`; guarded by lifecycle-matrix and real export tests. |
| F-017 | Medium | Implemented | Branch setup has a purpose-separated v1 schema and no Standalone handler; `.dobbylicense` remains plugin-license authority only. | Closed by `LC-01`; guarded by successor-schema and admin-command behavior tests. |
| F-018 | High | Implemented | Persistence failures expose stable safe code/category/retryability/correlation only; audit enforces event-specific bounded fields at its boundary; structured diagnostics and support export redact or exclude secrets, paths, SQL, stack data and non-log runtime files. | Closed by `PA-03`; guarded by adversarial audit, diagnostics, export and persistence tests. |
| F-019 | High | Incorrect | Plugin install is synchronous on the WPF dispatcher; some progress is cosmetic. | `UI-02`: genuinely async/cancellable install and dispatcher responsiveness tests. |
| F-020 | Medium | Implemented-Weak | `MainWindow.xaml.cs` (~1759 lines) and `ShellViewModel` (~1512 lines) combine view wiring and application orchestration. | `UI-01`: extract commands/presenters without changing behavior; add WPF cleanup tests. |
| F-021 | High | Partial | Sample plugin contains dummy hash/signature and leaks its work-session lifetime; no independent package verifier exists. | `RL-01`/`RL-02`: valid package, lifecycle proof and verifier tool. |
| F-022 | High | Evidence-only | MSIX is self-signed RC output with placeholders and no executed install/upgrade/data-preservation proof; LocalAppData behavior under package identity is unverified. | `RL-04`/`RL-05`: real installed scenarios and production signing profile. |
| F-023 | Medium | Partial | Portable constraints are documentation-first and candidate output uses production settings without enforceable support-only identity/policy. | `RL-03`: enforce artifact mode and prove launch/scope. |
| F-024 | Medium | Implemented-Weak | About/support data includes `support@example.invalid` and other release placeholders. | `UI-03`/`RL-05`: owner-provided configuration plus release placeholder scan. |
| F-025 | Medium | Implemented | A machine-readable 25-finding/69-row register validates classifications, owners, implemented evidence, row coverage and human-table drift. | Closed by `PA-01`; release evidence still belongs to `RL-06`. |

## 3. Legacy 69-Item Reclassification

This table replaces the status claims in
`PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`. It preserves its stable row numbers.

| # | Current classification | Audit conclusion | Closure owner |
|---:|---|---|---|
| 1 | Implemented | Plugin-visible storage contracts, compartments, host backend and migration exist. | Regression gate |
| 2 | Implemented | Canonical runtime roots exist; staging/diagnostics are nested/owned rather than required as top-level siblings. Old row is stale. | `RL-04` packaged-path proof |
| 3 | Implemented | Durable audit participates in provider-owned mutation transactions; structured event allow-lists and diagnostics redaction are enforced at write/export boundaries. | `PA-02`, `PA-03` regression gate |
| 4 | Implemented | Authenticated maintenance provides cancellable encrypted SQLite online backup, checkpoint/integrity/hash verification, bounded retention and explicit restore with same-device validation, immutable evidence overlay, current-store quarantine and fault-injected rollback. Startup recovery removes only validated incomplete outputs. | `PA-04`; guarded by persistence, retention, strict-login-first and runtime-recovery tests. |
| 5 | Implemented | Login-visible composition remains pre-auth; full authenticated runtime is constructed only after a committed session. | `ID-04` regression gate |
| 6 | Implemented | Display names are trimmed/NFC-normalized, bounded, and reject controls, format/bidi and invalid Unicode while preserving user-facing Unicode. | `ID-01` regression gate |
| 7 | Implemented-Weak | Password validation is minimal. Preserve length/password-manager usability and add offline common-value controls only if product data is available. | `ID-01`, `ID-03` |
| 8 | Implemented | PBKDF2 floor/resolver path works for the current selected algorithm; Argon2 remains allowed by configuration without an implementation and must be reconciled. | `ID-01` |
| 9 | Implemented | Lock window/view model/runtime path exists. | Regression gate |
| 10 | Implemented | UI requires current password plus confirmation; state/audit/outbox commit atomically and current/other session policy is executable. | `ID-05` regression gate |
| 11 | Implemented | Timing instrumentation and executable composition probes prove forbidden runtime work does not start before authentication. | `ID-04` regression gate |
| 12 | Implemented | User Management surface and permission hiding exist. | `ID-05`, `UI-04` hardening |
| 13 | Implemented | Settings navigation tree exists. | Regression gate |
| 14 | Implemented | Appearance settings and persisted toolbar/theme synchronization exist. | Regression gate |
| 15 | Implemented | Account surface exposes the current/new/confirmation flow and delegates to the atomically audited reauthentication coordinator. | `ID-05` regression gate |
| 16 | Implemented | Security/session preferences are rendered and consumed. | Regression gate |
| 17 | Partial | About provider/surface exists and the support device code is now keyed, versioned and checksummed; remaining About/settings UX work stays under `UI-03`. | `UI-03`; `SupportDeviceCodeV2Tests` proves the security portion. |
| 18 | Implemented | Three-culture resource parity exists. | `UI-03` regression |
| 19 | Implemented | Required shell regions render. | `UI-01`, `UI-04` maintainability/e2e |
| 20 | Implemented | Profile/session actions are wired. Diagnostics licensing bug applies. | `LC-04` |
| 21 | Implemented | Route host/fallback paths exist. | Regression gate |
| 22 | Implemented | Sidebar state/persistence exists. | Regression gate |
| 23 | Implemented | Third-bar state and ordering exist. | Regression gate |
| 24 | Implemented | Toolbar ordering/visibility state exists. | Regression gate |
| 25 | Implemented | Main content hosts bounded declarative plugin forms, inputs, lists, tables, validation and commands without accepting plugin WPF controls. | `PL-06` regression |
| 26 | Implemented | Shared host/plugin status and progress surface exists. | `UI-02` real progress proof |
| 27 | Implemented | Notification/feedback surfaces exist. | `UI-03` |
| 28 | Implemented | Minimized work-session strip and restore exist. | `UI-04` |
| 29 | Implemented | Theme settings/toolbar synchronization exists. | Regression gate |
| 30 | Implemented | Localization catalogs/fallback exist. | `PL-06`, `UI-03` |
| 31 | Implemented | Work-session preservation/continuation is wired for current lifecycle flows. | `RL-01` sample closure |
| 32 | Implemented | Host support diagnostics export is executable in every plugin-license lifecycle state and excludes plugin/domain diagnostic files by policy. | `LC-04`, `PA-03` regression gate |
| 33 | Implemented | Registry validates and renders the declarative component contract with culture fallback, accessibility metadata and opaque host-dispatched commands. | `PL-06` regression |
| 34 | Implemented | Plugin routes/pages integrate with Main Content and lifecycle rollback paths. | `PL-05`, `PL-06` regression |
| 35 | Implemented | Plugin progress snapshots share shell reporting. | `UI-02` real cancellation/progress |
| 36 | Implemented | Classified exception boundaries preserve whether a safe error was returned or caused by a thrown plugin exception; expected results remain fault-neutral and repeated defects trigger deterministic quarantine cleanup. | `PL-07`; `PluginFrameworkTests`, `HostPluginCommandDispatcherTests`, `PluginPageRouteCoordinatorTests` |
| 37 | Implemented | Safe persisted/user-facing plugin fault states exist. | `PA-03`, `PL-07` hardening |
| 38 | Implemented | `default/default` context is deliberate current Standalone baseline; real customer provisioning has an explicit Branch Shared deferral. | `BR-01` future |
| 39 | Implemented | License UI and click handler now exist; old status is stale. | `LC-02`/`LC-03` correctness |
| 40 | Implemented | File picker is called by production UI; old status is stale. | `UI-02` responsiveness |
| 41 | Implemented | Runtime mutation gate exists for current surfaces. Future domain surfaces must register at creation. | `LC-04` |
| 42 | Implemented | Plugin storage writes are mode-gated. | `LC-04` negative tests |
| 43 | Implemented | Plugin/background lifecycle start gates exist; absent future owners are not proof of all future behavior. | `LC-04` |
| 44 | Implemented | Package install lifecycle gates exist. | `LC-04`, `UI-02` |
| 45 | Deferred-by-design | No domain import/export/report owner exists yet. Host diagnostics must be exempt. | `LC-04`; gate future owners when introduced |
| 46 | Implemented | Non-physical output is gated at the current print coordinator. | `LC-04` behavioral regression |
| 47 | Implemented | Physical print policy/confirmation is present for the current surface. | `LC-04` behavioral regression |
| 48 | Implemented | License status/rejection text renders; details remain deliberately safe but can be more actionable by stable code. | `LC-02`, `UI-03` |
| 49 | Implemented | Plugins Manager and actions exist; old status is stale. | `UI-01`, `UI-02` |
| 50 | Implemented-Weak | List and lifecycle states exist; trust/license/fault presentation needs contract-v2 alignment. | `PL-05`, `UI-03` |
| 51 | Implemented-Weak | Detail is a summary string rather than structured accessible detail. | `UI-01`, `UI-03` |
| 52 | Incorrect | Install button is wired, but performs synchronous archive/crypto/database work on UI thread. | `UI-02`, `PL-02` |
| 53 | Implemented | Enable action is wired. | `PL-05`, `UI-02` |
| 54 | Implemented | Disable action is wired. | `PL-05`, `UI-04` |
| 55 | Implemented | Uninstall/confirmation action is wired. | `PL-04`, `UI-04` |
| 56 | Implemented-Weak | Rejections reach UI but safe actionable reason mapping is incomplete. | `PL-07`, `UI-03` |
| 57 | Partial | Sample source/manifest/icon exist; hash/signature are placeholders and package is not release-valid. | `RL-01` |
| 58 | Partial | Generic runtime can host it, but no final signed installed sample proof exists. | `RL-01` |
| 59 | Partial | Behaviors exist in source; final UI reachability/progress/denial proof and session close are incomplete. | `RL-01` |
| 60 | Partial | Signer exists but no purpose-separated production profile/evidence. | `RL-02`, `RL-05` |
| 61 | Partial | License generator exists mainly for Active/v2 path and external key fixture. | `RL-02`, `LC-02` |
| 62 | Unimplemented | Independent `verify-plugin-package` command/tool is absent. | `RL-02` |
| 63 | Implemented-Weak | Admin and host use strict recovery v2 with separately provisioned device-public-key possession verification and no silent v1 fallback. Technical tests are complete; published-artifact/operator evidence remains under `RL-02`. | `RL-02`; `AdminCommandHostTests`, `RecoveryResponseImportFixtureTests`. |
| 64 | Partial | Production rejects Dev/QA profile in code; final key/profile/customer artifact proof is absent. | `PL-03`, `RL-05` |
| 65 | Evidence-only | Self-signed RC MSIX builds; no GA identity/certificate/profile. | `RL-04`, `RL-05` |
| 66 | Evidence-only | Two versions can be built; upgrade/rollback has not been installed and executed. | `RL-04` |
| 67 | Evidence-only | Data root exists; packaged upgrade/data preservation is unproven and virtualization must be checked. | `RL-04` |
| 68 | Incorrect | Portable limitation is mainly prose; artifact/runtime constraints are not enforced. | `RL-03` |
| 69 | Evidence-only | Package contains the host, but installed launch over existing data/login-first is unproven. | `RL-04` |

## 4. Deferred Scope

The following are not defects in the current Standalone milestone:

- PostgreSQL/Npgsql runtime adapter;
- `Dobby.BranchSetup` executable;
- multi-device user/permission authority;
- Branch Shared backup/restore/credential-rotation execution;
- organization-shared plugin storage;
- hostile/untrusted third-party plugin execution.

They remain subject to `08_BRANCH_SHARED_FUTURE_TRACK.md` or a new trust-model
ADR. Do not use their absence to lower the completion standard for Standalone.
