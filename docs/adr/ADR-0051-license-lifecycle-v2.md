# ADR-0051: License Lifecycle v2

**Status:** Superseded by ADR-0060
**Date:** 2026-04-25
**Supersedes:** ADR-0026

## Context

Owner D-2 və D-3 qərarları Phase 1 licensing modelini dəyişdi:

- plugin license customer, deployment/device və exact plugin package binding ilə
  enforce olunmalıdır;
- expired plugin tam bloklanmır, `ReadOnlyExpired` mode-da köhnə nəticələri göstərə
  bilməlidir;
- plugin raw license detail görməməyə davam edir.
- plugin licensing host-controlled olmalıdır və plugin manifest/runtime səviyyəsində
  opt-out edilə bilməməlidir.

`ADR-0026` binding-i optional saxlayırdı və expired plugin-i bloklayırdı. Bu artıq
decision reversal olduğu üçün yeni ADR köhnəni əvəz edir.

## Decision

Phase 1 licensing modeli License Lifecycle v2-dir.

### 1. License artifact v2

License artifact vendor-signed, detached `.dobbylicense` JSON artefaktıdır. Plugin
package içinə embed edilmir və `.dobbyplugin` package boundary-dən ayrı import olunur.

Minimum payload field-ləri:

- `schemaVersion` — Phase 1 License Lifecycle v2 üçün numeric `2`
- `licenseId`
- `pluginId`
- `customerId`
- `deploymentId`
- `deviceFingerprintAlgorithm`
- `deviceFingerprint`
- `packageFingerprint`
- `licenseRevision`
- `issuedAt`
- `notBefore`
- `expiresAt`
- `gracePeriodDays`
- `status` — signed artifact status vocabulary only: `active`, `pending`,
  `readonly`, or `denied`
- `keyVersion` — numeric JSON value; Phase 1 dəyəri `1`, gələcək rotation dəyərləri
  `ADR-0033` qaydasına görə `2`, `3`, ... kimi artırılır

`customerId`, `deploymentId`, `deviceFingerprintAlgorithm`, `deviceFingerprint` və
`packageFingerprint` Phase 1-də mandatory binding field-ləridir. Package binding
`ADR-0045` canonical signed payload və package fingerprint semantics-i əsasında
hesablanır, raw zip/container bytes əsasında deyil.

### 1b. `.dobbylicense` v2 wire format

`.dobbylicense` faylı UTF-8 encoded JSON object-dir və outer shape yalnız iki root
property daşıyır:

```json
{
  "payload": {
    "schemaVersion": 2,
    "licenseId": "<stable license id>",
    "pluginId": "<plugin id>",
    "customerId": "<customer id>",
    "deploymentId": "<deployment id>",
    "deviceFingerprintAlgorithm": "v1",
    "deviceFingerprint": "<lowercase hex sha256>",
    "packageFingerprint": "<lowercase hex sha256>",
    "licenseRevision": 1,
    "issuedAt": "<ISO 8601 UTC>",
    "notBefore": "<ISO 8601 UTC>",
    "expiresAt": "<ISO 8601 UTC>",
    "gracePeriodDays": 0,
    "status": "active",
    "keyVersion": 1
  },
  "signature": {
    "algorithm": "ECDSA-P256-SHA256",
    "keyVersion": 1,
    "value": "<base64url-no-padding DER ECDSA signature>"
  }
}
```

Machine-readable schema artifact `docs/schemas/dobby-plugin-license.schema.json`
bu wire formatın canonical JSON Schema 2020-12 representation-ıdır. Bu ADR
payload semantics, canonical signing input, verification order və lifecycle
qaydalarının prose owner-i olaraq qalır; schema validator və fixture tests üçün
machine-readable shape owner-idir.

Signed `.dobbylicense` `payload.status` artifact statusu yalnız vendor-declared
artifact intent-i daşıyır: `active`, `pending`, `readonly`, or `denied`.
`no-license` signed artifact statusu deyil; imported license olmadığı zaman
persisted/state-machine vocabulary kimi yaranır. `grace` də signed artifact
statusu deyil; host `expiresAt`, `gracePeriodDays` və current time əsasında
computed lifecycle/runtime status kimi qiymətləndirir.

Root-level extra properties are validation failures. `payload` contains license
semantics and signed fields; `signature` contains only verification metadata and
signature bytes. `signature.keyVersion` must equal a trust profile known to the
host and uses the numeric `ADR-0033` key-version rule. `payload.keyVersion` is the
payload-visible copy used by tooling/fixtures and must match `signature.keyVersion`.

