# License Import Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-016`, `FR-017`, `FR-018`, `FR-028`, `AC-006`, `AC-020`, `AC-021`, `AC-034`, `NFR-015`, `NFR-016`, `ADR-0048`, `ADR-0060`, `ADR-0052`, `ADR-0053`

## Məqsəd

Bu spec `.dobbylicense` import flow-unu, import entry point-lərini, permission guard-ını, verification ardıcıllığını və user-facing status davranışını bağlayır.

## Product Surface

Canonical import yeri `Plugins > License`-dır.

Plugin Details ekranından `Import license` shortcut-u ola bilər, amma ayrıca flow açmır; eyni `Plugins > License` import flow-una yönləndirir.

About import yeri deyil. Dobby host özü pulsuz platforma olduğu üçün About host/support məlumatlarını göstərir, host license statusu göstərmir.

## Permission

License import built-in permission target-i:

| Target | Verb | Əməliyyat |
|---|---|---|
| `plugins.license` | `view` | Plugin license status siyahısını/detail-ini görmək |
| `plugins.license` | `import` | `.dobbylicense` import etmək |

`Owner` all-grants semantics daşıyır. Managed user üçün `plugins.license.import` grant yoxdursa import başlamır və deny audit trail-ə yazılır. Deny mesajı non-disclosing qalır.

## Import Flow

### Preconditions

License import requires an existing local customer/deployment context created by
the host bootstrap in `ADR-0052` and stored in the pre-login
`CustomerDeploymentContext` table documented by `docs/specs/storage-and-data-protection.md`.
Import validates artifact binding against that context and never creates or changes
`customerId` / `deploymentId` opportunistically.

1. User `Plugins > License` içində `Import license` action-ını seçir.
2. File picker yalnız `.dobbylicense` artifact-larını göstərir; köhnə `.entitlement` extension canonical deyil.
3. Host əvvəlcə bounded artifact bytes/string length/range limitlərini, sonra
   selected file extension, UTF-8 JSON parse və exact `{ payload, signature }`
   artifact shape yoxlaması edir (`ADR-0060` wire format owner-dir;
   `docs/schemas/dobby-plugin-license-v3.schema.json` canonical machine-readable
   schema-dır; v2 `docs/schemas/dobby-plugin-license.schema.json` yalnız bounded
   migration window-da legacy LKG oxunuşu üçündür — bax
   `docs/schemas/artifact-compatibility-matrix.md`).
4. Vendor signature canonical `payload` serialization üzərində purpose-separated
   license signing key ring ilə yoxlanılır (signature purpose/key ID daxil) və
   trust verification aparılır.
5. Artifact-dakı plugin/package/customer/deployment/device binding current host context-ə qarşı yoxlanılır.
6. `packageFingerprint` installed plugin package fingerprint-i ilə tutuşdurulur.
7. Eyni binding scope üçün prior state yoxdursa positive `licenseRevision`, prior state varsa saxlanılmış revision-dan strictly greater `licenseRevision` persisted olur.
8. Plugin license statusu refresh olunur.
9. Import nəticəsi audit trail-ə yazılır.

For a first valid import with no persisted revision for the same binding, a positive
`licenseRevision` is accepted. After persistence, imports with an older or equal revision
follow `ADR-0060` anti-rollback/replay rules and never replace last-known-good state.

Signed artifact intent yalnız `granted` və ya `revoked` ola bilər (`ADR-0060`,
`docs/schemas/license-v3-lifecycle-truth-table.md`). Pending/Active/Grace/
ReadOnlyExpired/Denied runtime state-ləri heç vaxt artifact-dan qəbul edilmir —
intent, binding, trusted time və policy-dən hər evaluation-da derive olunur.
Legacy v2 `payload.status` dəyərləri yalnız bounded migration window-da
non-escalating LKG oxunuşu üçündür; v2 status yeni v3 grant yarada bilməz.

Uğurlu import post-login runtime statusunu yeniləyə bilər, amma plugin raw license artifact detail almır.

## Reject Nəticələri

Reject səbəbləri user-facing səthdə typed və faydalı qalır, amma raw security/internal detail sızdırmır.

Minimum reject vocabulary:

| Səbəb | User-facing məna |
|---|---|
| `invalid-license-file` | Fayl Dobby license artifact-ı deyil. |
| `invalid-signature` | License doğrulanmadı. |
| `untrusted-vendor` | License trusted vendor tərəfindən imzalanmayıb. |
| `binding-mismatch` | License bu cihaz/deployment/plugin üçün keçərli deyil. |
| `package-mismatch` | License installed plugin package ilə uyğun deyil. |
| `older-revision` | Daha köhnə və ya replay edilmiş eyni license revision import edilə bilməz. |
| `permission-denied` | User license import etmək üçün icazəli deyil. |

Bu table user-facing typed reason code vocabulary-sidir. Audit action ID-ləri
`docs/specs/audit-and-diagnostics.md` tərəfindən idarə olunur; dotted audit event
names license import user-facing reason code kimi istifadə edilmir.

Older/equal revision checks use the `ADR-0060` (carried forward from ADR-0051) distinction between artifact binding
and revision series: `ArtifactBindingKey = (pluginId, customerId, deploymentId,
deviceFingerprint)` decides whether an imported artifact matches this host, while
`RevisionSeriesKey = (pluginId, customerId, deploymentId)` owns monotonic
revision continuity. Device rebind changes the artifact binding key, but the
next issued license for the same revision series must still be greater than the
prior maximum revision.

