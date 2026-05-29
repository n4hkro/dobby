# Storage and Data Protection Spec

**Tarix:** 2026-04-27
**Status:** P1 canonical spec
**Phase:** P1-core
**Scope:** Local persistence protection, storage taxonomy, plugin storage compartments və key source boundary

Bu spec `NFR-007`, `ADR-0030` və `ADR-0040` üçün icra dilini saxlayır. Məqsəd DB schema və exact provider seçmək deyil; release boundary, data scope və qoruma invariantlarını bağlamaqdır.

## 1. Local Persistence Protection

Host və plugin local persistence faylları data-at-rest qorunmalıdır. Dobby P1 release-i şifrəsiz/protection-sız production DB faylı ilə göndərilmir.

Normal business data field-level şifrələnmir. Bunun səbəbi query, sort, filter, import/export və report əməliyyatlarının lazımsız ağırlaşmamasıdır.

Yalnız secret sayılan data DB qorumasından əlavə field-level/protected-storage qoruması alır:

- password hash və credential material
- remember-me secret
- session material
- token/key material
- recovery secret və recovery nonce ilə bağlı həssas material
- protected config secrets

## 2. Key Source Boundary

DB qoruma açarı istifadəçidən əl ilə istənilmir. İstifadəçi ayrıca "DB password" bilməməlidir.

DB qoruma açarı təkcə `deviceId`-dən törədilmir. `deviceId` identity/support metadata-dır, encryption key mənbəyi deyil.

Qoruma açarı Dobby-nin yaratdığı lokal gizli məlumatlar və cihazın qorunan identity/protection mexanizmləri əsasında avtomatik idarə olunur. Bu mexanizm plugin-ə görünmür və plugin key material, backing file path və provider detalı almır.

Protected-storage scope və key wrapping secret/material tipinə görə `ADR-0055`
ilə bağlanır. Machine-bound material DPAPI `LocalMachine`-ı təkbaşına security
boundary kimi qəbul etmir; Dobby application-level entropy və host storage
boundary ilə birlikdə istifadə olunur. Remember-me, session və plugin
`UserScopedStorage` secret-ləri `CurrentUser`-bound davranış tələb edir.

Local data başqa cihaza self-service folder copy ilə açılmamalıdır. Cross-device restore/migration yalnız ayrıca vendor-assisted migration flow ilə mümkündür.

## 3. Storage Taxonomy

Storage taxonomy 3 bucket-dir:

| Bucket | Məna | Access |
|---|---|---|
| Host data | License artifact/cache, audit log, recovery secrets, user accounts, permission registry, global host preferences və per-user shell UI state | Host-owned, Owner/admin-permission gated |
| Plugin shared domain data | Business records, domain results, customer/device scope shared data | Plugin `SharedDomainStorage`, permission gated |
| Plugin user-scoped data | Drafts, preferences, UI state və fərdi in-progress work | Plugin `UserScopedStorage`, per-user |

Host data plugin domain storage deyil. Plugin host data backing store-a birbaşa access istəyə bilməz.

### 3.1 Runtime File Layout

Host runtime data `%LocalAppData%/Dobby/` altında aşağıdakı sahiblik xəritəsinə uyğun saxlanılır. Canonical runtime root config key-i `ADR-0027` içindəki `Dobby:Runtime:DataRoot`-dur. Plugin-ə görünən compartment-lər (`SharedDomainStorage`, `UserScopedStorage`) bu fiziki path-ləri abstrakt edir; plugin developer backing folder-i görmür.

