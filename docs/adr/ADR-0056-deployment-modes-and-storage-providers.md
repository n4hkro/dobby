# ADR-0056: Deployment Modes and Storage Providers

**Status:** Accepted
**Date:** 2026-05-06

## Context

Branch Shared storage/deployment review-dən sonra Dobby-nin storage qərarları
Standalone-only sənədlərlə məhdud qala bilməz. Eyni zamanda Phase 1 istəyi bir
branch və az sayda LAN cihazı üçündür; internet sync, multi-branch, manual
existing PostgreSQL support və skeleton/code implementation bu ADR-in hazırki
sənəd pass-inin scope-u deyil.

Bu ADR deployment mode, storage provider, data scope və release tier
sərhədlərini formalizə edir. Authentication/session detallarının canonical
owner-i `ADR-0057`-dir; bu ADR həmin nəticələri yalnız mode boundary kimi
göstərir.

## Decision

Dobby iki Phase 1 deployment mode tanıyır:

| Mode | Authority | Backing provider | Supported topology |
|---|---|---|---|
| Standalone Installation | Tək cihaz | Local encrypted SQLite / local protected storage | Single device |
| Branch Shared Installation | Bir branch LAN authority | PostgreSQL over LAN + DeviceLocal protected profile/secrets | Main device + seller devices in same branch |

Storage scope-ları belədir:

- `DeviceLocal`: hər cihazda local protected material, diagnostics, connection
  profile və setup/bootstrap evidence.
- `BranchShared`: Branch Shared authoritative business data, users,
  permissions, audit və shared plugin key/value data.
- `OrganizationShared`: future vocabulary-only anlayışdır; Phase 1 code,
  provider enum, schema, migration və runtime branch-larına əlavə edilmir.

SQLite yalnız embedded/local istifadə üçündür. SQLite faylı shared folder,
network drive və ya LAN database kimi istifadə edilmir.

PostgreSQL yalnız Branch Shared authoritative data üçündür. Branch Shared
customer release "selected PostgreSQL setup artifact/runbook" tələb edir. Exact
binary packaging/distribution forması həmin selected artifact/runbook daxilində
Tier B customer-release zamanı seçilir; bu ADR bundled PostgreSQL claim-i etmir.
Manual existing PostgreSQL və validator CLI Phase 2/deferred mövzularıdır.

`branchDeploymentId` Branch Shared server/provisioning/restore identity-sidir.
O, main-device provisioning zamanı yaradılır, PostgreSQL-də authoritative
saxlanır, cihazların DeviceLocal profile-larında immutable expectation kimi
saxlanır və wrong-server/wrong-deployment guardrail kimi istifadə olunur.
Bu identifier `.dobbylicense` içindəki `deploymentId` ilə birləşdirilmir.
License `customerId` / `deploymentId` və Branch Shared `branchDeploymentId`
yalnız gələcək provisioning flow-u signed canonical mapping yaradarsa və
ADR/spec amendment bunu qəbul edərsə bir-biri ilə əlaqələndirilə bilər.

`branchshared.json` non-secret topology/identity profile-dır. Secret və password
material bu faylda saxlanmır. Profile JSON Schema ilə validate olunur və
`serverIdentityPinMaterial` lowercase 64-character SHA-256 hex formatındadır.
Live profile reference field-i `deviceProfileRef` adlanır və current Branch
Shared device profile/registration record-a non-secret reference-dir. Audit
origin payload-dakı `localDeviceRef` isə event-time snapshot semantics saxlayır
və profile rename-i onu dəyişmir.

Tier sərhədi:

- Branch Shared **documentation tier** (pre-Tier A sənəd pass-i) yalnız ADR/spec/docs/schema
  update-ləridir.
- Future Tier A skeleton yalnız provider-neutral interface/config/result
  shape-dir və ayrıca human approval tələb edir.
- Npgsql runtime adapter, live PostgreSQL tests, functional BranchSetup,
  PostgreSQL setup packaging və customer release evidence Tier B/customer
  release işidir.
