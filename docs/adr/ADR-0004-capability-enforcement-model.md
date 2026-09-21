# ADR-0004: Capability Enforcement Model

**Status:** Accepted
**Date:** 2026-04-24

**Licensing surface note:** Typed policy pattern qüvvədə qalır, amma licensing **bu pattern ilə
ifadə olunmur**: plugin-visible licensing surface-i `ADR-0053` runtime mode modelidir. Bu ADR-in
əvvəlki redaksiyalarında licensing üçün verilən ilkin nümunə §1-dən silinib (bax: Revision
History).

## Context

`02_ARCHITECTURE_MAP.md` Row 22 `Policy Enforcement Contracts` Phase 1-də P1-core statusda, amma contract səthi və enforcement ownership-i canonical olaraq bağlanmamışdı. `Cədvəl 3` risk sətri iki problemə işarə edirdi:

- Contract ilə enforcement implementasiyası qarışa bilər (contract `Dobby.Contracts`-da qalmalı, enforcement `Dobby.Security`-də olmalıdır)
- Tək "god-interface" (məs., `IAuthorizationService`) bütün policy decision-ları tutmamalıdır — capability-yönümlü, typed və dar enforcement contract-ı istənilir (§1 həmin contract-ın forması olaraq tək generic `ICapabilityPolicy<TCapability>`-ni seçir)

`ADR-0012 Plugin Capability Boundary` plugin-ə hansı capability-lərin görünəcəyini bağlayır, amma **enforcement mexanizmi** (plugin bu capability-i faktiki olaraq istifadə edəndə host nə yoxlayır) bağlı deyildi. Bu ADR enforcement model-i rəsmiləşdirir.

## Decision

### 1. Capability-oriented interface pattern

Enforcement səthi **tək generic contract**-dır: `ICapabilityPolicy<in TCapability>`. Hər capability
öz **typed capability record**-una malikdir və həmin type generic parametr kimi verilir; hər
capability üçün yeni `I{Capability}Checker` / `I{Capability}Policy` interface-i **yaradılmır**.
God-interface (bütün resource/action cütlərini tutan tək `CheckAsync(...)`) qadağandır — qadağa
generic parametr vasitəsilə compile-time type safety saxlamaqla təmin olunur, plugin-facing
interface sayını artırmaqla deyil.

Aşağıdaki blok `Dobby.Contracts.Policies`-in **tam** Phase 1 səthidir
(`src/Dobby.Contracts/Policies/PolicyContracts.cs`):

```csharp
// namespace Dobby.Contracts.Policies (Row 22 `Policy Enforcement Contracts`)
public interface ICapabilityPolicy<in TCapability>
{
    Result<PolicyDecision> Evaluate(TCapability capability, PolicyContext context);
}

public sealed record PolicyContext(
    string? UserId,
    string? PluginId,
    CorrelationId CorrelationId);

public readonly record struct PolicyDecision(bool Allowed, string? DenyReason)
{
    public static PolicyDecision Allow() => new(true, null);

    public static PolicyDecision Deny(string reason) => new(false, reason);
}

public readonly record struct OperationPermissionCapability(string OperationKey);
```

`TCapability` **contravariant** (`in`) elan olunur ki, host bir capability hierarchy-si üçün yazılmış
policy-ni daha dar capability type-larına tətbiq edə bilsin; bu variance canonical contract
şəklinin bir hissəsidir. `PolicyDecision` yalnız `Allow()`/`Deny(reason)` factory-ləri ilə qurulur
ki, `Allowed = true` ilə birlikdə `DenyReason` daşıyan ziddiyyətli decision yaranmasın.

Phase 1-in **yeganə** typed policy capability-si `OperationPermissionCapability`-dir; onun
enforcement implementation-ı `ADR-0048` operation authorization modelini daşıyır. Bu ADR
speculative capability type adı təyin etmir — plugin-visible policy surface-i yalnız real,
shipped capability-lərdən ibarətdir (bax: Consequences, `Dobby.Contracts.Policies`
şişməməlidir).

Digər plugin-facing host capability-ləri **bu pattern-dən qəsdən kənardır** və onların gate-i
policy evaluation deyil:

- **Licensing:** plugin-visible surface `ICapabilityLicensing` runtime mode modelidir
  (`ADR-0053`), `ICapabilityPolicy<...>` deyil. Licensing üçün typed policy capability
  yaradılmır.
- **Plugin storage:** `IPluginStorage` `ADR-0012` §2a host-injectable capability-sidir; onun
  yeganə gate-i host-un per-plugin factory təqdim edib-etmədiyidir (factory yoxdursa type
  ümumiyyətlə resolve olunmur). Storage access-i policy evaluation ilə yoxlanılmır, ona görə
  storage üçün `TCapability` record-u mövcud deyil.

