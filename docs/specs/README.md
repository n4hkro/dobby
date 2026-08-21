# Dobby Specs Index

**Status:** Accepted index
**Date:** 2026-05-06

This file is the narrow specs index chosen by `HD-007` / `DM-094`. It replaces
the need for a global `docs/DOCS_STRUCTURE.md` file for the current
storage/deployment close-out.

## Ownership Table

| Spec | Scope / canonical ownership |
|---|---|
| `admin-tool.md` | Vendor-side Admin Tool, signing/recovery/license artifact generation, and boundary from customer-side BranchSetup. |
| `audit-and-diagnostics.md` | Audit taxonomy, diagnostics boundary, redaction, Branch Shared audit origin payload, and no-telemetry default. |
| `backup-restore-migration.md` | Backup/restore/migration boundaries, cross-device restore rules, and Branch Shared restore identity handling. |
| `data-retention.md` | Future-scope retention policy extensions, preserving P1 audit/diagnostics floors and redaction boundaries. |
| `design-system-assets.md` | Design asset inventory and visual baseline references. |
| `device-lifecycle.md` | Branch Shared device identity, registration, suspend/decommission, replacement, and support-safe metadata. |
| `license-import.md` | `.dobbylicense` import flow, permission guard, non-disclosing license status, and `customerDeploymentId` prose-only interpretation. |
| `onboarding-and-provisioning.md` | BranchSetup setup proof, first Owner provisioning, device registration, and setup evidence routing. |
| `plugins-manager.md` | Plugin install/enable/disable/uninstall behavior, runtime fault isolation, and plugin storage mode boundary. |
| `postgres-deployment-ops.md` | Branch Shared PostgreSQL setup artifact/runbook, grants, operational protection, backup/restore evidence, and Tier B release readiness. |
| `sample-plugin.md` | Sample/test plugin verification artifact and platform acceptance coverage. |
| `session-lifecycle.md` | Login/logout/lock/session invalidation behavior and Branch Shared session availability semantics. |
| `settings-and-about.md` | Settings/About surface, privacy-safe support fields, Account/Security/User Management entries. |
| `shared-data-tools.md` | P2 shared import/export mapping tools and mapping-profile storage boundary. |
| `shell-ui.md` | Shell layout, navigation surfaces, keyboard/accessibility baseline, and Branch Shared shell status rendering boundary. |
| `storage-and-data-protection.md` | Storage taxonomy, DeviceLocal/BranchShared protection, plugin storage compartments, and `branchshared.json` schema ownership. |
| `user-management.md` | User/permission management, Owner guard, first Owner mode boundary, and Branch Shared user authority. |
| `work-sessions.md` | Process-lifetime work session minimize/restore behavior and Branch Shared availability continuity boundary. |

## Governance

- This index is descriptive; each spec remains canonical for its own behavior.
- Cross-document skeleton naming and wire/storage terminology are centralized in
  `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md`.
- JSON Schema `$id` values are reserved by `docs/schemas/*` files. Specs may
  describe schema ownership and validation behavior, but must not introduce a
  second `$id` authority or prose-only replacement for a schema `$id`.
- `Cədvəl N` references are local to their owning document unless the reference
  names an explicit document path. Cross-doc references should prefer document
  path + section/row title over bare table numbers.
- Adding a new spec requires adding one row here with its ownership boundary.
- Skeleton-critical spec ownership additions should extend the table above rather
  than creating a second ownership index; this table is the durable specs index
  chosen by `HD-007` / `DM-094`.
- This file does not authorize code skeleton work. Implementation phases remain
  governed by the accepted ADR/spec set and explicit human approval.

## 2026-08 Authority Note

Conflicting feature wording is controlled by the amendment sections dated
2026-08 and `ADR-0058` through `ADR-0061`. Completion status and executable work
ordering are maintained in `docs/implementation-plan/00_MASTER_EXECUTION_PLAN.md`
and its traceability companion; historical sprint plans are evidence, not
current authority.