Signed input is the `payload` object canonical JSON serialization:

- UTF-8, no BOM;
- object property names sorted ordinal/culture-invariant ascending at every nested
  object level;
- arrays preserve declared order;
- no insignificant whitespace;
- strings are NFC-normalized before serialization;
- date/time values use ISO 8601 UTC strings with `Z`;
- `schemaVersion`, `licenseRevision`, `gracePeriodDays` and `keyVersion` are JSON
  numbers without string wrappers.

Signature verification order for artifact shape:

1. file extension `.dobbylicense` and UTF-8 JSON parse;
2. exact root shape `{ payload, signature }` and no root extras;
3. signature block shape: `algorithm`, numeric `keyVersion`, base64url no-padding
   `value`;
4. canonicalize `payload` using the rules above and verify ECDSA P-256 SHA-256
   signature with the selected vendor public key;
5. only after signature success, validate binding fields, validity window,
   revision and license lifecycle semantics.

Valid fixtures must include a minimal active license and a readonly-expired
license. Invalid fixtures must include malformed JSON, root extra property,
string `"keyVersion"`, padded base64 signature, signature mismatch, binding
mismatch, package mismatch and older/equal revision cases.

`.dobbylicense.deviceFingerprint` is a license-binding fingerprint, not the Owner
recovery 5-hash fuzzy-match artifact from ADR-0039. Both are computed by host-owned
infrastructure, but they have separate validation policies: license enforcement requires
the exact license binding policy in this ADR; Owner recovery uses ADR-0039 quorum/fuzzy
matching. Implementation may reuse lower-level machine identity probes, but must not
silently substitute ADR-0039 fuzzy recovery acceptance for license device binding
acceptance.

### 1a. Device Fingerprint Algorithm

Phase 1 license device binding uses `deviceFingerprintAlgorithm = "v1"`.

`v1` fingerprint input is canonical JSON with these fields:

- `algorithmVersion = "dobby-license-device-fingerprint-v1"`
- normalized Windows `MachineGuid`
- Dobby-generated stable device id

Canonicalization and comparison rules:

- inputs are trimmed, normalized with invariant string handling and serialized as
  deterministic canonical JSON before hashing;
- the fingerprint value is `SHA-256(canonicalJson)`;
- comparison uses constant-time byte equality;
- deterministic smoke/test fixture mode may provide fixed `MachineGuid` and Dobby
  device id values, but production code must not use fixture values;
- raw `MachineGuid`, raw Dobby device id and full fingerprint are never logged,
  exported to diagnostics or shown in UI;
- diagnostics may include only a short redacted fingerprint prefix.

License payload stores `deviceFingerprintAlgorithm` separately from
`deviceFingerprint`. The verifier reads `deviceFingerprintAlgorithm`, computes the
matching algorithm version and compares the result with the license-bound
`deviceFingerprint`. Future algorithm rotation, for example `v2`, must not invalidate
older `v1` licenses solely because the host supports a newer algorithm.

Package binding tests require ADR-0045 signed payload and package fingerprint semantics
to be resolved first or in the same patch, otherwise `packageFingerprint` comparison is
not deterministic.

Version metadata license artifact və plugin manifest-də qala bilər, amma trust binding
üçün canonical əsas package fingerprint-dir.

### 2. Vendor trust boundary

Phase 1-də single embedded master vendor identity qalır (`ADR-0033`). Multi-vendor
registry və vendorId trust-store P2+ mövzusudur. License artifact vendor master signing
key ilə yoxlanılır və plugin raw signature detail görmür.

### 3. Licensing opt-out qadağası

Phase 1-də bütün plugin-lər host licensing modelinə tabe olur. Plugin manifest və ya
runtime `licenseRequired=false` kimi opt-out siqnalı verə bilməz; belə field gələcəkdə
schema-da görünsə belə, P1 canonical enforcement-i bypass etmir.

### 4. License import surface

License import user-facing olaraq `Plugins > License` flow-u ilə aparılır. Plugin
Details ekranından `Import license` shortcut-u ola bilər, amma eyni host-level flow-a
yönləndirir. About import yeri deyil və Dobby host özü üçün license statusu göstərmir.

Import yalnız `.dobbylicense` file picker ilə başlayır və built-in
`plugins.license.import` operation permission-u tələb edir. Bu host-level import
plugin/domain import sayılmır və `ReadOnlyExpired` bərpa istisnası kimi mümkün qalır.

