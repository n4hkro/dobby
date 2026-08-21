# Device Lifecycle Spec

**Tarix:** 2026-05-06
**Status:** Accepted spec
**Phase:** Branch Shared documentation / future implementation input
**Scope:** Branch Shared device identity, registration, suspend/decommission,
replacement and support-safe metadata

This spec records device lifecycle decisions required by `ADR-0056` and
`ADR-0057`. It does not add implementation work to the current document pass.

## 1. Device Identity Boundary

Standalone device identity remains local/device-bound as described by existing
license and protected-storage documents.

Branch Shared adds server-issued device registration identity:

- `deviceId`: Branch Shared server-issued device identifier;
- `branchDeploymentId`: Branch Shared deployment identity, not a license field;
- support-safe binding material: non-secret material used for idempotent retry
  and support diagnostics.

`DobbyStableDeviceId`, raw MachineGuid, full license fingerprint and protected
secrets are not copied into support-safe registration fields.

## 2. Registration

Registration is performed by BranchSetup/provisioning flow. It records enough
non-secret material to:

- retry setup idempotently;
- distinguish a replacement from a duplicate attempt;
- audit the origin device where required;
- support future decommission and credential rotation.

The exact schema/table is owned by `ADR-0056` / PostgreSQL docs.

Audit origin uses the server-issued `deviceId` as `originDeviceId` after
registration. Before registration, audit/setup evidence may use
`originDeviceId = null` plus a support-safe `localDeviceRef` snapshot; this
snapshot is evidence of what was present at event time, not a live link to the
current connection profile.

## 3. Suspend, Decommission and Replacement

Customer-release readiness must define:

- how a device is suspended;
- how a device is decommissioned;
- when `dobby_app` credential rotation is required;
- how support identifies a replacement without exposing raw hardware secrets;
- how test/staging restore avoids production `branchDeploymentId` collision.

Per-device PostgreSQL roles are not Phase 1 default. They remain a future
improvement if forensic accountability or credential containment requires them.

## 4. Direct Database Bypass Risk

Branch Shared stores authoritative state in PostgreSQL. If an operator bypasses
Dobby and mutates the database directly, Dobby cannot enforce application-layer
authorization, audit semantics or domain invariants for those changes.

This risk is operationally mitigated by role grants, setup/runbook guidance and
support policy. It is not solved by pretending PostgreSQL is unreachable to
operators.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-003` | RESOLVED_BY_HUMAN_DECISION (`DM-090`): seller devices persist only `dobby_app`; setup authority stays in BranchSetup context. |
| `HD-004` | RESOLVED_BY_HUMAN_DECISION (`DM-091`): audit origin uses server-issued `originDeviceId`; pre-registration uses `localDeviceRef` snapshot fallback. |

## Cross-references

- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md`
- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`
- `docs/specs/onboarding-and-provisioning.md`
- `docs/specs/postgres-deployment-ops.md`
- `docs/adr/ADR-0051-license-lifecycle-v2.md`
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`

## Revision History

- **2026-05-06:** Accepted after `HD-003` and `HD-004` were resolved by human
  decision. This is documentation/spec acceptance only and does not open device
  lifecycle implementation work.

## 2026-08 Device Identity Amendment (Controlling)

Recovery authority is a device-generated protected asymmetric key, not a fuzzy
score over MachineGuid, CPU, board, disk or MAC values. Hardware observations may
be bounded risk/support signals only. Recovery request/response binds a nonce,
request ID, device/deployment identity and recovery public-key hash; replay and
wrong-key/purpose responses fail closed. User-visible support codes are derived
with a keyed/versioned digest and never expose raw MachineGuid material.
