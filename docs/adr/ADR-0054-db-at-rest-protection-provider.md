# ADR-0054: DB-at-Rest Protection Provider

**Status:** Accepted (Option A, 2026-04-28)
**Date:** 2026-04-28

## Context

`ADR-0030` və `NFR-007` host və plugin local persistence fayllarının
data-at-rest qorunmasını release boundary kimi bağlayır. `docs/specs/storage-and-data-protection.md`
də unprotected production DB file-ı release blocker elan edir.

Hazırkı stack SQLite + Dapper local persistence modelini saxlayır, amma exact
DB-at-rest protection provider hələ canonical qərar kimi bağlanmayıb. Bu boşluq
`P1 Persistence Materialization Pass` / security implementation zamanı gizli
texniki seçimə çevrilərsə,
release gate test edilə bilməyən niyyət kimi qalır.

Bu ADR exact provider qərarının sahibidir. `ADR-0030` storage taxonomy,
plugin boundary və key-source invariantlarını saxlayır; bu ADR yalnız backing
persistence faylının necə qorunacağını bağlayır.

## Decision

Phase 1 DB-at-rest provider direction **Option A** kimi qəbul edilir: Dobby host
və plugin local persistence backing faylları SQLite full-DB encryption layer-i
ilə qorunmalıdır. P1 direction `SQLite3MultipleCiphers` / SQLCipher-compatible
full-DB protection layer-dir.

Bu qərar `ADR-0030` və `NFR-007` invariantlarını saxlayır: şifrəsiz production
DB, WAL/journal/temp artifact və provider-disabled konfiqurasiya P1 release üçün
qəbul edilmir.

Minimum security floor:

- full DB encryption provider minimum AES-256-level confidentiality və integrity
  protection təmin etməlidir; ekvivalent modern authenticated cipher də qəbul
  olunur;
- konkret cipher, KDF parametrləri, native build/package strategy,
  package/version pinning və provider-enabled startup check `P1 Persistence
  Materialization Pass` / security implementation round-da bağlanır;
- DB qoruma açarı user-dən əl ilə istənilmir və təkcə `deviceId`-dən törədilmir;
- plugin provider, key material, backing file path və raw DB detail görmür;
- secret field-lər yenə field-level/protected-storage qoruması alır;
- multi-step host mutation-lar transactional commit/rollback davranışını saxlayır;
- crash/power-loss smoke testindən sonra storage recover olur.

P1 üçün full-store provider kimi qəbul edilməyən yollar:

- **Option B — explicit threat carve-out:** rədd edildi. Offline disk, copied DB,
  backup dump və portable media riskləri Dobby Phase 1 threat modelində qalır; bu
  carve-out `ADR-0030`, `NFR-007`, storage spec və `AC-037` rewrite tələb edərək
  security boundary-ni zəiflədərdi.
- **MSIX containerization:** runtime access isolation verir, amma diskdəki DB
  faylını encryption-at-rest ilə qorumaq demək deyil.
- **Field-level secret protection only:** password/token/recovery kimi secrets
  üçün əlavə qoruma olaraq qalır, amma backing DB faylını bütöv qorumaq
  invariantını əvəz etmir.
- **Custom DPAPI-wrapped SQLite blob:** SQLite journaling/WAL/atomicity/crash
  safety modelini host-un əl ilə blob encrypt/decrypt pipeline-ına bağlayır və
  Phase 1 üçün fragility yaradır.
- **EFS as default provider:** cross-user Owner recovery, profile-bound key
  lifecycle və recovery certificate/DRA idarəetməsi səbəbindən Phase 1 default
  kimi qəbul edilmir; enterprise deployment variantı kimi ayrıca qiymətləndirilə
  bilər.

## Considered Alternatives

Phase 1 üçün bu ADR iki real yolu nəzərdən keçirdi:

| Option | Meaning | Effect |
|---|---|---|
| A — SQLite full-DB encryption | `SQLite3MultipleCiphers` / SQLCipher-compatible full-DB protection layer P1 provider olur | `ADR-0030` və `NFR-007` invariantı olduğu kimi qalır; native build/package health proof skeleton/security round-da bağlanır |
| B — Explicit threat carve-out | Offline disk/backup dump attack P1 scope-dan explicit çıxarılır | `ADR-0030`, `NFR-007`, storage spec və `AC-037` rewrite tələb edərək security boundary-ni zəiflədir |

Option A seçildi: `ADR-0030` və `NFR-007` invariantları saxlandığı üçün
backing DB faylının bütöv qorunması canonical release boundary-dir. Option B
yalnız owner offline-disk threat modelini Phase 1-dən explicit çıxarmaq
istəsəydi qəbul ediləcəkdi; bu yol security weakening kimi ayrıca görünür və
mövcud invariantlar bunu tələb etmir.

## Skeleton Commit 1 Provider-Neutral Gate

Initial P1 skeleton commit storage/DB-at-rest sahəsində provider-neutral qalmalıdır.
Bu commit production DB-at-rest provider package-i, concrete migration, entity,
table schema, concrete repository və native encrypted SQLite bootstrap yaratmır.
Storage-facing public/host abstractions yalnız `ADR-0027` config schema, storage
taxonomy və public contract qərarları ilə uyğun adlandırıla bilər; exact type names
public-contract freeze ilə eyni patch-də saxlanmalıdır.

`P1 Persistence Materialization Pass` bu ADR-in Option A release invariantını
materialize edən ayrıca gate-dir. Həmin pass production provider package/cipher/KDF
seçimi, native binary supply-chain proof, startup provider-enabled probe,
migration/entity/table schema və concrete repository ownership-i bağlayır. Bu gate
keçmədən `Directory.Packages.props` içində production DB-at-rest provider package-i
release evidence kimi göstərilə bilməz.

## Implementation Guardrails For Skeleton/Security Round

Bu ADR hazırkı docs-only mərhələdə package və native binary yaratmır. Lakin
`P1 Persistence Materialization Pass` / security implementation round aşağıdakı
guardrail-ləri pozmamalıdır:

- SQLite full-DB encryption üçün custom native provider yolu seçilərsə,
  implementation `Microsoft.Data.Sqlite.Core` + `SQLitePCLRaw.core` + explicit
  native provider bootstrap modelini qiymətləndirməlidir; full bundled
  `Microsoft.Data.Sqlite` şifrəsiz native SQLite gətirə biləcəyi üçün production
  provider proof-u kimi qəbul edilmir.
- Skeleton commit 1 `Directory.Packages.props` yaradırsa SQLite package stance-i
  `docs/03_TECH_STACK_AND_VERSIONS.md` ilə uyğun olmalıdır: production DB-at-rest
  provider package-ləri initial skeleton-da pin olunmur; `Microsoft.Data.Sqlite.Core`
  və `SQLitePCLRaw.core` yalnız future materialization/security-round candidate
  kimi qala bilər və release evidence kimi istifadə edilməməlidir.
- Native encrypted SQLite provider proof keçmədən heç bir production DB-at-rest
  provider package default provider kimi `Directory.Packages.props`-a əlavə edilə
  bilməz. Candidate package set yalnız deferred security-round item kimi qala bilər
  və release acceptance evidence sayılmır.
- `SQLitePCLRaw.bundle_e_sqlite3mc`, `SQLitePCLRaw.bundle_e_sqlcipher` və ümumi
  `SQLitePCLRaw.bundle_*` encrypted SQLite bundle-ları default yol kimi istifadə
  olunmur; deprecated/wrong-native-provider riski yalnız ayrıca ADR amendment ilə
  qəbul edilə bilər.
- Native encrypted SQLite binary supply-chain qorunması üçün dəstəklənmiş
  yoldan əldə olunmalıdır — məsələn upstream `SQLite3MultipleCiphers`
  layihəsindən self-built. Naməlum third-party prebuilt binary default yol
  kimi qəbul edilmir; konkret build pipeline və version pin gələcək
  skeleton/security round-da bağlanır.
