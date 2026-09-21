# Onboarding and Provisioning Spec

**Tarix:** 2026-05-06
**Status:** Accepted spec
**Phase:** Branch Shared documentation / future implementation input
**Scope:** BranchSetup setup proof, first Owner provisioning, device
registration and setup evidence routing

This spec records the Branch Shared onboarding model required by `ADR-0057`. It
does not create a `Dobby.BranchSetup` implementation in the current document
pass.

## 1. Setup Context

Branch Shared setup uses a separate customer-side context:

- executable identity: `Dobby.BranchSetup.exe`;
- credential visibility: setup-authorized context may load protected
  `dobby_setup`;
- runtime separation: `Dobby.Host` does not load `dobby_setup` and does not run
  setup DDL;
- vendor separation: `Dobby.Admin` remains vendor-side admin/signing tooling.

Running BranchSetup and `Dobby.Host` simultaneously on the same device is
forbidden. BranchSetup has its own local coordinator separate from the
`Dobby.Host` single-instance coordinator.

## 2. Setup Authorization

Setup authorization requires **all** of the following gates. `HD-001`-in ilkin
`DM-071` modeli plugin license-i setup proof kimi qəbul edirdi; `ADR-0060` həmin
hissəni supersede etdi və `DM-071` reopen condition-u ("setup-specific signed
artifact tələb olunur") realizə olundu:

- Windows local administrator execution;
- protected `dobby_setup` credential;
- dedicated vendor-signed **setup authorization artifact** — schema
  [`dobby-branch-setup-authorization-v1.schema.json`](../schemas/dobby-branch-setup-authorization-v1.schema.json).

Setup authorization artifact-ının normativ tələbləri:

- öz artifact tipi, öz schema-sı və öz **leaf key purpose**-u (`purpose =
  dobby.branch-setup-authorization`) var; plugin package, plugin license və Owner
  recovery key purpose-ları ilə əvəzlənə bilməz
  (`docs/02_ARCHITECTURE_MAP.md` § `Key Purpose Separation`);
- payload explicit `schemaVersion`, `keyId`, `authorizationId`, `operation`
  (`provision-branch` | `join-branch` | `rotate-deployment`), `branchId`,
  `deploymentId`, `nonce`, `issuedAt` və `expiresAt` daşıyır; signature
  `ECDSA-P256-SHA256` + `IeeeP1363FixedFieldConcatenation` formatındadır.
  `keyId` canonical vendor selector-dur və schema onu `branch-<prod|test>-<YYYY-MM>`
  namespace-inə bağlayır (`ADR-0062` § 1): leaf key və trust anchor functional
  BranchSetup işi (Tier B / customer release, `ADR-0056` tier sərhədi) açılana
  qədər authorize edilmir, amma namespace
  indidən bağlıdır ki, deferred purpose sonradan plugin/license/recovery ring-inin
  selector-unu borc götürməsin;
- `nonce` cryptographically secure RNG-dən gəlir və minimum **128-bit** entropiya
  daşıyır (schema floor-u: 22 simvol unpadded base64url; maksimum 86 simvol).
  Nonce `authorizationId`-dən, vaxtdan, counter-dən, branch/deployment id-dən və ya
  digər predictable material-dan derive edilmir; floor-dan qısa, padded və ya
  alphabet-dən kənar dəyər schema validation-da fail-closed reject alır;
- expiry pəncərəsi host tərəfindən artifact dəyərinə güvənmədən tətbiq olunur:
  `expiresAt ≤ issuedAt`, host clock-skew tolerance-ından kənar future-dated
  `issuedAt` və konfiqurasiya olunmuş maksimum pəncərədən uzun `expiresAt −
  issuedAt` düzgün imzalanmış artifact-la da fail-closed rədd olunur;
- tətbiq **single-use və idempotent**-dir: `authorizationId`/`nonce` bir dəfə
  consume olunur, eyni artifact-ın təkrar tətbiqi yeni state dəyişikliyi
  yaratmır;
- replay (consumed nonce), branch/deployment mismatch, operation mismatch, key
  ID/purpose mismatch, signature failure və expiry **fail-closed** nəticədir və
  setup heç bir DDL, credential və ya provisioning mutation-u icra etmir;
- hər tətbiq cəhdi (uğurlu və uğursuz) setup evidence/audit-də typed reason code
  və correlation ID ilə qeyd olunur; raw artifact payload, imza dəyəri və
  credential material yazılmır.

Normal `.dobbylicense` setup authorization deyil. BranchSetup license handling
verification-only qalır: license state import olunmur, mutate edilmir,
entitlement yaradılmır, plugin activate edilmir və plugin package authorize
olunmur.

Bu bölmə Branch Shared implementation işi açmır; artifact kontraktı future
runtime phase üçün bağlanır.

## 3. First Owner Bootstrap

Branch Shared first Owner is created during main-device provisioning. Seller
devices cannot become first Owner by racing first login.

Provisioning order:

1. T1 creates required schemas/tables and deployment metadata.
2. T2 creates initial Owner, permissions and initial setup/audit evidence.

The exact schema names belong to `ADR-0056` and PostgreSQL ops docs.

## 4. Device Registration

Branch Shared registration issues a server-side `deviceId` and records
support-safe non-secret binding material for idempotent retry and device
lifecycle tracking. Registration material must not expose raw MachineGuid,
full license fingerprint, secrets or privacy-sensitive hardware details.
The local Branch Shared connection profile refers to the live device profile or
registration record with `deviceProfileRef`; audit docs retain `localDeviceRef`
only for pre-registration event-time snapshot semantics.

Device replacement and decommission behavior is owned jointly by this spec and
`docs/specs/device-lifecycle.md`.

## 5. Setup Evidence Routing

Before Branch Shared schema exists, BranchSetup writes authoritative setup
report evidence outside PostgreSQL. That pre-schema evidence is not re-injected
into PostgreSQL later.

After schema exists, setup events that are audit-worthy are written to
`dobby_audit` with `source=BranchSetup` taxonomy owned by audit docs.

A deterministic BranchSetup setup-report JSON Schema remains an optional future
improvement. It may be added later if onboarding acceptance requires a
wire-stable evidence artifact. Until then, `DM-071` setup-report bucket and this
routing rule are sufficient.

## Resolved Decisions

| ID | Resolution |
|---|---|
| `HD-001` | Initially RESOLVED_BY_HUMAN_DECISION (`DM-071`); the license-as-setup-proof portion is superseded by `ADR-0060`. Setup authorization now requires the dedicated purpose-signed artifact in § 2 plus the administrator and `dobby_setup` gates. |
| `HD-003` | RESOLVED_BY_HUMAN_DECISION (`DM-090`): normal seller runtime profiles persist only `dobby_app`; `dobby_setup` is BranchSetup-only. |
| `HD-004` | RESOLVED_BY_HUMAN_DECISION (`DM-091`): setup/audit origin uses closed `source` vocabulary and server-issued `originDeviceId`; pre-registration uses `localDeviceRef` snapshot fallback. |

## Cross-references

- `docs/adr/ADR-0056-deployment-modes-and-storage-providers.md`
- `docs/adr/ADR-0057-mode-aware-authentication-boundary.md`
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md`
- `docs/schemas/dobby-branch-setup-authorization-v1.schema.json`
- `docs/adr/ADR-0023-admin-host-boundary.md`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md`
- `docs/specs/device-lifecycle.md`
- `docs/specs/audit-and-diagnostics.md`
- `docs/specs/license-import.md`

## Revision History

- **2026-08-27:** `ADR-0056` cross-reference-i əlavə olundu (§3/§4 onu schema/ops
  authority kimi göstərirdi) və Revision History sibling spec-lərlə eyni
  newest-first ardıcıllığına gətirildi.
- **2026-08-26:** `ADR-0060` reconciliation. `2026-08 Provisioning Amendment`
  bölməsi ləğv edildi; setup authorization artefaktının normativ tələbləri § 2-yə
  köçürüldü, license-as-setup-proof modeli çıxarıldı və `HD-001` resolution row-u
  supersession-i əks etdirir. Bu dəyişiklik Branch Shared implementation işi
  açmır.
- **2026-05-06:** Accepted after `HD-003` and `HD-004` were resolved by human
  decision. This is documentation/spec acceptance only and does not create a
  BranchSetup implementation.
