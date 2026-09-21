# ADR-0060: License Lifecycle and Import v3

**Status:** Accepted (decision unchanged; v2-reader and `keyId` selector wording reconciled 2026-08-27 and supersession header recorded 2026-08-29 — see ## Revision History)

**Date:** 2026-08-21

**Clarified by:** `ADR-0063` — the canonical derivation of the `deviceKeyHash`
field this ADR requires in the v3 payload. Without this pointer the artifact
authority names a field whose computation is defined nowhere reachable from here.

**Supersedes:** `ADR-0051` (License lifecycle v2 — fully superseded)

**Amends:** `ADR-0057` (license-as-setup-proof sections and the related BranchSetup
specs). `ADR-0057` remains `Accepted` and remains the authority for its
mode-aware authentication boundary; only the setup-proof sections are amended
here. This is deliberately *not* `**Supersedes:**` — see `docs/adr/README.md`
§ `Supersession header formatı`.

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
- Canonical signature format, domain separation, and key purpose separation follow
  ADR-0059; the key selector is the string `keyId` governed by ADR-0062, and the
  retired numeric `keyVersion` is not a selector. License leaf keys are not
  plugin-package or recovery keys.
- P1 ships **no** v2 compatibility reader. The signature block is read first and
  must carry exactly the five properties `purpose`, `algorithm`, `format`,
  `keyId`, `value`; a block that carries a numeric `keyVersion` is a shape
  violation and is rejected before signature verification, so the retired
  selector has no interpretation path. An artifact that survives signature
  verification but whose `schemaVersion` is not `3` is then rejected fail-closed
  before any payload field is consumed, so no v2 payload vocabulary is ever
  interpreted. Adding a v2 reader later is permitted only through an explicit,
  separately approved compatibility path that normalizes to the v3 internal model
  and is covered by fixed retirement dates and negative fixtures. Until such a
  reader exists, no document may describe v2 vocabulary as something the host
  reads. New artifacts use v3 only.

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
  days have a small explicit upper bound. The concrete ceiling is owned by
  `docs/specs/license-import.md` § `Grace ceiling` and
  `docs/schemas/dobby-plugin-license-v3.schema.json` (`0..90` days) and is
  re-applied by the host independently of the signed value.

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
- This ADR fully supersedes ADR-0051 (whose `Status` is `Superseded`). It amends
  the license-as-setup-proof sections of ADR-0057 and the related BranchSetup
  specs; ADR-0057 remains `Accepted` and remains the authority for its mode-aware
  authentication boundary. ADR-0052 customer/deployment context remains active
  except where setup proof was coupled to a plugin license.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Licensing and License/Entitlement rows
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-009, FR-016, FR-017, FR-028
- `docs/07_ACCEPTANCE_CRITERIA.md` — AC-006, AC-020, AC-021, AC-034
- `docs/specs/license-import.md`
- `docs/specs/onboarding-and-provisioning.md`
- `docs/schemas/dobby-plugin-license-v3.schema.json` — v3 artifact schema
- `docs/schemas/license-v3-lifecycle-truth-table.md` — derived state truth table
  and runtime-mode mapping
- `docs/schemas/dobby-plugin-license.schema.json` — frozen v2 schema, retained as
  historical reference and negative-fixture source only; no reader consumes it
- `docs/adr/ADR-0051-license-lifecycle-v2.md` — superseded by this ADR
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — plugin-visible mode
- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — signature
  format, domain separation and key purpose separation
- `docs/adr/ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md` — `keyId`
  selector, purpose-separated leaf keys and rotation rings

## Revision History

- **2026-08-27:** Two factual reconciliations, no decision change. (1) The signing
  bullet delegated "key versioning" to `ADR-0059`; the selector is the string
  `keyId` owned by `ADR-0062` and the numeric `keyVersion` is retired, so the
  delegation now names the correct authority. (2) The v2 bullet said v2 import
  "may be supported only through an explicit compatibility reader", and the
  cross-reference called the frozen v2 schema the reader's schema. Read together
  they implied a reader exists, which `docs/specs/license-import.md`,
  `ADR-0046` and `ADR-0062` had already been corrected to deny. At HEAD there is
  no such reader and two independent gates make one unreachable without new code:
  `LicenseArtifactParser.TryReadSignature` requires the signature block to carry
  exactly `purpose`/`algorithm`/`format`/`keyId`/`value` and rejects a numeric
  `keyVersion` before signature verification, and `LicensePayloadValidator`
  rejects `schemaVersion != 3` with `invalid-license-file` before any payload
  field is consumed. The bullet now states the P1 position positively and keeps
  the future reader as a conditional, retirement-bounded permission. Lifecycle,
  anti-rollback and binding decisions are untouched, so no successor ADR is
  created.

- **2026-08-29:** Header-block supersession recorded, no decision change. The
  `## Consequences` section already stated that this ADR supersedes `ADR-0051` and
  the license-as-setup-proof sections of `ADR-0057`, and `ADR-0051` already carried
  `**Status:** Superseded by ADR-0060`. Because the successor's claim lived only in
  prose, the reverse direction of the supersession graph was unverifiable from the
  header block a reader and an audit scan consult first. The `**Supersedes:**`
  header now states it. Lifecycle, anti-rollback, import-transaction and binding
  decisions are untouched; the convention is recorded in `docs/adr/README.md`
  § `Supersession header formatı` and enforced by
  `AdrSupersessionConsistencyTests`.
