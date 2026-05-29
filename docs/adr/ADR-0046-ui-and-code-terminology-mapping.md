# ADR-0046: UI and Code Terminology Mapping

**Status:** Accepted
**Date:** 2026-04-25

## Context

Dobby sənədlərində code-level terminlər və visible UI terminləri qarışa bilər. Localization foundation olduğu halda visible UI terminology canonical mapping olmadan təkrar tərcümə, inconsistent label və hardcoded string riski yaranır.

## Decision

Code/ADR terminləri stabil texniki adları saxlaya bilər. Visible UI text isə localization resource və canonical terminology mapping üzərindən gəlməlidir.

Phase 1 default mapping:

| Code / technical term | Azerbaijani visible UI term |
|---|---|
| `Permission` | `İcazə` |

Shell/navigation target dili product sənədlərində texniki DTO adı kimi yazılmır.
Requirement və user-facing sənədlərdə canonical target terminləri:

- `TopToolbar`
- `Sidebar`
- `Third Bar`
- `plugin page region`

Kodda sonradan DTO, enum və contract type adları ola bilər, amma requirement dili
bu code-level adlara bağlanmamalıdır. Third Bar selected Sidebar item-in daha
dərin child navigation səviyyəsi kimi başa düşülür.

`Owner`, plugin id-lər, type adları və public contract adları texniki rol və contract semantikası olduğu üçün code/ADR-də dəyişdirilmir. Visible UI mətnində role/permission labels localization resource vasitəsilə idarə olunur.

Owner başqa termin seçmədiyi halda D-5 safe default `İcazə`-dir.

## P1 Skeleton Glossary

Bu glossary skeleton naming və wire/storage terminology drift-inin qarşısını almaq
üçündür. Visible UI text yenə localization resource-lardan gəlir; bu table raw
hardcoded UI string siyahısı deyil.

