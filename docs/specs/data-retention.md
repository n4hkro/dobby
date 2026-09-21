# Data Retention Spec

**Status:** Standalone floor implemented; advanced policy is future scope
**Phase:** P1-core floor + P2/P3 extensions

## Purpose

This placeholder records the future ownership boundary for data-retention policy
extensions referenced from `docs/02_ARCHITECTURE_MAP.md`.

The Standalone floors below are `P1-core` (release blocker); configurable
policy, scheduler ownership and legal hold are the `P2`/`P3` extensions.

Standalone keeps the existing retention floors:

- audit retention default minimum: 365 days;
- technical diagnostics retention default: 30 days plus finite total size cap;
- no weakening of write-time redaction or diagnostics export redaction.

## Standalone Enforcement

- Audit cleanup is an authenticated/background maintenance responsibility and
  does not run during pre-auth composition.
- The audit floor is enforced in code: values below 365 days are rejected.
- An audit row whose correlation identifier is referenced by a pending host
  outbox message is retained until delivery completes.
- A pending legacy outbox row without an evidence correlation identifier makes
  the sweep fail closed by preserving all otherwise-expired audit rows.
- Snapshot retention is count-bounded under the host-owned backup root. It runs
  only after a new encrypted snapshot and its metadata have been durably
  promoted, and it removes matching metadata with each expired snapshot.

## Security-State Retention Floor

Durable pre-login security state has its own retention rules and is not swept by
audit or diagnostics cleanup (`ADR-0058`, `docs/specs/storage-and-data-protection.md`):

| State | Retention rule | Prohibition |
|---|---|---|
| Durable login/unlock throttle ledger | A bucket row may be pruned only after its penalty window has expired plus a bounded grace; pruning runs as authenticated maintenance work and never during pre-auth composition | Pruning must not clear or shorten an active penalty, and must not be reachable from an unauthenticated surface |
| Consumed recovery nonces | Retained indefinitely; expiry of the originating artifact does not release the nonce | No sweep, retention policy, backup restore or repair flow may delete or reset consumed-nonce rows |
| Remembered-login slot | Cleared by a successful login with remember-me off, and on account deletion/deactivation; logout and lock do not clear it | Contains no secret; it is never retained as an authentication factor |
| Device identity and Owner recovery key material | Retained for the device lifetime; replacement happens through explicit rotation with a new versioned key id | Retention cleanup never deletes protected key material as if it were expired data |
| Hardware-derived device identifiers (`MachineGuid`, protected stable device id) | Retained for the install lifetime as license-binding and recovery material, not as history: no dated series, no per-session snapshot and no audit copy of the raw values is kept | Collected for license device binding and Owner recovery only. They are device-identifying data, so raw values are never exported, logged, shown in diagnostics or included in a support bundle (`ADR-0063` § 3); adding a further collected hardware property, or a second purpose for these, requires a successor ADR (`ADR-0040` § `2026-08-29 Collected-property narrowing`) |

Purpose limitation is part of the floor, not only retention length. `ADR-0040` § 2 keeps a
standing permission to collect hardware properties as support-safe risk signals, and that
permission currently has **no** consumer: the only collected property is `MachineGuid`, used
for license binding. A future property admitted under that permission is a new purpose and is
subject to the same rules as the row above — successor ADR, no raw export, and an explicit
statement of what it is collected for. A standing permission is not a standing licence to
collect.

Backup, restore and repair flows preserve these states: a restore must not
resurrect an expired-and-pruned penalty as active, and must not lose
consumed-nonce history. The binding per-state overlay rules and their
fail-closed semantics live in
`docs/specs/backup-restore-migration.md` § 2.2 `Restore And Security-Monotonic State`.

## Future Scope

P2/P3 work may deepen configurable retention defaults, cleanup scheduler
ownership, legal hold, advanced enforcement flows and export/backup retention
policy. Any change that lowers P1 floors or changes redaction boundaries requires
an ADR/spec amendment before implementation.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — retention/maintenance ownership rows
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — audit/diagnostics retention floors
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` — audit vs diagnostics boundary
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — durable throttle və recovery nonce state
- `docs/specs/audit-and-diagnostics.md` — audit append-only və redaction boundary
- `docs/specs/storage-and-data-protection.md` — pre-login closed table set və protected material
- `docs/specs/backup-restore-migration.md` — snapshot retention və restore boundary
