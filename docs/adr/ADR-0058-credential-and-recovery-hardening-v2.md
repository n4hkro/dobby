# ADR-0058: Credential and Recovery Hardening v2

**Status:** Accepted

**Date:** 2026-08-21

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
- Import verifies both signatures, purpose/key version, expiry, exact binding,
  and one-time nonce consumption before an atomic Owner credential reset.
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
- This ADR supersedes the saved-credential semantics in ADR-0038 and ADR-0055,
  the fuzzy hardware-quorum binding in ADR-0039, and any acceptance/sprint text
  that permits self-service password change without current-password proof.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Authentication/Security and Hardware Identity rows
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — FR-001, FR-004, FR-021, FR-024
- `docs/07_ACCEPTANCE_CRITERIA.md` — AC-001, AC-007, AC-025, AC-029, AC-051
- `docs/specs/session-lifecycle.md`
- `docs/specs/user-management.md`
- `docs/adr/ADR-0038-local-first-authentication.md`
- `docs/adr/ADR-0039-owner-password-recovery.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- NIST SP 800-63B: https://pages.nist.gov/800-63-4/sp800-63b.html
- Microsoft DPAPI scope reference: https://learn.microsoft.com/dotnet/api/system.security.cryptography.dataprotectionscope
