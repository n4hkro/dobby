# ADR-0005: UI Ordering Model

**Status:** Accepted
**Date:** 2026-04-22

## Context

UI extension ordering Dobby shell və plugin composition üçün kritik contract
davranışıdır. Arxitektura xəritəsində bu model həm risk sətrində, həm də
engineering convention-da eyni dillə təkrar olunur: default ordering təsadüfi
deyil və `group + weight + stable tiebreak` qaydası ilə işləməlidir.

Owner qərarı ilə Sidebar və Third Bar elementləri P1-də drag/drop edilə bilər,
bu sıra hər user üçün ayrıca saxlanır və default sıraya qaytarıla bilir. Bu
ADR default ordering modelini və user override boundary-sini birlikdə bağlayır.

Bu mərhələdə məqsəd public ordering modelini sabitləşdirməkdir; concrete
interface method adları və exact contract type shape-i də P1 skeleton-dan əvvəl
bu ADR-də bağlanır.

## Decision

Dobby UI ordering modeli deterministic total ordering key üzərində qurulur və
bu açar üç hissədən ibarətdir:

- `group`
- `weight`
- `stable tiebreak`

Bu üçlük ordering contract-ının canonical modelidir. Hər UI extension eyni
model üzrə müqayisə olunur; shell və digər consumer-lər ayrıca ad-hoc ordering
qaydası ixtira etmir.

Comparator semantikası Phase 1-də bu şəkildə bağlanır:

- `weight` numerik ascending müqayisə olunur
- omitted `weight` comparer baxımından normalized `100` sayılır
- `stable tiebreak` eyni normalized `weight` üçün bu ardıcıllıqla müqayisə
  olunur:
  1. `pluginId` ordinal (culture-invariant) ascending
  2. həmin plugin-in manifestində extension declaration index ascending

Phase 1 `group` davranışı:

- omitted `group` comparer baxımından `default` sayılır
- explicit `group: default` qəbul olunur
- explicit non-`default` `group` Phase 1-də dəstəklənmir

Bu semantikanın məqsədi eyni input üçün eyni visible order-in deterministik
isbat oluna bilməsidir.

### User override boundary

Default order yuxarıdakı comparator ilə hesablanır. User override yalnız aşağıdakı
səthlərdə P1 scope-dadır:

- Sidebar item order;
- Third Bar item order;
- TopToolbar-da Home və Settings arasında yerləşən plugin/top-level entries üçün
  user ordering/hidden state.

Home heç vaxt gizlədilmir və yeri dəyişdirilmir; həmişə birinci qalır. Settings
heç vaxt gizlədilmir və yeri dəyişdirilmir; həmişə sonuncu qalır.

User override per-user preference kimi saxlanır. Default sıraya qaytar action-ı
override-ları təmizləyir və yenidən canonical comparator nəticəsinə qayıdır.

### P1 public ordering contract shape

Ordering contract `Dobby.Contracts` altında aşağıdakı public names ilə bağlanır:

```csharp
public sealed record UiOrderingKey(
    string Group,
    int Weight,
    string PluginId,
    int DeclarationIndex);

public interface IUiOrderingComparer
{
    int Compare(UiOrderingKey left, UiOrderingKey right);
}
```

`UiOrderingKey.Group` omitted manifest `group` üçün normalized `default` daşıyır.
`UiOrderingKey.Weight` omitted manifest `weight` üçün normalized `100` daşıyır.
`PluginId` ordinal/culture-invariant müqayisə olunur. `DeclarationIndex`
manifest `extensionPoints` array index-idir və eyni plugin/weight üçün stable
tiebreak kimi istifadə olunur.

Shell və plugin framework bu comparer semantics-indən kənar ad-hoc ordering
yaratmır. User override-ları bu canonical default order-in üzərinə tətbiq olunan
per-user preference-dir; override təmizlənəndə comparer nəticəsinə qayıdılır.

## Consequences

- UI composition order artıq discovery və ya iteration təsadüfünə buraxılmır
- Eyni input üçün visible order deterministik və snapshot-da yoxlanıla biləndir
- `Dobby.Contracts` comparer-i və render qatı eyni semantika ilə işləyir;
  ad-hoc alternativlər qadağandır
- User drag/drop override default comparator-u silmir; yalnız per-user preference
  kimi tətbiq olunur
- Home və Settings fixed anchor invariant-ları shell render və persistence
  tərəfindən qorunmalıdır
- Real multi-group model gələcək ayrıca ADR ilə açılır; Phase 1 yalnız
  `default` group üzərində bağlanır
- Ordering modelində gələcək dəyişiklik ayrıca breaking-change review tələb edir
- API naming və comparer implementation bu public contract shape-ə uyğun qalır
  və artıq bu modelə zidd ola bilməz

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `UI Composition Ordering Contracts` row
- `docs/02_ARCHITECTURE_MAP.md` — `ADR-0005` risk sətri
- `docs/02_ARCHITECTURE_MAP.md` — `UI Extension Ordering` convention
- `docs/adr/ADR-0028-shell-composition-ordering-render.md` — render enforce
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `group` və `weight` üçün
  manifest normalization ownership-i
