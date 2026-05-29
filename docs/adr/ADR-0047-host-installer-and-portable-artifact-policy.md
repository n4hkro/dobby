# ADR-0047: Host Installer and Portable Artifact Policy

**Status:** Accepted
**Date:** 2026-04-25

## Context

`03_TECH_STACK_AND_VERSIONS.md` MSIX-i primary packaging kimi, MSI-ni enterprise fallback kimi seçir. Portable/self-contained artifact anlayışı support və offline test üçün faydalıdır, amma customer deployment formatı kimi oxunsa release policy bulanır.

## Decision

Phase 1 distribution policy:

- MSIX primary customer deployment formatıdır.
- MSI WiX fallback kimi saxlanılır və MSIX-i blok edən enterprise mühitlər üçün istifadə olunur.
- Portable self-contained zip və ya single-file build yalnız support/dev/offline test artifact-dır.
- Portable artifact primary customer deployment formatı deyil.
- Portable artifact yoxdursa Phase 1 release bloklanmır; MSIX clean install/uninstall release blocker olaraq qalır.

## Consequences

- Deployment sənədlərində MSIX primary policy aydın qalır.
- Enterprise fallback üçün MSI saxlanılır.
- Support/dev troubleshooting üçün portable artifact mümkündür, amma customer deployment standard-ına çevrilmir.
- Acceptance criteria portable artifact-ı yalnız non-blocking support/dev check kimi yoxlayır.

## Cross-references

- `docs/03_TECH_STACK_AND_VERSIONS.md` — Distribution Model
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-024`

## Branch Shared Packaging Amendment

Branch Shared customer-release packaging is Tier B. `HD-005` is resolved by
`DM-092`: docs must say "selected PostgreSQL setup artifact/runbook" unless
Tier B customer-release work explicitly chooses and evidences a bundled
PostgreSQL packaging model.

The current Mərhələ 1 document pass does not create BranchSetup installer
artifacts, PostgreSQL binaries, firewall scripts, service setup scripts or live
database smoke tests.