Minimum import pipeline:

1. file extension, UTF-8 JSON parse və exact `{ payload, signature }` artifact shape
   yoxlanılır;
2. canonical `payload` serialization üzərində vendor signature və trust boundary
   yoxlanılır;
3. customer/deployment/device/package binding current host context-ə qarşı yoxlanılır;
4. `packageFingerprint` installed plugin package fingerprint-i ilə tutuşdurulur;
5. eyni binding scope üçün prior state yoxdursa positive `licenseRevision`, prior state
   varsa persisted revision-dan strictly greater `licenseRevision` saxlanılır;
6. plugin license statusu refresh olunur və audit entry yaranır.

Reject səbəbləri user-facing səthdə typed və non-disclosing qalır; raw signature,
customer/deployment/device/package detail və raw license payload plugin-ə verilmir.

### 5. State machine

```
[no-license] -> (valid license import) -> [pending] if now < notBefore
[pending] -> (first plugin activation attempt where now >= notBefore) -> [active]
[active] -> (expiresAt passed) -> [grace]
[grace] -> (gracePeriodDays passed) -> [readonly]
[readonly] -> (newer valid license import) -> [active]
[any] -> (signature/customer/deployment/device/package mismatch) -> [denied]
[any] -> (older or equal licenseRevision for same binding scope after prior persistence) -> [denied]
```

`expired-blocked` Phase 1-də aktiv state deyil, çünki D-3 qərarı `readonly`
mode-u seçdi. Gələcək ADR read-only davranışını geri alıb expired-blocked
modelinə qayıdarsa, recovery keçidi yenə explicit olmalıdır:
`[expired-blocked] -> (newer valid license import) -> [active]`.

`[pending] -> [active]` üçün polling yoxdur. Keçid ilk plugin activation cəhdində və
ya license status refresh-də qiymətləndirilir.

### 6. Enforcement

- `active`: plugin normal işləyə bilər.
- `grace`: plugin normal işləyir, host warning göstərir və audit warning yaradır.
- `readonly`: köhnə nəticələr görünə bilər, amma mutation, plugin/domain import,
  export, report/file output, print-to-file/PDF və background task start host
  tərəfindən deny edilir.
- `denied`: plugin activation/read access non-disclosing deny olur.
- binding mismatch həmişə non-disclosing deny və audit nəticəsi yaradır.

Read-only enforcement UI düyməsini gizlətməklə məhdudlaşmır. Host storage write,
command/action, background task, plugin/domain import/export/report və print target
boundary-ləri fail-closed işləməlidir.

`.dobbylicense` import read-only expired state-dən çıxmaq üçün host-level recovery
istisnasıdır. Bu import plugin/domain import sayılmır; yenə signature, binding,
package fingerprint və authorization permission check-lərindən keçməlidir.

### 7. License revision

License artifact matching and replay/downgrade detection use two related keys:

- `ArtifactBindingKey = (pluginId, customerId, deploymentId, deviceFingerprint)`
  is the eligibility/match key. The host accepts an artifact only when this key
  matches the current plugin, customer/deployment and device context.
- `RevisionSeriesKey = (pluginId, customerId, deploymentId)` owns monotonic
  license revision continuity. Device rebind changes the artifact binding key,
  but does not reset the revision series.

If no prior license state exists for the same revision series, the first import
accepts any positive `licenseRevision` that passes signature, package binding,
customer/deployment binding, device binding and validity-window checks. After a
prior revision has been persisted for that revision series, only a strictly
greater/newer `licenseRevision` may replace state. Older or equal
`licenseRevision` is denied as replay/downgrade for that series.

### 7a. Artifact attestation metadata

Host import zamanı source artifact hash, `licenseRevision`, import timestamp,
validation result və correlation ID kimi attestation metadata-nı audit/diagnostics
boundary daxilində saxlaya bilər. Bu metadata license state machine-ə yeni state əlavə
etmir, plugin-visible licensing mode-u dəyişmir və user/plugin-facing reject səbəblərini
raw detail ilə genişləndirmir.

### 8. Plugin-visible surface

Plugin raw license JSON, license id, customer/deployment/device/package binding detail,
expiry, grace və revision detail görmür. Plugin-visible licensing mode `ADR-0053`
capability surface ilə verilir.

### 8a. State / runtime mode / UI status mapping

Code-facing runtime mode və user-facing status surface aşağıdakı mapping-i saxlayır:

