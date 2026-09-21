# ADR-0049: Hierarchical Navigation Contribution Model

**Status:** Accepted (§ `Plugin page/main-content runtime contract` minimum page/lifecycle surface amended by `ADR-0059` — see ## Revision History)
**Date:** 2026-04-25

## Context

`ADR-0036` plugin manifest extension target vocabulary-ni closed set kimi bağlayır: `top-toolbar`, `sidebar`, `third-bar`, `plugin-page-region`. Mövcud shell composition modelində `TopToolbar + Sidebar + Third Bar` var, amma declarative parent/child navigation contribution shape-i formal bağlanmamışdı.

Owner qərarı aktiv plugin-lərin TopToolbar-da görünməsini, seçilmiş top-level item-in
child-larının Sidebar-a, seçilmiş Sidebar item-in daha dərin child-larının isə
Third Bar-a düşməsini istəyir. Product sənədlərində DTO dili yox, TopToolbar,
Sidebar, Third Bar və plugin page region target dili istifadə olunmalıdır.

Hierarchical navigation faydalıdır, amma target vocabulary-ni açıq registry-yə çevirməməlidir.

## Decision

Built-in və plugin navigation contribution-ları hierarchical descriptor modeli ilə shell-ə qoşulur.

Descriptor semantics:

- contribution öz target-ini `ADR-0036` closed vocabulary-dən seçir;
- contribution optional parent reference daşıya bilər;
- child contribution yalnız öz parent node-un child list-ində sıralanır;
- ordering `group + weight + stable tiebreak` modelidir və hər parent node daxilində ayrıca tətbiq olunur;
- stable tiebreak `ADR-0005` və `ADR-0028` semantics-inə uyğun qalır.

Render mapping:

- TopToolbar top-level built-in və active plugin entries üçün primary target-dir.
- Selected TopToolbar contribution child-ları Sidebar-da görünür.
- Selected Sidebar contribution child-ları Third Bar-da görünür.
- Deeper UI composition və plugin-owned inner regions `plugin-page-region` target-i
  ilə saxlanır.
- Home fixed first, Settings fixed last qalır; plugin entries bu anchor-lar arasında
  yerləşir və user override qaydası `ADR-0005` ilə idarə olunur.

Closed target vocabulary dəyişmir:

- `top-toolbar`
- `sidebar`
- `third-bar`
- `plugin-page-region`

`plugin-page-region` target-i itmir və plugin page daxilində composition üçün saxlanılır.

### Plugin page/main-content runtime contract

Navigation descriptor-ları page hosting üçün yetərli deyil; ona görə bu ADR Phase 1
skeleton-un plugin-visible minimum runtime contract-ını da bağlayır. Contract
`Dobby.Contracts / Plugin Pages` altında qalır və `ADR-0006` registration modeli ilə
qoşulur.

Bu bölmənin cari authority-si `ADR-0059` § `Declarative host-rendered UI v2`-dir: plugin
page surface-inin declarative host-rendered component tree-yə, host-resolved plugin
localization-a və schema/version negotiation-a keçidi orada qərara alınıb, lifecycle
hook-larının async/cancellable və bounded-timeout forması isə `ADR-0059`
§ `Compatibility and lifecycle`-dədir. Aşağıdaki blok həmin amendment-dən əvvəlki
minimum surface-i və bu ADR-in dəyişməyən binding qaydalarını (host-owned render,
`Result` boundary, lifecycle sırası, host-authoritative identity) sənədləşdirir; yeni
plugin page işi üçün `ADR-0059` ilə birlikdə oxunur və konflikt halında `ADR-0059`
üstündür.

Canonical skeleton surface:

```csharp
namespace Dobby.Contracts.PluginPages;

public sealed record PluginPageDescriptor(
    string ExtensionPointId,
    string RouteKey,
    string LabelKey);

public sealed record PluginPageContext(
    string PluginId,
    string ExtensionPointId,
    string RouteKey,
    string CorrelationId,
    string? UserId = null);

public interface IPluginPageModel
{
}

public interface IPluginPage
{
    PluginPageDescriptor Descriptor { get; }

    ValueTask<Result<IPluginPageModel>> LoadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);

    ValueTask<Result> ActivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);

    ValueTask<Result> DeactivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);

    ValueTask<Result> UnloadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken);
}
```

Binding rules:

- `PluginPageDescriptor.ExtensionPointId` manifest `extensionPoints[].id` ilə match etməlidir.
- `PluginPageDescriptor.RouteKey` manifest `extensionPoints[].routeKey` və `ADR-0013` route identity ilə match etməlidir.
- `PluginPageContext.PluginId` host-authenticated plugin identity-dən gəlir; plugin bu dəyəri input kimi diktə etmir.
- `PluginPageContext.UserId` authenticated session-dan gələn host-authoritative dəyərdir;
  plugin onu input kimi diktə edə, override edə və ya spoof edə bilmir. Yalnız post-login
  page activation-da doldurulur, ona görə nullable-dır: null və ya boş dəyər authorization
  üçün fail-closed-dur -- host command boundary belə request-i `Unauthorized` ilə rədd edir
  və operation authorization `ADR-0048` `PolicyContext` üzərindən bu identity ilə
  qiymətləndirilir. Dəyər opaque identifier kimi ötürülür: plugin onu authorization
  qərarının mənbəyi kimi yenidən istifadə etmir, host qərarını təkrar şərh etmir və bu
  field plugin-visible tenant/customer runtime primitive-i açmır (`ADR-0052` həmin
  primitive-lərin plugin-visible olmadığını qərara alır). Plugin-facing surface-də user
  identity-nin göstərilməsi və ya audit payload-a əlavə edilməsi bu ADR-in icazəsi deyil;
  audit ownership `ADR-0019` və `ADR-0061`-dədir.
- Plugin page implementation `ADR-0006` ilə `AddExtensionPoint<IPluginPage, TPluginPage>()` formasında register olunur.
- Lifecycle hook-ları `Result` / `Result<T>` boundary istifadə edir; expected/domain/infrastructure failure raw exception kimi shell-ə sızmır.
- Plugin manifest yalnız navigation/route metadata elan edir. Manifest `viewKey`,
  raw WPF view type, assembly-qualified type adı və ya host internal view-model
  identity daşımır.
- Plugin page code-u `LoadAsync(...)` nəticəsi kimi yalnız approved
  `IPluginPageModel` contract type-ını `Result<IPluginPageModel>` ilə qaytarır.
- WPF render host-owned qalır: `Dobby.Shell` / `Dobby.UI` host-owned page render
  registry-si vasitəsilə approved `IPluginPageModel` contract type-larını host
  views-lara map edir. Plugin `UserControl`, `Window`, `FrameworkElement`, XAML
  path, assembly-qualified WPF view type name və ya host implementation type
  qaytarmır.
- `viewKey` manifest schema-ya və `IPluginPageModel`-ə əlavə edilmir; manifest
  schema (indi controlling `docs/schemas/dobby-plugin-manifest-v2.schema.json`,
  frozen v1 `docs/schemas/dobby-plugin-manifest.schema.json`) bu qərara görə
  dəyişmir.
- Lifecycle order: `LoadAsync` bir dəfə first activation-dan əvvəl çağırılır;
  `ActivateAsync` / `DeactivateAsync` eyni page lifetime-da təkrar çağırıla bilər
  və idempotent olmalıdır; `UnloadAsync` final deactivate-dən sonra bir dəfə
  çağırılır.
- Contract stability tier `evolving`-dir, amma `Dobby.Contracts` public surface-i olduğu üçün breaking change yalnız ADR/release review ilə edilə bilər.

## Consequences

- `Settings > Settings 2 > Settings 2.1` kimi nested navigation declarative əlavə oluna bilər.
- Shell layout code-u hər yeni nested item üçün dəyişməməlidir.
- Ordering global flat list kimi yox, per-parent child list üzrə tətbiq olunur.
- Manifest target vocabulary genişlənmir; `ADR-0036` closed-set qaydası qorunur.
- Third Bar seçilmiş Sidebar item-in daha dərin alt səviyyəsi kimi formal bağlanır.
- Active plugin top-level visibility TopToolbar contribution modelinin hissəsi olur.
- Persisted və ya contributed navigation route resolve edilə, authorization-dan keçə və ya aktivləşdirilə bilmirsə, shell fail-closed şəkildə Home-a qayıdır və diagnostic warning yazır; protected operation cəhdi yoxdursa audit event yaranmır.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — Login Sonrası Shell
- `docs/02_ARCHITECTURE_MAP.md` — Row 51 `Navigation Composition`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-019`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-023`
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — plugin page contracts live in plugin-visible contract surface
- `docs/adr/ADR-0005-ui-ordering-model.md`
- `docs/adr/ADR-0006-plugin-di-registration.md` — `AddExtensionPoint<TContract, TImpl>()` registration model
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — host-owned rendering and plugin capability boundary
- `docs/adr/ADR-0028-shell-composition-ordering-render.md`
- `docs/adr/ADR-0036-plugin-manifest-schema.md`
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — current authority for the
  § `Plugin page/main-content runtime contract` surface (declarative host-rendered UI v2,
  async/cancellable bounded-timeout lifecycle)
- `docs/adr/ADR-0048-operation-authorization-model.md` — `PluginPageContext.UserId` feeds the
  operation authorization policy context
- `docs/specs/sample-plugin.md` — `AC-036` Main Content hosting verification artifact
- `docs/specs/shell-ui.md`

## Revision History

- **2026-08-31:** Amendment record yazıldı və relation vocabulary düzəldildi.
  `**Status:**` sətri "minimum page/lifecycle portions superseded by ADR-0059" deyirdi,
  halbuki README bu sözü yalnız tam supersession üçün ayırır və öz Supersession Index-ində
  bu ADR-i `Accepted (partial)` kimi sıralayır. Bu sənəd hierarchical navigation
  contribution modeli, qapalı target vocabulary-si, per-parent ordering və toolbar
  qaydaları üçün authority olaraq qalır; `ADR-0059` yalnız § `Plugin page/main-content
  runtime contract` minimum surface-ini amend edir. Dated bölmə yox idi, `ADR-0059`
  `## Cross-references`-də adlanmırdı və `ADR-0059` bu ADR-i geri claim etmirdi -- üçü də
  bağlandı.
- **2026-08-31 (2):** `PluginPageContext.UserId` documented və governed. Field `5b598aa`
  commit-indən bəri shipped idi (`src/Dobby.Contracts/PluginPages/PluginPageContracts.cs`
  beş parametrli record), lakin bu ADR-in canonical bloku dörd parametr göstərirdi və heç
  bir ADR, spec, FR, NFR və ya AC field-i idarə etmirdi. Bu, security-relevant boundary-də
  documentation gap idi: host-un authenticated session `UserId`-si third-party plugin
  code-a ötürülür və o, fail-closed authorization precondition-dır --
  `HostPluginCommandDispatcher` boş dəyəri `ErrorCode.Unauthorized` ilə rədd edir və
  `ADR-0048` `PolicyContext`-ini həmin identity ilə qurur. Kod və green test contradictory
  product decision üçün authority olmadığından qayda implementation-dan səssiz nəticə
  çıxarılmaqla deyil, contract owner-i olan bu ADR-də açıq yazıldı; `ADR-0052`-nin
  "tenant/customer runtime primitive-ləri plugin-visible deyil" scope-u ilə uzlaşdırıldı.
