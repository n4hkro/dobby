# ADR-0036: Plugin Manifest Schema

**Status:** Accepted
**Date:** 2026-04-22

## Context

Plugin framework discovery, compatibility və shell composition davranışını
manifest üzərindən idarə edir. Əgər manifest schema loose string siyahıları kimi
qalarsa validation zəifləyər, contract drift artar və host fərqli yerlərdə
eyni mənanı yenidən şərh etməli olar.

Arxitektura xəritəsi Phase 1 üçün manifest modelini artıq typed declarative
schema kimi formalaşdırır və closed vocabulary-ləri konkret sadalayır.

## Decision

Plugin manifest schema Phase 1-də typed declarative modeldir. Aşağıdakı shape
canonical hesab olunur:

Machine-readable schema artifact `docs/schemas/dobby-plugin-manifest.schema.json`
bu ADR-in canonical JSON Schema 2020-12 representation-ıdır. Bu ADR prose
semantics və validation rationale owner-i olaraq qalır; JSON Schema skeleton
validator, sample fixture və package validation tests üçün canonical machine-readable
inputdur.

### Property naming convention

Manifest root və nested property adları canonical olaraq `camelCase` saxlanılır.
JSON Schema validation bu casing-i enforce etməlidir; PascalCase, snake_case və
alternate alias field-lər Phase 1 package formatında qəbul edilmir. Field
comparison-ları culture-sensitive UI text kimi yox, ordinal/culture-invariant
schema key kimi aparılır.

### Identity və integrity field-ləri (plugin-level, root)

- `pluginId` — ordinal (culture-invariant) ascending müqayisə olunan stable identifier; reverse-domain convention tövsiyə olunur (`com.vendor.plugin-name`). Boş və ya whitespace qiymət validation failure-dır.
- `version` — SemVer 2.0.0 compatible string (`MAJOR.MINOR.PATCH` + optional pre-release). Pre-release versiyalar Phase 1-də rədd olunur; yalnız stable build-lər qəbul olunur.
- `vendorId` — `Dobby.Security / Plugin Trust Verification` tərəfindən tanınan vendor identifier. Boş qiymət və ya trust store-da olmayan dəyər manifest validation failure-dır.
- `minHostVersion` — plugin-in çalışa bilməsi üçün minimum host versiyası (SemVer). Host versiyası bu dəyərdən kiçikdirsə plugin activation-a buraxılmır (ADR-0037 compatibility uyğun).
- `entryAssembly` — plugin-in əsas assembly path-i paket daxilində (relative path).
- `signature` — paket signature bloku. Canonical signature placement və payload
  shape `ADR-0045` tərəfindən, ECDSA/trust verification semantics isə `ADR-0032`
  tərəfindən idarə olunur. İmza olmayan manifest validation failure-dır və trust
  verification baş tutmur. `signature.keyVersion` Phase 1-də numeric `1`-dir;
  gələcək signing-key rotation yalnız `ADR-0033` amendment-i ilə açıla bilər.

### Management/display metadata field-ləri

- `displayName` — Plugins list və shell contribution-larında göstərilən qısa ad.
  Boş və ya whitespace dəyər validation failure-dır.
- `description` — Plugins list-də göstərilən qısa təsvir. Phase 1-də maksimum
  qısa plain-text metadata kimi qəbul olunur; rich markup və remote content yoxdur.
- `icon = { path }` — optional plugin icon metadata. `path` package daxilində
  canonical relative path-dir və `ADR-0045` path canonicalization qaydalarına
  tabedir. Icon yalnız host-validated PNG asset ola bilər; minimum source ölçüsü
  `64x64` olmalıdır. Missing, invalid, unsupported və ya validation-dan keçməyən
  icon manifest validation failure yaratmır, amma host default plugin icon-u ilə
  əvəz olunur. Runtime plugin SVG loader P1-də açılmır.

Bu field-lər plugin runtime code execution olmadan oxunan metadata-dır və
`ADR-0008` login-visible cached metadata qaydası ilə cache-lənə bilər.

### Structural field-lər (arrays)

- `dependsOn = { pluginId, minVersion }` — plugin dependency graph girişləri
- `capabilities = { name, scope }` — elan olunan capability-lər
- `operationTargets = { id, verbs, labelKey }` — signed manifest metadata-dan
  package-validation zamanı permission registry-yə seed edilən plugin-owned
  operation target-lər. Bu field `ADR-0048` operation verb vocabulary-si ilə
  bağlıdır və plugin assembly load-dan əvvəl validate olunur.
