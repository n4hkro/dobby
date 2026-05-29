# ADR-0013: Routing and Deep Link Model

**Status:** Accepted
**Date:** 2026-04-30

## Context

`Dobby.Navigation / Routing / Deep Link Handling` P1-core namespace kimi xəritədə
mövcuddur, amma route identity, route owner və plugin page hosting sərhədi ayrıca
bağlanmamışdı. Shell skeleton-u Home/Plugins/Settings və plugin page route-larını
guess etmədən yaratmalıdır.

## Decision

### 1. Route identity

Phase 1 route identity stable string key-dir. Route key-lər user-facing label deyil
və localization-dan asılı olmur.

Canonical P1 route classes:

| Route class | Owner | Example shape | Notes |
|---|---|---|---|
| Built-in shell route | `Dobby.Navigation` registry, hosted by `Dobby.Shell` | `host/home`, `host/plugins`, `host/settings` | Home fixed first, Settings fixed last. |
| Built-in subpage route | owning built-in feature | `host/settings/security-session` | Permission və session state ilə gate edilə bilər. |
| Plugin page route | plugin contribution descriptor, normalized by host | `plugin/{pluginId}/{routeKey}` | Plugin raw WPF view type-i route identity deyil. |

Route key comparisons ordinal/culture-invariant aparılır. Persisted route
preference-ləri label, display name və ya localized text saxlamır.

### 2. Ownership və enforcement

- `Dobby.Navigation` route registry, matching və route descriptor normalization
  sahibidir.
- `Dobby.Shell` route hosting, visual navigation state və fallback orchestration
  sahibidir.
- `Dobby.Security` authorization decision sahibidir; Navigation və Shell
  authorization məntiqini duplicate etmir.
- Plugin yalnız manifest/SDK descriptor vasitəsilə route təklif edir; host
  internal view model, WPF control və ya page type reference-i plugin-visible
  contract deyil.

### 3. Login boundary

Login success-dən əvvəl only login-visible internal navigation mövcuddur. Plugin
routes login öncəsi resolve və activate edilmir. Persisted last-active module
startup restore üçün istifadə olunmur; login success-dən sonra shell Home-dan
başlayır.

### 4. Deep link scope

Phase 1 external OS protocol/deep link registration optional/future scope-dur.
P1 skeleton yalnız internal route modelini yaratmalıdır. Əgər external link gələcəkdə
açılarsa, link login-dən əvvəl authenticated route-u birbaşa açmır; route intent
post-login security gate-dən keçdikdən sonra host edilir və fail olarsa Home fallback
işləyir.

### 5. Invalid route fallback

Persisted, contributed və ya requested route resolve edilə, authorization-dan keçə
və ya aktivləşdirilə bilmirsə:

- shell fail-closed şəkildə Home-a qayıdır;
- stale preference safe default-a normalize edilir və ya təmizlənir;
- diagnostics warning correlation id ilə yazılır;
- protected operation cəhdi yoxdursa audit event yaranmır.

## Consequences

- Shell skeleton route registry və Home/Plugins/Settings route-larını deterministik
  yarada bilər.
- Plugin page hosting raw UI type-lara yox, host-normalized descriptor-lara bağlanır.
- External deep link polishing P2/P3-ə qala bilər, amma internal route model P1-də
  sabitdir.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 52 `Routing / Deep Link Handling`
- `docs/specs/shell-ui.md` — Navigation model və fallback behavior
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`
- `docs/adr/ADR-0048-operation-based-authorization.md`
- `docs/adr/ADR-0041-login-first-startup-policy.md`
