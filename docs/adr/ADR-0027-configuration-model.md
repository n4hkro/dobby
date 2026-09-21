# ADR-0027: Configuration Model

**Status:** Accepted
**Date:** 2026-04-30

## Context

`Dobby.Configuration` P1-core project kimi xəritədə mövcuddur, amma config source,
override, validation, protected values və plugin configuration boundary açıq idi.
Skeleton `appsettings.json`, user/runtime config və security floors üçün tək model
götürməlidir.

## Decision

### 1. Configuration ownership

`Dobby.Configuration` application config, user preferences, plugin settings və
feature/capability settings contract-larının host owner-idir. Physical persistence
və protected storage adapter-ləri `Dobby.Infrastructure` tərəfindən implement edilir
və `Dobby.Host` composition root-da wire olunur.

### 2. Source order

Phase 1 source order:

1. packaged defaults / `appsettings.json`;
2. host runtime config override under `%LocalAppData%/Dobby/config/`;
3. environment variables for developer/operator scenarios;
4. development user secrets only in dev builds.

Customer production behavior runtime config faylı və validated options üzərindən
idarə olunur. Config source-ları login UI first paint-i bloklayan heavy IO və ya
network dependency yaratmır.

### 3. Validation və defaults

Typed options validation startup-da fail-fast/fail-closed işləyir. Missing optional
dəyərlər documented default alır; malformed security-critical dəyər ignore edilmir,
startup fail-closed və ya owning spec-in göstərdiyi safe fallback ilə nəticələnir.

### 4. Security floors

Config aşağıdakı floor-ları aşağı sala bilməz:

- DB-at-rest provider requirement;
- protected-storage minimumları;
- password hashing/KDF minimumları;
- signature/trust verification;
- no-telemetry default;
- audit redaction boundary.

Config bu floor-ları zəiflətməyə çalışarsa Dobby startup fail-closed davranır və
support-safe diagnostics yazır.

### 5. Plugin configuration boundary

P1 plugin-facing settings access surface-i təqdim etmir. Pluginlər host config,
raw appsettings, security floors, license binding detail, protected secret
material və başqa plugin config-lərini görmür və physical config path almır.

Plugin-declared settings schema, validation və `Dobby.Contracts` / `Dobby.SDK`
üzərindən plugin settings access-i P2-deferred scope-dur. P2 bu surface-i açarsa,
host normalize edilmiş schema/validation sahibi qalmalı, plugin yalnız öz
declared settings-lərinə çata bilməli və host/security/license config boundary-si
qorunmalıdır.

### 6. Protected values

Token/key/session/remember-me/recovery material və protected config secrets raw
config file-da saxlanmır. Protected values `ADR-0055` scope/key wrapping modelinə
uyğun saxlanılır.

### 6a. P1 Configuration Schema

ADR-0027 P1 üçün yeganə configuration schema owner-idir. Skeleton `appsettings.json`,
runtime override və environment variable binding-i aşağıdakı section/key modelindən
götürməlidir. Başqa ADR/spec yalnız öz davranış qaydasını saxlayır; config section,
key adı, default və environment variable adı burada canonical qalır.

Environment variable mapping deterministic-dir: top-level `Dobby` section-u
`DOBBY` prefix-inə çevrilir və `DOBBY__<SECTION>__<KEY>` forması istifadə olunur.
Məsələn `Dobby:Runtime:DataRoot` -> `DOBBY__RUNTIME__DATAROOT`. Nested section-lar
üçün `__` separator-u saxlanılır. Environment variable-lar developer/operator
override üçündür; customer production default-u runtime config faylı və validated
options üzərindən idarə olunur.

