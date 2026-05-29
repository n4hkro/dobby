# Data Retention Spec

**Status:** Future scope
**Phase:** P2/P3 candidate

## Purpose

This placeholder records the future ownership boundary for data-retention policy
extensions referenced from `docs/02_ARCHITECTURE_MAP.md`.

P1 keeps the existing retention floors:

- audit retention default minimum: 365 days;
- technical diagnostics retention default: 30 days plus finite total size cap;
- no weakening of write-time redaction or diagnostics export redaction.

## Future Scope

P2/P3 work may deepen configurable retention defaults, cleanup scheduler
ownership, legal hold, advanced enforcement flows and export/backup retention
policy. Any change that lowers P1 floors or changes redaction boundaries requires
an ADR/spec amendment before implementation.
