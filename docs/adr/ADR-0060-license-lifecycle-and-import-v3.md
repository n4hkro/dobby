# ADR-0060: License Lifecycle and Import v3

**Status:** Accepted

**Date:** 2026-08-21

## Context

License v2 mixes signed artifact intent with time-derived runtime states. The
signed `status` vocabulary contains `pending` and `readonly`, while the runtime
also derives those states from `notBefore`, `expiresAt`, and grace. Current code
ignores signed `pending` in one path. ADR-0051 also says a mismatched candidate
can move any state to denied, which would let an invalid import destroy a valid
last-known-good license. State, attestation, and audit currently commit through
separate writes. Artifact sizes, identifiers, and grace values are insufficiently
bounded. BranchSetup reuses a normal plugin license as setup proof.

## Decision

### Artifact v3

- A signed license declares entitlement intent: `granted` or `revoked`. Pending,
  active, grace, read-only, and not-yet-valid are derived by the host from signed
  time bounds plus current time; they are not independently signed states.
- Required payload fields remain plugin/customer/deployment/device/package
  bindings, revision, issue/not-before/expiry, bounded grace, key id, and schema
  version. Every string, collection, timestamp range, artifact byte length, and
  numeric value has a documented maximum.
- Canonical signature format, domain separation, key purpose, and key versioning
  follow ADR-0059. License leaf keys are not plugin-package or recovery keys.
- v2 import may be supported only through an explicit compatibility reader that
  normalizes to the v3 internal model and is covered by fixed retirement dates
  and fixtures. New artifacts use v3 only.

### Last-known-good and transactionality

- Parsing, signature, schema, binding, package, revision, and time validation are
  completed before persistent state changes.
- Invalid, mismatched, older/equal, malformed, oversized, or cancelled candidate
  imports are rejected without changing the current last-known-good license,
  runtime mode, plugin activation state, or stored revision.
- A valid newer import commits license state, import attestation, activation
  refresh request, and audit event atomically. If the persistence design cannot
  share a transaction, it uses a durable outbox and idempotent recovery protocol
  that produces the same externally observable result.
- Revocation/denied runtime mode comes only from a currently valid signed
  revocation or from failure to validate the stored last-known-good artifact at
  use time. It does not come from a bad candidate file.
- State parsing is non-throwing and typed. Date arithmetic is checked and grace
  days have a small explicit upper bound.

### Diagnostics and recovery paths

- Host diagnostics export, license import, and support/recovery UI remain
  available when plugin runtime mode is read-only or denied. Plugin/domain data
  export and report generation remain license-gated.
- User/plugin surfaces are non-disclosing. Audit stores typed reason categories;
  raw artifacts, signatures, bindings, exception text, and full paths stay out of
  audit and exported diagnostics.

### BranchSetup authorization

- A normal `.dobbylicense` is not setup authorization.
- Future BranchSetup uses a separate vendor-signed setup authorization artifact
  with its own type, schema, key purpose, branch/deployment binding, request id,
  nonce, expiry, and one-use semantics. Windows administrator status and the
  protected setup credential are additional gates.
- Branch Shared remains deferred-by-design until its runtime phase is explicitly
  opened; this decision only removes the ambiguous proof model.

## Consequences

- `dobby-plugin-license.schema.json` requires a v3 successor or versioned union;
  v2 fixtures cannot be silently reinterpreted.
- Import persistence and audit wiring need a transaction/outbox redesign.
- The runtime-mode evaluator has one deterministic state derivation function and
  exhaustive boundary tests.
- This ADR supersedes ADR-0051. It supersedes the license-as-setup-proof sections
  of ADR-0057 and related BranchSetup specs. ADR-0052 customer/deployment context
  remains active except where setup proof was coupled to a plugin license.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Licensing and License/Entitlement rows
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-009, FR-016, FR-017, FR-028
- `docs/07_ACCEPTANCE_CRITERIA.md` — AC-006, AC-020, AC-021, AC-034
- `docs/specs/license-import.md`
- `docs/specs/onboarding-and-provisioning.md`
- `docs/schemas/dobby-plugin-license.schema.json`
- `docs/adr/ADR-0051-license-lifecycle-v2.md`
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`
- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`
