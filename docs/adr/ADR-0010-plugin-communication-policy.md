# ADR-0010: Plugin Communication Policy

**Status:** Accepted
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Engineering Conventions bölməsində `Plugin-to-Plugin Əlaqə` qaydası var: `pluginlər bir-birinə birbaşa referans vermir, əlaqə yalnız public contract və IntegrationEvent üzərindən qurulur`. Cədvəl 3 `Plugin-to-Plugin Communication Policy` sətri bu qaydanı Phase 1 blocker kimi göstərir və `ADR-0010 Plugin communication policy` ADR-ini tələb edir.

Bu ADR həmin convention-u enforcement-lə bağlayır və plugin-to-plugin, plugin-to-host, host-to-plugin communication-ların icazəli kanallarını rəsmiləşdirir.

## Decision

Phase 1 plugin communication policy:

### 1. İcazəli kanallar

Plugin aşağıdakı üç kanal vasitəsilə digər plugin-lərlə və ya host ilə ünsiyyət qura bilər:

1. **Public contract interfaces** (`Dobby.Contracts`): Plugin `Dobby.Contracts` altında elan olunmuş interface-i implement edir və ya consume edir. Bu interface-lər dar və explicit saxlanır (`ADR-0001` surface stability qaydası).
2. **Integration events** (`Dobby.Messaging / IEventBus`): Plugin `IEventBus` vasitəsilə `IntegrationEvent` publish edir və ya subscribe olur. Event contract-ları `Dobby.Contracts / Event Contracts` namespace-ində elan olunur.
3. **Extension points** (manifest `extensionPoints`): Plugin shell composition üçün host-un elan etdiyi extension point-lərə qoşulur; direct UI object reference yoxdur.

### 2. Qadağan olunan kanallar

- **Direct plugin-to-plugin və host-runtime reference**: Plugin A `.csproj` faylında plugin B-nin assembly-sinə və ya host implementation project-lərinə (`Dobby.Host`, `Dobby.Security`, `Dobby.PluginFramework` və s.) direct reference saxlamır. Plugin öz daxili dependency-lərini paketlə daşıya bilər, amma bu dependency-lər public Dobby boundary-ni bypass etmək üçün istifadə oluna bilməz.
- **Reflection-based type discovery across plugins**: Plugin A reflection vasitəsilə plugin B-nin internal type-larına çatmağa çalışmamalıdır; plugin load context isolation (`ADR-0007`) bunun qarşısını qismən alır, amma bu ADR bunu policy səviyyəsində də qadağan edir.
- **Shared static state**: Plugin A və plugin B ortaq static field və ya singleton istifadə etməməlidir; state exchange yalnız event və ya contract üzərindən olur.
- **Filesystem-based backdoor**: Plugin A plugin B-nin storage area-sına direct file access edə bilmir; bütün storage access `IPluginStorage` contract-ından keçir (`ADR-0030`).

### 3. Event visibility səviyyələri

Event-lər üç səviyyədə fərqləndirilir (Architecture Map engineering convention-ı ilə uyğun):

- `internal host events`: yalnız host daxilində; plugin visibility yoxdur.
- `public integration events` (`IntegrationEvent`): plugin-visible; `Dobby.Contracts / Event Contracts` altında elan olunur.
- `UI-local events`: yalnız UI thread və shell/built-in UI orchestration daxilindədir; `WeakReferenceMessenger` və ya UI bridge implementation detail-i istifadə oluna bilər, amma plugin bu event-ləri publish/subscribe etmir.

Plugin yalnız `public integration events`-ləri publish/subscribe edə bilər.

Bu ADR event visibility modelinin canonical owner-idir; ayrıca ADR-0002 yaradılmır.
Event contract type-i plugin-visible olacaqsa, həmin type `Dobby.Contracts / Event
Contracts` namespace-ində olmalıdır və `ADR-0001` public surface review-undan keçir.
Host-internal və UI-local event-lər Contracts-a daşınmır.

### 3.1 P1 public event bus contract shape

P1 plugin-visible event bus shape async publish/subscribe və explicit
subscription cleanup discipline-i daşıyır:

```csharp
public abstract record PublicIntegrationEvent {
    public CorrelationId CorrelationId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public interface IEventBus {
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : PublicIntegrationEvent;

    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : PublicIntegrationEvent;
}
```

Minimum guarantees:

- `Subscribe(...)` return etdiyi `IDisposable` dispose olunanda həmin
  subscription cleanup edilir.
- Eyni subscriber üçün handler invocation-ları per-event ardıcıllığını qoruyur:
  əvvəlki call qayıtmadan həmin subscriber-ə növbəti call verilmir.
- Bir subscriber handler exception-u digər subscriber-lərin delivery-sini
  pozmur; failure diagnostics-a düşür və event bus fail-closed isolation saxlayır.
- Plugin yalnız `PublicIntegrationEvent` event-lərini publish/subscribe edir;
  host-internal və UI-local event-lər plugin-visible surface-ə çıxmır.

Cross-subscriber scheduling, parallelism, priority və backpressure detalları P1
contract surface-də pinlənmir və implementation-owned qalır.

### 3a. UI dispatch və Messaging UI Bridge