- Phase 2/future işlər `OrganizationShared`, multi-branch, app-server mediated
  runtime, manual existing PostgreSQL validator və plugin-owned relational
  schemas kimi ayrıca qərar tələb edən mövzulardır.

Terminology guardrail: Branch Shared tier vocabulary-si (`documentation tier`,
`Tier A`, `Tier B`) məhsul phase taxonomy-si (`P1-core`, `P1-if-time`, `P2`,
`P3`, `later`) ilə eyni ox deyil və onunla əvəzlənmir. Branch Shared rollout
addımlarına "Mərhələ 1" / "Phase 1" adı verilmir, çünki bu ad məhsulun Phase 1
scope-u ilə qarışdırılır.

Provider semantic guardrails:

- Data scope semantics physical provider-dən ayrıdır.
- Plugin contracts storage topology almır və `StorageScope` manifest/API field-i
  əlavə edilmir.
- Host-owned plugin shared key/value semantics Branch Shared mode-da qorunur;
  plugin business schema/migration Phase 2-yə qalır.
- Standalone-to-Branch Shared promotion workflow deferred-dir; migration history
  və provider compatibility guardrails spec-lərdə sənəd kimi saxlanır.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-005` | RESOLVED_BY_HUMAN_DECISION (`DM-092`): Branch Shared uses a Dobby-selected setup artifact/runbook. Bundled-vs-official-binary shape is selected inside Tier B customer-release work. |
| `HD-006` | RESOLVED_BY_HUMAN_DECISION (`DM-093`): `customerDeploymentId` is prose-only shorthand for `(customerId, deploymentId)`. It is not a code field, storage column or signed license wire field. |
| `HD-007` | RESOLVED_BY_HUMAN_DECISION (`DM-094`): do not create `docs/DOCS_STRUCTURE.md`; create/use the narrow `docs/specs/README.md` specs index. |

## Reopen Conditions

These resolved decisions must not be reopened for preference-only reasons.

| Decision | Reopen condition |
|---|---|
| `DM-092` / `HD-005` | Branch Shared customer-release packaging chooses a concrete binary/distribution model, manual existing PostgreSQL becomes required, or the deployment support model changes. |
| `DM-093` / `HD-006` | License artifact schema versioning is approved, the license/customer identity model changes, or a later ADR approves a single-field merged identifier. |
| `DM-094` / `HD-007` | Docs governance later requires a global docs structure index, spec count/ownership changes require a different index, or repository documentation governance is redesigned. |

## Consequences

- Existing Standalone docs remain correct for Standalone but are mode-bounded.
- Branch Shared does not create offline/shared authority assumptions. LAN
  PostgreSQL unavailability fails closed for shared operations.
- PostgreSQL operational protection floor is owned here and by
  `docs/specs/postgres-deployment-ops.md`; SQLite/local-file at-rest protection
  remains owned by `ADR-0054`.
- Branch Shared schema/grant/backup/restore evidence belongs to docs/specs now
  and runtime implementation later.
- This ADR does not authorize skeleton code generation by itself.

## Cross-references

- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md` - Branch Shared auth,
  setup and session boundary.
- `docs/specs/postgres-deployment-ops.md` - PostgreSQL setup, grants, backup and
  customer-release evidence.
- `docs/specs/storage-and-data-protection.md` - storage taxonomy and protected
  storage spec language.
- `docs/schemas/branchshared.schema.json` - Branch Shared profile schema.
- `docs/03_TECH_STACK_AND_VERSIONS.md` - PostgreSQL 16.x reservation and no Tier
  A Npgsql dependency.
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md` - exact failure/result vocabulary.

## Revision History

- **2026-05-06:** Accepted after `HD-005`, `HD-006` and `HD-007` were resolved
  by human decision. This status promotion is documentation lifecycle only and
  does not open Tier A skeleton implementation.
