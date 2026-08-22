# Dobby Completion Master Execution Plan

**Plan date:** 2026-08-21  
**Implementation baseline:** `f87ab515b82df1fb605fb7a52a088bc06954bc7e`  
**Repository cutoff preserved:** all work through 2026-08-16; later local work was removed  
**Execution status (2026-08-22):** Phases 0–3 and repository-owned Phase 4
implementation are complete; GA remains externally blocked by the installed
MSIX upgrade run, production signing/distribution inputs and owner license review.

## 1. Objective

Bring the Standalone Dobby product from its current partially implemented state
to a security-correct, behavior-verified, customer-release-ready state. This
file is the execution index. The companion plans contain file-level tasks and
acceptance evidence that another AI can follow without reinterpreting the audit.

Branch Shared is intentionally isolated as a future track. Do not add Npgsql,
`Dobby.BranchSetup`, shared-authority runtime code or Branch Shared release work
while executing the Standalone plan unless the owner explicitly reopens it.

## 2. Controlling Inputs

Read these before editing code:

1. `docs/00_DOCUMENT_AUTHORITY_AND_AUDIT_BASELINE.md`
2. `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md`
3. `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md`
4. `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md`
5. `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md`
6. the 2026-08 amendment in each of `docs/01_...md` through `docs/09_...md`
7. `docs/implementation-plan/01_TRACEABILITY_AND_FINDINGS.md`
8. the workstream plan for the task being executed

Historical sprint/superpowers plans and
`PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md` are evidence only. They do not
override the inputs above.

## 3. Execution Rules

For every task:

1. Reproduce or encode the current defect with a behavioral test first. A
   source-text assertion is acceptable only for an architecture dependency rule.
2. Make the smallest coherent production change that satisfies the controlling
   ADR/spec and all negative cases.
3. Run the focused tests named by the workstream, then the affected project
   tests, then the repository gate for that phase.
4. Update current documentation, schemas, fixtures, localization and release
   evidence in the same task when the public behavior changes.
5. Never hide an expected failure in a broad `catch`; return a typed safe result
   and preserve a correlation ID for diagnostics.
6. Never claim atomic behavior for sequential independent writes. Use the
   provider transaction/outbox boundary and inject failure at every write step.
7. Keep the WPF dispatcher responsive. Heavy file, archive, database, hash and
   signature work must be cancellable and performed off the dispatcher through
   a lifetime-owned asynchronous application operation.
8. Preserve unrelated user changes. Do not rewrite history or push remotely
   without explicit owner authorization.

## 4. Workstream Index

| Plan | Scope | Primary outcome |
|---|---|---|
| `01_TRACEABILITY_AND_FINDINGS.md` | audit register | one authoritative classification and closure map |
| `02_IDENTITY_STARTUP_AND_AUTHORIZATION.md` | login, sessions, recovery, users | no replayable saved password; strict login-first; durable throttling; correct recovery |
| `03_PERSISTENCE_ATOMICITY_AND_DIAGNOSTICS.md` | transactions, audit, diagnostics, recovery | atomic mutations, safe failures, crash consistency, host diagnostics availability |
| `04_PLUGIN_PLATFORM_PACKAGING_AND_UI.md` | plugin trust, packages, runtime, SDK/UI | bounded safe packages and an honest trusted-plugin contract |
| `05_LICENSING_PROVISIONING_AND_RUNTIME_GATES.md` | license v3 and enforcement | derived lifecycle, atomic LKG import and complete existing-surface gates |
| `06_SHELL_ACCESSIBILITY_AND_RESPONSIVENESS.md` | WPF shell and UX | responsive, localized, accessible, maintainable UI flows |
| `07_RELEASE_TOOLS_ARTIFACTS_AND_EVIDENCE.md` | tools, sample, portable, MSIX | reproducible customer artifacts and executed release proof |
| `08_BRANCH_SHARED_FUTURE_TRACK.md` | deferred design/readiness | explicit non-blocking future backlog, no accidental scope expansion |

## 5. Ordered Phases

### Phase 0 — Contract and test-fixture freeze

Execute before production refactors:

- `ID-01`, `PL-01`, `LC-01`, `PA-01`
- add versioned successor schemas for plugin manifest v2, plugin license v3,
  recovery request/response v2 and setup authorization v1;
- add fixture builders that produce both valid successor artifacts and bounded
  negative corpora;
- add an implementation status generator/check so the traceability document
  cannot silently become stale.

**Exit gate:** schemas validate all committed positive fixtures and reject every
committed negative fixture; old artifacts have an explicit accept/migrate/reject
matrix; no production behavior has silently changed.

### Phase 1 — Security and correctness blockers

Execute in this dependency order:

1. `PA-02` transaction/outbox abstraction and failure injection
2. `ID-02` remove password persistence and migrate legacy records
3. `ID-03` durable account/device throttle and bounded verification
4. `ID-04` strict pre-auth/authenticated runtime scopes
5. `ID-05` password change, managed-user and session atomicity
6. `ID-06` recovery-key protocol and support-safe device code
7. `PL-02` package resource/path safety
8. `PL-03` purpose-separated signature/key profile
9. `LC-02` license-v3 parser, bounds and derived lifecycle
10. `LC-03` atomic import/LKG/outbox
11. `PA-03` safe error/audit redaction

**Exit gate:** all Phase 1 focused tests, unit tests and integration tests pass;
restart, replay, malformed-state, archive-bomb and injected-commit-failure suites
are executable behavior tests; no password-equivalent record remains under the
canonical or legacy local roots.

### Phase 2 — Platform contract completion

Execute:

- `PL-04` crash-consistent plugin install/recovery
- `PL-05` host/API compatibility and async lifecycle contract
- `PL-06` declarative/localizable component UI
- `PL-07` correct fault attribution and runtime lifetime
- `LC-04` complete existing runtime gates with host-diagnostics exemption
- `PA-04` retention/recovery lifecycle correction

**Exit gate:** a real signed sample candidate installs, activates, renders,
executes, cancels, disables, recovers after injected crashes and uninstalls; all
expected rejections leave runtime-fault counters unchanged; license state
transitions are derived and deterministic under trusted-time scenarios.

### Phase 3 — Product and UX completion

Execute:

- `UI-01` move orchestration out of oversized code-behind/view model
- `UI-02` asynchronous cancellable install/import/action UX
- `UI-03` localization, support contact, accessibility and focus
- `UI-04` end-to-end shell/session/plugin-manager scenarios
- `RL-01` production-complete sample plugin
- `RL-02` deterministic signing/license/recovery/verification tools

**Exit gate:** WPF behavior tests cover user-visible scenarios without timing
guesses, dispatcher probes remain responsive during large valid packages, all
three cultures have parity, and no customer-visible placeholder remains.

### Phase 4 — Release candidate and GA evidence

Execute:

- `RL-03` portable support-only constraints and refreshed portable build
- `RL-04` MSIX identity, installed launch, upgrade and data preservation
- `RL-05` production signing, SBOM/security/dependency evidence
- `RL-06` consolidated release runner and evidence manifest

**Exit gate:** every command in Section 6 succeeds against candidate bits; the
evidence manifest points to hashes/logs/results for the exact artifacts; MSIX is
actually installed/launched/upgraded and portable support constraints are
enforced, not merely documented.

## 6. Repository Gates

Use PowerShell from the repository root. Do not add `--no-restore` unless the
required assets already exist; when using it, record asset provenance.

```powershell
dotnet format .\Dobby.sln --verify-no-changes --no-restore
dotnet build .\Dobby.sln -c Release --no-restore
dotnet test .\Dobby.sln -c Release --no-restore
.\tools\publish-portable\publish-portable.ps1 -NoRestore
```

Phase-specific security/release runners must also pass:

```powershell
.\tools\verify-plugin-package\verify-plugin-package.ps1 --input-path <sample-package> --trust-profile <profile> --host-version <version> --public-key-path <public-key>
.\tools\verify-security-regressions\verify-security-regressions.ps1 -NoBuild
.\tools\verify-release-candidate\verify-release-candidate.ps1 -NoRestore -EvidenceRoot <new-empty-directory>
```

The runners aggregate commands and evidence; they do not replace assertions.
`verify-release-candidate -SourceOnly` records missing owner/environment inputs
as blockers and cannot emit a release-ready result.

## 7. Definition of Done

The Standalone plan is complete only when all are true:

- every finding in the traceability register has an implementation commit or an
  explicit owner-approved deferral;
- no Critical/High finding remains open;
- full Release tests pass without known flakes or hidden retries;
- test counts and evidence files are recorded for the exact candidate commit;
- a refreshed portable executable is reported;
- MSIX clean-install, installed-launch, upgrade and data-preservation evidence is
  present for the exact package hashes;
- sample and admin artifacts use non-placeholder hashes/signatures and the
  verifier independently accepts them;
- docs, schemas, code, tests and evidence agree on the implemented status;
- Branch Shared remains isolated unless separately approved.

## 8. Stop Conditions

Stop and ask the owner instead of guessing if execution requires:

- production private signing keys or certificate issuance;
- a customer/support email, legal text or distribution identity not present in
  repository configuration;
- acceptance of an incompatible wire-format migration policy;
- opening Branch Shared implementation;
- changing the product trust promise to allow hostile/untrusted plugins;
- destructive migration of real user data without a rehearsed rollback.

Absence of these external inputs does not block implementation and testing with
clearly marked test-only keys/fixtures; it blocks only the affected production
evidence gate.
