# Storage and Data Protection Spec

**Tarix:** 2026-04-27
**Status:** P1 canonical spec
**Phase:** P1-core
**Scope:** Local persistence protection, storage taxonomy, plugin storage compartments və key source boundary

Bu spec `NFR-007`, `ADR-0030` və `ADR-0040` üçün icra dilini saxlayır. Məqsəd DB schema və exact provider seçmək deyil; release boundary, data scope və qoruma invariantlarını bağlamaqdır.

**Controlling decisions:** DB-at-rest provider `ADR-0054`-ün, protected-storage
scope və key wrapping `ADR-0055`-in, deployment/provider mode-ları `ADR-0056`-nın
öhdəliyindədir. Credential/remembered-login/throttle və device/recovery key
material `ADR-0058` ilə, license persistence authority `ADR-0060` ilə, pre-auth
persistence allow-list-i və atomic mutation contract-ı `ADR-0061` ilə bağlanır;
bu qərarlar `ADR-0030`, `ADR-0038`, `ADR-0051` və `ADR-0055`-in ziddiyyətli köhnə
mətnini əvəz edir.

## 1. Local Persistence Protection

Host və plugin local persistence faylları data-at-rest qorunmalıdır. Dobby P1 release-i şifrəsiz/protection-sız production DB faylı ilə göndərilmir.

Normal business data field-level şifrələnmir. Bunun səbəbi query, sort, filter, import/export və report əməliyyatlarının lazımsız ağırlaşmamasıdır.

Yalnız secret sayılan data DB qorumasından əlavə field-level/protected-storage qoruması alır:

- password hash və credential material
- session material
- token/key material
- non-exportable Owner recovery device private key material və recovery
  nonce/request binding ilə bağlı həssas material (`ADR-0058`). License device
  binding üçün ayrıca private key mövcud deyil: binding dəyəri protected stable
  device id üzərindən versioned derivation ilə hesablanır və heç bir public key
  hash-ı deyil (`ADR-0063`)
- protected config secrets

Remembered login bu siyahıya daxil deyil, çünki secret saxlamır: yalnız
normalize olunmuş username və non-secret slot identifier persist olunur
(`ADR-0058`). Password, reversible credential, password-equivalent verifier,
startup snapshot və ya retained password yerinə keçən masked placeholder
saxlanmır. DPAPI storage protection mexanizmidir; onun mövcudluğu replayable
login credential saxlamağı əsaslandırmır. Legacy saved-password qeydləri və
duplicate startup snapshot idempotent migration ilə silinir: secret payload
overwrite/delete olunur və yalnız təhlükəsiz olduqda normalize olunmuş account
selection saxlanılır.

Durable login/unlock throttle ledger-i secret deyil, amma pre-login persisted
security state-dir: `TimeProvider` üzərindən ölçülür, proses restart-ından sonra
qüvvədə qalır, minimum normalize olunmuş account və device/global pressure
bucket ilə açarlanır və backup/restore, repair və retention qaydalarına daxil
olur (`ADR-0058`, `FR-022`, `docs/specs/data-retention.md`).

## 2. Key Source Boundary

DB qoruma açarı istifadəçidən əl ilə istənilmir. İstifadəçi ayrıca "DB password" bilməməlidir.

DB qoruma açarı təkcə `deviceId`-dən törədilmir. `deviceId` identity/support metadata-dır, encryption key mənbəyi deyil.

Qoruma açarı Dobby-nin yaratdığı lokal gizli məlumatlar və cihazın qorunan identity/protection mexanizmləri əsasında avtomatik idarə olunur. Bu mexanizm plugin-ə görünmür və plugin key material, backing file path və provider detalı almır.

Protected-storage scope və key wrapping secret/material tipinə görə `ADR-0055`
ilə bağlanır. Machine-bound material DPAPI `LocalMachine`-ı təkbaşına security
boundary kimi qəbul etmir; Dobby application-level entropy və host storage
boundary ilə birlikdə istifadə olunur. Remember-me slot metadata-sı, session və
plugin `UserScopedStorage` secret-ləri `CurrentUser`-bound davranış tələb edir.

Protected material purpose-separated saxlanılır və bir purpose-un material-ı
digərinin əvəzinə istifadə olunmur (`ADR-0059` key-purpose separation,
`ADR-0058`):

