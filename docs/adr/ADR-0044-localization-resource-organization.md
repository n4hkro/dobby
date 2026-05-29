# ADR-0044: Localization Resource Organization

**Status:** Accepted
**Date:** 2026-04-25

## Context

Localization project-i mövcuddur, amma resource-ların necə təşkil olunacağı və plugin resource ownership-in harada qalacağı açıq deyildi. Əgər resource-lar flat saxlanarsa, host və plugin string-ləri qarışar, key collision-lar artar və plugin-lər host implementation detalına bağlana bilər.

## Decision

Localization resource-ları namespace-based təşkil olunur.

Host resource naming convention:

```text
<Project>.<FeatureArea>.<Category>.<Key>
```

Nümunələr:

- `Dobby.Shell.Navigation.Home`
- `Dobby.Plugins.Install.SelectPackage`
- `Dobby.Security.Users.Create`

Host resources mərkəzləşdirilmiş `Dobby.Localization` ownership-i altında idarə olunur. Plugin localization resources isə plugin assembly-lərində qalır; host onları plugin assembly resource provider-i vasitəsilə oxuya bilər, amma plugin string-ləri host central resource file-larına köçürülmür.

Plugin resource-ları host implementation project-lərinə birbaşa bağlanmamalıdır. Plugin yalnız `Dobby.Contracts`/`Dobby.SDK`-da exposed localization helper surface varsa onu görə bilər.

Biznes, qanun, qayda və plugin domain text-ləri avtomatik UI localization resource-u sayılmır. UI/system mesajları localization resource organization-a daxildir; domain data isə öz storage, ownership, validation və migration modelini saxlayır.

## Consequences

- Host resource key-ləri collision riskini azaldan namespace convention-a tabe olur.
- Plugin resource ownership plugin package/assembly sərhədində qalır.
- `Dobby.Localization` provider və fallback behavior-u sahib edir, plugin business string-lərinin məzmununa sahib olmur.
- Runtime language switching bu ADR ilə açılmır; `ADR-0043` bunu P2-yə saxlayır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 49 `String / Localization Management`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — Localization stack row
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-013`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-013`
- `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`
- `docs/adr/ADR-0001-contracts-dependency-boundary.md`
