# Data Retention Spec

**Status:** Standalone floor implemented; advanced policy is future scope
**Phase:** P2/P3 candidate

## Purpose

This placeholder records the future ownership boundary for data-retention policy
extensions referenced from `docs/02_ARCHITECTURE_MAP.md`.

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

## Future Scope

P2/P3 work may deepen configurable retention defaults, cleanup scheduler
ownership, legal hold, advanced enforcement flows and export/backup retention
policy. Any change that lowers P1 floors or changes redaction boundaries requires
an ADR/spec amendment before implementation.