| Material | Purpose | İstifadə sərhədi |
|---|---|---|
| DB/protected-storage wrapping key material | data-at-rest qoruması | imza yaratmır və artifact verification-da istifadə olunmur |
| `DobbyStableDeviceId` | support-safe device identity mənbəyi | key source deyil; yalnız versioned keyed digest kimi göstərilir |
| Owner recovery device key pair (`ADR-0058`) | recovery request signature və possession proof | license binding üçün istifadə olunmur (license `deviceKeyHash` heç bir public key hash-ı deyil — `ADR-0063`); license və plugin artifact-larını imzalamaq üçün istifadə olunmur; private hissə application boundary-dən export olunmur |
| vendor trust anchor-ları (plugin-package, license, recovery-response, setup-authorization) | verification-only public material | leaf purpose-lar bir-birini əvəz etmir |

Bir purpose-un compromise-i digər purpose-a avtomatik authority vermir. Private
key material restrictive ACL ilə protected storage-də qalır, application
boundary-dən export olunmur; scope, rotation və failure davranışı `ADR-0055` və
`ADR-0062` ilə idarə olunur.

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
| `%LocalAppData%/Dobby/backup/` | Encrypted local backup snapshots, verification metadata and restore quarantine | Host |
| `%LocalAppData%/Dobby/temp/` | İşləmə müddətində yaradılmış müvəqqəti fayl-lar | Host |

## Runtime folder recovery

Startup zamanı required runtime folder-lər yoxdursa təhlükəsiz şəkildə yaradılmalıdır. Optional folder silinibsə recreate edilə bilər.

Protected data, key backing material və ya provider metadata itibsə app silent recreate etməməli, `ADR-0055` failure mode-larına uyğun fail-closed davranmalı və support-safe diagnostics göstərməlidir.

Standalone startup recovery has one host-owned order after authentication:
database checkpoint/integrity, post-login migrations, plugin install journal and
projection recovery, plugin staging/orphan/cache cleanup, incomplete temporary
output cleanup, and finally cancellation-state reset. Temporary and backup
maintenance validates canonical roots and refuses reparse-point traversal.

Standalone local backup uses the encrypted SQLite online-backup boundary. A
snapshot retains the same protected database key/device binding, is verified
before publication, and cannot be treated as a portable cross-device package.
Restore preserves current immutable audit evidence and outbox delivery state,
quarantines the previous store before promotion, and rolls back on a failed
transition. Cross-device key transport remains subject to the separate
request/package contract in `backup-restore-migration.md`.

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
- license grant state, `licenseId`, `revisionSeriesId` üzrə persisted maximum
  `licenseRevision`, last-known-good artifact state və import attestation
  metadata (`ADR-0060`, `docs/specs/license-import.md`);