- Native asset path və bootstrap class adı implementation detail-dir. Gələcək
  skeleton round audit visibility üçün `native/win-x64/...` kimi path-i sənədləşdirə
  bilər, amma bu ADR exact class/path requirement qoymur.
- Konkret cipher/KDF seçimi bu ADR-in AES-256-level confidentiality + integrity
  floor-unu saxlamalı, həmçinin support və tooling compatibility-ni
  qiymətləndirməlidir (məsələn SQLCipher-compatible ecosystem, DB Browser və
  forensic/support tooling dəstəyi).

### Startup provider-enabled probe

Release candidate host startup storage initialization zamanı provider-enabled probe
işlətməlidir. Probe minimum bunları sübut edir:

- selected native encrypted SQLite provider loaded və active-dir;
- provider-disabled və ya provider-bypass configuration fail-closed edir;
- `foreign_keys=ON`, bounded `busy_timeout` və WAL/journal/temp protection
  guardrail-ləri aktivdir;
- probe output support-safe diagnostics/evidence kimi saxlanır və plugin-ə provider,
  key material, native path və raw DB detail göstərmir.

## Consequences

- `ADR-0030` storage taxonomy və plugin boundary qərarını saxlayır; DB-at-rest
  provider direction bu ADR-də Option A kimi bağlanır.
- `docs/specs/storage-and-data-protection.md`, `NFR-007` və `AC-037` şifrəsiz
  production persistence-i release blocker kimi saxlamağa davam edir.
- `Directory.Packages.props` içində production DB-at-rest provider pin-ləri,
  native binaries, SQLite encryption provider package, KDF/cipher parametrləri,
  provider-enabled startup check və build scripts bu docs-only amendment round-unda
  və initial provider-neutral skeleton commit-də yaradılmır; `P1 Persistence
  Materialization Pass` / security implementation planının öhdəliyindədir.
- Option B seçilmədiyi üçün offline disk / backup dump threat P1 scope-dan
  çıxarılmır.

## Cross-references

- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — storage taxonomy və protection boundary
- `docs/specs/storage-and-data-protection.md` — executable storage/data protection spec
- `docs/03_TECH_STACK_AND_VERSIONS.md` — SQLite + Dapper stack və provider reference
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-007`
- `docs/07_ACCEPTANCE_CRITERIA.md` — DB-at-rest provider release-gate criterion
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` — DB-at-rest provider key / DEK wrapping scope

## Revision History

- **2026-04-28:** Option A qəbul edildi — SQLite full-DB encryption
  (`SQLite3MultipleCiphers` / SQLCipher-compatible) Phase 1 DB-at-rest provider
  direction-u oldu. AES-256-level confidentiality və integrity protection minimum
  cipher floor əlavə olundu; konkret cipher/KDF parametrləri, native build,
  package selection və version pinning `P1 Persistence Materialization Pass` /
  security implementation round-a saxlanıldı. Option B (offline disk threat carve-out) rejected alternatives
  siyahısına əlavə olundu.
- **2026-04-28 (hardening):** `Considered Alternatives` bölməsi əlavə olundu —
  Option A vs Option B side-by-side cədvəl və Option A seçim rationale-ı
  review hardening zamanı canonical ADR-ə köçürüldü; əvvəl ayrıca saxlanılan
  deliberation context indi bütünlüklə bu sənəddədir.

## PostgreSQL Scope Amendment

This ADR remains the owner of SQLite/local-file DB-at-rest protection. Branch
Shared PostgreSQL data-directory protection is a mode-bounded operational floor
owned by `ADR-0056` and `docs/specs/postgres-deployment-ops.md`.

This does not revive the rejected SQLite/local-file Option B. DeviceLocal SQLite
protection still applies in both Standalone and Branch Shared mode. PostgreSQL
release evidence must document service account/data-directory ACLs, encrypted
backup artifacts where selected, restore smoke and post-restore schema/grant
validation.
