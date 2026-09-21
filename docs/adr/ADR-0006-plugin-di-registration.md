# ADR-0006: Plugin DI Registration

**Status:** Accepted (minimum page/lifecycle portions amended by `ADR-0059` — see ## Revision History)
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Row 30 `Plugin Service Registration Bridge` Phase 1-də P1-core statusda idi, amma plugin service-lərinin host DI container-ə necə qoşulduğu, lifetime qaydaları və namespace collision protection-ı canonical olaraq bağlanmamışdı.

Açıq suallar:
- Plugin-in öz service-lərini register etmək səthi nədir?
- Plugin service-lərinin lifetime-ı necə hesablanır (host singleton ≠ plugin singleton)?
- Plugin-in həm host service-lərini implement-ləyən (contract registration override), həm də özünəməxsus internal service-ləri olur — bu ikisi necə ayrılır?
- Plugin disable/uninstall zamanı DI scope təmizlənməsi necə işləyir?

## Decision

### 1. `IPluginModule` registration surface

Hər plugin `Dobby.Contracts / PluginFramework` altındakı `IPluginModule` interface-ini implement edir:

```csharp
public interface IPluginModule {
    void RegisterServices(IPluginServiceCollection services);
    void OnActivated(IPluginActivationContext context);
}

public interface IPluginServiceCollection {
    // Plugin internal services (plugin private scope)
    IPluginServiceCollection AddInternal<TService, TImpl>(PluginLifetime lifetime)
        where TService : class where TImpl : class, TService;

    // Host contracts implementation (extension points)
    IPluginServiceCollection AddExtensionPoint<TContract, TImpl>()
        where TContract : class where TImpl : class, TContract;
}

public enum PluginLifetime {
    Singleton,   // per plugin instance (not host singleton)
    Scoped,      // per-request scope
    Transient    // new instance per resolution
}
```

- `AddInternal` plugin-in öz private service-lərini register edir — host container-ə **görünmür**
- `AddExtensionPoint` plugin-in host contract-ını (extension point) implement-lədiyini bildirir — host registry-ə əlavə olunur

Service registration səthi bu ADR-in qərarıdır və dəyişməyib: `RegisterServices(...)`
plugin service-lərini activation-dan əvvəl declare edir və synchronous qalır, çünki o
yalnız descriptor toplayır — heç bir I/O icra etmir.

Lifecycle isə iki-tier contract-dır (`ADR-0037`, `ADR-0059`; bu ADR-in
"`IPluginModule` Phase 1-in yeganə plugin lifecycle contract-ıdır" iddiası artıq
qüvvədə deyil):

```csharp
public interface IPluginModuleV2 : IPluginModule {
    ValueTask<Result> InitializeAsync(IPluginActivationContext context, CancellationToken cancellationToken);
    ValueTask<Result> ActivateAsync(IPluginActivationContext context, CancellationToken cancellationToken);
    ValueTask<Result> DeactivateAsync(CancellationToken cancellationToken);
    ValueTask<Result> UnloadAsync(CancellationToken cancellationToken);
}
```

- `IPluginModuleV2` canonical lifecycle contract-ıdır: hər phase host-controlled
  cancellation token alır və bounded timeout altında işləyir; `Result` failure,
  external cancellation, timeout və plugin exception-ı ayrı typed nəticələrdir
  (`ADR-0037`).
- `IPluginModule.OnActivated(...)` yalnız v1 adapter window-una aiddir. V2 module-lar
  üçün host activation-ı həmişə `ActivateAsync(...)` ilə dispatch edir və
  `PluginBaseV2.OnActivated` no-op-dur (`ADR-0021`). V1 adapter istifadəsi
  `legacy-v1-adapter-upgrade-required` warning-i və `plugin.lifecycle.v1-adapter-used`
  audit event-ini yaradır və növbəti host API major gate-də silinir (`ADR-0037`).
- Deactivate/unload üçün ayrı hook mövcuddur (`DeactivateAsync` / `UnloadAsync`) və
  logical cleanup — registration, subscription, work session və extension point
  removal — həmin phase-lərdə baş verir. Plugin assembly load context-i isə
  process-lifetime və non-collectible qalır (`ADR-0007`); DI cleanup plugin service
  lifetime disposal-ı ilə (§2) tamamlanır.

### 1a. Activation context shape

`IPluginActivationContext` identity/correlation məlumatını və §2a
(`ADR-0012`) capability set-inin activation-time hissəsini daşıyır:

```csharp
public interface IPluginActivationContext {
    string PluginId { get; }
    string PluginVersion { get; }
    CorrelationId CorrelationId { get; }
    IEventBus EventBus { get; }
    IBackgroundOperationReporter BackgroundOperations { get; }
    IBackgroundOperationCancellation BackgroundOperationCancellation { get; }
}
```

- `PluginId` manifest `pluginId` field-inin projection-ıdır.
- `PluginVersion` manifest `version` field-inin projection-ıdır.
- `CorrelationId` host correlation context-indən gələn `ADR-0029` primitive-idir.
- `EventBus` per-plugin subscription lease üzərində scoped `IEventBus`-dur, host-un
  raw bus instance-ı deyil (`ADR-0010`, `ADR-0012` §2a).
- `BackgroundOperations` və `BackgroundOperationCancellation` host-owned shared
  instance-lardır: birinci progress attribution daşıyır, ikinci host-lifetime
  cancellation token verir (`ADR-0015`).

Bu dəyərlər plugin-supplied ayrı runtime source deyil; manifest, host correlation
context və host capability factory-lərindən derive olunur. Activation context
`IServiceProvider`, host container reference-i, service locator və `ADR-0012` §2a
list-indən kənar heç bir host type daşımır.

### 1.1 Lifecycle Signature Rationale

`RegisterServices(...)` synchronous qalır: o yalnız descriptor toplayır, I/O icra
etmir, ona görə cancellation ora heç nə əlavə etməzdi.

`IPluginModuleV2` phase-ləri (`InitializeAsync`, `ActivateAsync`, `DeactivateAsync`,
`UnloadAsync`) `ValueTask<Result>` qaytarır və hər biri host-controlled
`CancellationToken` alır; timeout və cancellation semantikası `ADR-0037`-dədir.
Uzun sürən iş bu phase-lərin içində icra olunmur — o host-owned background operation
contract-ına verilir və host-lifetime token-i ilə cancel olunur (`ADR-0015`).

V1 `IPluginModule.OnActivated(...)` synchronous və non-cancellable-dır; ona görə
başlamış v1 invocation preemptively dayandırılmır və session-end cancellation yalnız
operation boundary-lərində müşahidə olunur: step başlamazdan əvvəl, step-lər arasında
və ya step qayıtdıqdan sonra. Bu davranış v1 adapter window-unun məlum
məhdudiyyətidir, hədəf model deyil — V2-yə keçid onu aradan qaldırır (`ADR-0037`).

Generic plugin exception-to-result mapping `docs/04_RESULT_VS_EXCEPTION_POLICY.md`
tərəfindən idarə olunur; bu ADR həmin mapping-i təkrar etmir.

### 2. Lifetime semantics

- `PluginLifetime.Singleton` = plugin lifetime (plugin disable/unload zamanı instance dispose olunur), host singleton yox
- `PluginLifetime.Scoped` = host scope ilə sync (məs., HTTP request scope host-da səhifə üçün yaradılırsa plugin scoped service həmin scope-da yaşayır)
- `PluginLifetime.Transient` = həmişə yeni instance

Plugin singleton **host DI root-a yazılmır**. Plugin lifetime-ı `PluginLifetimeManager` (Row 25 Plugin Framework) tərəfindən idarə olunur; plugin disable zamanı həmin plugin üçün yaradılan bütün `Singleton` service instance-ları `IDisposable` chain-i ilə təmizlənir.

### 3. Host ↔ plugin DI boundary

- Plugin private service-lərinə host birbaşa çıxa bilməz (capability boundary ADR-0012)
- Plugin host service-lərinə yalnız `Dobby.Contracts` və `Dobby.SDK` üzərindən çıxır (NFR-008)
- Plugin başqa plugin-in service-lərinə birbaşa çıxa bilməz (ADR-0010 plugin communication policy)

### 4. Contract registration konflikti

Plugin-in register etdiyi `IExtensionPoint<TContract>` host-ın built-in implementation-ı ilə konflikt **yarada bilər**. Resolution:
- Host built-in implementation-ları həmişə mövcuddur
- Plugin extension point implementation-ları ayrıca registry-də saxlanılır (multiple plugins eyni contract-ı implement-ləyə bilər — ordering `ADR-0005` ilə idarə olunur)
- Replacement semantic-i (plugin host built-in-i override edir) Phase 1-də dəstəklənmir

### 5. Rejected alternatives

- **Plugin birbaşa `IServiceCollection`-ə çıxış:** rədd edildi — host DI root-a plugin yazsa, plugin disable zamanı təmizləmək problem olar
- **Plugin DI-i tamamilə host scope-dan ayırsın (öz child container):** rədd edildi — host contract injection çətinləşər
- **`PluginLifetime.Singleton` host singleton olsun:** rədd edildi — plugin disable zamanı host singleton dispose etmək yanlışdır

## Consequences

- `Dobby.Contracts / PluginFramework / IPluginModule` public surface-ə əlavə olunur
- `Dobby.PluginFramework / Plugin Service Registration Bridge` (Row 30) implementation bu contract-ı host DI-yə bağlayır
- `Dobby.PluginFramework / Plugin Framework` (Row 25) plugin lifetime manager-i `PluginLifetime.Singleton` instance-larının dispose-unu təmin edir
- Plugin author developer experience: sample/template plugin `PluginBaseV2` skeleton-u ilə gəlir (`ADR-0021`); yalnız `IPluginModule` implement edən plugin v1 adapter window-una düşür və upgrade warning-i alır (`ADR-0037`)
- Plugin uninstall audit flow-unda DI təmizlənməsi də logged olur (FR-011 audit scope)

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 25 `Plugin Framework`, Row 30 `Plugin Service Registration Bridge`
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts-a əlavə olunan `IPluginModule` surface
- `docs/adr/ADR-0007-plugin-load-context-strategy.md` — plugin AssemblyLoadContext isolation (bu ADR ilə birlikdə işləyir)
- `docs/adr/ADR-0010-plugin-communication-policy.md` — plugin-plugin service access qadağası
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — §2a qapalı host-injectable capability set
- `docs/adr/ADR-0015-background-operation-reporting.md` — background operation reporting və host-lifetime cancellation
- `docs/adr/ADR-0021-sdk-surface-area.md` — `PluginBase` / `PluginBaseV2` base class shape
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — `IPluginModuleV2` lifecycle, bounded timeout, v1 adapter window
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — bu ADR-in minimum page/lifecycle hissələrini amend edir (bu ADR `Accepted` qalır və toxunulmamış bölmələri üçün authority-dir)

## Revision History

- **2026-08-27:** §1-in "`IPluginModule` Phase 1-in yeganə plugin lifecycle
  contract-ıdır" və "Phase 1 ayrıca disable/uninstall hook-u define etmir"
  iddiaları, §1a-nın üç-üzvlü activation context bloku və §1.1-in "hook-lar
  `CancellationToken` qəbul etmir" qaydası shipped contract-a uyğun deyildi:
  `IPluginModuleV2 : IPluginModule` async `InitializeAsync`/`ActivateAsync`/
  `DeactivateAsync`/`UnloadAsync` phase-lərini per-phase `CancellationToken` ilə
  təqdim edir və `IPluginActivationContext` altı üzv daşıyır (`IEventBus`,
  `IBackgroundOperationReporter`, `IBackgroundOperationCancellation` daxil).
  Yanlış mətn iki real risk yaradırdı: plugin author-ları cancellation-ın
  mövcud olmadığını fərz edib uzun işi non-cancellable hook-da icra edə bilərdi, və
  activation context-in host capability daşımadığı fərziyyəsi `ADR-0012` §2a
  boundary auditini natamam edərdi. §1 iki-tier contract-ı, §1a real context
  shape-ini və hər üzvün scoping-ini, §1.1 isə v1 (sync, non-cancellable adapter
  window) ilə V2 (async, per-phase token, bounded timeout) arasındaki fərqi explicit
  edir. Cavablanmış "Reopen only if a required plugin needs async or cancellable
  activation" bəndi silindi, çünki həmin ehtiyac `ADR-0037` ilə artıq
  qərarlaşdırılıb. Qərar dəyişmir — lifecycle `ADR-0037`-də qərarlaşıb və bu ADR-in
  lifecycle hissələri onsuz da `ADR-0059` ilə amend olunub, ona görə successor
  ADR yaradılmır; bu factual contract reconciliation-dır. Registration surface,
  lifetime semantics (§2), host↔plugin boundary (§3) və conflict resolution (§4)
  dəyişməz qalır.
