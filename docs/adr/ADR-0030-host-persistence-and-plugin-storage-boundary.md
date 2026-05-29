# ADR-0030: Host Persistence and Plugin Storage Boundary

**Status:** Accepted
**Date:** 2026-04-22

## Context

Plugin storage Dobby-də lazımdır, amma bu ehtiyac host persistence qatının
plugin-ə açılması demək deyil. Əgər storage ownership, data scope və protection
modeli bu mərhələdə bağlanmasa, gələcəkdə ayrıca `Dobby.Storage` project-i açmaq,
plugin-lərə internal persistence detail-larını sızdırmaq və ya müxtəlif user-lərin
business data visibility qaydasını qarışdırmaq riski yaranar.

Owner qərarı storage modelini 3 bucket-ə ayırır: host data, plugin shared domain
data və plugin user-scoped data. Plugin SDK `SharedDomainStorage` və
`UserScopedStorage` compartment-lərini explicit verməlidir; default fallback
yoxdur.

## Decision

Phase 1-də ayrıca `Dobby.Storage` project-i əlavə edilmir. Host persistence
implementation-u internal qalır və plugin sərhədinə sızmır.

Plugin storage üçün canonical public surface host-controlled storage
contract/capability-ləridir. SDK yeni storage capability yaratmır; yalnız bu
contract-lar üçün convenience wrapper verə bilər.

### 1. Storage taxonomy

Phase 1 storage taxonomy:

| Bucket | Məna | Plugin-visible? |
|---|---|---|
| Host data | License artifact/cache, audit log, recovery secrets, user accounts, permission registry və global host preferences | Yox |
| Plugin shared domain data | Müştəri/device scope business records və plugin domain nəticələri | Yalnız `SharedDomainStorage` |
| Plugin user-scoped data | Drafts, preferences, UI state və fərdi in-progress work | Yalnız `UserScopedStorage` |

Host data Owner/admin-permission gated-dir və plugin domain storage sayılmır.
Plugin shared domain data default olaraq customer/device scope-dadır və permission
gating ilə görünür. Plugin user-scoped data per-user scope-dadır.

### 2. Plugin storage compartments

Plugin SDK iki explicit storage compartment təqdim edir:

- `SharedDomainStorage` — customer/device scope business data üçün;
- `UserScopedStorage` — per-user drafts, preferences, UI state və fərdi
  in-progress work üçün.

Plugin developer hər data parçasını explicit compartment-ə yazmalıdır. Default
fallback yoxdur. Plugin başqa plugin-in storage-ına və host data backing store-una
birbaşa access istəyə bilməz.

Köhnə vocabulary mapping:

| Köhnə vocabulary | Yeni canonical izah |
|---|---|
| `plugin-wide` / `tenant` | `SharedDomainStorage` customer/device scope altında ifadə olunur |
| `user` | `UserScopedStorage` altında ifadə olunur |
| `session` | Phase 1-ə daxil deyil |

`session` scope Phase 1-ə daxil deyil, çünki session lifecycle minimum modeli
refresh və long-session davranışını daşımır.

### 3. Local persistence protection

Host və plugin local persistence faylları data-at-rest qorunmalıdır. Normal
business data field-level şifrələnmir ki, query, sort, filter, import/export və
report əməliyyatları lazımsız ağırlaşmasın. Yalnız password, token, key,
remember-me secret, recovery secret və session material kimi secrets DB
qorumasından əlavə field-level/protected-storage qoruması alır.

DB qoruma açarı istifadəçidən əl ilə istənilmir və təkcə `deviceId`-dən
törədilmir. `deviceId` identity/support metadata-dır, encryption key mənbəyi
deyil. Açar Dobby-nin yaratdığı lokal gizli məlumatlar və cihazın qorunan
identity/protection mexanizmləri əsasında avtomatik idarə olunur. Cross-device
data açılışı self-service folder copy ilə yox, ayrıca vendor migration flow ilə
mümkündür.

Bu ADR ownership, taxonomy və protection boundary qərarını sahib edir. Storage
method surface-i, async semantics, atomicity, get/set/delete/keys shape-i və
digər contract detail-ları ayrıca storage contract/spec işinin öhdəliyindədir.

## Consequences

- Skeleton mərhələsində ayrıca storage project-i açmağa ehtiyac qalmır
- Plugin storage capability-si host persistence detail-larından təmiz ayrılır
- Storage taxonomy Phase 1 üçün sabitləşir və contract code pass-da yenidən
  müzakirə olunmur
- `SharedDomainStorage` və `UserScopedStorage` plugin SDK üçün canonical
  compartment adlarıdır
- Local DB qoruması release boundary-dir; şifrəsiz host/plugin persistence
  P1 release üçün qəbul edilmir
- Secret field-level protection yalnız həssas secret-lərə tətbiq olunur, bütün
  business data-ya yox

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Plugin Storage Contracts` row
- `docs/02_ARCHITECTURE_MAP.md` — `ADR-0030` risk sətri
- `docs/02_ARCHITECTURE_MAP.md` — `Storage Sərhədi` convention
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-007`
- `docs/specs/storage-and-data-protection.md` — storage/data protection spec
- `docs/adr/ADR-0003-plugin-storage-boundary-and-access-model.md` — method surface, async semantics, key/value shape and atomicity
- `docs/adr/ADR-0001-contracts-dependency-boundary.md` — contract surface daralma
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` — protected-storage scope və key wrapping mapping

## DeviceLocal / BranchShared Amendment

`ADR-0056` adds a host-internal deployment scope axis without changing plugin
contracts. `DeviceLocal` contains local protected profile/secrets/bootstrap and
diagnostics material. `BranchShared` contains authoritative Branch Shared users,
permissions, audit, business data and host-owned plugin shared key/value data.

`OrganizationShared` is future vocabulary only and is not added to Phase 1
provider enums, schemas, migrations or plugin-facing APIs. SQLite remains local
embedded storage only; shared-folder/network-drive SQLite is not supported.
