# ADR-0004: Capability Enforcement Model

**Status:** Accepted
**Date:** 2026-04-24

**Licensing surface note:** Typed policy pattern qüvvədə qalır. Bu ADR-də licensing üçün
verilən ilkin nümunə `ADR-0053` runtime mode surface-i ilə oxunmalıdır.

## Context

`02_ARCHITECTURE_MAP.md` Row 22 `Policy Enforcement Contracts` Phase 1-də P1-core statusda, amma contract səthi və enforcement ownership-i canonical olaraq bağlanmamışdı. `Cədvəl 3` risk sətri iki problemə işarə edirdi:

- Contract ilə enforcement implementasiyası qarışa bilər (contract `Dobby.Contracts`-da qalmalı, enforcement `Dobby.Security`-də olmalıdır)
- Tək "god-interface" (məs., `IAuthorizationService`) bütün policy decision-ları tutmamalıdır — capability yönümlü kiçik interface-lər istənilir

`ADR-0012 Plugin Capability Boundary` plugin-ə hansı capability-lərin görünəcəyini bağlayır, amma **enforcement mexanizmi** (plugin bu capability-i faktiki olaraq istifadə edəndə host nə yoxlayır) bağlı deyildi. Bu ADR enforcement model-i rəsmiləşdirir.

## Decision

### 1. Capability-oriented interface pattern

Hər capability öz dar `I{Capability}Checker` və ya `I{Capability}Policy` interface-inə malikdir. God-interface qadağandır. Nümunələr:

```csharp
// Dobby.Contracts / Policy
public interface ICapabilityPolicy<TCapability> {
    Result<PolicyDecision> Evaluate(TCapability capability, PolicyContext context);
}

public readonly record struct PolicyDecision(bool Allowed, string? DenyReason);
```

Hər capability öz `TCapability` record type-ına sahibdir (məs., `PluginStorageCapability`, `PluginLicensingCapability`). Licensing surface-i `ADR-0053` runtime mode modeli ilə, operation authorization isə `ADR-0048` `OperationPermissionCapability` modeli ilə ifadə olunur.

Operation authorization capability-si registry-produced `<targetId>.<verb>` key-i daşıyır. Bu key storage/UI vocabulary-dir; typed capability və `ICapabilityPolicy<OperationPermissionCapability>` enforcement contract-ı dəyişmir. `ADR-0048`-dəki grant-only/on-off permission qaydası capability pattern-i əvəz etmir, həmin capability-nin policy data modelini müəyyən edir.

### 2. Contract vs enforcement ownership

- **Contract** (`Dobby.Contracts / Policy`): `ICapabilityPolicy<TCapability>`, `PolicyDecision`, `PolicyContext`, `TCapability` record type-ları
- **Enforcement** (`Dobby.Security / Permissions / Policy` Row 37): `CapabilityPolicyEvaluator` concrete implementation; host-controlled permission/role/licensing integration

Plugin `Dobby.Contracts`-dakı interface-ləri görür amma enforcement logic-ə birbaşa çıxmır.

`ICapabilityPolicy<TCapability>` və `PolicyDecision` canonical plugin-visible policy contract adlarıdır. Host-side orchestration internal evaluator/authorizer service istifadə edə bilər, amma həmin implementation adları bu ADR amendment olunmadıqca əlavə plugin-facing contract type yaratmır.

### 3. Async model

Phase 1-də capability evaluation **sync əsaslıdır** (`Result<PolicyDecision>` sync return). Lisenziya grace state-i, user permission check-i və tenant scope lookup-u sync davranır çünki local, in-memory data-dır. Async ehtiyacı yaranarsa (məs., external identity provider integration) ayrıca ADR amendment tələb edir; Phase 1 scope-unda deyil.

### 4. Non-disclosing failure model

Plugin-ə qaytarılan `PolicyDecision.DenyReason` **yüksək səviyyəli** və non-disclosing olmalıdır (məs., `"capability-not-allowed"`), raw user role, license state, missing grant, unknown operation key və ya capability evaluation internal detail sızdırmamalıdır. Detailed audit context `Dobby.Audit / Audit Trail` (Row 70) tərəfində saxlanılır.

### 5. Rejected alternatives

- **God-interface (`IAuthorizationService.CheckAsync(resource, action, user)`):** rədd edildi — generic sign, compile-time type safety yoxdur, test boundary-ləri genişlənir
- **Hər capability üçün ayrı custom method host surface-də:** rədd edildi — host surface şişər və plugin compatibility yoxlaması çətinləşir
- **Async evaluation Phase 1-də:** deferred — heç bir Phase 1 capability async data mənbəyi tələb etmir

## Consequences

- `Dobby.Contracts / Policy` sub-namespace-i açılır: `ICapabilityPolicy<TCapability>`, `PolicyDecision`, `PolicyContext` tipləri
- `Dobby.Security / Permissions / Policy` (Row 37) enforcement concrete implementation-ı daşıyır
- Plugin-visible licensing runtime mode surface-i (`ADR-0053`) və operation authorization capability-si (`ADR-0048`) bu pattern-in real Phase 1 istifadələridir
- Yeni capability əlavə olunduqda (məs., `IStorageCapability`, `INavigationCapability`) eyni pattern istifadə olunur
- `Dobby.Contracts / Policy` Phase 1 şişməməlidir: yalnız mövcud real capability-lər üçün type-lar gəlir; future-facing speculative contracts əlavə olunmur

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 22 `Policy Enforcement Contracts`, Row 37 `Permissions / Policy`
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — plugin-ə görünən capability surface
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — licensing runtime mode surface
- `docs/adr/ADR-0048-operation-based-authorization.md` — operation authorization capability
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — Contracts namespace ownership