Customer/deployment/device/package binding mismatch-ləri UI-a və plugin-ə yalnız non-disclosing typed reason kimi göstərilir; konkret customer id, deployment id, device fingerprint və package fingerprint açıqlanmır. Raw signature, local path, private key material, raw payload və stack trace UI-a/plugin-ə göstərilmir və raw audit/log payload kimi yazılmır. Diagnostics yalnız redacted support-safe summary saxlayır; audit isə action id, actor, target, typed reason code və correlation id kimi audit-safe field-lərlə məhdudlaşır.

## ReadOnlyExpired Recovery Exception

`ReadOnlyExpired` mode-da plugin/domain import, export, report/file output, mutation və background task start qapalıdır. `.dobbylicense` import isə host-level licensing recovery flow-u olduğu üçün mümkün qalır.

Bu exception permission-siz bypass deyil: signature, binding, package fingerprint, revision və `plugins.license.import` permission yoxlamaları yenə tətbiq olunur.

## Status Surface

`Plugins > License` və Plugin Details plugin license statusunu broad, non-raw formada göstərə bilər:

Runtime mode-lar yalnız bunlardır:

- `FullAccess`;
- `ReadOnlyExpired`;
- `Denied`.

UI/license status vocabulary-si runtime enum-dan ayrıdır və aşağıdakı broad statusları
göstərə bilər:

- `NoLicense` / localized `No license`;
- `Pending`;
- `Grace`;
- `Denied`;
- `FullAccess`;
- `ReadOnlyExpired`.

`NoLicense` runtime mode deyil; `no-license` lifecycle state-i plugin-facing runtime
surface-də `Denied` kimi görünür.

`Grace` də import artifact statusu deyil; `grace` lifecycle state-i yalnız host
refresh/activation zamanı `ADR-0060` derived-state truth table-ına görə computed olur.

Status mapping-in canonical source-u `ADR-0060` və
`docs/schemas/license-v3-lifecycle-truth-table.md`-dir (vocabulary bölgüsü
`ADR-0051 § 8a`-dan carried forward): lowercase lifecycle
state-lər persisted/state-machine vocabulary-dir, PascalCase runtime mode/status
adları isə code-facing runtime və broad UI-facing status səthlərinə bölünür.

Plugin və normal user-facing UI raw expiry, grace period, revision, customer/deployment/device/package binding detail və raw license JSON göstərmir. Support üçün lazım olan detail audit/diagnostics boundary-də saxlanılır.

## Runtime license refresh boundary

Periodik background refresh P1 requirement deyil. Əlavə edilərsə vendor telemetry və ya online activation modeli kimi işləməməlidir.

## Audit və Diagnostics

Minimum audit event-lər:

- `license.import.attempted`;
- `license.import.succeeded`;
- `license.import.rejected`;
- `license.enforcement.denied`;
- `license.enforcement.warning`.

Diagnostics import failure detail-i saxlaya bilər, amma `docs/specs/audit-and-diagnostics.md` redaction boundary-sinə tabedir.

## Artifact Attestation Metadata

License artifact attestation license state machine-i dəyişmir və plugin-visible status-a çevrilmir. Import zamanı host audit/diagnostics metadata kimi source artifact hash, `licenseRevision`, import timestamp, validation result və correlation ID saxlayır.

Attestation metadata support və forensic diagnostics üçündür. User-facing və plugin-facing reject/status mesajları yenə non-disclosing qalır; raw license payload, raw signature, full local path və binding detail UI-a və plugin-ə göstərilmir.

## Acceptance Hooks

- `AC-006` lifecycle mode və raw detail boundary-ni yoxlayır.
- `AC-020` binding mismatch non-disclosing behavior-u yoxlayır.
- `AC-021` ReadOnlyExpired enforcement və license import recovery exception-ını yoxlayır.
- `AC-034` `Plugins > License` import flow-u, permission guard və reject nəticələrini yoxlayır.

## Branch Shared License / Setup-Proof Addendum

BranchSetup may read an existing `.dobbylicense` as setup-time proof. This is
read-only verification only. It does not import license state, mutate license
state, create entitlement, activate plugins or authorize plugin packages.

The signed `.dobbylicense` field remains `deploymentId`. `customerDeploymentId`
is resolved by `DM-093` as prose-only shorthand for `(customerId,
deploymentId)`.

`customerDeploymentId` is not:

- a `.dobbylicense` wire field;
- a code identifier;
- a storage column;
- a new merged identifier;
- a license fixture re-signing trigger.

License import continues to validate the existing `customerId` and
`deploymentId` fields. A future single-field identity would require explicit
license schema versioning and ADR approval.

`branchDeploymentId` is separate Branch Shared server/provisioning/restore
identity and is not embedded into `.dobbylicense`.
It must not be treated as interchangeable with license `customerId` /
`deploymentId` unless a future provisioning flow signs an explicit canonical
mapping and an ADR/spec amendment accepts that mapping.

## 2026-08 License v3 Amendment (Controlling)

- The signed authoritative intent is `Granted` or `Revoked`. `Pending`, `Active`,
  `Grace` and `ReadOnlyExpired` are derived from intent, binding, trusted time and
  policy whenever evaluated; a persisted lifecycle label is not authority.
- Import validates bounded artifact bytes/string lengths/ranges before parsing,
  then canonical signature purpose/key ID, customer/deployment/device/package
  binding, policy and anti-rollback/trusted-time rules.
- Any malformed, invalid, mismatched, revoked or failed import preserves the
  previous last-known-good grant. A valid import atomically commits grant state,
  attestation/trusted-time state and audit/outbox evidence.
- Date arithmetic is checked and policy bounds cap grace duration and identifiers.
  Malformed persisted enum/value data becomes a typed fail-closed result.
- Host support diagnostics are license-state independent. Plugin/domain data
  mutations and exports remain gated.
- BranchSetup uses a separate purpose-signed setup authorization artifact; an
  ordinary `.dobbylicense` is not setup proof.
