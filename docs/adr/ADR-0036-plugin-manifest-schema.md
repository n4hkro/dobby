# ADR-0036: Plugin Manifest Schema

**Status:** Accepted (manifest v2 root shape, bounded collection limits, `hostApiVersion` and `signature.format` portions amended by `ADR-0059` — see ## Revision History)
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

Machine-readable schema artifact `docs/schemas/dobby-plugin-manifest-v2.schema.json`
bu ADR-in canonical JSON Schema 2020-12 representation-ıdır. `ADR-0059` manifest
shape-ini v2-yə qaldırdı (`schemaVersion = 2`, `purpose = dobby.plugin-package`,
required `hostApiVersion`, bounded collection limitləri, explicit signature
`format`); frozen `docs/schemas/dobby-plugin-manifest.schema.json` artıq heç bir
read path-ın authority-si deyil — nə package validation, nə də inventory oxusu onu
yükləyir. O yalnız frozen negative reference kimi saxlanılır: v1 shape-i
`additionalProperties: false` altında v2 discriminator member-lərini (`schemaVersion`,
`purpose`, `hostApiVersion`) qəbul etmir, ona görə də iki schema mutually exclusive-dir
və bu fərq enforced schema-nın v2 olduğunu behavioral olaraq sübut etmək üçün istifadə
olunur (`docs/schemas/artifact-compatibility-matrix.md`). Bu ADR prose
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
- `version` — stable canonical SemVer: **yalnız** `MAJOR.MINOR.PATCH`. Pre-release
  (`-rc.1`) və build-metadata (`+build.5`) suffix-ləri, həmçinin leading-zero component
  (`01.2.3`) qəbul edilmir. Bu iki qatda fail-closed enforce olunur: owning schema
  `$defs/semanticVersion` pattern-i (`^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$`)
  suffix daşıyan dəyəri schema validation-da rədd edir, typed compatibility evaluation isə
  onu müstəqil olaraq `invalid-manifest` reason code-u ilə rədd edir (`ADR-0037`). Prose
  əvvəl "SemVer 2.0.0 compatible (`MAJOR.MINOR.PATCH` + optional pre-release)" deyirdi;
  o oxunuş vendor-a schema-nın qəbul etmədiyi field shape-i vəd edirdi.
- `vendorId` — `Dobby.Security / Plugin Trust Verification` tərəfindən tanınan vendor identifier. Boş qiymət və ya trust store-da olmayan dəyər manifest validation failure-dır.
- `hostApiVersion` — **required** plugin-facing host API contract versiyası, `MAJOR.MINOR`
  formatında (owning schema `$defs/apiVersion`; product SemVer deyil). Bu, plugin-in hansı
  host contract surface-inə qarşı yazıldığını bildirən əsas compatibility gate-dir:
  dəstəklənəndən fərqli major `unsupported-host-api-major`, host-un dəstəklədiyindən böyük
  minor `unsupported-host-api-minor`, absent və ya `MAJOR.MINOR` olmayan dəyər isə
  `invalid-host-api-version` ilə rədd olunur — heç bir halda host-un öz versiyasına
  default-lanmır. Dəstəklənən major/minor authority-si `PluginCompatibilityMatrix`-dir
  (`ADR-0059`, `ADR-0037`); v1 manifest bu member-i tanımadığı üçün iki schema mutually
  exclusive qalır.
- `minHostVersion` — plugin-in çalışa bilməsi üçün minimum host **product/build** versiyası;
  `version` ilə eyni stable canonical SemVer qaydasına tabedir. Host versiyası bu dəyərdən
  kiçikdirsə package code load olunmadan `host-version-below-minimum` ilə rədd olunur;
  absent və ya qeyri-canonical dəyər `invalid-minimum-host-version`-dır. Bu field
  `hostApiVersion` contract gate-ini **əvəz etmir və genişləndirmir**: product floor-u
  ödəyən plugin API major uyğun gəlmirsə və ya API minor host-un dəstəklədiyindən
  böyükdürsə yenə rədd olunur. İki gate müstəqildir və hər ikisi qiymətləndirilir
  (`ADR-0037`, `ADR-0059`).
- `entryAssembly` — plugin-in əsas assembly path-i paket daxilində (relative path).
- `signature` — paket signature bloku. Canonical signature placement və payload
  shape `ADR-0045` tərəfindən, ECDSA/trust verification semantics isə `ADR-0032`
  tərəfindən idarə olunur. İmza olmayan manifest validation failure-dır və trust
  verification baş tutmur. Block field shape-i `schemaVersion = 2` manifest-də
  `docs/schemas/dobby-plugin-manifest-v2.schema.json` tərəfindən bağlanır:
  `purpose`, `algorithm`, `format`, string `keyId`, `value`. Numeric
  `signature.keyVersion` **retired**-dir və `additionalProperties: false`
  qaydası onu unknown property kimi fail-closed rədd edir; signing-key selector-u
  `keyId`-dir və rotation `ADR-0062` § 4 bounded verification ring qaydası ilə
  idarə olunur.

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

Bu field-lər plugin runtime code execution olmadan oxunan metadata-dır və host
tərəfindən cache-lənə bilər. Cache oxunuşu da plugin işidir: `ADR-0008` § 1 və
`ADR-0061` § `Strict pre-auth boundary` üzrə nə fresh, nə də cached manifest metadata
successful authentication-dan əvvəl oxunur.

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
  normalize edilir. `labelKey` plugin-owned localization/resource key-dir və
  omitted olması yalnız bir halda qanunidir: contribution top-level
  `top-toolbar` girişidir, yəni `target == "top-toolbar"` **və** `parentId`
  yoxdur; bu halda root `displayName` fallback kimi istifadə olunur. Bütün digər
  contribution-lar üçün — `sidebar`, `third-bar`, `plugin-page-region`, və
  `parentId` daşıyan istənilən child (child `top-toolbar` girişi daxil) —
  `labelKey` required-dır, çünki child üçün root `displayName` yanlış label
  verərdi. Bu conditional owning v2 schema-da (`extensionPoints.items`
  `if`/`then`) və typed manifest validator-da eyni formada enforce olunur;
  ikisindən biri onu yoxlamırsa label mənbəyi olmayan contribution package
  read-dən keçər. Bu wording ADR-0049
  TopToolbar top-level modelinə uyğundur. `parentId` optional-dır və
  eyni manifest-dəki başqa `extensionPoints.id` dəyərinə referans verir; cross-plugin
  parent reference Phase 1-də qəbul edilmir. `group` və `weight` ordering
  field-ləridir.
- `nativeLibraries` — optional array (≤ 32 entry), package daxilində canonical relative
  path-lar. Plugin-in yüklədiyi hər native library burada elan olunmalı **və** `files`
  hash coverage-inə daxil olmalıdır; undeclared native binary install validation-da rədd
  olunur. Load semantics və path qaydaları `ADR-0007` § 4-dədir.
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
manifest validation failure hesab olunur. Bu qayda owning v2 schema-da
`const: "default"` kimi, typed validator-da isə `invalid-manifest` reason kimi
enforce olunur; `plugin-manifest-v2.nondefault-group.invalid.json` committed
negative fixture-i rədd nöqtəsini sübut edir. Group vocabulary-nin genişlənməsi
`ADR-0005` ordering policy qərarıdır və schema/validator dəyişikliyi tələb edir.

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
- `docs/schemas/dobby-plugin-manifest-v2.schema.json` package validator və sample
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
- `docs/schemas/dobby-plugin-manifest-v2.schema.json` — controlling machine-readable manifest schema artifact (`ADR-0059`)
- `docs/schemas/dobby-plugin-manifest.schema.json` — frozen v1 schema; heç bir read path-da yüklənmir, yalnız negative reference
- `docs/schemas/artifact-compatibility-matrix.md` — v1 → v2 transition policy
- `docs/adr/ADR-0005-ui-ordering-model.md` — `group`, `weight` və declaration-index tiebreak semantics
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — `signature` trust verification semantics
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — `signature` placement, canonical signed payload, `files` section verification və package fingerprint semantics
- `docs/adr/ADR-0048-operation-based-authorization.md` — operation key vocabulary and plugin target registration semantics
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — `minHostVersion` və `version` comparison semantics
- `docs/adr/ADR-0013-routing-and-deep-link-model.md` — `routeKey` və plugin page route normalization semantics
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` — `parentId`, parent/child ordering və render semantics
- `docs/specs/plugins-manager.md` — plugin list metadata və install validation UI

## Revision History

- **2026-08-31:** Root field dokumentasiyası owning schema
  (`docs/schemas/dobby-plugin-manifest-v2.schema.json`) və typed compatibility
  evaluation (`ADR-0037`, `ADR-0059`) ilə uzlaşdırıldı. Field semantics qərarı
  dəyişmir — bu ADR həmin qərarın authority-si olaraq qalır; dəyişən yalnız
  prose-un schema-nı düzgün ifadə etməsidir, ona görə successor ADR tələb olunmur.
  (a) `version` bullet-i "SemVer 2.0.0 compatible (`MAJOR.MINOR.PATCH` + optional
  pre-release)" deyirdi, halbuki owning `$defs/semanticVersion` pattern-i heç bir
  pre-release/build-metadata suffix-i və leading-zero component qəbul etmir. Bu,
  Accepted ADR səviyyəsində schema-nın **qadağan etdiyi** field shape-ini vendor-a
  vəd edən over-claim idi: ADR-i izləyən vendor `1.2.0-rc.1` yazıb install
  validation-da xam pattern xətası alırdı. Yeni mətn stable canonical formanı və
  iki fail-closed qatı (schema pattern; typed evaluation-da `invalid-manifest`)
  adlandırır.
  (b) `hostApiVersion` — required root property və plugin-facing yeganə real
  compatibility gate — field dokumentasiyasında **heç bir** girişi yox idi. Field
  semantics owner-i bu ADR olduğu üçün manifest yazan tərəf üçün ən nəticəli
  field-in mənası, `MAJOR.MINOR` formatı və üç rədd səbəbi
  (`invalid-host-api-version`, `unsupported-host-api-major`,
  `unsupported-host-api-minor`) sənədsiz qalırdı; "absent → host-un öz versiyasına
  default" kimi təhlükəsizlik baxımından yanlış oxunuş açıq idi. Bullet əlavə
  olundu və default-lanmama explicit yazıldı.
  (c) `minHostVersion` bullet-i "(ADR-0037 compatibility uyğun)" deyərək product
  floor-u API contract gate-i ilə eyni şey kimi oxunmağa imkan verirdi. İki gate
  müstəqildir və hər ikisi qiymətləndirilir; floor-u ödəyən plugin API major/minor
  uyğunsuzluğunda yenə rədd olunur. Bullet indi bunu, canonical SemVer tələbini və
  `invalid-minimum-host-version` / `host-version-below-minimum` fərqini yazır.
  (d) `nativeLibraries` owning schema-da optional root property (≤ 32 entry) kimi
  mövcud idi, lakin field-semantics owner-i olan bu ADR-də heç bir prose semantic-i
  yox idi; undeclared native binary-nin install validation-da rədd olunması və
  `files` hash coverage-inə daxil olma öhdəliyi yalnız `ADR-0007` § 4-də qalırdı.
  Bullet əlavə olundu və load semantics authority-si `ADR-0007`-də saxlanıldı.
- **2026-08-27 (2):** `extensionPoints[].labelKey` required qaydası dəqiqləşdirildi
  və owning schema-da enforce olundu. Əvvəlki mətn fallback-ı "top-level
  `top-toolbar` contribution" üçün açırdı, sonra required-liyi "bütün
  non-`top-toolbar` contribution" kimi ifadə edirdi; bu iki cümlə arasında
  `top-toolbar` **child** girişi (`parentId` var) unstated qalırdı, halbuki
  `PluginManifestValidator` onu label mənbəyi olmadan rədd edir və host root
  `displayName` fallback-ını yalnız `parentId is null` halında tətbiq edir. Eyni
  zamanda v2 schema-da heç bir conditional yox idi, yəni labelKey-siz `sidebar`
  contribution schema gate-indən keçib yalnız typed validator-da dayanırdı;
  `02_ARCHITECTURE_MAP.md` isə bu qaydanı səhvən "schema tərəfindən required"
  kimi göstərirdi. Yeni mətn şərti tam yazır, schema-ya `if`/`then` conditional
  əlavə olundu və iki committed negative fixture
  (`plugin-manifest-v2.sidebar-without-labelkey.invalid.json`,
  `plugin-manifest-v2.child-without-labelkey.invalid.json`) rədd nöqtəsini sübut
  edir. Bu gap fill-dir: field semantics qərarı və ADR-0049 top-level modeli
  dəyişmir, ona görə successor ADR tələb olunmur.
- **2026-08-27:** `signature` field təsvirindəki "`signature.keyVersion` Phase 1-də
  numeric `1`-dir" iddiası düzəldildi; qərar dəyişikliyi deyil. Owning schema
  (`docs/schemas/dobby-plugin-manifest-v2.schema.json`) signature block-unda
  `purpose`, `algorithm`, `format`, string `keyId`, `value` tələb edir və
  `additionalProperties: false` ilə numeric `keyVersion`-u unknown property kimi
  fail-closed rədd edir, yəni əvvəlki mətn schema-nın **qadağan etdiyi** field-i
  Accepted ADR səviyyəsində tələb kimi göstərirdi. Eyni over-claim `ADR-0045`
  § `Canonical signature model`-də də düzəldildi. Manifest field semantics
  qərarları dəyişmir.
- **2026-08-27:** Frozen v1 schema-nın "legacy inventory oxusu üçün saxlanılır"
  təsviri düzəldildi. Repozitoriyada v1 manifest oxuyan heç bir kod yolu yoxdur:
  `PluginManifestSchemaValidator` yalnız v2 resource-unu yükləyir, compatibility
  evaluation `schemaVersion != 2`-ni `unsupported-manifest-version` ilə həm install
  validation-da, həm activation re-verification-da rədd edir və installed plugin
  sətri persisted install metadata-dan render olunur. Ona görə əvvəlki mətn mövcud
  olmayan read path-a authority verirdi. Yeni mətn v1 faylını frozen negative
  reference kimi təsvir edir; bu, `ADR-0059` qərarının factual reconciliation-ıdır və
  prose semantics qərarlarını dəyişmir, deməli successor ADR tələb etmir.
- **2026-08-26:** Machine-readable schema pointer-ləri controlling
  `docs/schemas/dobby-plugin-manifest-v2.schema.json`-a düzəldildi; frozen v1
  schema legacy inventory oxusu kimi qeyd olundu. Bu, `ADR-0059` manifest v2
  qərarının stale reference reconciliation-ıdır və bu ADR-in prose semantics
  qərarlarını dəyişmir.
