# Onboarding and Provisioning Spec

**Tarix:** 2026-05-06
**Status:** Accepted spec
**Phase:** Branch Shared documentation / future implementation input
**Scope:** BranchSetup setup proof, first Owner provisioning, device
registration and setup evidence routing

This spec records the Branch Shared onboarding model required by `ADR-0057`. It
does not create a `Dobby.BranchSetup` implementation in the current document
pass.

## 1. Setup Context

Branch Shared setup uses a separate customer-side context:

- executable identity: `Dobby.BranchSetup.exe`;
- credential visibility: setup-authorized context may load protected
  `dobby_setup`;
- runtime separation: `Dobby.Host` does not load `dobby_setup` and does not run
  setup DDL;
- vendor separation: `Dobby.Admin` remains vendor-side admin/signing tooling.

Running BranchSetup and `Dobby.Host` simultaneously on the same device is
forbidden. BranchSetup has its own local coordinator separate from the
`Dobby.Host` single-instance coordinator.

## 2. Setup Authorization

`HD-001` is closed by the following model:

- Windows local administrator execution;
- protected `dobby_setup` credential;
- a dedicated bounded, purpose-signed BranchSetup authorization artifact
  (`docs/schemas/dobby-branch-setup-authorization-v1.schema.json`) as setup
  proof. Per `ADR-0060`, an ordinary plugin `.dobbylicense` is not setup proof;
  the earlier `HD-001` license-as-setup-proof wording is superseded.

BranchSetup license handling is verification-only. It does not import license
state, mutate license state, create entitlement, activate plugins, or authorize
plugin packages.

## 3. First Owner Bootstrap

Branch Shared first Owner is created during main-device provisioning. Seller
devices cannot become first Owner by racing first login.

Provisioning order:

1. T1 creates required schemas/tables and deployment metadata.
2. T2 creates initial Owner, permissions and initial setup/audit evidence.

The exact schema names belong to `ADR-0056` and PostgreSQL ops docs.

## 4. Device Registration

Branch Shared registration issues a server-side `deviceId` and records
support-safe non-secret binding material for idempotent retry and device
lifecycle tracking. Registration material must not expose raw MachineGuid,
full license fingerprint, secrets or privacy-sensitive hardware details.
The local Branch Shared connection profile refers to the live device profile or
registration record with `deviceProfileRef`; audit docs retain `localDeviceRef`
only for pre-registration event-time snapshot semantics.

Device replacement and decommission behavior is owned jointly by this spec and
`docs/specs/device-lifecycle.md`.

## 5. Setup Evidence Routing

Before Branch Shared schema exists, BranchSetup writes authoritative setup
report evidence outside PostgreSQL. That pre-schema evidence is not re-injected
into PostgreSQL later.

After schema exists, setup events that are audit-worthy are written to
`dobby_audit` with `source=BranchSetup` taxonomy owned by audit docs.

A deterministic BranchSetup setup-report JSON Schema remains an optional future
improvement. It may be added later if onboarding acceptance requires a
wire-stable evidence artifact. Until then, `DM-071` setup-report bucket and this
routing rule are sufficient.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-003` | RESOLVED_BY_HUMAN_DECISION (`DM-090`): normal seller runtime profiles persist only `dobby_app`; `dobby_setup` is BranchSetup-only. |
| `HD-004` | RESOLVED_BY_HUMAN_DECISION (`DM-091`): setup/audit origin uses closed `source` vocabulary and server-issued `originDeviceId`; pre-registration uses `localDeviceRef` snapshot fallback. |

## Cross-references

- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`
- `docs/adr/ADR-0023-admin-host-boundary.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- `docs/specs/device-lifecycle.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/specs/license-import.md`

## Revision History

- **2026-05-06:** Accepted after `HD-003` and `HD-004` were resolved by human
  decision. This is documentation/spec acceptance only and does not create a
  BranchSetup implementation.

## 2026-08 Provisioning Amendment (Controlling)

Future BranchSetup authorization is a dedicated bounded, purpose-signed artifact
with explicit branch/deployment identity, operation scope, nonce, issued/expiry
times and key ID. A plugin license cannot authorize setup. Applying it is
single-use/idempotent, audited and fails closed on replay, mismatch or expiry.
This amendment does not open Branch Shared implementation work.
