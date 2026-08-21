# Branch Shared Future Track

**Status:** Deferred-by-design; not part of the Standalone completion plan  
**Authority:** ADR-0056/0057 as amended by ADR-0058/0060/0061

## Purpose

Preserve the accepted Branch Shared direction without allowing it to leak into or
delay current Standalone hardening. This file is a reopening checklist, not an
authorization to add projects, Npgsql packages, PostgreSQL runtime behavior or a
customer setup executable.

## BR-01 — Reopening decision gate

Before implementation, obtain explicit owner decisions/evidence for:

1. customer deployment/provisioning workflow and roles;
2. PostgreSQL distribution/setup artifact choice;
3. server identity/TLS/pin issuance and rotation;
4. branch/device registration and decommission authority;
5. dedicated setup-authorization issuer, purpose-separated key ring and expiry/
   replay policy from LC-01;
6. supported topology, device count, latency, outage and recovery objectives;
7. backup/restore/point-in-time recovery and credential-rotation operations;
8. schema migration compatibility window and mixed-version behavior;
9. customer support ownership and release tier.

Record decisions in a new Accepted ADR/spec amendment. Do not infer them from an
ordinary `.dobbylicense` or the current `default/default` Standalone context.

## BR-02 — Contract and threat-model phase

Only after BR-01 approval:

1. Threat-model setup credential theft, seller-device impersonation, TLS/pin
   mismatch, branch identity restore/clone, replay, split brain, database outage,
   stale session/permission and audit unavailability.
2. Freeze schemas for branch profile and setup authorization. The profile stores
   no password/token/private key/raw connection string; it references protected
   material only.
3. Freeze provider-neutral application repository/unit-of-work contracts. Plugin
   manifests and plugin-facing storage do not select SQLite/PostgreSQL scope.
4. Define a mode-aware authentication/session state machine with no local
   credential/permission mirror and explicit fail-closed shared operations.
5. Define the closed pre-auth PostgreSQL audit exception and behavior when that
   insert cannot occur; local diagnostics never masquerades as authoritative
   Branch Shared audit.
6. Add architecture/contract tests without adding runtime provider dependencies.

## BR-03 — Tier A skeleton (separate approved change)

Create only after BR-02 is accepted:

- provider-neutral Branch Shared contracts/options validation;
- separate `Dobby.BranchSetup` composition boundary and no setup credential in
  normal Host runtime;
- no Npgsql/live database behavior unless the approved Tier explicitly includes
  it;
- dependency tests proving Host/plugins cannot access setup authority.

**Exit:** skeleton compiles and contract tests pass; no claim of customer runtime
readiness.

## BR-04 — Tier B runtime and customer-release evidence

Create only after a further explicit approval:

- PostgreSQL 16.x/Npgsql adapter, migrations/grants and connection resilience;
- setup/provision/device lifecycle and mode-aware auth/session/audit;
- multi-device concurrency, outage/reconnect and restore identity tests;
- selected PostgreSQL setup artifact/runbook, firewall/TLS, backup/restore,
  rotation, monitoring and redaction evidence;
- clean main/seller device installation and mixed-version upgrade rehearsal.

**Exit:** all operational evidence in `docs/specs/postgres-deployment-ops.md` is
executed against customer-equivalent machines. Documentation or a live connection
smoke alone is insufficient.

## Non-Goals Until Reopened

- shared-folder/network-drive SQLite;
- local credential or permission caches for Branch Shared authority;
- plugin-owned PostgreSQL schemas/migrations;
- plugin-visible storage-provider selection;
- internet/vendor dependency for normal branch operation;
- using a plugin license as BranchSetup authorization;
- treating DeviceLocal diagnostics as authoritative shared audit.