Messaging UI Bridge public plugin channel deyil. Integration event UI feedback-ə
çevrilməlidirsə, `Dobby.Messaging` event-i host daxilində qəbul edir, `Dobby.Shell`
və ya owning UI service isə `IUiDispatcher` / WPF dispatcher adapter-i ilə UI thread-ə
keçirir.

P1 `IUiDispatcher` host-internal abstraction-dır və plugin-visible Contracts/SDK
surface-inə çıxmır. Minimal surface:

`IUiDispatcher` abstraction-ının canonical project/namespace placement-i
`Dobby.Messaging` / `Dobby.Messaging.UiDispatch`-dır. WPF-specific
implementation və `Application.Current.Dispatcher` adapter wiring-i
`Dobby.Shell` ownership-ində qalır.

```csharp
public interface IUiDispatcher {
    bool IsOnUiThread { get; }
    Task InvokeAsync(Func<Task> action);
    Task<T> InvokeAsync<T>(Func<Task<T>> action);
    void Post(Action action);
}
```

P1-də priority-aware dispatcher API bağlanmır. WPF `Dispatcher`,
`Application.Current.Dispatcher` və shell view model-ləri plugin-ə direct access
kimi verilmir.

Plugin UI thread dispatcher-ini, `Application.Current.Dispatcher`-i və ya shell view
model-lərini direct access kimi almır. Plugin UI feedback yalnız declared
extension/page contract və host-owned notification/progress contract-ları ilə gedir.
UI-local dispatch, notification taxonomy və focus/render behavior
`docs/specs/shell-ui.md` tərəfindən operationalize olunur.

### 4. Enforcement

- Plugin package install zamanı `Dobby.PluginFramework / Plugin Packaging` namespace-i plugin assembly-nin Dobby runtime implementation project-lərinə və başqa plugin assembly-lərinə direct reference saxlamadığını yoxlayır.
- Plugin activation zamanı `Dobby.Security / Plugin Capability Boundary` (`ADR-0012`) plugin-in görə biləcəyi host API surface-ni məhdudlaşdırır.
- Plugin-to-plugin direct invocation `Dobby.Security / Plugin Trust / Policy Enforcement` (`ADR-0011`, `ADR-0059`) design və authorization sərhədi ilə qadağandır: package/dependency validation direct reference-i install zamanı reject edir və capability boundary runtime-da host API surface-ni məhdudlaşdırır. Bu sərhəd fully trusted in-process kod üçün containment zəmanəti kimi təqdim edilmir; hostile-code isolation iddiası yoxdur.

### 5. Nümunə axın

`02_ARCHITECTURE_MAP.md` convention-ında verilmiş nümunə bu ADR altında canonical-dir:

- Plugin A `CustomerChangedIntegrationEvent` publish edir → `Dobby.Messaging / IEventBus` bunu qəbul edib paylayır → Plugin B subscribe olduğu üçün məlumatı alıb UI-nı yeniləyir.

### 6. Rejected alternatives

- **Plugin-to-plugin shared interface assembly**: rədd edildi — shared interface `Dobby.Contracts`-a köçürülməlidir; plugin-in öz aralarında contract ixtira etməsi ecosystem fragmentasiyasına gətirir.
- **RPC-like cross-plugin invocation**: Phase 1-də rədd edildi — RPC abstraction plugin boundary-nin təkrar mühəndisliyini tələb edir; event-based communication sadə və sufficient-dir.
- **Optional direct reference with capability gate**: rədd edildi — "optional" enforcement-i zəiflədir; qayda hər plugin üçün eyni olmalıdır.

## Consequences

- Plugin ecosystem decoupled qalır; bir plugin-in yenilənməsi digər plugin-ləri breaking-change riski altına atmır.
- Event-driven communication idiom plugin authors üçün əsas pattern-dir; Phase 1 developer documentation (`Dobby.SDK`) bu pattern-i vurğulayır.
- Direct assembly reference enforcement plugin package install zamanı baş verir; runtime-da plugin isolation (ADR-0011) bu qaydanı ikinci səviyyədə qoruyur.
- Event visibility və UI dispatch boundary bu ADR-də bağlandığı üçün ADR-0002 və ADR-0014 ayrıca P1 ADR kimi yaradılmır.
- Cross-plugin integration event-lərin sayı artdıqca Contracts surface şişir; bu `ADR-0001` surface stability review ilə control olunur.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Engineering Conventions `Plugin-to-Plugin Əlaqə`, `Event Visibility`, `Messaging UI Bridge`, Cədvəl 3 communication/event risk sətrləri
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts surface stability
- `docs/adr/ADR-0007-plugin-load-context-strategy.md` — load context isolation
- `docs/adr/ADR-0011-plugin-trust-and-policy-enforcement-principle.md` — trust and policy enforcement boundary (not hostile-code containment)
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — capability-based host API surface
- `docs/adr/ADR-0036-plugin-manifest-schema.md` — `capabilities` və `extensionPoints` schema
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — current trust posture and declarative host-rendered plugin UI contract
- `docs/specs/shell-ui.md` — UI-local notification, dispatcher/render və shell feedback behavior