Operation authorization capability-si registry-produced `<targetId>.<verb>` key-i daşıyır. Bu key storage/UI vocabulary-dir; typed capability və `ICapabilityPolicy<OperationPermissionCapability>` enforcement contract-ı dəyişmir. `ADR-0048`-dəki grant-only/on-off permission qaydası capability pattern-i əvəz etmir, həmin capability-nin policy data modelini müəyyən edir.

### 2. Contract vs enforcement ownership

- **Contract** (`02_ARCHITECTURE_MAP.md` Row 22 `Policy Enforcement Contracts`, namespace
  `Dobby.Contracts.Policies`): `ICapabilityPolicy<TCapability>`, `PolicyDecision`,
  `PolicyContext`, `TCapability` record type-ları
- **Enforcement** (`02_ARCHITECTURE_MAP.md` Row 37 `Permissions / Policy`, namespace
  `Dobby.Security.Authorization`): host-controlled permission/role/licensing integration daşıyan
  concrete `ICapabilityPolicy<TCapability>` implementation-ları

Plugin `Dobby.Contracts`-dakı interface-ləri görür amma enforcement logic-ə birbaşa çıxmır.

`ICapabilityPolicy<TCapability>`, `PolicyContext` və `PolicyDecision` canonical plugin-visible
policy contract adlarıdır və bu ADR amendment olunmadan dəyişmir.

Enforcement tərəfdəki **concrete type adları və folder yerləşməsi bu ADR-də normativ deyil** —
normativ olan yalnız ownership boundary-sidir: enforcement `Dobby.Security` altında qalır,
`Dobby.Contracts`-a enforcement logic-i keçmir və heç bir internal evaluator/authorizer adı əlavə
plugin-facing contract type yaratmır. Phase 1-in shipped enforcement implementation-ı
`Dobby.Security.Authorization.OperationAuthorizationPolicy`-dir
(`ICapabilityPolicy<OperationPermissionCapability>`, `ADR-0048` operation authorization modeli).
Bu ad informativ mövcud vəziyyət qeydidir; onu dəyişmək ADR amendment tələb etmir, amma
namespace/ownership boundary-ni dəyişmək tələb edir.

### 3. Async model

Phase 1-də capability evaluation **sync əsaslıdır** (`Result<PolicyDecision>` sync return). Lisenziya grace state-i, user permission check-i və tenant scope lookup-u sync davranır çünki local, in-memory data-dır. Async ehtiyacı yaranarsa (məs., external identity provider integration) ayrıca ADR amendment tələb edir; Phase 1 scope-unda deyil.

### 4. Non-disclosing failure model

Plugin-ə qaytarılan `PolicyDecision.DenyReason` **yüksək səviyyəli** və non-disclosing olmalıdır (məs., `"capability-not-allowed"`), raw user role, license state, missing grant, unknown operation key və ya capability evaluation internal detail sızdırmamalıdır. Detailed audit context `Dobby.Audit / Audit Trail` (Row 70) tərəfində saxlanılır.

### 5. Rejected alternatives

- **God-interface (`IAuthorizationService.CheckAsync(resource, action, user)`):** rədd edildi — generic sign, compile-time type safety yoxdur, test boundary-ləri genişlənir
- **Hər capability üçün ayrı custom method host surface-də:** rədd edildi — host surface şişər və plugin compatibility yoxlaması çətinləşir
- **Async evaluation Phase 1-də:** deferred — heç bir Phase 1 capability async data mənbəyi tələb etmir

## Consequences

- `Dobby.Contracts.Policies` sub-namespace-i açılır (Row 22): `ICapabilityPolicy<TCapability>`, `PolicyDecision`, `PolicyContext` tipləri
- `Dobby.Security` (Row 37 `Permissions / Policy`) enforcement concrete implementation-larını daşıyır; concrete type adı normativ deyil, ownership boundary-si normativdir
- Plugin-visible licensing runtime mode surface-i (`ADR-0053`) və operation authorization capability-si (`ADR-0048`) bu pattern-in real Phase 1 istifadələridir
- Yeni typed policy capability əlavə olunduqda pattern dəyişmir: yeni **capability record type**-ı
  `Dobby.Contracts.Policies`-ə, onun `ICapabilityPolicy<...>` implementation-ı isə
  `Dobby.Security` altına gəlir. Yeni plugin-facing policy **interface**-i yaradılmır və yeni
  capability yalnız real Phase scope tələbi olduqda əlavə olunur
