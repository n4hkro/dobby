# ADR-0052: Tenant / Customer Context v2

**Status:** Accepted
**Date:** 2026-04-25
**Supersedes:** ADR-0035

## Context

Owner D-2 qərarı ilə Phase 1 license customer/deployment/device/package binding
mandatory oldu. `ADR-0035` Phase 1-də customer gating-i no-op saxlayırdı və tenant
primitive-i yalnız forward-compatible `default` scope kimi bağlayırdı. Bu qərar artıq
yetərli deyil.

Bu ADR customer/deployment context-in Phase 1-də necə enforce olunacağını və tenant
runtime primitive-in plugin-visible olmadan necə saxlanacağını bağlayır.

## Decision

### 1. Customer və deployment product-level binding-dir

Phase 1 Dobby installation single active customer/deployment context ilə işləyir:

- `customerId` — müştəri şirkət / owner account scope
- `deploymentId` — həmin müştəri üçün konkret lokal installation/deployment scope

Bu dəyərlər host-internal context-dir və license binding enforcement üçün istifadə
olunur. License import bu mövcud context-ə qarşı validate edilir; ilk `.dobbylicense`
import zamanı customer/deployment context opportunistic yaradılmır və ya dəyişdirilmir.

### 2. Tenant runtime primitive qalır

`tenant` runtime scope primitive-i qalır, amma Phase 1-də single active scope-dur:

- `TenantId.Default` hələ də storage scope üçün compatibility primitive-dir;
- multi-customer tenant switching P2-dir;
- plugin tenant/customer id-ləri birbaşa görmür.

### 3. Licensing customer/deployment gating P1-də enforce olunur

License artifact-dakı `customerId` və `deploymentId` current local customer/deployment
context ilə uyğun gəlməlidir. Uyğunsuzluq:

- plugin activation/read access üçün non-disclosing deny yaradır;
- `.dobbylicense` import zamanı artifact saxlanılmadan non-disclosing reject yaradır;
- audit trail-də detailed host-side entry yaradır;
- plugin-ə customer, deployment və ya tenant detail sızdırmır.

Customer/deployment mismatch-i user-facing şəkildə hansı sahənin uyğunsuz olduğu kimi
açıqlanmır. Support üçün lazımi detal audit/diagnostics tərəfində qalır.

### 4. Storage scope

`IPluginStorage` tenant scope vocabulary-si `ADR-0030`-a uyğun qalır. Phase 1-də tenant
storage scope `default` ilə işləyir. Customer/deployment binding storage key formatını
P1-də multi-tenant formatına çevirmir; bu P2 multi-customer storage migration mövzusudur.

### 5. Plugin visibility

Plugin:

- `customerId`, `deploymentId`, `tenantId` raw dəyərlərini görmür;
- license binding mismatch detail almır;
- yalnız `ADR-0053` runtime mode və non-disclosing failure görür.

### 6. Initial customer / deployment context provisioning

Phase 1 host bootstrap clean local install zamanı exactly one local default
customer/deployment context yaradır və saxlayır. Bu bootstrap login-first path-in
minimum host/auth storage workload-u daxilində idempotent olmalıdır; eyni local
profile üçün təkrar launch yeni context yaratmır.

Storage owner `docs/specs/storage-and-data-protection.md`-dəki pre-login
`CustomerDeploymentContext` table-ıdır. Minimum schema `customerId`, `deploymentId`,
`source`, `protectionScope`, `schemaVersion`, `createdAt` və `updatedAt` field-lərini
daşıyır. Context `HostConfiguration` generic row-larında gizli saxlanmır.

Canonical P1 default:

- `customerId = "default"`
- `deploymentId = "default"`

Bu dəyərlər plugin-visible deyil və user-facing tenant/customer switching UI
yaratmır. License import yalnız bu mövcud context-ə qarşı validate edir;
`.dobbylicense` import heç vaxt customer/deployment identifier-i opportunistic
yaratmır və dəyişdirmir.

Non-default vendor/customer deployment context provisioning Phase 1 host UI
scope-una daxil deyil. Phase 1-də host-internal explicit admin/operator provisioning
workflow-u non-default context-i provision edə bilər. Bu workflow bütün mövcud context
row-larını atomik olaraq exactly one active context ilə əvəz edir; customer-switching UI,
multi-customer runtime və ya license import-dan opportunistic provisioning yaratmır.

### 7. Rejected alternatives

- **P1 customer gating no-op qalsın:** rədd edildi — Owner D-2 mandatory binding seçdi.
- **Full multi-tenant switching P1-də:** rədd edildi — Phase 1 single active deployment
  context-dir; multi-customer runtime switching P2-dir.
- **Plugin-ə customer/tenant id verilsin:** rədd edildi — plugin visibility boundary-ni
  genişləndirir və licensing detail-i sızdıra bilər.

## Consequences

- `ADR-0035` superseded olur.
- `Dobby.Security / Current Context` customer/deployment binding üçün host-internal
  context saxlayır.
- Clean install bootstrap default customer/deployment context-i license import-dan
  əvvəl yaradır; license import opportunistic context creation yolu deyil.
- `Dobby.Licensing` License v2 binding check zamanı bu context-i istifadə edir.
- Customer/device migration P1-də hələ də yoxdur; data-folder copy/rebind unsupported
  qalır.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 36 `Current Context`, Row 43 `License / Entitlement`, Cədvəl 3 `Tenant / Customer Context v2`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-016`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-015`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-020`
- `docs/specs/license-import.md`
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — tenant storage scope
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — mandatory binding fields (current authority; `ADR-0051`-i əvəz edir)
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — non-disclosing plugin surface

## Branch Shared Identity Amendment

`customerId` + `.dobbylicense` `deploymentId` remain license/customer context.
`branchDeploymentId` is a separate Branch Shared server/provisioning/restore
identity owned by `ADR-0056`.

The internal/prose term `customerDeploymentId` is resolved by `DM-093` as a
prose-only shorthand for the existing `(customerId, deploymentId)` tuple.

It is not:

- a signed `.dobbylicense` wire field;
- a code identifier;
- a storage column;
- a merged replacement identifier;
- a reason to rename existing `customerId` or `deploymentId` fields.

When rendered in UI, support text, docs tables or diagnostics prose, the values
must stay visibly decomposed as `customerId` + `deploymentId` or as the prose
label "customer/deployment". They must not be rendered as a persisted field name,
wire key, storage key or copyable identifier named `customerDeploymentId`.

Future readers must not treat `customerDeploymentId` as a new identifier unless
a later ADR explicitly versions the license/customer identity schema.
