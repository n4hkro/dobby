# Sample Plugin Spec

**Status:** P1 canonical spec
**Phase:** P1-core verification artifact
**Sources:** `FR-007`, `FR-008`, `FR-015`, `FR-018`, `FR-026`, `NFR-006`, `NFR-008`, `NFR-009`, `AC-004`, `AC-005`, `AC-009`, `AC-015`, `AC-019`, `AC-022`, `AC-031`, `AC-032`, `AC-043`, `ADR-0001`, `ADR-0011`, `ADR-0012`, `ADR-0021`, `ADR-0032`, `ADR-0036`, `ADR-0048`, `ADR-0050`, `docs/specs/plugins-manager.md`, `docs/specs/work-sessions.md`

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
- Dev/QA public-key bytes və private-key custody policy `ADR-0033 §2a` tərəfindən
  idarə olunur; scripted signing wrapper process-i `docs/specs/admin-tool.md`
  içindəki P1 Minimal Scripted Operator Path-dır.
- Customer production host build dev/QA trust profile-i tanımır; sample `.dobbyplugin`
  yalnız explicit dev/QA smoke flow-da install edilə bilər.

Distribution rule: the sample plugin is a dev/QA verification artifact. It is not bundled as an active or preinstalled plugin in customer production installs. Production visibility requires an explicit non-default packaging decision and release note.

## Minimum Capabilities

Sample plugin aşağıdakı invariantları yoxlamağa imkan verməlidir:

| Invariant | Sample plugin behavior |
|---|---|
| Manifest typed model | Valid manifest uses the ADR-0059 v2 schema and validates against `docs/schemas/dobby-plugin-manifest-v2.schema.json`: `schemaVersion: 2`, `purpose`, `hostApiVersion`, plus the ADR-0036 canonical field names `pluginId`, `version`, `vendorId`, `minHostVersion`, `entryAssembly`, `displayName`, `description`, optional `icon`, `dependsOn`, `capabilities`, `operationTargets`, `extensionPoints`, `files` and `signature`. |
| Extension point hosting | Manifest-declared extension point render/hosting contract-ı yoxlanılır. |
| Signed package install | Signed `.dobbyplugin` artifact kimi install olunur. |
| Main Content hosting | Bir page yalnız `Dobby.Contracts` / `Dobby.SDK` contract ilə host olunur; page code `Result<IPluginPageModel>` qaytarır və host-owned page render registry-si approved model contract type-ını host view-a map edir. |
| Permission registration | Manifest `operationTargets` metadata-sında `com.dobby.sample.greet` target-ini `verbs: ["execute"]` və plugin-owned `labelKey` ilə elan edir; permission key `com.dobby.sample.greet.execute` formasında yoxlanılır. |
| Permission deny | Managed user grant olmadan sample execute action-u deny alır. |
| Result boundary | Success button `Result.Success`, invalid input button typed `InvalidInput` failure verir. |
| Runtime fault isolation | Fault button unhandled exception yaradır və host həmin plugin instance/screen/task sərhədində isolate edir. |
| Work session | Progress simulation screen explicit minimize/restore ilə state saxlayır. |

## Reference Manifest Fixture

The sample plugin reference manifest must exercise the P1 manifest fields that
prove package validation, shell contribution, permission registration, route
normalization and signature placement. The skeleton sample project owns the
physical manifest file under `samples/Dobby.Sample.Plugin/`; this spec owns the
minimum semantic content.

Minimum sample manifest field set:

| Field | Required sample value / rule |
|---|---|
| `pluginId` | `com.dobby.sample` |
| `version` | stable SemVer such as `1.0.0`; pre-release suffix is not used |
| `vendorId` | `dobby.dev-qa` for dev/QA smoke artifact |
| `minHostVersion` | the current host major/minor baseline used by the sample smoke run |
| `entryAssembly` | package-relative path to the sample plugin assembly |
| `displayName` | short generic sample name; no business vertical wording |
| `description` | short plain-text verification description |
| `icon.path` | package-relative PNG path that passes host validation; minimum source size `64x64` |
| `dependsOn` | empty array unless a dependency-specific sample test is introduced by a separate ADR/spec change |
| `capabilities` | at least one `plugin-page` capability used by hosting tests; permission-deny tests use `operationTargets`, not capability categories |
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

## Acceptance Hooks

- `AC-004` signed install validation.
- `AC-005` rejected/skipped/faulted plugin lifecycle.
- `AC-009` Result boundary.
- `AC-015` Contracts/SDK-only main content hosting.
- `AC-019` work session minimize/restore.
- `AC-022` permission guard behavior.
- `AC-031` plugin list/lifecycle permission behavior.
- `AC-032` runtime fault isolation.

## 2026-08 Completion Amendment (Controlling)

The sample is release evidence only when its package has real computed content
hashes, a valid purpose-separated development/release signature, compatible
manifest/API versions and installs through the same manager path as customer
packages. Dummy hashes or signatures are fixtures, not a deliverable.

Its declarative page demonstrates localization, validated input, a table/list,
an asynchronous cancellable command and deterministic work-session closure.
The release pipeline verifies it with the repository-owned
`verify-plugin-package` command and includes negative tamper/compatibility cases.
