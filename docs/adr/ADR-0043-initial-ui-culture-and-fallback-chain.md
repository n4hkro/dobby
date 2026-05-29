# ADR-0043: Initial UI Culture and Fallback Chain

**Status:** Accepted
**Date:** 2026-04-25

## Context

Dobby Phase 1-də localization foundation saxlayır, amma default UI culture, gələcək dil skeleton-ları və missing-key davranışı ayrıca formal bağlanmamışdı. `01_PRODUCT_SCOPE.md` string-lərin mərkəzləşdirilmiş idarə olunmasını tələb edir, `02_ARCHITECTURE_MAP.md` Row 49 isə `Dobby.Localization` ownership-ini göstərir.

Bu ADR Phase 1 üçün minimal localization davranışını bağlayır və runtime language switching scope-unu P2-də saxlayır.

## Decision

Phase 1 default UI culture `az-AZ`-dir.

Resource skeleton Phase 1-də aşağıdakı culture-ları daşıya bilər:

- `az-AZ` — default və canonical UI culture
- `en-US` — future English language readiness skeleton
- `tr-TR` — future Turkish language readiness skeleton

Runtime language switching Phase 1 scope-una daxil deyil və P2-yə saxlanılır. Phase 1-də user-facing host UI string-ləri localization resource-larından gəlməlidir; hardcoded visible UI string release gate-də qəbul edilmir.

Missing-key davranışı:

- development/debug mühitində missing key görünən olmalıdır ki, UI review zamanı qaçmasın;
- production-da fallback culture istifadə olunur və `localization.missing-key` diagnostics event/log yaranır;
- missing localization key compliance audit hadisəsi deyil və `Dobby.Audit`-ə yazılmır.

## Consequences

- `Dobby.Localization` Phase 1-də default culture və fallback chain üçün canonical owner olur.
- Release gate hardcoded visible UI string-ləri yoxlamalıdır.
- Missing-key hadisələri `Dobby.Diagnostics` sərhədində qalır və `ADR-0019` audit/diagnostics ayrılığını pozmur.
- Full translations, per-user language selection və runtime language switch P2-də ayrıca qərarla açılır.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — Əsas Məhsul Prinsipləri, localization və terminology qaydaları
- `docs/02_ARCHITECTURE_MAP.md` — Row 49 `String / Localization Management`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — Localization stack row
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-013`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-013`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-017`
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` — missing key diagnostics, audit deyil
