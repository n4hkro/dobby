# Device Lifecycle Spec

**Tarix:** 2026-05-06
**Status:** Accepted spec
**Phase:** `P1-core` — §1/§1a Standalone device identity, recovery key boundary və
identifier disambiguation; `P2` — §2–§4 Branch Shared registration, suspend,
decommission və replacement işi (`ADR-0056` tier oxu: `documentation tier`)
**Scope:** Device identity boundary, recovery/support-safe identifier semantics,
Branch Shared registration, suspend/decommission, replacement and support-safe
metadata
**Sources:** `ADR-0056`, `ADR-0057`, `ADR-0058`, `ADR-0060`, `ADR-0063`

This spec records device lifecycle decisions required by `ADR-0056`,
`ADR-0057` and `ADR-0058`. §1/§1a are controlling for the Phase 1 device
identity/recovery identifier boundary; §2–§4 remain Branch Shared design input and
do not open implementation work in the current document pass.

## 1. Device Identity Boundary

Standalone device identity remains local/device-bound as described by existing
license and protected-storage documents.

Recovery authority is a device-generated protected asymmetric key pair, not a
fuzzy score over MachineGuid, CPU, board, disk or MAC values (`ADR-0058`).
Hardware observations may be bounded risk/support signals only; they are never a
recovery authorization factor and never a key source. Recovery request/response
binds a nonce, request identifier, device/deployment identity and recovery
public-key hash; replay, wrong-key and wrong-purpose responses fail closed. The
normative recovery sequence lives in `docs/specs/session-lifecycle.md`
§ `Owner Recovery (v2)`.

User-visible support codes are derived with a keyed, versioned, domain-separated
digest of the protected stable device id. Raw MachineGuid material, the raw
`DobbyStableDeviceId`, the raw recovery public key and the full license
fingerprint are never displayed, logged, audited or exported.

Branch Shared adds server-issued device registration identity:

- `deviceId`: Branch Shared server-issued device identifier;
- `branchDeploymentId`: Branch Shared deployment identity, not a license field;
- support-safe binding material: non-secret material used for idempotent retry
  and support diagnostics.

`DobbyStableDeviceId`, raw MachineGuid, full license fingerprint and protected
secrets are not copied into support-safe registration fields.

### 1a. Device identifier disambiguation

Six distinct values live in this area and must not be conflated. Two are raw material, three are derived identifiers with disjoint purposes, and one is a support-facing truncation. Only the derived values may leave the device, and never in place of one another:

| Value | Owner | Meaning |
|---|---|---|
| Branch Shared `deviceId` | this spec / `ADR-0056` | server-issued registration identifier; also the recovery artifact `deviceId` in Branch Shared deployments |
| Standalone recovery artifact `deviceId` | `docs/specs/session-lifecycle.md` § `Owner Recovery (v2)` | versioned, domain-separated keyed digest computed over the protected stable device id with a recovery-binding purpose label; never the raw stable device id |
| License `deviceKeyHash` | `ADR-0063` derivation, `ADR-0060` artifact, `docs/schemas/dobby-plugin-license-v3.schema.json` | versioned canonical digest over `algorithmVersion` + normalized `MachineGuid` + protected stable device id; it is **not** a hash of any device public key, is a separate purpose from both `deviceId` values, and is never interchangeable with them |
| Raw `MachineGuid` | `ADR-0040` § 2 Row 88 adapter | Windows registry value read verbatim by `Dobby.Infrastructure / Hardware Identity`; **not** normalized by the adapter and **never** exported or shown. Consumers normalize per `ADR-0063` § `Canonical derivation`. Not unique across cloned VMs or images restored from a golden master, so it is not a clone-resistance control on its own. |
| Protected stable device id | `ADR-0058` / `docs/specs/storage-and-data-protection.md` | DPAPI-protected, device-generated, non-exportable local material. This — not `MachineGuid` — is what distinguishes two machines that share a `MachineGuid`. Raw value never leaves the device; only digests derived from it do. |
| Support-safe device code | `ADR-0058` § `Support identity` | truncated, versioned, domain-separated keyed digest of the protected stable device id, intended for a human to read aloud to support. Truncation makes it non-authoritative by construction: it must never be accepted as a binding value, a recovery selector, or a licence input. |

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
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — recovery key material və support-code derivation
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — license artifact və binding field authority
- `docs/adr/ADR-0063-device-license-binding-derivation.md` — `deviceKeyHash` derivation, versioning və raw-material export qadağası
- `docs/specs/onboarding-and-provisioning.md`
- `docs/specs/postgres-deployment-ops.md`
- `docs/specs/session-lifecycle.md` — Owner Recovery (v2) normativ ardıcıllığı
- `docs/specs/storage-and-data-protection.md` — device identity/recovery key material persistence
- `docs/schemas/dobby-recovery-request-v2.schema.json`
- `docs/schemas/dobby-recovery-response-v2.schema.json`
- `docs/adr/ADR-0052-tenant-customer-context-v2.md`

## Revision History

- **2026-08-27:** Sources sətrinə `ADR-0063` əlavə olundu (§1a və cross-references
  artıq həmin ADR-i controlling authority kimi göstərirdi).
- **2026-08-26:** License `deviceKeyHash` sətri `ADR-0063` derivation-ı ilə
  dəqiqləşdirildi (public-key-hash təsviri ləğv edildi) və ADR cross-reference-i
  əlavə olundu.
- **2026-08-26:** `ADR-0058` reconciliation. Recovery authority, support-code
  derivation və raw-material prohibition §1-ə normativ mətn kimi köçürüldü;
  `deviceId` vs license `deviceKeyHash` disambiguation cədvəli əlavə edildi;
  stale `ADR-0051` cross-reference-i `ADR-0060` ilə əvəz olundu;
  `2026-08 Device Identity Amendment` bölməsi ləğv edildi.
- **2026-05-06:** Accepted after `HD-003` and `HD-004` were resolved by human
  decision. This is documentation/spec acceptance only and does not open device
  lifecycle implementation work.
