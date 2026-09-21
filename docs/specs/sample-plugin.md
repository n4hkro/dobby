# Sample Plugin Spec

**Status:** P1 canonical spec
**Phase:** P1-core verification artifact
**Sources:** `FR-007`, `FR-008`, `FR-015`, `FR-018`, `FR-026`, `NFR-006`, `NFR-008`, `NFR-009`, `AC-004`, `AC-005`, `AC-009`, `AC-015`, `AC-019`, `AC-022`, `AC-031`, `AC-032`, `AC-043`, `ADR-0001`, `ADR-0006`, `ADR-0011`, `ADR-0012`, `ADR-0021`, `ADR-0032`, `ADR-0036`, `ADR-0037`, `ADR-0048`, `ADR-0050`, `ADR-0059`, `docs/schemas/dobby-plugin-manifest-v2.schema.json`, `docs/schemas/artifact-compatibility-matrix.md`, `docs/specs/plugins-manager.md`, `docs/specs/work-sessions.md`

## Məqsəd

Sample plugin Dobby platformasının release verification anchor-ıdır. O, müştəriyə təqdim olunan business feature deyil və Dobby-ni vergi/satış/SQL kimi konkret vertical məhsul kimi göstərmir.

## Distribution Boundary

- Production customer install-da default görünmür.
- Developer/QA test artifact kimi build edilir.
- Test/dev signing production vendor key management ilə qarışdırılmır.
- Dev/QA sample artifact signing yalnız non-production signing material, `vendorId =
  "dobby.dev-qa"` və scripted operator path (`docs/specs/admin-tool.md`) ilə aparılır;
  production vendor private signing key sample smoke artifact-ları üçün istifadə
  olunmur.
- Dev/QA public-key bytes və private-key custody policy `ADR-0062 § 3` tərəfindən
  idarə olunur; scripted signing wrapper process-i `docs/specs/admin-tool.md`
  içindəki P1 Minimal Scripted Operator Path-dır.
- Customer production host build dev/QA trust profile-i tanımır; sample `.dobbyplugin`
  yalnız explicit dev/QA smoke flow-da install edilə bilər.

Distribution rule: the sample plugin is a dev/QA verification artifact. It is not bundled as an active or preinstalled plugin in customer production installs. Production visibility requires an explicit non-default packaging decision and release note.

## Minimum Capabilities

Sample plugin aşağıdakı invariantları yoxlamağa imkan verməlidir:

| Invariant | Sample plugin behavior |
|---|---|
| Manifest typed model | Valid manifest uses the ADR-0036 canonical field names in their ADR-0059 manifest **v2** shape and validates against `docs/schemas/dobby-plugin-manifest-v2.schema.json`: `schemaVersion = 2`, `purpose = dobby.plugin-package`, `pluginId`, `version`, `vendorId`, `hostApiVersion`, `minHostVersion`, `entryAssembly`, `displayName`, `description`, optional `icon`, optional `nativeLibraries`, `dependsOn`, `capabilities`, `operationTargets`, `extensionPoints`, `files` and `signature`. The frozen v1 schema (`dobby-plugin-manifest.schema.json`) is not loaded by any read path and is never valid for the sample smoke artifact. |
| Module contract | Sample plugin module `Dobby.SDK.PluginFramework.PluginBaseV2`-dən törəyir, yəni `IPluginModuleV2` altında qeydiyyatdan keçir və v1 adapter window-una düşmür: `OnActivated` override edilmir (base-də `sealed override` no-op-dur) və module yalnız `RegisterServices(...)`-i override edir — qalan lifecycle phase-ləri SDK virtual default-larından gəlir. Buna görə smoke run-da `legacy-v1-adapter-upgrade-required` warning-i və `plugin.lifecycle.v1-adapter-used` audit event-i **gözlənilmir**; onların görünməsi regression sayılır (`ADR-0021` §3a, `ADR-0037`). Page-level async lifecycle (`ActivateAsync`/`DeactivateAsync`/`UnloadAsync` üzərində `PluginPageContext`) ayrı page contract-ıdır və module lifecycle-ı ilə qarışdırılmır. |
| Host capability surface | Plugin yalnız `ADR-0012` §2a qapalı injectable set-indən host service alır; manifest `capabilities[]` elanı heç bir əlavə host type-a resolve imkanı vermir. |
| Extension point hosting | Manifest-declared extension point render/hosting contract-ı yoxlanılır. |
| Signed package install | Signed `.dobbyplugin` artifact kimi install olunur. |
| Main Content hosting | Bir page yalnız `Dobby.Contracts` / `Dobby.SDK` contract ilə host olunur; page code `Result<IPluginPageModel>` qaytarır və host-owned page render registry-si approved model contract type-ını host view-a map edir. |
| Permission registration | Manifest `operationTargets` metadata-sında `com.dobby.sample.greet` target-ini `verbs: ["execute"]` və plugin-owned `labelKey` ilə elan edir; permission key `com.dobby.sample.greet.execute` formasında yoxlanılır. |
| Permission deny | Managed user grant olmadan sample execute action-u deny alır. |
| Result boundary | Success button `Result.Success`, invalid input button typed `InvalidInput` failure verir. |
| Runtime fault isolation | Fault button unhandled exception yaradır və host həmin plugin instance/screen/task sərhədində isolate edir. |
| Work session | Progress simulation screen explicit minimize/restore ilə state saxlayır. Work-session command-ı deterministic closure göstərir: uğursuz və ya cancel olunmuş icra da session-u bağlayır, ona görə də təkrar failure host-un per-owner work session limitini tükətmir (`docs/specs/work-sessions.md` § `Resource Limits`). Sample plugin yalnız öz session id-ləri ilə işləyir; foreign id ilə əməliyyat `Forbidden` alır (§ `Owner Isolation`). |
| Declarative page content | Sample page localization-dan gələn mətn, validated input, table/list göstərişi, asynchronous **cancellable** command və deterministic work-session closure nümayiş etdirir; heç bir WPF tipi contract boundary-ni keçmir. |

