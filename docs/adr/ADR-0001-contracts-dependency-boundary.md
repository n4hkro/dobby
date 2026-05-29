# ADR-0001: Contracts Dependency Boundary

**Status:** Accepted
**Date:** 2026-04-22

## Context

`Dobby.Contracts` həm host daxilində, həm də plugin-facing sərhəddə ən stabil
qatlardan biridir. Bu qatın `Dobby.Core`-a həddindən artıq bağlanması contracts
surface-ni şişirdər, runtime implementation detail-larını plugin sərhədinə
daşıyar və `Dobby.Core` daxilindəki normal dəyişiklikləri lazımsız breaking
change riskinə çevirər.

Arxitektura xəritəsi `Dobby.Contracts` üçün `Dobby.Core` dependency-sinin
minimal saxlanmasını tələb edir və `Core Public Surface Stability` convention-ı
yalnız dar, plugin-ə transitive görünən Core surface-in Contracts ilə eyni
stabilliklə idarə olunacağını açıq yazır.

## Decision

`Dobby.Contracts` yalnız aşağıdakı dar `Dobby.Core` surface-ə söykənə bilər:

- `Result` və `Result<T>`
- well-known error code types və registry-si
- correlation primitive-ləri: `CorrelationId`, `ICorrelationIdGenerator`,
  `ICorrelationContextAccessor`
- time abstractions: BCL `TimeProvider`
- host version surface: `HostVersion` və `IHostVersionProvider` (plugin compatibility yoxlaması üçün, `ADR-0037` tərəfindən istifadə olunur)

Bu siyahı Phase 1 üçün explicit transitive Core surface-dir. Bundan kənar yeni
`Dobby.Core` type-ının `Dobby.Contracts` üzərindən plugin sərhədinə çıxması
default olaraq qadağandır və ayrıca review/ADR tələb edir.
Bu qayda gələcəkdə meydana çıxa biləcək Dobby time wrapper-lərinə, yeni error
code qruplarına və ya əlavə correlation primitive-lərinə də şamil olunur; Phase
1 siyahısına əlavə yalnız ADR amendment/review ilə edilir.

`Dobby.Contracts` aşağıdakı kateqoriyaları öz dependency sərhədinə daxil etmir:

- DI, hosting və composition primitive-ləri
- serialization, logging və configuration implementation helper-ləri
- storage, security, session, licensing və UI runtime implementation type-ları
- validation engine və ya convenience abstraction-ları

Plugin page/extension contracts host-neutral olmalıdır. Plugin-in "mənim belə bir
page-im var" deməsi üçün lazım olan descriptor və request/result modelləri
`Dobby.Contracts` daxilində saxlanıla bilər, amma WPF control, window, dispatcher,
host view model və ya shell implementation type-ları Contracts sərhədinə daxil
edilmir. WPF və digər UI convenience wrapper-ləri lazımdırsa, bu `Dobby.SDK` və ya
`Dobby.UI` public extension surface-i ilə məhdudlaşdırılır.

Plugin developer convenience ehtiyacı yaranarsa bu, `Dobby.Contracts` deyil,
`Dobby.SDK` üzərində həll olunur.

## Consequences

- Skeleton mərhələsində `Dobby.Contracts -> Dobby.Core` reference-i dar və
  əsaslandırılmış qalır
- Bu siyahıdakı tiplər üçün breaking change Contracts breaking change-i kimi
  idarə olunur
- Bu siyahıdan kənar information model lazım olduqda ya Contracts daxilində
  ayrıca model qurulur, ya da runtime daxilində gizli saxlanılır
- `Dobby.Core` dumping ground-a çevrilməməsi üçün transitive surface aktiv
  şəkildə qorunur
- Plugin UI/Main Content hosting contract-ları host implementation detail-i
  sızdırmadan page descriptor/result modeli kimi qalır

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Dobby.Contracts` project row
- `docs/02_ARCHITECTURE_MAP.md` — `ADR-0001` risk sətri
- `docs/02_ARCHITECTURE_MAP.md` — `Core Public Surface Stability`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md:7` — `Result<T>` plugin-facing stabil tip
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-026`
- `docs/specs/plugins-manager.md` — plugin runtime və page hosting boundary
