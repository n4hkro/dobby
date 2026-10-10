# Document Authority and Audit Baseline

**Status:** Canonical repository index

**Audit baseline:** `35148c2` (post implementation-plan retirement)

**Audit date:** 2026-08-28 (Asia/Baku)

## Purpose

This file fills the `00` position in the `docs/00-09` set. It defines which
documents control implementation when accepted ADRs, specifications, schemas,
status tables, historical plans, and the current code disagree.

## Authority Order

Use the first applicable source in this order:

1. the newest Accepted ADR that explicitly supersedes an older decision;
2. the owning machine-readable schema for an artifact wire format;
3. the owning executable feature spec under `docs/specs/`;
4. the `docs/01-09` canonical product, architecture, requirement, acceptance,
   engineering, and release documents;
5. historical execution plans (`docs/superpowers/plans/*` in the tree, and the
   retired `docs/implementation-plan/` / `SPRINT_*` documents preserved only in
   git history — see `Retired Execution Plans` below);
6. release evidence and status tables under `artifacts/`, which report
   observations but do not create product behavior.

Code and passing tests are evidence of current behavior, not authority for a
contradictory product decision. A source-text/static-scan test proves only the
text it checks; it is not runtime proof.

## 2026-08 Hardening Decisions

The following Accepted ADRs resolve security and architecture weaknesses found
during the 2026-08 baseline audit and control wherever older documents or older
ADR sections conflict:

- `ADR-0058`: credential, throttle, password-change, support-code, and Owner
  recovery hardening (supersedes `ADR-0039`; username-only remember-me;
  device-generated recovery key pair v2);
- `ADR-0059`: trusted in-process plugin posture, package resource limits,
  explicit compatibility versioning (`hostApiVersion`, manifest schema v2),
  purpose-separated versioned trust key rings, asynchronous cancellable
  lifecycle, and declarative host-rendered UI contract v2;
- `ADR-0060`: license lifecycle and import v3 (supersedes `ADR-0051`): signed
  intent is only `granted`/`revoked`, runtime states are derived, transactional
  last-known-good import, bounded artifacts, key separation, and a dedicated
  BranchSetup authorization proof artifact;
- `ADR-0061`: strict login-first runtime boundary, atomic mutation/audit/outbox
  rules, host diagnostics availability, and behavior-first verification.

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

## Retired Execution Plans

`docs/implementation-plan/` (master execution plan, traceability and findings,
status register) and the historical `SPRINT_*` documents were retired in
commits `a8a5914` and `35148c2`. They remain available only in git history for
provenance and hold no execution or product authority. `docs/superpowers/plans/*`
documents remain in the tree as historical execution records only. No current
document may cite retired or historical plans as a controlling source;
requirements they described are authoritative only where they were promoted
into ADRs, schemas, specs, or `docs/01-09`.