| Path | Contents | Owner |
|---|---|---|
| `%LocalAppData%/Dobby/data/` | Backing DB file(s) və related persistence artifact-ları | Host |
| `%LocalAppData%/Dobby/config/` | `appsettings.json` və runtime config override-ləri | Host |
| `%LocalAppData%/Dobby/logs/` | Technical/diagnostic log file-ları | Host |
| `%LocalAppData%/Dobby/plugins/` | Quraşdırılmış plugin package-ları və metadata | Host |
| `%LocalAppData%/Dobby/licenses/` | License artifact-ları və cache | Host |
| `%LocalAppData%/Dobby/recovery/` | Owner recovery request/response artifact-ları | Host |
| `%LocalAppData%/Dobby/backup/` | Backup/restore artifact-ları (P2 scope) | Host |
| `%LocalAppData%/Dobby/temp/` | İşləmə müddətində yaradılmış müvəqqəti fayl-lar | Host |

## Runtime folder recovery

Startup zamanı required runtime folder-lər yoxdursa təhlükəsiz şəkildə yaradılmalıdır. Optional folder silinibsə recreate edilə bilər.

Protected data, key backing material və ya provider metadata itibsə app silent recreate etməməli, `ADR-0055` failure mode-larına uyğun fail-closed davranmalı və support-safe diagnostics göstərməlidir.

## 4. Plugin Storage Compartments

Plugin SDK iki explicit compartment təqdim etməlidir:

- `SharedDomainStorage`
- `UserScopedStorage`

Plugin developer hər data parçasını explicit compartment-ə yazmalıdır. Default fallback yoxdur.

`SharedDomainStorage` customer/device scope-dadır. ERP-tip business data, satış/müştəri/report nəticələri və shared domain records üçün istifadə olunur. Data visibility permission registry və plugin domain qaydaları ilə gate edilir.

`UserScopedStorage` per-user scope-dadır. Drafts, user preferences, UI state və fərdi in-progress work üçün istifadə olunur.

Collaborative/shared draft modeli P1 default deyil. Lazım olarsa P2/P3-də ayrıca plugin capability və conflict semantics ilə dizayn edilir.

## 5. Implementation Notes

SQLite + Dapper local persistence stack kimi qala bilər, amma DB-at-rest protection layer ayrıca doğrulanmalıdır. Exact provider direction `ADR-0054`-ün öhdəliyindədir; initial skeleton isə provider-neutral qalır və production provider package-i yaratmır.

Provider/package guardrail-ləri, deprecated encrypted SQLite bundle qadağası və
cipher/tooling compatibility meyarları `ADR-0054` tərəfindən idarə olunur; bu
spec yalnız release boundary və storage invariantlarını saxlayır.

### 5a. P1 Persistence Materialization Pass

Initial P1 skeleton commit storage sahəsində yalnız provider-neutral
persistence/storage abstraction-larına qədər gedə bilər. Bu commit aşağıdakıları
yaratmamalıdır:

- concrete migrations;
- concrete entities;
- table schema-ları;
- concrete repositories;
- production DB-at-rest provider package-ləri;
- native encrypted SQLite bootstrap.

Exact host-internal provider-neutral abstraction names, owner project/namespace
placements, method surfaces, `Result<T>` behavior and error-code surfaces are
blocked pending human/architecture-approved input. Docs-fix or skeleton agents
must not invent repository/session factory names for settings storage, license
cache, audit append-only writing, recovery nonce storage, protected-storage
wrappers or plugin-storage backing primitives. If that approved table is not
available, storage-facing host abstraction creation is a stop condition; only
the already-defined plugin-visible ADR-0003 storage contracts remain canonical.

Pre-login bootstrap closed table set-i və post-login P1 host-owned persisted
state-lər yalnız `P1 Persistence Materialization Pass` zamanı schema-ready
materializə olunur. Həmin pass ən azı bu sahələri bağlamalıdır:

- `CustomerDeploymentContext` minimum field set-i (`ADR-0052`);
- license state, revision və artifact attestation metadata (`ADR-0051`);
- plugin install/lifecycle/cache state (`docs/specs/plugins-manager.md`);
- host preferences;
- per-user shell UI state və favorites;
- recovery artifact/secret state;
- audit append-only storage və retention fields;
- `StorageMetadata` schema/bootstrap version və provider proof metadata.

