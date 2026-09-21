# ADR-0000: Title Goes Here

**Status:** Proposed
**Date:** YYYY-MM-DD

<!--
Relation fields. Yalnız tətbiq olunanları saxla, qalanını sil. Bunların shape-i
`tests/Dobby.IntegrationTests/Architecture/AdrSupersessionConsistencyTests.cs`
tərəfindən enforce olunur; qaydanın izahı `docs/adr/README.md`
§ `Supersession header formatı`-dadır.

**Supersedes:** `ADR-NNNN` — YALNIZ tam supersession. Adlanan ADR-in `Status`-u
`Superseded by ADR-<bu>` olmalıdır (iki istiqamət də yoxlanılır). Hələ `Accepted`
qalan sənədi burada adlandırmaq onu retired kimi elan edir.

**Amends:** `ADR-NNNN` — qismən dəyişiklik. Adlanan ADR `Accepted` qalır və
toxunulmamış bölmələri üçün authority olaraq qalır; onun `Status` sətri də bu
ADR-i adlandırmalıdır (`Accepted (… amended by ADR-<bu>)`).

**Clarifies:** `ADR-NNNN` § `bölmə` — qərar dəyişmir, mövcud öhdəliyin dəqiq
mənası burada sahiblənir. Adlanan ADR `**Clarified by:** `ADR-<bu>`` daşımalıdır.

**Superseded by:** `ADR-NNNN` — bu sənəd retired olduqda; `Status` sətrində də
təkrarlanır, çünki audit və tooling ilk növbədə `Status`-u oxuyur.
-->

## Context

Bu qərarın hansı problemi bağladığını, hansı sənəd və ya risk sətrindən gəldiyini
və niyə indi formalizasiya edildiyini yaz.

## Decision

Qəbul olunan qərarı qısa, enforce edilə bilən cümlələrlə yaz. Əgər qərarın scope-u
başqa ADR ilə bölünürsə, sərhədi açıq göstər.

## Consequences

- Bu qərarın implementasiya və ya contract səviyyəsində nəyi sadələşdirdiyini yaz
- Hansı şeylərin qadağan və ya deferred qaldığını yaz
- Breaking change və ownership təsiri varsa qeyd et

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md:<line>` — uyğun row, risk və ya convention
- `docs/03_TECH_STACK_AND_VERSIONS.md:<line>` — varsa əlaqəli stack qərarı
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md:<line>` — varsa əlaqəli policy
- `docs/adr/ADR-NNNN-other-decision.md` — varsa bağlı ADR
