# ADR-0029: Correlation ID Ownership

**Status:** Accepted
**Date:** 2026-04-22

## Context

Phase 1 üçün correlation semantics logging, messaging, plugin boundary və
background task axınlarına toxunur. Correlation primitive-lərinin ayrıca
namespace və ya ayrıca project-ə çıxarılması bu mərhələdə lazımsız parçalanma
yaradar.

Arxitektura xəritəsi correlation ownership-i artıq `Dobby.Core / ID / Key
Generation` altında bağlayır və minimum required shape-i konkret sadalayır.

## Decision

Correlation ID ownership `Dobby.Core` daxilində mövcud `ID / Key Generation`
namespace-ində qalır. Ayrıca correlation namespace-i açılmır.

Phase 1 minimum shape aşağıdakılardır:

- `CorrelationId`
- `ICorrelationIdGenerator`
- `ICorrelationContextAccessor`

Davranış qaydaları:

- generator yalnız yeni scope-da işləyir
- mövcud correlation id overwrite edilmir
- context async-flow uyğun daşınır
- background task-a correlation explicit ötürülür
- scope bitəndə context təmizlənir

## Consequences

- Skeleton və logging ADR-ləri üçün minimal correlation primitive-ləri əvvəlcədən
  sabitləşir
- Correlation ownership ayrılıqda yeni module yaratmadan `Dobby.Core` içində
  saxlanılır
- Sonrakı ID ehtiyacları (job id, audit id və s.) bu minimum correlation
  primitive-lərindən ayrıca genişlənə bilər

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `ID / Key Generation` row
- `docs/02_ARCHITECTURE_MAP.md` — `ADR-0029` risk sətri
- `docs/02_ARCHITECTURE_MAP.md` — `Correlation ID Ownership` convention
- `docs/02_ARCHITECTURE_MAP.md` — `Correlation Context Lifecycle` convention

## Branch Shared Evidence Amendment

Correlation ID remains a runtime/operation tracing primitive and is not reused
as Branch Shared device identity, `branchDeploymentId`, audit id or setup-report
id.

BranchSetup setup reports may carry correlation/context metadata for support,
but their evidence routing and schema stability are owned by `ADR-0057` and
`docs/specs/onboarding-and-provisioning.md`. Audit origin device representation
is resolved by `DM-091`: correlation remains separate from the closed
`source` vocabulary, server-issued `originDeviceId`, and pre-registration
`localDeviceRef` snapshot used by Branch Shared audit payloads.