| License lifecycle state | Runtime mode | UI status | Notes |
|---|---|---|---|
| `no-license` | `Denied` | `NoLicense` / `No license` | `NoLicense` UI/license status-dur, runtime mode deyil; plugin runtime access almır və import flow mümkün qalır. |
| `pending` | `Denied` | `Pending` | `notBefore` çatmayıb; plugin access non-disclosing deny olur. |
| `active` | `FullAccess` | `FullAccess` | Normal operation. |
| `grace` | `FullAccess` | `Grace` | Plugin işləyir; warning və audit warning yaranır. |
| `readonly` | `ReadOnlyExpired` | `ReadOnlyExpired` | Köhnə render/read görünə bilər; mutation/export/report/file output/print-to-file/background task deny. |
| `denied` | `Denied` | `Denied` | Signature/binding/revision və ya enforcement failure non-disclosing deny. |

Lowercase lifecycle state adları ADR state machine və persisted state vocabulary-si
üçündür. PascalCase runtime mode adları yalnız `FullAccess`, `ReadOnlyExpired` və
`Denied` code-facing capability surface dəyərləridir. `NoLicense`, `Pending` və
`Grace` UI/license status vocabulary-sidir, plugin-visible runtime enum deyil. UI
localized display text resource-lardan gəlir və raw license detail göstərmir.

Artifact-declared `payload.status` vocabulary-si persisted lifecycle vocabulary ilə
eyni deyil. Signed import artifact `no-license` və ya `grace` göndərə bilməz:
`no-license` importdan əvvəlki persisted absence state-dir, `grace` isə
`expiresAt` keçdikdən sonra `gracePeriodDays` siyasəti ilə host tərəfindən
computed state-dir.

### 9. Rejected alternatives

- **Optional customer/device binding:** rədd edildi — Owner D-2 binding-i P1 mandatory
  seçdi.
- **Expired plugin tam bloklansın:** rədd edildi — Owner D-3 read-only expired mode-u
  seçdi.
- **Online license server:** rədd edildi — offline-first prinsipinə ziddir.
- **Polling-based pending activation:** rədd edildi — unnecessary CPU/battery cost və
  startup complexity yaradır.
- **Plugin opt-out licensing:** rədd edildi — host-controlled plugin licensing
  boundary-ni zəiflədir.

## Consequences

- `ADR-0026` superseded olur.
- `AC-006` expired plugin davranışı read-only mode-a uyğun yenilənir.
- `04_RESULT_VS_EXCEPTION_POLICY.md` licensing well-known reason code-ları əlavə edir.
- License binding mismatch audit-ə düşür, amma plugin/user-facing reason non-disclosing
  qalır.
- Read-only expired mode Dobby.Application və Dobby.Shell boundary-lərində minimal P1
  enforcement gate tələb edir.
- License import UI və permission davranışı ayrıca `docs/specs/license-import.md`
  ilə test edilə bilən flow-a çevrilir.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — plugin licensing məhsul sərhədi
- `docs/02_ARCHITECTURE_MAP.md` — Row 42 `Dobby.Licensing`, Row 43 `License / Entitlement`, Cədvəl 3 `License Lifecycle v2`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md` — licensing reason code-ları
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-016`, `FR-017`, `FR-028`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-015`, `NFR-016`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-006`, `AC-020`, `AC-021`, `AC-034`
- `docs/specs/license-import.md`
- `docs/schemas/dobby-plugin-license.schema.json` — machine-readable license artifact schema
- `docs/adr/ADR-0045-plugin-package-and-license-artifact-boundary.md` — package fingerprint
- `docs/adr/ADR-0052-tenant-customer-context-v2.md` — customer/deployment context
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — plugin-visible mode

## Branch Shared Setup-Proof Amendment

BranchSetup may read an existing `.dobbylicense` file as setup-time proof under
the closed `HD-001` model. This is verification-only: BranchSetup does not
import license state, mutate license state, create entitlement, activate
plugins or authorize plugin packages.

The signed `.dobbylicense` wire field remains `deploymentId`. The prose term
`customerDeploymentId` is resolved by `DM-093` as prose-only shorthand for
`(customerId, deploymentId)`; this amendment does not rename the signed field,
does not add a new wire field and does not require license fixture re-signing.

Branch Shared `branchDeploymentId` is separate server/provisioning identity and
is not interchangeable with license `customerId` / `deploymentId` unless a
future provisioning flow signs a canonical mapping and an ADR/spec amendment
accepts it.
