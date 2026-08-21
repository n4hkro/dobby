# ADR-0061: Login-First Runtime and Atomic Mutation Boundary

**Status:** Accepted

**Date:** 2026-08-21

## Context

The login window is shown first, but the current preparer starts construction of
the full shell/plugin runtime before authentication and starts audit-retention
maintenance from that construction path. Several multi-step mutations commit
business state, session state, cache/attestation, and audit through independent
writes. Plugin installation performs archive, hash, signature, file, and DB work
synchronously from a WPF click handler. Many release tests verify source strings
or evidence headings rather than runtime behavior.

## Decision

### Strict pre-auth boundary

- Before successful authentication, only the single-instance coordinator,
  minimum theme/localization, username-only remembered-login metadata, protected
  material required to open the credential/throttle store, credential/throttle
  queries, and login UI are allowed.
- Full shell composition, plugin state/cache/staging construction or recovery,
  plugin discovery/verification/activation, license-heavy evaluation, audit
  retention, diagnostics retention, imports/exports, and domain/background jobs
  start only after the authenticated-session commit point.
- Pre-auth allowed work is represented by an executable allow-list and an
  instrumented event ledger. Construction of a service that performs forbidden
  I/O counts as work; merely delaying its public method call is insufficient.
- Authentication success commits the session first, then creates the post-login
  scope. Logout disposes that scope and all plugin services before another user
  can authenticate.

### Atomic business mutations

- A command that changes authoritative state and requires audit has one of two
  approved consistency models:
  1. state, dependent cache/index records, session invalidation/version, and
     audit commit in one database transaction; or
  2. authoritative state plus a durable outbox commit in one transaction, with
     idempotent consumers and repair/replay evidence.
- A method must not publish success until its required transaction/outbox commit
  succeeds. In-memory active state is changed after durable commit or rolled back
  deterministically.
- The rule applies to Owner/user creation, password change/reset, activation
  state, plugin install/uninstall/cache, license import/attestation, recovery
  nonce consumption/reset, and customer/deployment provisioning.

### UI responsiveness and cancellation

- File selection remains on the UI thread; archive reading, hashing, signature
  verification, extraction, DB mutations, storage maintenance, and license
  validation run asynchronously off the dispatcher through cancellable services.
- Operations expose start/progress/cancel/completion handles. Cancellation has a
  deterministic safe point and never leaves a staged artifact authoritative.
- Dispatcher-heartbeat tests synchronize on explicit operation-start and
  operation-complete signals; timing guesses and source-text assertions are not
  acceptance evidence.

### Diagnostics and fault classification

- Host support diagnostics export is a recovery/support function and is never
  blocked by a plugin license state. Only plugin/domain data extraction remains
  license-gated.
- Expected failures (`Forbidden`, validation, license read-only/denied,
  cancellation, not found) are results, not plugin runtime faults. Unexpected
  exceptions produce redacted diagnostics; audit contains only typed categories,
  never raw exception message/stack/payload.

### Verification authority

- Static scans are architecture and wiring guardrails only. Runtime acceptance
  requires behavior tests against production composition and, for release claims,
  the produced portable/installed artifact.
- Evidence files are generated from machine-readable test/artifact outputs and
  include command, commit, configuration/profile, hashes, timestamps, and result.
  A checked heading or hard-coded test count is not completion proof.

## Consequences

- `LoginRuntimePreparer` and `HostLoginRuntimeFactory` must be split into a minimal
  pre-auth scope and a real post-auth scope.
- Coordinators that currently perform independent writes require shared
  transaction executors or an outbox.
- Synchronous WPF handlers for heavy operations require async command services.
- Some currently passing static/evidence tests will be demoted or replaced by
  runtime tests.
- This ADR strengthens and partially supersedes ADR-0041, ADR-0008, ADR-0019,
  and historical sprint completion claims wherever construction/I/O, atomicity,
  diagnostics access, or proof strength conflicts.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Host, Application, Plugin Framework, Audit rows
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-001, FR-011, FR-027
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — NFR-002, NFR-003, NFR-021
- `docs/07_ACCEPTANCE_CRITERIA.md` — AC-001, AC-010, AC-033, AC-046
- `docs/specs/session-lifecycle.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/adr/ADR-0041-login-first-startup-policy.md`
- `docs/adr/ADR-0008-deferred-plugin-activation.md`