`Pre-login bootstrap closed table set` invariantı saxlanır: post-login feature
tables həmin closed set-in içinə qatılmır. Materialization pass pre-login bootstrap
və sonrakı full migration ardıcıllığını idempotent/re-entrant etməlidir.

Implementation üçün invariantlar:

- pre-login schema bootstrap yalnız login-visible closed table set-i ilə məhdudlaşır və idempotent olmalıdır; initial skeleton bu table schema-larını materializə etmir; full migration-lar `P1 Persistence Materialization Pass` zamanı storage provider və required application services hazır olandan sonra işləyir;
- unprotected production DB file release blocker-dir;
- secret field-lər protected wrapper-dan keçməlidir;
- plugin backing storage detail-larını görə bilməz;
- host data və plugin domain data eyni physical DB-də ola bilər, amma ownership və access boundary qarışmamalıdır;
- audit append-only invariantı storage implementation tərəfindən pozulmamalıdır.
- yazma əməliyyatları transactional/atomic olmalıdır: partial-write halında persistence son tutarlı vəziyyətə qayıdır, multi-table mutation (məsələn permission grant + audit + cache) tək transaction içində commit/rollback olunur;
- crash safety qorunur: process crash, kill və ya power loss zamanı host storage növbəti açılışda recover olur və "corrupted, reinstall" tələb etmir; exact provider/journaling seçimi `P1 Persistence Materialization Pass` zamanı bağlanır və P1-də default-on saxlanılır.
- SQLite connection setup-da `foreign_keys=ON` pragma və bounded `busy_timeout` məcburi tətbiq olunur.
- WAL, journal və temp artifact-ları `ADR-0054`-ün protected storage boundary-si daxilində qalır və ayrı unprotected fayllara sızmır.
- Storage startup encrypted provider-in aktiv olduğunu sübut etməlidir; provider-disabled və ya provider-bypass konfiqurasiya P1 release blocker-dir.
- config security floor-ları (storage protection, signing verification, KDF, protected-storage minimumları) `appsettings.json` və ya runtime config tərəfindən aşağı salına bilməz; canonical floor list `docs/03_TECH_STACK_AND_VERSIONS.md` Security Floors section-dadır (`ADR-0054`, `ADR-0055`); zəiflədilmə cəhdi startup fail-closed bloklayır və ya owning spec qaydasına uyğun weaker dəyər ignore olunur.

### Pre-login bootstrap closed table set

Pre-login schema bootstrap login UI first paint üçün yalnız aşağıdakı closed table
set-i yarada və ya verify edə bilər:

| Table / area | Purpose | Pre-login reason |
|---|---|---|
| `Users` | Local user identity və Owner bootstrap marker-i | login və first-user Owner creation |
| `Credentials` | Password hash metadata və credential version/salt metadata | credential verification |
| `RememberMeSlot` | single masked saved-login slot metadata; successful login replaces or clears the same slot according to remember-me state | login form prefill |
| `Sessions` | current/last session metadata və invalidation markers | lock/logout/session cleanup |
| `Permissions` | minimal Owner/all-grants və user activation status checks | authenticated shell entry gate |
| `AuditLog` | pre-auth login/recovery/security attempts | audit storage allowed pre-login workload |
| `ConsumedRecoveryNonces` | Owner recovery replay prevention | recovery import before authenticated session |
| `CustomerDeploymentContext` | `customerId`, `deploymentId`, `source`, `protectionScope`, `schemaVersion`, `createdAt`, `updatedAt` ilə single active customer/deployment binding context | license import precondition and current context bootstrap |
| `HostConfiguration` | startup-safe config floor metadata | security floor validation |
| `StorageMetadata` | schema/bootstrap version və provider proof marker | idempotent bootstrap and storage proof |
| `DeviceIdentity` | protected `DobbyStableDeviceId` material, `algorithmVersion`, `protectionVersion`, `createdAt` və redacted support prefix metadata | license device fingerprint bootstrap and support-safe device identity |