- plugin install/lifecycle/cache state (`docs/specs/plugins-manager.md`);
- host preferences;
- per-user shell UI state və favorites;
- durable login/unlock throttle ledger (`ADR-0058`, `FR-022`);
- purpose-separated device identity/Owner recovery key material, recovery
  request/response artifact state və consumed recovery nonce state (`ADR-0058`);
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
| `LoginThrottleBuckets` | durable login/unlock throttle ledger: `BucketKey` primary key, `BucketKind` (`account` və ya `device` ilə CHECK-məhdud), bounded `FailedAttempts` counter, `WindowStartedAtUtc`/`ThrottledUntilUtc`/`UpdatedAtUtc` UTC instant-ları (`TimeProvider`) və monoton `Version` optimistic-concurrency discriminator-u | restart-durable throttle qərarı login submit-dən əvvəl |
| `RememberMeSlot` | yalnız normalize olunmuş username və non-secret slot identifier; password, reversible credential, password-equivalent verifier və ya masked placeholder surrogate saxlanmır (`ADR-0058`) | login form username prefill |
| `Sessions` | current/last session metadata və invalidation markers | lock/logout/session cleanup |
| `Permissions` | minimal Owner/all-grants və user activation status checks | authenticated shell entry gate |
| `AuditLog` | pre-auth login/recovery/security attempts | audit storage allowed pre-login workload |
| `RecoveryRequests` | pending Owner recovery request ledger və **primary** recovery replay/binding gate: `RequestId` UUID primary key, globally `UNIQUE NonceHash` (64-hex SHA-256; raw nonce persist olunmur), `UNIQUE ResponseId`, `(ConsumedAtUtc, ResponseId)` üzərində paired-null CHECK (ya hər ikisi NULL — pending, ya hər ikisi dolu — consumed; yarım-consumed sətir schema səviyyəsində mümkün deyil) və import zamanı reconstruct olunan payload üzərində yenidən hesablanan `PayloadHash` integrity dəyəri; binding sütunları (`CustomerId`, `DeploymentId`, `DeviceId`, `RecoveryPublicKeyHash`, `VendorKeyId`) və `IssuedAtUtc`/`ExpiresAtUtc` pəncərəsi response import-unda stored row həm də cari authority context ilə tutuşdurulur | recovery request export və vendor-signed response import authenticated session-dan əvvəl baş verir |
| `ConsumedRecoveryNonces` | Owner recovery replay prevention üçün **defense-in-depth** burned-nonce ledger-i: yalnız nonce-un SHA-256 hash-i (`NonceHash` primary key), `ConsumedAtUtc` və mənbə `RequestId` saxlanır; raw nonce heç vaxt persist olunmur. Bu cədvəl primary gate deyil — primary gate `RecoveryRequests` sətirinin atomik consume-udur; bu ledger-in read enforcement point-i snapshot promotion carry-forward-dur: promotion köhnə snapshot-dan pending sətir dirildə bilməsin deyə `DELETE FROM RecoveryRequests WHERE ConsumedAtUtc IS NULL AND NonceHash IN (SELECT NonceHash FROM ConsumedRecoveryNonces)` icra olunur | recovery import before authenticated session |
| `HostOutbox` | pre-login mutation-ların (recovery consume, credential reset, session invalidation) yan-effekt dispatch ledger-i: `OutboxId` primary key, `UNIQUE IdempotencyKey` (at-most-once dispatch), bounded `Purpose`/`MessageType`/`PayloadJson`, `CreatedAtUtc`/`NextAttemptAtUtc`/`AttemptCount`/`CompletedAtUtc`, `LastErrorCode` və `(LeaseId, LeaseExpiresAtUtc)` üzərində paired-null CHECK ilə bounded lease. Payload non-secret olmalıdır: password, verifier, raw nonce və private key material outbox payload-una yazılmır | pre-login recovery/credential mutation-u öz dispatch-i ilə eyni transaction-da atomik commit olunmalıdır |
| `CustomerDeploymentContext` | `customerId`, `deploymentId`, `source`, `protectionScope`, `schemaVersion`, `createdAt`, `updatedAt` ilə single active customer/deployment binding context | license import precondition and current context bootstrap |
| `HostConfiguration` | startup-safe config floor metadata | security floor validation |
| `StorageMetadata` | schema/bootstrap version və provider proof marker | idempotent bootstrap and storage proof |
| `DeviceIdentity` | protected `DobbyStableDeviceId` material, `owner-recovery` purpose-tagged device key public material, versioned key id, `algorithmVersion`, `protectionVersion`, `createdAt` və protected private material referansı; plus internal uniqueness discriminator (`RedactedSupportPrefix`: protected stable device id üzərində domain-separated truncated digest — nə raw material prefiksi, nə support code, nə license binding dəyəri) | Owner recovery possession proof, support-safe device identity və license `deviceKeyHash` derivation girişi (`ADR-0063`) |

Plugin tables, plugin discovery cache, plugin domain data, license-heavy
verification cache, backup/restore data, shared-data mapping profile-ları və
post-login feature tables pre-login bootstrap set-inə daxil deyil.

Remembered-login slot tək slot-dur: uğurlu login remember-me state-inə uyğun
olaraq həmin slot-u əvəz edir və ya təmizləyir. Remember-me OFF seçilmiş
uğurlu login-də və account silinəndə/deaktiv ediləndə slot təmizlənir; logout və
lock slot-u silmir, çünki slot heç bir secret daşımır
(`docs/specs/session-lifecycle.md`). Slot heç bir halda authentication factor
kimi istifadə olunmur.

#### Remembered-login slot legacy migration (owning rule)

`docs/specs/session-lifecycle.md` legacy saved-password qeydlərinin və masked
surrogate slot-un idempotent migration ilə silinməsini bu spec-ə delegate edir;
qayda budur (`PreLoginRememberedAccountMigrationV7`, `ADR-0058`):

