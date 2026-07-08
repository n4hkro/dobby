# Sprint 11 Installer Verification

Status: HD-S11-003 MSIX-primary RC mechanics accepted only; release-ready installer closure remains blocked until installer evidence passes.

## AC-024

AC-024 requires installer install/uninstall/upgrade and data-preservation evidence for the approved release installer path.

## Installer Authority

- Primary installer policy: MSIX.
- MSI fallback: retained as ADR-0047 fallback only; not produced or tested unless MSIX is blocked and fallback is explicitly approved.
- Portable artifact: support/dev/offline-test scope; not a production installer substitute.
- MSIX-primary RC mechanics accepted only: self-signed certificate evidence may support RC installer mechanics, but it does not imply GA/customer production signing readiness unless release authority separately accepts that.
- Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

## Blocked Fields

| Field | Status |
| --- | --- |
| MSIX package identity | BLOCKED by HD-S11-003 |
| Signing certificate | BLOCKED by HD-S11-003 |
| Versioning policy | BLOCKED by HD-S11-003 |
| Packaging tooling | BLOCKED by HD-S11-003 |
| Installer artifact hash | NOT AVAILABLE |
| Install/uninstall result | NOT RUN |
| Upgrade data preservation | NOT RUN |
| Self-signed certificate GA/customer signing readiness | NOT CLAIMED |

No installer identity, signing material, package tooling, or new package pin was invented in Sprint 11.
