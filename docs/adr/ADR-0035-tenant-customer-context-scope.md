# ADR-0035: Tenant / Customer Context Scope

**Status:** Superseded
**Date:** 2026-04-24
**Superseded by:** ADR-0052

> Historical note: Code snippets and vocabulary below are not Phase 1 canonical.
> Current customer/deployment context model is `ADR-0052`; current plugin storage
> compartments are `SharedDomainStorage` and `UserScopedStorage`. Skeleton or
> documentation-fix work must not copy code/interface shapes from this ADR body
> without an explicit ADR-0052 amendment.

## Context

Bu ADR-dən əvvəl tenant/customer context sənədlərdə iki fərqli səviyyədə istifadə olunurdu və bu qarışıqlıq yaradırdı:

- **Product-level concept**: `01_PRODUCT_SCOPE.md` customer binding-i `lazım gələrsə` opsional elan edirdi (plugin activation artefaktının optional field-i).
- **Runtime primitive**: `02_ARCHITECTURE_MAP.md` Row 36 `Current Context` namespace-i `current customer/tenant` kontekstini runtime primitive kimi saxlayırdı. `03_TECH_STACK_AND_VERSIONS.md` `ICurrentTenant` async context accessor-unu elan edirdi. `ADR-0030` `tenant` storage scope-unu closed vocabulary-yə almışdı.
- **Acceptance ambiguity**: `02_ARCHITECTURE_MAP.md` Cədvəl 3 `Tenant / Customer Context Scope` sətri bu ADR-dən əvvəl Phase 1 blocker kimi qırmızı işarələnirdi və owner qərarı gözləyirdi.

Bu ADR owner qərarı ilə həmin boşluğu rəsmiləşdirir, minimum runtime primitive-i açır və product-level customer konsepti ilə sərhədi qoyur.

Əlaqəli örtük ambiguity: `ADR-0030` tenant storage scope-unu bu ADR-dən əvvəl accepted etmişdi, amma ADR-0035 olmayınca həmin scope-un runtime semantic-i açılmırdı. Bu ADR həmin örtük boşluğu da bağlayır.

## Decision

Phase 1 tenant/customer context scope modeli:

### 1. Tenant = runtime scope key primitive

- `tenant` Phase 1-də yalnız **runtime scope identifier primitive-dir**.
- Phase 1-də single-tenant model işləyir: bütün runtime tenant key-i `default` dəyərini daşıyır.
- Multi-tenant behavior (fərqli tenant key-ləri) P2-də açılır; Phase 1 yalnız primitive-i və default scope-u bağlayır.

### 2. `ICurrentTenant` primitive

```csharp
public interface ICurrentTenant {
    TenantId Current { get; }   // Phase 1-də həmişə TenantId.Default
}

public readonly record struct TenantId(string Value) {
    public static TenantId Default { get; } = new("default");
}
```

- `ICurrentTenant` `Dobby.Core / Time`-da deyil, `Dobby.Security / Current Context` namespace-ində yerləşir (Row 36).
- Implementation `AsyncLocal<T>` üzərindədir (ADR-0029 correlation context ilə eyni mexanizm).
- Background task enqueue zamanı current tenant explicit propagation tələb edir (ADR-0029 pattern).

### 3. Storage tenant scope (ADR-0030 uyğun)

- `IPluginStorage.Get(key, scope: StorageScope.Tenant)` Phase 1-də həmişə `default` tenant altında data yazır/oxuyur.
- Plugin author `StorageScope.Tenant` istifadə edə bilər; Phase 1-də bu `default` scope ilə identical davranır, amma API forward-compatible qalır.
- Multi-tenant açıldıqda (P2+) storage backend key composition dəyişdirilir: `{tenantId}:{pluginId}:{key}`.

### 4. Customer binding vs tenant binding

- **Customer** product-level konseptdir: müştəri şirkətidir, `Dobby.Admin` tərəfindən idarə olunur.
- **Tenant** runtime scope key-dir: host çalışma kontekstinin identifier-idir.
- Phase 1-də customer ↔ tenant 1:1 mapping olur; single-customer deployment üçün `default` tenant mövcuddur.
- Multi-customer senariosunda (P2+) hər customer öz tenant key-inə sahib olacaq.

### 5. Licensing customer binding — Phase 1 runtime-da gating yoxdur

- License artifact `customerId` field-i opsionaldır (`ADR-0026`).
- **Phase 1-də runtime licensing-to-tenant gating fəallaşdırılmır.** Arch Map Row 36 və Cədvəl 3 (Tenant / Customer Context Scope) bu semantic-i P2-yə saxlayır. License `customerId` field-i qeyd olunsa belə, plugin activation zamanı customer-tenant müqayisəsi aparan enforcement path-i Phase 1-də `no-op`-dur.
- Yalnız **API surface forward-compatible** saxlanılır: gələcəkdə (P2+) multi-tenant açıldıqda license `customerId` + `ICurrentTenant.Current` asociated customer-i müqayisə ediləcək; bu Phase 1-də həyəcana gətirilmir.
- Phase 1 license enforcement canonical davranışı `allowed | grace | expired | denied` state machine ilə məhdudlaşır (ADR-0026 + AC-006); tenant-binding check bu state-in içində deyil.

### 6. Plugin-visible tenant surface

Plugin `tenant` konseptini birbaşa görmür. Plugin-ə aşağıdakı sütunlar görünür:

- `IPluginStorage.Get(key, scope)` — plugin data saxlayır, scope "tenant" olsa belə plugin tenant key-ini görmür
- `ICurrentUser.UserId` — current user
- `ICapabilityLicensing.IsFeatureAllowed(feature)` — licensing decision (tenant scope host tərəfdə hesablanır)

Plugin `ICurrentTenant`-ə birbaşa access etmir; tenant scope host responsibility-sidir.

### 7. Rejected alternatives

- **Tenant Phase 1-dən tamamilə çıxarılsın**: rədd edildi — `ADR-0030` tenant scope-u closed vocabulary-yə daxil edib; backward compatibility tələb edir.
- **Tenant = full multi-tenant model Phase 1-də**: rədd edildi — Phase 1 skeleton məqsədi üçün overkill; P2-dən sonra real multi-tenant ehtiyacı yaranarsa açılır.
- **Customer concept product-dən tamamilə çıxarılsın**: rədd edildi — vendor `Dobby.Admin` ilə customer CRUD aparır (ADR-0023); product scope-da customer binding opsional qalmalıdır.

## Consequences

- Phase 1 implementation üçün `ICurrentTenant` primitive-i açıqdır; test və runtime code default tenant ilə işləyir.
- `IPluginStorage` tenant scope key `default`-dur; yeni tenant key-i açmaq üçün P2 amendment lazımdır.
- Licensing artifact `customerId` field-i opsional saxlanır; Phase 1 müştəri deployment-lərində default single-customer işləyir.
- Plugin authors tenant konseptinin birbaşa farkında olmamalıdır; capability boundary (`ADR-0012`) tenant information-i gizlədir.
- Multi-tenant üçün future work: ADR-0043 (təxminən) Multi-tenant enablement + identity management + per-tenant settings isolation.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — `## Plugins və Licensing`
- `docs/02_ARCHITECTURE_MAP.md` — Row 36 `Current Context`, Cədvəl 3 `Tenant / Customer Context Scope` risk sətri
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `## 7. Core Primitives (Dobby-owned)`
- `docs/adr/ADR-0029-correlation-id-ownership.md` — `AsyncLocal<T>` pattern
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — `tenant` storage scope
- `docs/adr/ADR-0026-license-lifecycle.md` — customer binding semantics
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — plugin tenant visibility
