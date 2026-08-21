# Document Authority and Audit Baseline

**Status:** Canonical repository index

**Audit baseline:** `f87ab515b82df1fb605fb7a52a088bc06954bc7e`

**Audit date:** 2026-08-21 (Asia/Baku)

## Purpose

This file fills the missing `00` position in the `docs/00-09` set. It defines
which documents control implementation when historical sprint plans, accepted
ADRs, specifications, schemas, status tables, and the current code disagree.

The baseline commit is older than the requested 2026-08-16 cutoff, but the
rollback inspection found no retained local source change dated on or before
2026-08-16. All later uncommitted tracked and untracked changes were removed;
the audited `main` worktree therefore starts from the commit above.

## Authority Order

Use the first applicable source in this order:

1. the newest Accepted ADR that explicitly supersedes an older decision;
2. the owning machine-readable schema for an artifact wire format;
3. the owning executable feature spec under `docs/specs/`;
4. the `docs/01-09` canonical product, architecture, requirement, acceptance,
   engineering, and release documents;
5. the current implementation plans under `docs/implementation-plan/`;
6. historical `SPRINT_*` and `docs/superpowers/plans/*` execution plans;
7. release evidence and status tables, which report observations but do not
   create product behavior.

Code and passing tests are evidence of current behavior, not authority for a
contradictory product decision. A source-text/static-scan test proves only the
text it checks; it is not runtime proof.

## 2026-08 Hardening Decisions

The following Accepted ADRs resolve security and architecture weaknesses found
during the baseline audit:

- `ADR-0058`: credential, throttle, password-change, support-code, and Owner
  recovery hardening;
- `ADR-0059`: trusted in-process plugin posture, package resource limits,
  explicit compatibility versioning, asynchronous lifecycle, and declarative
  host-rendered UI contract;
- `ADR-0060`: license lifecycle v3, transactional last-known-good import, bounded
  artifacts, key separation, and dedicated BranchSetup authorization proof;
- `ADR-0061`: strict login-first runtime boundary, atomic mutation/audit rules,
  host diagnostics availability, and behavior-first verification.

These ADRs control wherever older sprint plans or older ADR sections conflict.

## Audit Classification Vocabulary

The traceability audit uses these exact states:

- `Implemented`: runtime behavior and meaningful verification exist.
- `Implemented-Weak`: the main behavior exists but has a material design,
  resilience, security, usability, or proof weakness.
- `Partial`: only part of the required end-to-end behavior exists.
- `Incorrect`: implementation contradicts the controlling requirement or has a
  correctness/security defect.
- `Unimplemented`: no production implementation exists.
- `Deferred-by-design`: intentionally outside the current release scope.
- `Evidence-only`: documentation, static scan, fixture, or artifact text exists
  without sufficient runtime proof.

## Current Execution Authority

The handoff entry point is
`docs/implementation-plan/00_MASTER_EXECUTION_PLAN.md`. Its auxiliary plans and
`01_TRACEABILITY_AND_FINDINGS.md` are the current execution and audit authority.
Historical sprint plans remain available only for provenance.