| Section | Key | Type / unit | Default | Validation / floor | Env var | Owner |
|---|---|---|---|---|---|---|
| `Dobby:Runtime` | `DataRoot` | path | `%LocalAppData%/Dobby/` | absolute or profile-relative local path; network/share path P1 local storage üçün qəbul edilmir | `DOBBY__RUNTIME__DATAROOT` | `docs/specs/storage-and-data-protection.md` |
| `Dobby:Runtime` | `EnvironmentName` | string | `Production` | `Production`, `Development`, `Test` | `DOBBY__RUNTIME__ENVIRONMENTNAME` | this ADR |
| `Dobby:Storage` | `ProviderMode` | string enum | `ProviderNeutralSkeleton` until materialization pass | `ProviderNeutralSkeleton` or later approved production provider; unprotected production DB mode forbidden | `DOBBY__STORAGE__PROVIDERMODE` | `ADR-0054`, storage spec |
| `Dobby:Storage` | `RequireDataAtRestProtection` | bool | `true` | cannot be set to `false` for production release | `DOBBY__STORAGE__REQUIREDATAATRESTPROTECTION` | `ADR-0054`, `NFR-007` |
| `Dobby:Storage` | `BusyTimeoutMilliseconds` | integer ms | `5000` | min `100`, max `30000` | `DOBBY__STORAGE__BUSYTIMEOUTMILLISECONDS` | storage spec |
| `Dobby:Auth` | `LoginThrottleStepsSeconds` | integer array seconds | `[0,0,10,30,60]` | later attempts use final value; values cannot be lower than this floor without ADR amendment | `DOBBY__AUTH__LOGINTHROTTLESTEPSSECONDS` | `ADR-0038` |
| `Dobby:Auth` | `PasswordKdfMode` | string enum | `HumanApprovedPinRequired` until skeleton/toolchain decision | `Argon2id`, `Pbkdf2Sha256`, or stop condition; floors in ADR-0038 cannot be weakened | `DOBBY__AUTH__PASSWORDKDFMODE` | `ADR-0038`, tech stack |
| `Dobby:Session` | `AbsoluteTimeoutMinutes` | integer minutes | `480` | min `15`, max `1440` | `DOBBY__SESSION__ABSOLUTETIMEOUTMINUTES` | `ADR-0031` |
| `Dobby:Session` | `IdleTimeoutMinutes` | integer minutes | `30` | min `5`, max `480` | `DOBBY__SESSION__IDLETIMEOUTMINUTES` | `ADR-0031` |
| `Dobby:Session` | `AutoLockEnabledByDefault` | bool | `false` | user may enable per profile; default remains passive | `DOBBY__SESSION__AUTOLOCKENABLEDBYDEFAULT` | `ADR-0031`, settings spec |
| `Dobby:Diagnostics` | `TechnicalLogRetentionDays` | integer days | `30` | can be reduced only by explicit support/privacy policy; redaction floor cannot change | `DOBBY__DIAGNOSTICS__TECHNICALLOGRETENTIONDAYS` | audit/diagnostics spec |
| `Dobby:Diagnostics` | `TechnicalLogTotalSizeCapMegabytes` | integer MiB | `256` | finite cap required for release; min `32`, max `2048` | `DOBBY__DIAGNOSTICS__TECHNICALLOGTOTALSIZECAPMEGABYTES` | audit/diagnostics spec |
| `Dobby:Audit` | `RetentionDays` | integer days | `365` | config may only increase this value unless future legal/support policy says otherwise | `DOBBY__AUDIT__RETENTIONDAYS` | audit/diagnostics spec |
| `Dobby:Localization` | `DefaultCulture` | BCP-47 culture | `az-AZ` | P1 default cannot change without ADR-0043 amendment | `DOBBY__LOCALIZATION__DEFAULTCULTURE` | `ADR-0043` |
| `Dobby:Localization` | `SupportedCultures` | string array | `["az-AZ","en-US","tr-TR"]` | must include `az-AZ` | `DOBBY__LOCALIZATION__SUPPORTEDCULTURES` | `ADR-0043`, tech stack |
| `Dobby:Plugins` | `AllowPreLoginDiscovery` | bool | `false` | cannot be true in P1; login-first policy owns the floor | `DOBBY__PLUGINS__ALLOWPRELOGINDISCOVERY` | `ADR-0041`, plugins manager spec |
| `Dobby:Licensing` | `ImportRequiresPermission` | bool | `true` | cannot be false; `plugins.license.import` remains required | `DOBBY__LICENSING__IMPORTREQUIRESPERMISSION` | `ADR-0060`, license import spec |
| `Dobby:BranchShared` | `RuntimeEnabled` | bool | `false` | current pass is docs-only/Tier A shape; enabling runtime requires later approved implementation phase | `DOBBY__BRANCHSHARED__RUNTIMEENABLED` | `ADR-0056`, `ADR-0057` |

Protected value handling:

- raw password, token, session, remember-me, recovery, signing-key and protected
  config secret values are never stored in plain config;
- config may store only protected-reference identifiers or non-secret topology;
- malformed protected references fail closed instead of silently recreating secret
  material;
- a config value cannot lower security floors from `ADR-0038`, `ADR-0054`,
  `ADR-0055`, signing verification or diagnostics/audit redaction policy.

Startup validation behavior:

- unknown top-level `Dobby:*` sections are warnings in development and diagnostics
  entries in production, but they do not change runtime behavior;
- malformed or out-of-range security/session/storage/audit values fail startup
  closed unless the owning spec explicitly defines a safe fallback;
- optional missing values take the defaults above;
- skeleton must bind typed options from this table and must not create competing
  option section names in downstream specs.

### 7. Feature flags

Feature flag model P2-də dərinləşir. P1-də config feature flags licensing və
authorization boundary-ni bypass edə bilməz; flag yalnız hüququ olan capability-nin
texniki aktiv/passiv olmasını idarə edə bilər.

## Consequences

- Skeleton config projects, options validation və composition-root wiring üçün
  deterministic model alır.
- Plugin config host storage/path/detail sızdırmadan contracts/SDK boundary-də qalır.
- Security floors runtime config ilə zəiflədilmir.

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 44-47 `Dobby.Configuration`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — config/options package decisions və Security Floors
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- `docs/specs/storage-and-data-protection.md`

## Branch Shared Configuration Amendment

Branch Shared connection profile configuration is non-secret topology/identity
material validated by `docs/schemas/branchshared.schema.json`. It may include
host, port, database, `branchDeploymentId`, local device reference and
`serverIdentityPinMaterial`.

Password, connection string and credential values are not stored in the JSON
profile. Runtime and setup credential values live behind protected-storage
references governed by `ADR-0055`.