- migration slot table-ını yerində `ALTER` etmir, onu **yenidən qurur**: legacy
  shape-in `ProtectedRememberMeBlob`, `ProtectionVersion` və `ExpiresAtUtc`
  sütunları — yəni saved-credential dövrünə aid password-equivalent protected
  material və onun expiry-si — yeni table-da **heç bir formada mövcud deyil**,
  köçürülmür və başqa sütuna transform edilmir. Yeni table `Version` sütununda
  `CHECK (Version = 'username-only-v1')` daşıyır, ona görə də masked-surrogate
  dövrünə aid shape schema səviyyəsində geri yazıla bilməz; sonra legacy table
  `DROP` olunur və yenisi `RememberMeSlot` adına `RENAME` edilir, yəni final
  closed-set adı dəyişmir;
- legacy sətir yalnız o halda carry-forward olunur ki, `SlotId = 'single'`
  olsun, mövcud `Users` sətri ilə `UserId` üzrə join olunsun **və** legacy
  `MaskedUsername` dəyəri həmin user-in `UsernameNormalized` dəyəri ilə eyni
  olsun, üstəgəl uzunluq (3..64) və charset (`a-z0-9_.-`) qaydalarına uysun;
- bu şərtlərdən hər hansı biri pozulursa sətir **carry-forward olunmur**: slot
  itir və user növbəti login-də username-i yenidən yazır. Bu fail-closed
  seçimdir və qəsdəndir — reconcile olunmayan legacy dəyəri prefill etmək
  masked surrogate-i yeni schema-ya qanuniləşdirər (`ADR-0058`);
- migration heç bir password, verifier və ya reversible material köçürmür; carry
  forward olunan yeganə dəyər normalize olunmuş username-dir;
- rebuild `DROP TABLE`-dan əvvəl eyni transaction-da `PRAGMA secure_delete=ON`
  ilə icra olunur, beləliklə carry-forward olunmayan legacy sətirlərin qalığı
  DB free page-lərində oxunaqlı qalmır;
- migration idempotent-dir: `StorageMetadata` migration marker-i sayəsində
  təkrar startup-da yenidən icra olunmur, ona görə də təkrar run slot-u ikinci
  dəfə silmir. Marker müqayisəsi yalnız `MigrationId` mövcudluğuna deyil, həm də
  qeyd olunmuş `ChecksumSha256` dəyərinin cari migration SQL-inin SHA-256-sı ilə
  eyniliyinə baxır; uyğunsuzluq migration-ı fail-closed dayandırır
  (`PersistenceError.MigrationFailed`), yəni artıq tətbiq olunmuş bir migration
  mətninin sonradan redaktəsi startup-da aşkarlanır, sükutla keçilmir;

Rollback: bu migration **geri qaytarılmır**. Downgrade ehtiyacı olarsa yol
snapshot restore-dur (`docs/specs/backup-restore-migration.md`), sətir səviyyəsində
reverse migration deyil, çünki masked surrogate-i bərpa edən reverse script
`ADR-0058`-i pozar.

Pre-login table set-i **closed**-dır: bu cədvəldəki sətirlərdən kənar hər hansı
pre-login persisted state əlavə etmək `ADR-0061` pre-auth allow-list-inə və bu
spec-ə amendment tələb edir. Yeni pre-login state əlavə edilirsə backup/restore,
repair və retention qaydaları da eyni pass-də yenilənir.

Closed set yuxarıdakı 14 sətirdən ibarətdir və onun enforcement point-i
prose deyil, davranışdır: `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`
pre-login migration ardıcıllığını real DB üzərində icra edib faktiki yaranmış
table-ları `ApprovedPreLoginTables` siyahısı ilə tutuşdurur, ona görə də bu
cədvəldə olmayan bir table-ın pre-login bootstrap tərəfindən yaradılması testi
sındırır (`NFR-027`: source-text scan bu closure-u sübut etmir). Bu spec-dəki
table adları migration DDL-indəki adlarla eyni yazılmalıdır — ad drift-i closure
iddiasını yoxlanılmaz edir. Cari mənbə migration-lar:
`PreLoginMigrationV1` (`0001_prelogin_bootstrap`),
`PreLoginRememberedAccountMigrationV7` (`0007_prelogin_remembered_account`),
`PreLoginThrottleMigrationV8` (`0008_prelogin_login_throttle`),
`PreLoginOutboxMigrationV9` (`0009_prelogin_host_outbox`),
`PreLoginRecoveryRequestMigrationV10` (`0010_prelogin_recovery_requests`),
`PreLoginOutboxEvidenceMigrationV11` (`0011_prelogin_outbox_evidence`; yeni table
yaratmır, `HostOutbox`-a `EvidenceCorrelationId` sütunu əlavə edir).

