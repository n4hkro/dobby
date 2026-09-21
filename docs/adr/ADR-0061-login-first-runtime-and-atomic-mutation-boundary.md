# ADR-0061: Login-First Runtime and Atomic Mutation Boundary

**Status:** Accepted

**Date:** 2026-08-21

**Amends:** `ADR-0011` (§ 4 test-evidence strength: static/dependency scans enforce shape
but are not runtime containment or release-readiness proof), `ADR-0019` (audit mutation
atomicity, diagnostics export availability), `ADR-0041` (pre-auth allowed/forbidden
closed sets, post-auth boundary point, mutation atomicity) and `ADR-0008` (§ 1
login-visible cached plugin-metadata window, which the closed pre-auth allow-list in
§ 1 of this ADR does not contain, and § 2.1 cancellation observation points). Each
ADR named above remains `Accepted` and remains the authority for its untouched sections;
only the named areas are amended here. `ADR-0041` § 1 requires that new pre-login workload
be added only by an amendment of that ADR, which is what this header records. This is
deliberately not `**Supersedes:**`, which a header-scanning reader and
`AdrSupersessionConsistencyTests` both read as a claim that the named document is retired.

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
  queries, login UI, and the Owner recovery request/response surface are allowed.
- The Owner recovery surface is pre-auth by construction (it exists because the
  Owner cannot authenticate), so the allow-list must cover everything one
  recovery attempt needs and nothing more: the pending recovery request ledger
  and burned-nonce ledger, the customer/deployment binding context and device
  identity material read to bind and verify an artifact, the closed pre-auth
  audit event set, and the durable outbox row that the atomicity model below
  requires for a pre-auth mutation. The authoritative row-level enumeration is
  the pre-login closed table set in
  `docs/specs/storage-and-data-protection.md`; that spec and this allow-list are
  amended together, and the closed set is enforced behaviorally by executing the
  pre-login migration sequence and comparing the tables actually created, not by
  scanning source text.
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
- This ADR strengthens and amends ADR-0011, ADR-0041, ADR-0008 and ADR-0019
  wherever construction/I/O, atomicity, diagnostics access, or proof strength
  conflicts, and overrides historical sprint completion claims that rest on the
  weaker proof. Each amended ADR remains `Accepted` and remains the authority for
  its untouched sections; "partially supersedes" is not a relation
  `docs/adr/README.md` defines, so it is not used here.

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
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md`
- `docs/specs/storage-and-data-protection.md` — pre-login bootstrap closed table set

## Revision History

- **2026-08-27:** Pre-auth allow-list enumeration completed. The original bullet
  omitted the Owner recovery surface, even though this same ADR's atomic-mutation
  section already governs "recovery nonce consumption/reset" and already accepts
  a durable-outbox consistency model — both of which are pre-auth by
  construction, because Owner recovery exists precisely for the case where no
  authentication is possible. The omission made the allow-list internally
  contradictory and left the pre-auth persisted state that recovery requires
  (pending request ledger, burned-nonce ledger, binding context, device identity
  material, closed pre-auth audit set, outbox row) unauthorized by the very
  document the storage spec cites as its gate. This entry completes the
  enumeration and names `docs/specs/storage-and-data-protection.md` as the
  row-level authority; it does not widen what pre-auth code may do beyond what
  this ADR already required, and no decision is reversed, so no successor ADR is
  created.