- `extensionPoints = { id, target, routeKey, labelKey, parentId, group, weight }`
  — shell/navigation composition girişləri. `id` plugin manifest daxilində unique,
  stable contribution identifier-dir. `target` Phase 1 closed vocabulary-dən gəlir.
  `routeKey` plugin page route-u üçün stable, non-localized key-dir və host
  tərəfindən `ADR-0013` modelinə uyğun `plugin/{pluginId}/{routeKey}` formasına
  normalize edilir. `labelKey` plugin-owned localization/resource key-dir;
  `target == "top-toolbar"` olan top-level plugin contribution üçün omitted
  olarsa root `displayName` fallback kimi istifadə oluna bilər; bütün
  non-`top-toolbar` contribution-lar üçün required-dır. Bu wording ADR-0049
  TopToolbar top-level modelinə uyğundur. `parentId` optional-dır və
  eyni manifest-dəki başqa `extensionPoints.id` dəyərinə referans verir; cross-plugin
  parent reference Phase 1-də qəbul edilmir. `group` və `weight` ordering
  field-ləridir.
- `files = { path, sha256 }` — package completeness/hash manifest-i; verification
  semantics və deterministic sort qaydası `ADR-0045` tərəfindən idarə olunur

Closed vocabulary-lər Phase 1 üçün explicit və host-defined set-dir.

### Array declaration order semantics

`extensionPoints` array-inin declaration order-i `ADR-0005 UI Ordering Model`-in `stable tiebreak` semantics-i üçün significant-dir: eyni `pluginId` və eyni normalized `weight` olan iki extension için manifest-dəki array index ascending müqayisə olunur. Bu semantic plugin author-lara deterministik ordering verir.

JSON Schema default-ları validation zamanı loaded manifest object-inə in-place
yazılmamalıdır. `group` və `weight` default-ları yalnız in-memory typed
projection-a tətbiq olunur; manifest faylının array order-i və canonical signed
payload bytes-ı validator default-fill reserialization-u ilə dəyişdirilmir.

`dependsOn` və `capabilities` array-lərinin declaration order-i runtime behavior-a təsir etmir.

`operationTargets` P1-də plugin permission target-lərinin canonical source-udur.
Hər target `id`, non-empty `verbs` və `labelKey` daşımalıdır:

- `id` plugin-owned namespace formatındadır:
  `<plugin-id-namespace>.<segment>[.<segment>...]`. Target başqa plugin-in
  namespace-ini claim edə bilməz.
  Target id ASCII-only olmalıdır: segment-lər `[A-Za-z0-9][A-Za-z0-9_-]*`
  formasındadır, nöqtə segment separator-udur, id nöqtə ilə başlaya və ya bitə
  bilməz və empty segment qəbul edilmir. Bu plugin-owned namespace qaydası
  yalnız `operationTargets[].id` üçündür; `extensionPoints[].id` üçün
  plugin-id prefix requirement yaratmır.
- `verbs` `ADR-0048` closed verb list-inin non-empty subset-i olmalıdır:
  `view`, `create`, `update`, `delete`, `execute`, `import`, `export`,
  `print`, `configure`, `grant`.
- `labelKey` plugin-owned localization/resource key-dir. Raw user-facing label
  string-ləri P1 manifest metadata-sında qəbul edilmir.
- Duplicate target IDs, unknown verbs, empty `verbs`, invalid IDs, missing
  `labelKey` və raw label text package validation failure-dır.

`capabilities = { name, scope }` və `operationTargets` ayrı anlayışlardır.
`capabilities` functional capability category-lərini elan edir;
`operationTargets` permission grid/registry target-lərini seed edir. Onlar eyni
manifest-də birlikdə mövcud ola bilər, amma biri digərinin source-u deyil.

`capabilities.scope` yalnız aşağıdakı dəyərləri qəbul edir:

- `shell`
- `plugin-page`
- `background-task`

`extensionPoints.target` yalnız aşağıdakı dəyərləri qəbul edir:

- `top-toolbar`
- `sidebar`
- `third-bar`
- `plugin-page-region`

`extensionPoints.group` Phase 1-də yalnız `default` dəyərini qəbul edir.
Omitted `group` `default` kimi normallaşdırılır. Explicit non-`default` dəyər
manifest validation failure hesab olunur.

`extensionPoints.weight` omitted olarsa `100` kimi normallaşdırılır.