## Host UI state and favorite storage invariants

- UI state records user scope və stable key ilə unique olmalıdır.
- Favorite records `(UserId, SourcePluginId, ElementId)` natural uniqueness saxlamalıdır.
- Favorite və UI state plugin business data-nı kopyalamamalıdır.
- Pre-login bootstrap tərəfindən yaradılan table-lara toxunan sonrakı migration-lar `IF NOT EXISTS`, column-exists check, migration metadata və ya bənzər re-entrancy guard-ları ilə bootstrap -> migration ardıcıllığında xəta yaratmamalıdır.

## 6. Verification

Release verification minimumları:

- `NFR-007` testləri protected local persistence-in enabled olduğunu yoxlayır;
- storage contract testləri plugin-in yalnız `SharedDomainStorage` və `UserScopedStorage` access aldığını yoxlayır;
- dependency/reference scan plugin project-lərinin `Dobby.Infrastructure` və host persistence implementation-a reference saxlamadığını yoxlayır; bu scan architecture shape sübutudur, runtime containment sübutu deyil (`NFR-027`);
- secret material plaintext DB field kimi saxlanmamalıdır;
- `deviceId` key source kimi istifadə olunmamalıdır.
- transactional write testləri multi-step host mutation üçün commit/rollback davranışını yoxlayır;
- crash-recovery smoke test process kill və ya power-loss simulyasiyasından sonra host storage-in növbəti açılışda recover olduğunu yoxlayır.
- `DobbyStableDeviceId` application-level entropy-dən ayrı materialdır, key source kimi istifadə olunmur, protected storage-dən keçir və raw formada log/diagnostics/plugin surface-ə çıxmır.

### 6a. Verification Obligations

Aşağıdakı maddələr behavior-first sübut tələb edir (`ADR-0061`, `NFR-027`).
Source-text, regex, fayl mövcudluğu yoxlaması və ya "test list green" iddiası
onları qapatmır:

| Yoxlama | Minimum sübut |
|---|---|
| Protected provider aktivdir | Startup provider-enabled probe (`ADR-0054`) + byte-level sentinel testi: store-a yazılmış known sentinel plaintext bağlı DB faylının, WAL/journal/rollback/checkpoint side file-larının və temp artifact-larının raw byte-larında tapılmır; provider-disabled, provider-bypass və plain (unencrypted) mövcud DB faylı fixture-ları startup-da fail-closed olur və store-u oxunaqlı açmır. Package reference və ya config assertion-ı bu sətri qapatmır |
| Remembered login secret saxlamır | Login → restart senarisindən sonra persisted slot-un bytes-larını yoxlayan test: password, verifier və ya reversible material yoxdur; password field boş açılır |
| Legacy saved-password migration | Legacy shape fixture (`ProtectedRememberMeBlob` sütunu ilə): migration-dan sonra sütun fiziki olaraq yoxdur, reconcile olunan slot yalnız username kimi qalır, idempotent təkrar icra əlavə dəyişiklik yaratmır; **negative branch də tələb olunur** — masked surrogate, uyğun gəlməyən user, charset və minimum-length pozuntusu fixture-larında slot carry-forward edilmir (sıfır sətir), üstəgəl rebuild olunmuş schema-ya masked surrogate və köhnə `Version` dəyəri yazmaq cəhdi rədd olunur |
| Durable throttle | `TimeProvider` seam-i ilə restart-across test: qalan penalty bərpa olunur, account və device/global bucket-lər müstəqil qalır |
| Pre-login closed set | Real DB üzərində pre-login migration ardıcıllığını icra edən behavior test: `sqlite_master`-dən faktiki yaranmış table siyahısı oxunur və approved closed set ilə **bərabər** olmalıdır (superset/subset kifayət deyil), hər migration marker-i `StorageMetadata`-da qeydə alınır, post-login table və plugin cache toxunulmur. Bu spec-dəki table adları migration DDL-indəki adlarla eyni olmalıdır; ad drift-i bu sətri açıq sayır |
| Atomicity | Fault injection: multi-table mutation (state + audit/outbox) yarımçıq commit qoymur və success publish olunmur |
| Crash consistency | Kill/power-loss simulyasiyası: bootstrap → migration ardıcıllığı re-entrant olur və "corrupted, reinstall" tələb etmir |
| Backup/restore boundary | Snapshot verification, quarantine və failed-transition rollback testləri; snapshot başqa cihazda açılmır |
| Reparse/traversal refusal | Temp/backup maintenance canonical root xaricinə və reparse point-lərə keçmir |
| Purpose separation | Bir purpose-un key material-ı digər purpose-un verification/imza yolunda qəbul edilmir |
| Discriminator ayrılığı | Real migration-dan sonra `DeviceIdentity.RedactedSupportPrefix` dəyəri eyni stable device id üzərindən hesablanan license binding digest-inin nə bərabəri, nə prefiksi, nə də substring-idir (`ADR-0063` § 3); "sütun mövcuddur" və ya source-da domain string-inin görünməsi bu sətri qapatmır |

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
- `docs/adr/ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md` — vendor trust anchor custody, provisioning və key rotation
- `docs/adr/ADR-0052-tenant-customer-context-v2.md` — `CustomerDeploymentContext` field set-i
- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md` — DeviceLocal/BranchShared scope və provider boundary
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — remembered-login no-secret qaydası, durable throttle state, device/recovery key material
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — license grant/revision/attestation persistence authority
- `docs/adr/ADR-0063-device-license-binding-derivation.md` — license device binding derivation girişləri və raw-material qadağası
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — pre-auth allow-list, atomic unit of work, behavior-first proof
- `docs/specs/license-import.md` — license state, `deviceKeyHash` binding və attestation metadata
- `docs/specs/session-lifecycle.md` — remembered-login və throttle davranışı
- `docs/specs/audit-and-diagnostics.md` — audit append-only storage və redaction boundary
- `docs/specs/data-retention.md` — audit/diagnostics/security-state retention floor-ları
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

The profile carries `profileVersion`, pinned to `1` by the owning schema. This
file has no row in `docs/schemas/artifact-compatibility-matrix.md`: that matrix
freezes transition policy for *signed, versioned* artifact families, and
`branchshared.json` is unsigned local configuration. The pin is nonetheless
deliberate rather than incidental. An unrecognised `profileVersion` is rejected as
a shape violation, not accepted and interpreted as version 1, so this file follows
the same no-silent-fallback principle the matrix states for artifacts that do have
rows, and matches the `const` convention every other Dobby schema uses for its own
version. Branch Shared runtime is deferred, so no reader and no committed instance
exists and the pin invalidates nothing today; widening it to admit a version 2 is
an ADR/spec amendment that must also state how a version-1 profile is migrated,
not an implementation detail.

## Revision History

- **2026-08-29:** `branchshared.json` `profileVersion` schema-da `"minimum": 1`
  idi, yəni bu shape-in təsvir etmədiyi istənilən gələcək integer conforming
  sayılırdı və schema oxucuya hansı versiyanı təsvir etdiyini demirdi. Digər bütün
  Dobby schema-ları öz versiyasını `const` ilə ifadə edir; bu yeganə istisna idi.
  Schema `"const": 1`-ə pinned edildi və rejection semantikası bu spec-ə yazıldı.
  Qeyd: `branchshared.json` unsigned local configuration-dır və
  `artifact-compatibility-matrix.md`-də sətri yoxdur — matrix signed versioned
  artifact family-ləri idarə edir, ona görə burada authority deyil, yalnız
  no-silent-fallback prinsipi üzrə uyğunluq analogiyasıdır. Branch Shared runtime
  deferred-dir, nə reader, nə committed instance mövcuddur, ona görə heç bir
  artifact invalid olmur. Qərar dəyişikliyi yoxdur, successor ADR tələb olunmur.

- **2026-08-27:** Pre-login closed table set reconciled with the enforced
  migration reality; closure iddiası HEAD-də üç yerdə yanlış idi. `RecoveryRequests`
  (owner recovery-nin **primary** replay/binding gate-i, `PreLoginRecoveryRequestMigrationV10`)
  və `HostOutbox` (`PreLoginOutboxMigrationV9`; `ADR-0061` atomicity modelinin
  pre-auth mutation üçün tələb etdiyi durable outbox) cədvəldə heç mövcud deyildi,
  `LoginThrottleState` adı isə migration-dakı faktiki `LoginThrottleBuckets` adı
  ilə uyğun gəlmirdi — bu ad drift-i əvvəlki **2026-08-25** entry-sində də
  təkrarlanıb (həmin tarixi entry olduğu kimi saxlanılır, düzəliş burada qeyd
  olunur). `ConsumedRecoveryNonces` sətri gerçək rolunu bildirəcək formaya
  gətirildi: yalnız nonce-un SHA-256 hash-ı saxlanır, o primary gate deyil
  defense-in-depth-dir və read enforcement point-i snapshot promotion
  carry-forward-dur. Closed set-in enforcement point-i (real DB üzərində
  pre-login migration icrası + `ApprovedPreLoginTables` müqayisəsi) və mənbə
  migration siyahısı explicit yazıldı, çünki əvvəl closure iddiasının nə ilə
  yoxlandığı sənəddə görünmürdü (`NFR-027`). Propagation: `ADR-0061` pre-auth
  allow-list-i Owner recovery səthini əhatə edəcək şəkildə tamamlandı,
  `docs/specs/session-lifecycle.md` pending-request state təsviri "requestId /
  nonce / expiresAt local saxlanılır" ifadəsindən hash-only ledger invariantlarına
  keçirildi.

- **2026-08-27:** `Remembered-login slot legacy migration (owning rule)`
  subsection-ı əlavə olundu. `docs/specs/session-lifecycle.md` legacy
  saved-password/masked-surrogate migration qaydasını bu spec-ə delegate edirdi,
  lakin qayda burada mövcud deyildi — dangling delegation. Artıq
  `PreLoginRememberedAccountMigrationV7`-nin faktiki davranışı normativ yazılıb:
  rebuild-and-rename, `ProtectedRememberMeBlob`/`ProtectionVersion`/`ExpiresAtUtc`
  sütunlarının tamamilə itməsi, `CHECK (Version = 'username-only-v1')`,
  reconcile olunmayan legacy sətrin fail-closed **carry-forward edilməməsi**,
  `PRAGMA secure_delete=ON`, checksum-gated idempotency və reverse migration-un
  olmaması (rollback yolu snapshot restore-dur).

- **2026-08-26:** Internal consistency repair: § 1 secret siyahısındaki
  "non-exportable device identity private key material (license-binding purpose)"
  sətri `ADR-0063` ilə ziddiyyət yaradırdı (belə key pair mövcud deyil) — Owner
  recovery device private key + `ADR-0063` derivation qeydi ilə əvəz olundu; § 2
  purpose cədvəlindəki iki dublikat "Owner recovery ... key pair" sətri tək
  normativ sətirdə birləşdirildi.

- **2026-08-26:** `ADR-0063` reconciliation: license binding üçün ayrıca "device
  identity key pair" sətri ləğv edildi (belə key pair mövcud deyil və binding
  public-key hash-ı deyil), `DeviceIdentity` sətri həqiqi saxlanan sahələrə —
  `owner-recovery` public material və domain-separated internal uniqueness
  discriminator — uyğunlaşdırıldı.
- **2026-08-26:** `Verification Obligations` cədvəlinin `Protected provider
  aktivdir` sətri behavior-first formaya gətirildi: startup provider-enabled
  probe (`ADR-0054`) plus byte-level sentinel scan (DB + WAL/journal/rollback/
  checkpoint + temp) və provider-disabled / provider-bypass / plain-DB
  fixture-ları tələb olunur; package reference və ya config assertion-ı bu sətri
  qapatmır (`AC-037`, `AC-042`, `NFR-027`).
- **2026-08-25:** Protected-material və v3 reconciliation. Remembered-login
  no-secret qaydası, legacy saved-password migration, purpose-separated material
  cədvəli, durable `LoginThrottleState` pre-login table-ı, `deviceKeyHash`
  binding terminologiyası, license persistence authority-nin `ADR-0051`-dən
  `ADR-0060`-a keçidi, closed-set amendment qaydası, behavior-first verification
  obligations və genişləndirilmiş cross-reference-lər body-yə köçürüldü; ayrıca
  `2026-08 Protected-Material Amendment` bölməsi ləğv edildi, çünki onun
  qərarları artıq müvafiq bölmələrin normativ mətnindədir.
- **2026-04-27:** İlk canonical storage/data protection spec-i.
