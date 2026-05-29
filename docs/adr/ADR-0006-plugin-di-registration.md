# ADR-0006: Plugin DI Registration

**Status:** Accepted
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

`IPluginModule` Phase 1-in yeganə plugin lifecycle contract-ıdır. Service registration və post-load activation hook-u bu contract-a məxsusdur:

- `RegisterServices(...)` plugin service-lərini activation-dan əvvəl declare edir.
- `IPluginModule.OnActivated(...)` compatibility, license, assembly load və service registration uğurla tamamlandıqdan sonra işləyir.

Phase 1 ayrıca plugin lifecycle interface-i və ayrıca disable/uninstall hook-u define etmir. Disable/uninstall cleanup plugin service lifetime disposal və PluginFramework state transition-ları ilə idarə olunur.

### 1a. P1 activation context shape

`IPluginActivationContext` Phase 1-də yalnız activation üçün lazım olan identity
və correlation məlumatını daşıyır:

```csharp
public interface IPluginActivationContext {
    string PluginId { get; }
    string PluginVersion { get; }
    CorrelationId CorrelationId { get; }
}
```

- `PluginId` manifest `pluginId` field-inin projection-ıdır.
- `PluginVersion` manifest `version` field-inin projection-ıdır.
- `CorrelationId` host correlation context-indən gələn `ADR-0029` primitive-idir.

Bu dəyərlər plugin-supplied ayrı runtime source deyil; manifest və host
correlation context-dən derive olunur. Activation context `CancellationToken`,
capability handle, `IServiceProvider`, host container reference-i və ya internal
host service locator daşımır.

### 1.1 Lifecycle Signature Rationale

P1 `IPluginModule.RegisterServices(...)` və `IPluginModule.OnActivated(...)`
hook-larını synchronous saxlayır ki, ilkin plugin lifecycle surface kiçik qalsın.

Bu hook-lar sürətli olmalıdır. Onlar long-running I/O, blocking network work,
migration, user-scoped background work və ya sənədləşdirilmiş activation step-dən
kənar storage work icra etməməlidir. Generic plugin exception-to-result mapping
`docs/04_RESULT_VS_EXCEPTION_POLICY.md` tərəfindən idarə olunur; bu ADR həmin
mapping-i təkrar etmir.

Bu P1 hook-ları synchronous olduğuna və `CancellationToken` qəbul etmədiyinə görə
başlamış `RegisterServices(...)` və ya `OnActivated(...)` invocation host
tərəfindən preemptively dayandırılmır. Session-end cancellation activation
operation boundary-lərində müşahidə olunur: step başlamazdan əvvəl, step-lər
arasında və ya step qayıtdıqdan sonra.

Reopen only if a required plugin needs async or cancellable activation that
cannot be background-deferred.

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
- Plugin author developer experience: plugin template `IPluginModule` skeleton-u ilə gəlir
- Plugin uninstall audit flow-unda DI təmizlənməsi də logged olur (FR-011 audit scope)

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 25 `Plugin Framework`, Row 30 `Plugin Service Registration Bridge`
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts-a əlavə olunan `IPluginModule` surface
- `docs/adr/ADR-0007-plugin-load-context-strategy.md` — plugin AssemblyLoadContext isolation (bu ADR ilə birlikdə işləyir)
- `docs/adr/ADR-0010-plugin-communication-policy.md` — plugin-plugin service access qadağası
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — host service-ləri plugin-ə yalnız capability surface ilə görünür