## Reference Manifest Fixture

The sample plugin reference manifest must exercise the P1 manifest fields that
prove package validation, shell contribution, permission registration, route
normalization and signature placement. The skeleton sample project owns the
physical manifest file under `samples/Dobby.Sample.Plugin/`; this spec owns the
minimum semantic content.

Minimum sample manifest field set:

| Field | Required sample value / rule |
|---|---|
| `schemaVersion` | `2` — manifest v2 is the only shape accepted for a new installable package (`ADR-0059`, `docs/schemas/artifact-compatibility-matrix.md`) |
| `purpose` | `dobby.plugin-package` |
| `pluginId` | `com.dobby.sample` |
| `version` | stable SemVer such as `1.0.0`; pre-release suffix is not used |
| `vendorId` | `dobby.dev-qa` for dev/QA smoke artifact |
| `hostApiVersion` | `MAJOR.MINOR` host plugin API version the sample is built against; must satisfy the host compatibility rule in `ADR-0037` |
| `minHostVersion` | the current host major/minor baseline used by the sample smoke run |
| `entryAssembly` | package-relative path to the sample plugin assembly |
| `displayName` | short generic sample name; no business vertical wording |
| `description` | short plain-text verification description |
| `icon.path` | package-relative PNG path that passes host validation; minimum source size `64x64` |
| `dependsOn` | empty array unless a dependency-specific sample test is introduced by a separate ADR/spec change |
| `capabilities` | at least one entry with `scope: "plugin-page"`, present so packaging, signature binding and admin verification output carry a non-empty declaration. The declaration is review metadata, not a runtime grant: page hosting is driven by `extensionPoints` and host service access by the `ADR-0012` §2a closed injectable set, so hosting tests must not depend on this entry to obtain any capability. Permission-deny tests use `operationTargets`, not capability categories |
| `operationTargets` | declares `com.dobby.sample.greet` with `verbs: ["execute"]` and a plugin-owned localization `labelKey` according to ADR-0036/ADR-0048 |
| `extensionPoints` | at least one top-level `top-toolbar` contribution and one child `sidebar` contribution with `id`, `parentId`, `routeKey`, `labelKey`, `group`, and `weight` according to ADR-0036 |
| `files` | deterministic package file hash list generated by the signing/package tool according to ADR-0045 |
| `signature` | manifest signature block generated by the signing tool according to ADR-0045 |

The committed
`samples/Dobby.Sample.Plugin/dobby-plugin.manifest.template.json` is metadata for
an unsigned package candidate, not a positive manifest or installable package.
Its conspicuous all-`f` DLL hash and all-`_` signature are template sentinels.
`Dobby.Admin --cli sign-plugin-package` stages the template under the canonical
manifest name, refreshes every declared hash from the package candidate layout,
replaces the signature, and writes the only positive `.dobbyplugin` artifact.
The build output therefore never presents placeholder metadata as an installable
positive package.

The sample contribution route keys must be stable, non-localized values. Visible
navigation text must come from plugin-owned resource keys referenced by
`labelKey`, with root `displayName` allowed only as the top-level fallback
defined by ADR-0036.

Minimum sample manifest contribution snippet:

```json
{
  "operationTargets": [
    {
      "id": "com.dobby.sample.greet",
      "verbs": ["execute"],
      "labelKey": "sample.greet.label"
    }
  ],
  "extensionPoints": [
    {
      "id": "sample.root",
      "target": "top-toolbar",
      "routeKey": "sample",
      "labelKey": "sample.nav.root",
      "group": "default",
      "weight": 100
    },
    {
      "id": "sample.greet.page",
      "target": "sidebar",
      "parentId": "sample.root",
      "routeKey": "greet",
      "labelKey": "sample.nav.greet",
      "group": "default",
      "weight": 100
    }
  ]
}
```