| Code / domain term | Meaning | UI / localization guidance | Storage / wire status | Owner |
|---|---|---|---|---|
| `Owner` | Local device-də ən yüksək admin rol; first-user bootstrap invariantı var | Role label localization resource ilə göstərilir; code/ADR-də `Owner` qalır | `Users`/permission bootstrap state-də rol semantikasıdır | `ADR-0038`, user-management spec |
| managed user | Owner olmayan local user | Visible label localization resource-dan gəlir; `Owner` ilə qarışdırılmır | User/account state; permission grants ayrıca saxlanır | user-management spec |
| `Permission` | Operation/capability grant anlayışı | Default AZ visible term `İcazə` | Permission registry/grant records; explicit deny record yoxdur | `ADR-0048`, this ADR |
| operation key | `<targetId>.<verb>` permission check key-i | UI raw key göstərmir; resource label göstərir | Stable code/storage key; ordinal comparison | `ADR-0048` |
| `customerId` | Customer identity field | Raw customer id normal user UI-da göstərilməməlidir | `.dobbylicense` və `CustomerDeploymentContext` field-i | `ADR-0051`, `ADR-0052` |
| `deploymentId` | License/customer deployment binding field-i | Support-safe display lazımdırsa redacted/labelled göstərilir | `.dobbylicense` signed payload field-i; Branch Shared `branchDeploymentId` deyil | `ADR-0051`, `ADR-0052` |
| `CustomerDeploymentContext` | Single active customer/deployment context | UI concept kimi göstərilmir; setup/support wording istifadə edir | Minimum fields: `customerId`, `deploymentId`, `source`, `protectionScope`, `schemaVersion`, `createdAt`, `updatedAt` | `ADR-0052`, storage spec |
| `customerDeploymentId` | `(customerId, deploymentId)` üçün prose-only shorthand | UI/code/storage adı kimi istifadə olunmur | Wire field, code identifier və storage column deyil | license-import spec, `ADR-0052` |
| `branchDeploymentId` | Branch Shared server/provisioning/restore identity | Branch Shared setup/status wording-də ayrıca göstərilir | `.dobbylicense` `deploymentId` ilə birləşdirilmir | `ADR-0056`, `ADR-0057` |
| `DeviceLocal` | Local encrypted SQLite/protected storage scope | User-facing storage mode label localization-dan gəlir | Mode-aware storage taxonomy value | `ADR-0056`, storage spec |
| `BranchShared` | Branch LAN PostgreSQL shared authority scope | Branch Shared mode label localization-dan gəlir | Mode-aware storage/provider scope; runtime Tier B | `ADR-0056`, postgres ops spec |
| `OrganizationShared` | Future organization-wide vocabulary | P1 UI-da active mode kimi göstərilmir | Future vocabulary only; no P1 schema/provider | `ADR-0056` |
| `keyVersion` | Vendor signing key version selector | UI-da raw göstərilmir | Numeric JSON value; P1 `1`, future `2`, `3`, ...; string `"v1"` deyil | `ADR-0033` |
| `.dobbylicense` | Detached vendor-signed license artifact | User entry point `Plugins > License`; raw JSON göstərilmir | UTF-8 JSON `{ payload, signature }` envelope | `ADR-0051`, license-import spec |
| `.dobbylicense.payload` | Signed license semantic object | UI raw payload göstərmir | Canonical JSON serialization imzalanır | `ADR-0051` |
| `.dobbylicense.signature` | Signature metadata and bytes | UI raw signature göstərmir | `algorithm`, numeric `keyVersion`, base64url no-padding `value` | `ADR-0051`, `ADR-0033` |
| license lifecycle state | Persisted/state-machine vocabulary: `no-license`, `pending`, `active`, `grace`, `readonly`, `denied` | Normal user UI broad status göstərir, raw lifecycle detail deyil | Lowercase state-machine/persistence vocabulary | `ADR-0051` |
| `PluginRuntimeMode` | Plugin-visible runtime enum | Not a UI status label | Values: `FullAccess`, `ReadOnlyExpired`, `Denied` | `ADR-0053` |
| UI/license status | Broad user-facing status vocabulary | `NoLicense`, `Pending`, `Grace`, `Denied`, `FullAccess`, `ReadOnlyExpired` localization resource-larla göstərilir | Runtime enum deyil | `ADR-0051`, license-import spec |
| `P1 Persistence Materialization Pass` | Later gate that materializes schemas/provider packages after provider-neutral skeleton | UI term deyil | Owns table schemas, concrete migrations/entities/repositories and production DB-at-rest provider packages | storage spec, `ADR-0054` |
| `ProviderNeutralSkeleton` | Initial skeleton storage stance | UI term deyil | Config/storage stance; no concrete provider package/schema materialization | `ADR-0027`, `ADR-0054` |
| `Dobby.UI` host/built-in inventory | Host/shell WPF implementation controls | Plugin UI contract kimi sənədləşdirilmir | Implementation inventory only; plugin-visible contract deyil | architecture map, design-system assets |
| `TopToolbar` | Shell top navigation surface | Product docs may use this term; visible label localization-dan gəlir | Navigation target vocabulary, not DTO name | shell-ui spec, ADR-0005 |
| `Sidebar` | Shell side navigation surface | Product docs may use this term | Navigation target vocabulary | shell-ui spec, ADR-0005 |
| `Third Bar` | Selected Sidebar item-in child navigation surface-i | Product docs may use this term | Navigation target vocabulary | shell-ui spec, ADR-0005 |
| `plugin page region` | Plugin page-local composition target | Product docs may use this term | Manifest `extensionPoints.target` value | shell-ui spec, ADR-0036 |

## Consequences

- UI string-lər hardcoded termin variantları ilə səpələnmir.
- Code surface `Permission`, `Owner`, `OperationPermissionCapability` kimi stabil adları saxlayır.
- Product sənədləri `SidebarTargetDto` və oxşar code-level adlara bağlanmır;
  shell target-ləri TopToolbar, Sidebar, Third Bar və plugin page region kimi qalır.
- Termin dəyişməsi resource/mapping səviyyəsində edilir, contract rename tələb etmir.
- Static scan visible UI hardcoded string-ləri və mixed terminology riskini release gate-də tuta bilər.
- Skeleton public type, storage field, wire field və UI label naming-i bu glossary,
  owner ADR/spec və localization resource boundary-lərinə uyğun saxlanır.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — Əsas Məhsul Prinsipləri, terminology qaydası
- `docs/02_ARCHITECTURE_MAP.md` — Row 49 `String / Localization Management`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-013`, `FR-019`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-017`, `AC-023`
- `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`
- `docs/adr/ADR-0044-localization-resource-organization.md`
- `docs/specs/shell-ui.md`
- `docs/specs/README.md` — glossary ownership reference
- `docs/adr/ADR-0033-signing-key-ownership.md`
- `docs/adr/ADR-0051-license-lifecycle-v2.md`
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md`
- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md`
