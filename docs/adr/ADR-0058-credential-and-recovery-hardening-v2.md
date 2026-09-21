# ADR-0058: Credential and Recovery Hardening v2

**Status:** Accepted (decision unchanged; § `Owner recovery v2` precision-hardened 2026-08-27 and supersession header recorded 2026-08-29 — see ## Revision History)

**Date:** 2026-08-21

**Clarified by:** `ADR-0063` — § `Support identity` device-code derivation and the
separation between license device binding and recovery device binding.

**Supersedes:** `ADR-0039` (Owner password recovery — fully superseded)

**Amends:** `ADR-0031` (remembered-login material only; session lifecycle, lock and
password-change decisions untouched); `ADR-0038` (credential storage, throttle);
`ADR-0040` (Row 88 hardware identity purpose and recovery proof material: hardware
properties are support-safe risk signals, not recovery binding authority); `ADR-0055`
(remembered-login, device recovery key pair, throttle ledger scope); `ADR-0057`
(Branch Shared credential and throttling details only; the mode-aware authentication
boundary decision is untouched); `ADR-0041` (§ 1 pre-login allowed set only: the
duplicate `startup-remember-me.json` DPAPI snapshot is removed, not merely unused).

Each amended ADR remains `Accepted` and remains the authority for its untouched sections;
only the named areas are amended here. This is deliberately *not* `**Supersedes:**`, which
a header-scanning reader and `AdrSupersessionConsistencyTests` both read as a claim that
the named document is retired.

## Context

The current implementation stores a reversible DPAPI-protected password in two
locations, can replay it after one click, performs uncapped credential warm-up
verification without recording throttle failures, and keeps throttle state only
in process memory. Self-service password change does not verify the current
password. Owner recovery binds to a fuzzy low-entropy hardware fingerprint, and
the support code exposes a prefix of raw `MachineGuid`.

DPAPI is a storage protection mechanism, not proof that replayable passwords are
safe to retain. `CurrentUser` data can be unprotected by code running as the
same Windows user. Authentication state therefore needs an explicit credential
and rate-limit model rather than reliance on masking or UI wording.

## Decision

### Remembered login

- P1 remember-me stores only the normalized username and a non-secret slot
  identifier. It never stores, snapshots, reconstructs, or replays a password.
- The password box starts empty after process restart. Masked placeholder text
  must not stand in for a retained password.
- Any future one-click device login is a separate authenticator feature. It must
  use a random, revocable, purpose-bound device credential and must be described
  as authentication, not as password masking.
- Existing saved-password records and the duplicate startup snapshot are removed
  through an idempotent migration that overwrites/deletes the secret payload.

### Credential verification and throttling

- Login and unlock throttling is durable, uses `TimeProvider`, survives process
  restart, and is keyed at least by normalized account plus a device/global
  pressure bucket.
- Unknown and known accounts follow indistinguishable user-facing timing and
  error behavior. An account-specific success resets only the appropriate
  account/authenticator counters, not unrelated accounts.
- Password verification has one bounded owner. UI text changes do not start
  unlimited PBKDF work. A warm-up, if retained, is cancellable, concurrency
  bounded, one-shot, short-lived, and participates in the same throttle ledger.
- KDF work, throttle decisions, and tests use deterministic clock and scheduler
  seams. Timing sleeps are not acceptance evidence.

### Password change

- Self-service password change requires the current password (or an explicitly
  approved equivalent reauthentication) and a new-password confirmation.
- Admin reset and Owner recovery remain separate flows and do not require the
  old password.
- Credential replacement, session-version change, remembered-login cleanup,
  session disposition, and audit append commit as one transaction or recover
  through a durable outbox/repair protocol. Partial success is forbidden.

### Owner recovery v2

- The host creates a random device recovery key pair during protected-material
  bootstrap. The private key is non-exported from the application boundary and
  is DPAPI-protected with restrictive ACLs; the public key and a versioned key id
  are non-secret.
- A recovery request contains a random nonce, request id, expiry, device recovery
  public-key hash, protocol version, and host/customer context required by the
  vendor. The device signs the canonical request.
- The vendor response is signed by a purpose-specific recovery signing key and
  is bound to the exact request id, nonce, expiry, and device public-key hash.
  The signing key is selected by the canonical `keyId` string; the retired
  numeric `keyVersion` selector is not used (`ADR-0062`).
- The host keeps a durable pending-request ledger, and that ledger is hash-only:
  the raw nonce is never persisted, logged, or audited. The stored selector is a
  SHA-256 nonce hash, and both the nonce hash and the accepted response id are
  globally unique across the ledger, so a response that is individually
  well-formed and correctly bound is still refused when its response id was
  already recorded for any other request.
- Import verifies both signatures, purpose, `keyId`, expiry, and binding, then
  consumes the request exactly once before an atomic Owner credential reset.
  Binding is checked by recomputing the payload hash from the received artifact
  and comparing it against the pending row, so a tampered persisted row fails
  closed rather than authorising a reset.
- Atomicity alone does not satisfy this decision: the request match is re-run
  inside the mutation transaction, and the burn is a conditional single-row
  update that requires the row to still be unconsumed. Two challenges that both
  passed verification while the same request was pending must not both confirm.
  The row-level ledger enumeration is owned by
  `docs/specs/storage-and-data-protection.md`; the pre-auth reachability of this
  surface is owned by `ADR-0061`.
- Hardware properties may be collected only as support-safe risk signals. They
  are not authentication factors, quorum hashes, encryption keys, or recovery
  binding authority.

### Support identity

- Raw `MachineGuid`, raw stable device id, raw recovery public key, and full
  license fingerprint are never displayed, logged, audited, or exported.
- A support code is a domain-separated keyed digest of the protected stable
  device id, truncated for display and explicitly versioned.

## Consequences

- Saved-password convenience is intentionally removed; the user re-enters the
  password after restart.
- Recovery request/response wire formats require a version bump and compatibility
  policy. Existing test-only v1 fixtures remain test provenance, not production
  authority.
- Durable throttle state adds a small pre-login persistence surface and must be
  included in backup/repair and retention rules.
- This ADR fully supersedes ADR-0039, including its fuzzy hardware-quorum binding.
  It amends the saved-credential semantics in ADR-0038 and ADR-0055; both remain
  `Accepted` and remain the authority for their untouched sections. Any
  acceptance/sprint text that permits self-service password change without
  current-password proof is overridden. The vocabulary here matches the `**Supersedes:**` /
  `**Amends:**` headers above (`docs/adr/README.md` § `Supersession header
  formatı`): only a document whose `Status` is `Superseded` is described as
  superseded.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Authentication/Security and Hardware Identity rows
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-001, FR-004, FR-021, FR-024
- `docs/07_ACCEPTANCE_CRITERIA.md` — AC-001, AC-007, AC-025, AC-029, AC-051
- `docs/specs/session-lifecycle.md`
- `docs/specs/user-management.md`
- `docs/specs/storage-and-data-protection.md` — pre-login closed table set and the
  recovery/throttle ledger row definitions
- `docs/adr/ADR-0038-local-first-authentication.md`
- `docs/adr/ADR-0039-owner-password-recovery.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — pre-auth
  allow-list and atomic mutation boundary
- `docs/adr/ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md` — recovery
  signing purpose and `keyId` selector
- NIST SP 800-63B: https://pages.nist.gov/800-63-4/sp800-63b.html
- Microsoft DPAPI scope reference: https://learn.microsoft.com/dotnet/api/system.security.cryptography.dataprotectionscope

## Revision History

- **2026-08-27:** Precision hardening of § `Owner recovery v2`, not a decision
  change. The original text said "purpose/key version" (the numeric `keyVersion`
  selector retired by `ADR-0062`), and closed the replay requirement with the two
  phrases "one-time nonce consumption" and "atomic Owner credential reset". Both
  were satisfiable by an implementation that persists the raw nonce, trusts the
  persisted binding fields without re-deriving them, verifies the match before
  opening the transaction, and re-checks nothing during the burn — i.e. by a
  TOCTOU-vulnerable flow. The section now states the hash-only ledger invariant,
  global uniqueness of nonce hash and response id, reconstruct-and-compare
  binding, in-transaction re-match and the conditional single-row burn, and
  delegates row-level enumeration to the storage spec. Every added sentence
  matches the enforced behaviour at HEAD, so no earlier decision is reversed and
  no successor ADR is created.

- **2026-08-29:** Header-block supersession recorded, no decision change. This ADR
  already declared under `## Consequences` that it supersedes the fuzzy
  hardware-quorum binding in `ADR-0039` and amends the saved-credential semantics
  of `ADR-0038` and `ADR-0055`, and `ADR-0039` already carried
  `**Status:** Superseded by ADR-0058`. The claim was invisible to a header read,
  so the reverse direction of the supersession graph could not be verified without
  reading prose in every candidate successor. The `**Supersedes:**` header now
  states it. Nothing in the decision, scope or enforced behaviour changes; the
  convention itself is written down in `docs/adr/README.md`
  § `Supersession header formatı` and enforced by
  `AdrSupersessionConsistencyTests`.
