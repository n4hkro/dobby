# ADR-0057: Mode-Aware Authentication Boundary

**Status:** Accepted (credential and throttling details amended by `ADR-0058`; the
BranchSetup setup-proof sections amended by `ADR-0060` — see ## Revision History)
**Date:** 2026-05-06

## Context

Existing authentication documents define a local-first Standalone model where a
clean local install creates the first Owner on first successful login. Branch
Shared changes the authority location: users, credentials, permissions and
audit authority live in Branch Shared PostgreSQL after provisioning.

This ADR formalizes the authentication/session impact of `ADR-0056` without
moving PostgreSQL provider/deployment decisions into the auth layer.

## Decision

Standalone authentication remains device-local and follows
`ADR-0038-local-first-authentication.md`.

Branch Shared authentication is mode-aware:

- Branch Shared devices do not mirror credential hashes, permission state or
  stale user authority into DeviceLocal storage.
- Branch Shared login validates server identity, `branchDeploymentId`, schema
  readiness and grant readiness before reading credential authority.
- Seller devices cannot race to become first Owner. Initial Owner provisioning
  belongs to main-device BranchSetup flow.
- If Branch Shared authority is unavailable, shared auth and shared commands
  fail closed. Runtime may preserve in-memory UI/session state for recovery, but
  it does not authorize new shared writes while authority is unavailable.
- Local diagnostics may record failure evidence. They are not authoritative
  Branch Shared audit unless the canonical PostgreSQL audit insert succeeds.

BranchSetup is a separate customer-side setup context:

- `Dobby.BranchSetup.exe` is distinct from vendor-side `Dobby.Admin` and normal
  `Dobby.Host` runtime.
- `HD-001` is closed: setup authorization requires Windows local administrator
  execution, the protected `dobby_setup` credential, and a dedicated purpose-signed
  setup authorization artifact defined in
  `docs/schemas/dobby-branch-setup-authorization-v1.schema.json` (`ADR-0060`). All
  three are required; none of them substitutes for another. A plugin license
  (`.dobbylicense`) is **not** accepted as setup proof in any form, including as a
  read-only file argument — the earlier license-as-setup-proof position is retired
  (see ## Revision History, 2026-08-26: the `DM-071` reopen condition fired).
- BranchSetup does not import license state, mutate license state, create
  entitlement, activate plugins or perform plugin/package authorization.
- `Dobby.Host` never loads `dobby_setup`, never executes setup DDL and never
  performs initial Owner provisioning.

Pre-auth audit boundary:

- The only canonical Branch Shared pre-auth audit events are
  `user.login.failed`, `recovery.export`, `recovery.import` and
  `security.verification.failed`.
- The event-name whitelist is enforced in application code, not by PostgreSQL
  grants alone.
- If PostgreSQL is unreachable before that insert can occur, the event is
  DeviceLocal diagnostics/setup evidence only. It is not authoritative Branch
  Shared audit. Pending-audit buffering remains deferred and reopens only if
  security review rejects direct pre-auth PostgreSQL audit INSERT or a later
  ADR/spec amendment explicitly approves a signed DeviceLocal pending-audit
  buffer.

Audit origin representation is resolved as follows:

- audit `source` is a closed vocabulary: `Host`, `BranchSetup`, `Support`;
- `originDeviceId` is the Branch Shared server-issued `deviceId`;
- pre-registration events use `originDeviceId = null` and include a support-safe
  `localDeviceRef` snapshot;
- adding a new `source` value requires an ADR/spec amendment.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-001` | RESOLVED_BY_HUMAN_DECISION (`DM-071`): BranchSetup uses Windows local admin execution, protected `dobby_setup`, and existing `.dobbylicense` file-argument proof. `.dobbylicense` is read-only setup proof, not license import, activation, entitlement, plugin authorization, or package authorization. **Reopened and re-resolved by `ADR-0060`:** the license-as-setup-proof part is withdrawn; setup authorization is a dedicated purpose-signed artifact (`docs/schemas/dobby-branch-setup-authorization-v1.schema.json`) and the admin/`dobby_setup` gates remain. |
| `HD-002` | RESOLVED_BY_HUMAN_DECISION (`DM-089`): Branch Shared P1 audit is write-only from the app perspective; viewer/export read grants remain deferred until explicitly scoped. |
| `HD-003` | RESOLVED_BY_HUMAN_DECISION (`DM-090`): seller devices persist only `dobby_app`; `dobby_setup` remains BranchSetup-only. |
| `HD-004` | RESOLVED_BY_HUMAN_DECISION (`DM-091`): audit origin uses closed `source` plus server-issued `originDeviceId`, with pre-registration `localDeviceRef` snapshot fallback. |
| `HD-006` | RESOLVED_BY_HUMAN_DECISION (`DM-093`): `customerDeploymentId` is prose-only shorthand for `(customerId, deploymentId)` and not a wire/code/storage identifier. |

## Reopen Conditions

These resolved decisions must not be reopened for preference-only reasons.

| Decision | Reopen condition |
|---|---|
| `DM-071` / `HD-001` | Security rejects minimal setup proof, a setup-specific signed artifact such as `.dobbysetup` is required, `.dobbylicense` schema changes, plugin/package/device binding is proven necessary for setup proof, mandatory online freshness becomes a product/security requirement, or audit architecture approves safe pre-schema evidence re-ingestion with immutable provenance. |
| `DM-089` / `HD-002` | Audit viewer/export becomes Branch Shared release scope, compliance requires DB-enforced reader separation, or a dedicated audit reader role/function is approved. |
| `DM-090` / `HD-003` | Pin rotation, onboarding, restore, or support model proves persistent seller-device setup authority is required. |
| `DM-091` / `HD-004` | A future correlation/origin envelope model supersedes the payload shape, source taxonomy changes, or device registration identity model changes. `ADR-0029` currently owns correlation id propagation only and does not define that envelope. |
| `DM-093` / `HD-006` | License artifact schema versioning is approved, the license/customer identity model changes, or a later ADR approves a single-field merged identifier. |

## Consequences

- Branch Shared auth is not "offline auth". No local credential mirror or stale
  permission cache is created for seller devices.
- BranchSetup setup reports and Branch Shared audit are separate evidence
  routes until schema exists.
- Auth/session behavior has explicit unavailable, wrong-server,
  wrong-deployment, schema/grant mismatch and not-provisioned failure
  categories; exact result identifiers remain owned by result policy.
- This ADR does not authorize implementation of BranchSetup or Npgsql runtime
  adapter in the current document pass.

## Cross-references

- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md`
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — amends the
  credential and throttling details of this ADR
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — amends the BranchSetup
  setup-proof sections of this ADR; current setup authorization artifact authority
- `docs/schemas/dobby-branch-setup-authorization-v1.schema.json` — setup
  authorization artifact schema
- `docs/adr/ADR-0038-local-first-authentication.md`
- `docs/adr/ADR-0041-login-first-startup-policy.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- `docs/specs/onboarding-and-provisioning.md`
- `docs/specs/session-lifecycle.md`
- `docs/specs/user-management.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`

## Revision History

- **2026-05-06:** Accepted after `HD-001`, `HD-002`, `HD-003`, `HD-004` and
  `HD-006` were resolved by human decision. This status promotion is
  documentation lifecycle only and does not open BranchSetup or Npgsql
  implementation.
- **2026-08-26:** `HD-001` license-as-setup-proof portion amended by `ADR-0060`;
  the `DM-071` reopen condition ("a setup-specific signed artifact is required") is
  recorded as fired. Retired position: a plugin license (`.dobbylicense`) supplied
  as a read-only file argument was accepted as setup proof. Current position: a
  dedicated purpose-signed setup authorization artifact is required. This ADR
  remains `Accepted` and remains the authority for the mode-aware authentication
  boundary itself; credential/throttling details remain amended by `ADR-0058`. No
  other decision in this ADR changes.
- **2026-08-31:** Vocabulary and reverse-reference repair. (a) The `HD-001` bullet
  kept the retired license-as-setup-proof sentence and appended a
  "**Superseded by `ADR-0060`:**" note on top of it; a reader who stopped at the
  first sentence would implement the retired gate. The bullet now states only the
  current requirement and points to this section for history. (b) "Superseded by"
  was the wrong relation vocabulary: `ADR-0060` declares `**Amends:**` for this ADR
  and this ADR stays `Accepted`, so the body now says "amended" (`docs/adr/README.md`
  § `Supersession header formatı` defines only full supersession and partial
  amendment). (c) `ADR-0058` and `ADR-0060` were missing from ## Cross-references
  even though both amend this ADR — the reverse pointers are now present.