- `Dobby.Contracts.Policies` Phase 1 şişməməlidir: yalnız mövcud real capability-lər üçün type-lar gəlir; future-facing speculative contracts əlavə olunmur

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 22 `Policy Enforcement Contracts`, Row 37 `Permissions / Policy`
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — plugin-ə görünən capability surface, §2a qapalı host-injectable set
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — licensing runtime mode surface
- `docs/adr/ADR-0048-operation-based-authorization.md` — operation authorization capability
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts namespace ownership
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-027` behavior-first proof qaydası (capability gate-i source oxumaqla deyil, resolution/evaluation davranışı ilə sübut olunur)

## Revision History

- **2026-08-27:** Contract/enforcement ownership bölməsindəki namespace və concrete type
  referansları shipped kod ilə uzlaşdırıldı. Əvvəlki mətn enforcement-i
  `Dobby.Security / Permissions / Policy` namespace path-i kimi göstərir və concrete
  implementation-ı `CapabilityPolicyEvaluator` adlandırırdı; həmin ad repository-də heç vaxt
  mövcud olmayıb, real implementation
  `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`-dir və contract namespace-i
  `Dobby.Contracts.Policies`-dir. `Permissions / Policy` və `Policy Enforcement Contracts`
  `02_ARCHITECTURE_MAP.md` logical row adlarıdır, namespace path deyil; bu fərq indi explicit
  yazılıb. Enforcement tərəfdə concrete type adının normativ olmadığı, normativ olanın yalnız
  `Dobby.Contracts` ↔ `Dobby.Security` ownership boundary-si və plugin-visible contract adları
  olduğu əlavə edildi. Enforcement qərarı dəyişməyib, ona görə successor ADR yaradılmır — bu
  factual naming reconciliation-dır. Eyni pass-da §1 code block-u shipped canonical contract ilə
  uzlaşdırıldı: `ICapabilityPolicy<in TCapability>` contravariance-ı, `PolicyContext` record-u və
  `PolicyDecision.Allow()`/`Deny(reason)` factory-ləri əlavə edildi (əvvəlki blok variance-ı və
  factory-ləri göstərmirdi, ona görə ADR-dən birbaşa implement edən oxucu ziddiyyətli
  `Allowed = true` + `DenyReason` decision-u qura bilərdi).
- **2026-08-27:** §1-in capability-shaping qaydası və speculative capability adları shipped
  contract ilə uzlaşdırıldı. Əvvəlki mətn eyni bölmədə iki ziddiyyətli model saxlayırdı: "hər
  capability öz dar `I{Capability}Checker` və ya `I{Capability}Policy` interface-inə malikdir"
  (per-capability interface) və "hər capability öz `TCapability` record type-ına sahibdir"
  (tək generic contract). Shipped model ikincidir — `Dobby.Contracts.Policies` yalnız
  `ICapabilityPolicy<in TCapability>` daşıyır və capability-lər typed record-lardır. Bundan
  başqa mətn mövcud olmayan üç nümunə ad verirdi: `PluginStorageCapability`,
  `PluginLicensingCapability` (§1) və `IStorageCapability`, `INavigationCapability`
  (Consequences). Bu iki qüsur birlikdə real risk yaradırdı: yeni gate yazan implementor
  interface-per-capability variantını seçib plugin-visible contract namespace-inə paralel
  ikinci enforcement şəkli gətirə bilərdi (eyni bölmənin "`Dobby.Contracts.Policies` Phase 1
  şişməməlidir" qaydasını pozaraq), və reviewer `PluginStorageCapability` adını storage
  access-inin policy evaluation ilə yoxlandığının sübutu kimi oxuya bilərdi — halbuki
  `IPluginStorage` `ADR-0012` §2a capability-sidir və onun yeganə gate-i host-un per-plugin
  factory təqdim edib-etməməsidir; licensing isə `ADR-0053` runtime mode surface-idir, typed
  policy capability deyil. §1 indi tək generic contract qaydasını explicit yazır, code block
  `Dobby.Contracts.Policies`-in tam səthini (`OperationPermissionCapability` daxil) göstərir,
  storage və licensing-in pattern-dən qəsdən kənarda qaldığı qeyd olunur, Consequences bəndi
  isə yeni capability-nin record type + host-side implementation kimi əlavə olunduğunu bildirir.
  Header-dəki licensing note artıq silinmiş nümunəyə istinad etmir. Enforcement qərarı
  dəyişmir — bu factual contract reconciliation-dır, ona görə successor ADR yaradılmır.
