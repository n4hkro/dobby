# PostgreSQL Deployment Operations Spec

**Tarix:** 2026-05-06
**Status:** Accepted spec
**Phase:** Branch Shared Tier B / customer-release readiness
**Scope:** PostgreSQL setup evidence, operational protection, backup/restore,
schema/grant smoke checks and release gates for Branch Shared mode

This spec is the operational owner for Branch Shared PostgreSQL deployment after
`ADR-0056`. It is not a Tier A skeleton implementation task.

## 1. Supported Model

Branch Shared customer release uses a Dobby-selected PostgreSQL setup
artifact/runbook on the branch LAN. This spec does not claim a specific bundled
binary distribution model; the selected artifact/runbook may later choose
bundled packaging or a documented official PostgreSQL installer path as Tier B
release work.

Manual existing PostgreSQL instances and validator CLI support remain deferred
unless a later ADR/spec explicitly opens them.

## 2. Version and Network Boundary

- Target major version: PostgreSQL 16.x.
- Network scope: same branch LAN only.
- Internet connectivity is not mandatory for runtime operation.
- Multi-branch, internet sync and app-server mediated runtime are out of Phase 1
  scope.

## 3. Roles and Grants

Branch Shared uses separate setup and runtime authority:

- `dobby_setup`: setup/provisioning role, DDL-capable only in BranchSetup
  context.
- `dobby_app`: runtime role, no DDL, minimum grants for normal application
  operations.

`dobby_app` may perform the narrow canonical pre-auth audit INSERT path only for
events owned by `ADR-0057`. PostgreSQL grants prove table-level permission; event
name whitelist enforcement is application-layer behavior.

The audit read/export grant path is not part of P1 runtime support. P1 is
write-only from the app perspective; dedicated reader roles/functions wait until
audit viewer/export becomes release scope.

## 4. Schema and Extension Guardrails

Dobby-issued PostgreSQL DDL must not require optional extensions such as
`pgcrypto` or `uuid-ossp`.

Customer-release setup evidence must include a smoke check that confirms:

- Dobby-required schemas/tables exist.
- Required grants exist for `dobby_app`.
- `dobby_app` cannot execute DDL.
- No Dobby-required optional PostgreSQL extension is needed.
- No extension-owned object in `public` is required by Dobby setup.

## 5. Server Identity and TLS Pinning

Branch Shared profiles store `serverIdentityPinMaterial` as lowercase
64-character SHA-256 hex. Display formatting may differ, but storage encoding
does not.

TLS pin rotation is a planned-outage reprovisioning operation unless a later ADR
introduces dual-pin or managed trust.

## 6. Operational Protection Floor

PostgreSQL data directory protection is not governed by the SQLite/local-file
full-DB encryption invariant from `ADR-0054`. The Branch Shared operational
floor is:

- PostgreSQL service account and data-directory ACL baseline.
- Secret redaction in logs/setup reports.
- Encrypted dump/backup artifacts where the selected setup artifact/runbook
  supports it.
- Restore smoke checks and post-restore schema/grant validation.
- Clear statement that no PostgreSQL data-directory encryption claim is made
  unless the selected artifact proves one.

## 7. Backup and Restore

Branch Shared authoritative backup uses PostgreSQL-native backup/dump tooling
selected by this spec and the setup artifact. Standalone file-copy style SQLite
backup does not apply to Branch Shared authority.

Restore-to-production preserves `branchDeploymentId`. Restore-to-test/staging
requires explicit BranchSetup reassignment so test clones do not collide with
production identity.

Main-device failure recovery is a Tier B/customer-release readiness topic. It
must be documented as a runbook/evidence requirement, not as a Tier A skeleton
feature.

## 8. Credential Rotation and Device Decommission

Customer-release readiness requires a `dobby_app` credential rotation procedure
when a device is decommissioned or replaced. This is a Tier B operational
procedure unless per-device database roles are approved later.

## 9. Release Evidence

Branch Shared customer-release evidence must include:

- setup artifact/runbook provenance;
- firewall/port setup;
- service recovery policy;
- TLS pin proof;
- schema/grant smoke output;
- backup/restore rehearsal;
- credential rotation/decommission procedure;
- secret-redaction proof for setup logs/reports.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-002` | RESOLVED_BY_HUMAN_DECISION (`DM-089`): write-only P1 audit; read/export grants deferred. |
| `HD-005` | RESOLVED_BY_HUMAN_DECISION (`DM-092`): Dobby-selected setup artifact/runbook; exact binary packaging shape selected in Tier B. |

## Cross-references

- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md`
- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`
- `docs/adr/ADR-0054-db-at-rest-protection-provider.md`
- `docs/specs/backup-restore-migration.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/schemas/branchshared.schema.json`

## Revision History

- **2026-05-06:** Accepted after `HD-002` and `HD-005` were resolved by human
  decision. This is a documentation/spec acceptance only; customer-release
  operational evidence remains Tier B work.