The sample manifest must not contain `.dobbylicense`, customer data, device
fingerprint, vendor private key material, host internal configuration, host
persistence migration script, raw WPF view type names, assembly-qualified view
model names or customer production trust material.

The sample manifest fixture must pass `docs/schemas/dobby-plugin-manifest-v2.schema.json`
before signing/package verification. Procedural checks that JSON Schema cannot fully
express, such as parent cycle detection and trust-store vendor lookup, still run in
the package validation flow.

## Release evidence conditions

Sample plugin yalnız aşağıdakı şərtlərin hamısı ödəndikdə release evidence sayılır:

- package real hesablanmış content hash-ları daşıyır (template sentinel deyil);
- signature valid-dir və purpose-separated development/release signing key ilə
  yaradılıb (`ADR-0062`, `docs/02_ARCHITECTURE_MAP.md` § `Key Purpose Separation`);
- manifest/API version-ları host baseline-ı ilə uyğundur;
- install customer package-ləri ilə **eyni** plugins-manager path-ından keçir —
  ayrıca test-only install shortcut evidence sayılmır;
- release pipeline repository-owned `verify-plugin-package` command-ı ilə paketi
  yoxlayır və negative tamper/compatibility fixture-ları da icra edir
  (`docs/09_BUILD_RUN_AND_RELEASE.md` § `Candidate release evidence set`).

Dummy hash və ya placeholder signature daşıyan artifact fixture-dır, deliverable
deyil, və release gate-i keçirə bilməz.

## Acceptance Hooks

- `AC-004` signed install validation.
- `AC-005` rejected/skipped/faulted plugin lifecycle.
- `AC-009` Result boundary.
- `AC-015` Contracts/SDK-only main content hosting.
- `AC-019` work session minimize/restore, deterministic closure.
- `AC-022` permission guard behavior.
- `AC-031` plugin list/lifecycle permission behavior.
- `AC-032` runtime fault isolation.
- `AC-043` plugin project yalnız `Dobby.Contracts`/`Dobby.SDK` reference edir və plugin-to-plugin əlaqə host boundary-dən keçir.

## Revision History

- **2026-08-27:** `Minimum Capabilities` cədvəlinə `Module contract` və
  `Host capability surface` sətirləri əlavə olundu, `Reference Manifest Fixture`
  cədvəlindəki `capabilities` sətri isə düzəldildi. Əvvəlki mətn həmin sətirdə
  "at least one `plugin-page` capability **used by hosting tests**" yazırdı; bu,
  page hosting-in manifest capability declaration-ından asılı olduğunu ima edirdi.
  Shipped davranışda hosting `extensionPoints` ilə, host service access-i isə
  `ADR-0012` §2a qapalı injectable set-i ilə idarə olunur və plugin service
  resolution manifest `capabilities[]` array-inə heç vaxt baxmır. Risk konkret idi:
  sample smoke run capability declaration-ını hosting üçün ön şərt kimi göstərsə,
  o run AC-036/AC-043 evidence-i kimi mövcud olmayan declaration→grant gate-inin
  sübutu kimi oxunardı (plugin trust misrepresentation). Module contract sətri
  shipped `samples/Dobby.Sample.Plugin/SamplePlugin.cs` (`PluginBaseV2`) ilə
  uzlaşdırılıb və smoke run-da v1 adapter warning/audit event-inin
  **gözlənilmədiyini** explicit edir (`ADR-0021` §3a, `ADR-0037`); həmçinin
  module lifecycle-ı ilə page lifecycle-ının fərqli contract-lar olduğunu qeyd edir,
  çünki shipped sample-da async phase-lər page tərəfdədir, module isə yalnız
  `RegisterServices(...)`-i override edir.

- **2026-08-27:** `Minimum Capabilities` work-session sətri deterministic closure və
  owner isolation tələbi ilə gücləndirildi (`docs/specs/work-sessions.md`
  § `Owner Isolation`, § `Resource Limits`); `Acceptance Hooks` siyahısına Sources
  sətrində artıq göstərilən `AC-043` əlavə olundu.

- **2026-08-26:** `ADR-0059` reconciliation. `2026-08 Completion Amendment`
  bölməsi ləğv edildi; release-evidence şərtləri yeni
  `Release evidence conditions` bölməsinə, declarative page tələbləri isə
  `Minimum Capabilities` cədvəlinə köçürüldü.
- **2026-08-26:** Manifest schema citation-ları frozen v1
  (`dobby-plugin-manifest.schema.json`) yerinə controlling
  `dobby-plugin-manifest-v2.schema.json`-a düzəldildi; `schemaVersion`, `purpose`
  və `hostApiVersion` sətirləri `Reference Manifest Fixture` cədvəlinə əlavə
  olundu (`ADR-0059`, `docs/schemas/artifact-compatibility-matrix.md`,
  `samples/Dobby.Sample.Plugin/dobby-plugin.manifest.template.json`).