Plugin tables, plugin discovery cache, plugin domain data, license-heavy
verification cache, backup/restore data, shared-data mapping profile-ları və
post-login feature tables pre-login bootstrap set-inə daxil deyil.

## Host UI state and favorite storage invariants

- UI state records user scope və stable key ilə unique olmalıdır.
- Favorite records `(UserId, SourcePluginId, ElementId)` natural uniqueness saxlamalıdır.
- Favorite və UI state plugin business data-nı kopyalamamalıdır.
- Pre-login bootstrap tərəfindən yaradılan table-lara toxunan sonrakı migration-lar `IF NOT EXISTS`, column-exists check, migration metadata və ya bənzər re-entrancy guard-ları ilə bootstrap -> migration ardıcıllığında xəta yaratmamalıdır.

## 6. Verification

Release verification minimumları:

- `NFR-007` testləri protected local persistence-in enabled olduğunu yoxlayır;
- storage contract testləri plugin-in yalnız `SharedDomainStorage` və `UserScopedStorage` access aldığını yoxlayır;
- static/dependency scan plugin project-lərinin `Dobby.Infrastructure` və host persistence implementation-a reference saxlamadığını yoxlayır;
- secret material plaintext DB field kimi saxlanmamalıdır;
- `deviceId` key source kimi istifadə olunmamalıdır.
- transactional write testləri multi-step host mutation üçün commit/rollback davranışını yoxlayır;
- crash-recovery smoke test process kill və ya power-loss simulyasiyasından sonra host storage-in növbəti açılışda recover olduğunu yoxlayır.
- `DobbyStableDeviceId` application-level entropy-dən ayrı materialdır, key source kimi istifadə olunmur, protected storage-dən keçir və raw formada log/diagnostics/plugin surface-ə çıxmır.

## Cross-references

- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-007`
- `docs/02_ARCHITECTURE_MAP.md` — `Plugin Storage Contracts`, `Host Persistence`, `Secure Storage Boundary`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — storage və secure storage stack
- `docs/adr/ADR-0003-plugin-storage-boundary-and-access-model.md` — plugin storage method surface and access model
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md`
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md`
- `docs/adr/ADR-0038-local-first-authentication.md`
- `docs/adr/ADR-0054-db-at-rest-protection-provider.md` — exact DB-at-rest provider decision owner
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` — protected-storage scope və key wrapping mapping
- `docs/specs/shared-data-tools.md` — mapping profile storage identity tuple and plugin caller binding for P2 Shared Data Tools.
- `docs/specs/backup-restore-migration.md` — P2 backup/restore artifact model, cross-device restore and future app key lifecycle guardrails.

## Branch Shared Mode Addendum

Storage taxonomy becomes mode-aware under `ADR-0056`:

| Scope | Contents | Provider boundary |
|---|---|---|
| `DeviceLocal` | connection profile, protected credential refs, local diagnostics, bootstrap/setup evidence | local encrypted SQLite/protected storage |
| `BranchShared` | authoritative users, credentials, permissions, audit, business data and host-owned shared plugin key/value data | PostgreSQL over branch LAN |
| `OrganizationShared` | future vocabulary only | no Phase 1 code/schema/provider |

SQLite remains local embedded storage only. Shared-folder, network-drive or
multi-device SQLite usage is prohibited.

In Branch Shared mode, DeviceLocal SQLite retains the same `ADR-0054`
protection requirements as Standalone. PostgreSQL operational protection is
owned by `ADR-0056` and `docs/specs/postgres-deployment-ops.md`.

`branchshared.json` is validated by `docs/schemas/branchshared.schema.json` at
composition-root load time. The file contains no credential values; it contains
only topology/identity material and protected-storage references. Future schema
evolution must not add password, token, private key, raw connection string or
other credential-bearing fields without an ADR/spec amendment; new secret
material stays in protected storage and may be referenced only by non-secret
profile keys.