`extensionPoints.id` boş ola bilməz və eyni manifest daxilində duplicate ola
bilməz. `parentId` varsa, həmin manifest daxilində mövcud `extensionPoints.id`
dəyərinə resolve olunmalıdır və cycle yarada bilməz. Parent/child ordering və
render semantics `ADR-0049` tərəfindən idarə olunur; bu ADR yalnız manifest
field shape, normalization və validation qaydasını sahib edir.

Parent extension point dedicated plugin page daşımaq məcburiyyətində deyil.
Top-level parent yalnız navigation container ola bilər; belə parent-lərdə
`routeKey` stable, non-localized route/container identity kimi qalır və child
entries həmin `parentId` altında render olunur. Parent entry aktivləşdiriləndə
host default child selection və ya empty/container state-i `ADR-0049` shell
binding qaydası ilə idarə edir.

`routeKey` user-facing label deyil, localization-dan asılı deyil və route
comparison ordinal/culture-invariant aparılır. Plugin raw WPF view type-i,
assembly-qualified type adı və ya host internal view model adı manifest route
identity kimi istifadə edilmir.

Loose string list və ya open-ended vocabulary Phase 1 üçün qəbul edilmir.
Unknown scope, target və ya explicit non-`default` group dəyərləri manifest validation
failure hesab olunur. `extensionPoints.type` Phase 1 manifest shape-ində yoxdur; extension kind-i
`target` vocabulary-si ilə ifadə olunur və ayrıca open-ended type field qəbul edilmir.
Ordering comparator semantics ADR-0005-də bağlanır; bu ADR həmin comparator-a
daxil olan manifest normalization və validation qaydasını sahib edir.

## Consequences

- Manifest validation və runtime interpretation eyni typed modelə söykənir
- Plugins Manager list-i plugin code execution etmədən manifest/cached metadata-dan
  name, version və description göstərə bilir
- Plugin author-lar üçün contract gözləntisi daha dəqiq olur
- Compatibility, capability və shell composition qərarları loose parsing deyil,
  schema-controlled input üzərindən qurulur
- Omitted `group` və `weight` üçün normalization author ergonomics saxlayır,
  amma deterministik ordering-i pozmur
- Vocabulary genişlənməsi ayrıca deliberate qərar tələb edir
- Manifest schema validator `files` section-unun mövcudluğunu və shape-ini bu
  ADR əsasında yoxlayır; package content completeness/hash verification isə
  `ADR-0045` sərhədində qalır.
- `docs/schemas/dobby-plugin-manifest.schema.json` package validator və sample
  manifest fixture-ləri üçün canonical machine-readable artifact-dır. JSON Schema
  duplicate id by property, `parentId` existence/cycle, target namespace ownership,
  path NFC normalization, declaration-index ordering və trust-store lookup kimi
  procedural qaydaları tam ifadə etmir; həmin qaydalar bu ADR, `ADR-0045`,
  `ADR-0048` və validator code tərəfindən fail-closed yoxlanmalıdır.
- Permission target-ləri package-validation-time manifest metadata-dan gəldiyi
  üçün host plugin assembly load etmədən permission registry shape-ini qura və
  malformed/unknown target-ləri fail-closed reject edə bilir.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-009` | RESOLVED_BY_HUMAN_DECISION (`DM-096`): plugin permission targets are declared by signed manifest `operationTargets = [{ id, verbs, labelKey }]`; package validation owns target shape before plugin assembly load. |

## Reopen Conditions

These resolved decisions must not be reopened for preference-only reasons.

| Decision | Reopen condition |
|---|---|
| `DM-096` / `HD-009` | A required plugin needs dynamic operation targets that cannot be known at package-validation time. |

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Manifest / Metadata Management` row və `ADR-0036` risk sətri
- `docs/02_ARCHITECTURE_MAP.md` — `Manifest Typed Model` convention
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `## 2. DI, Hosting & Infrastructure`, JSON Schema row
- `docs/schemas/dobby-plugin-manifest.schema.json` — machine-readable manifest schema artifact
- `docs/adr/ADR-0005-ui-ordering-model.md` — `group`, `weight` və declaration-index tiebreak semantics
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — `signature` trust verification semantics
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — `signature` placement, canonical signed payload, `files` section verification və package fingerprint semantics
- `docs/adr/ADR-0048-operation-based-authorization.md` — operation key vocabulary and plugin target registration semantics
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — `minHostVersion` və `version` comparison semantics
- `docs/adr/ADR-0013-routing-and-deep-link-model.md` — `routeKey` və plugin page route normalization semantics
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` — `parentId`, parent/child ordering və render semantics
- `docs/specs/plugins-manager.md` — plugin list metadata və install validation UI
