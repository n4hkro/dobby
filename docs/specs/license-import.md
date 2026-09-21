# License Import Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-016`, `FR-017`, `FR-018`, `FR-028`, `AC-006`, `AC-020`, `AC-021`, `AC-034`, `NFR-014`, `NFR-015`, `NFR-016`, `NFR-025`, `NFR-026`, `ADR-0060`, `ADR-0059`, `ADR-0061`, `ADR-0063`, `ADR-0053`, `ADR-0052`, `ADR-0048`
**Controlling decisions:** `ADR-0060` (license lifecycle və import v3) artifact/lifecycle authority-sidir və `ADR-0051`-i supersede edir; signature/encoding/key-purpose profile `ADR-0059`-dan, atomic commit semantics `ADR-0061`-dən gəlir; plugin-visible runtime mode `ADR-0053`-dədir. Machine-readable authority: [`docs/schemas/dobby-plugin-license-v3.schema.json`](../schemas/dobby-plugin-license-v3.schema.json) və [`docs/schemas/license-v3-lifecycle-truth-table.md`](../schemas/license-v3-lifecycle-truth-table.md).

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

Import authenticated session daxilində icra olunur (`ADR-0061`); pre-auth
runtime-da license import başlamır.

1. User `Plugins > License` içində `Import license` action-ını seçir.
2. File picker yalnız `.dobbylicense` artifact-larını göstərir; köhnə `.entitlement` extension canonical deyil.
3. Host artifact byte length-ini, UTF-8 JSON parse-ını, string/collection/timestamp/numeric bound-larını və exact `{ payload, signature }` shape-ini yoxlayır. Bound yoxlaması **allocation-dan əvvəl** aparılır; limit aşılırsa parse davam etmir. Canonical wire format owner-i `ADR-0060` və [`dobby-plugin-license-v3.schema.json`](../schemas/dobby-plugin-license-v3.schema.json)-dır (`schemaVersion: 3`, `purpose: dobby.plugin-license`).
4. `signature.purpose`, `signature.algorithm` (`ECDSA-P256-SHA256`), `signature.format` (`IeeeP1363FixedFieldConcatenation`), `keyId` və unpadded base64url encoding `ADR-0059` profile-ına görə yoxlanılır; `payload.keyId` ilə `signature.keyId` **ordinal bərabər** olmalıdır (fərqli olarsa `invalid-signature`), `keyId` bounded identifier-dir (`3..64`) və `signature.value` dəqiq `86` simvol unpadded base64url-dur. Vendor signature domain-separated canonical `payload` serialization üzərində verify olunur, key `keyId` üzrə exact match ilə seçilir (`ADR-0062` § 4; "try every key" davranışı yoxdur) və license leaf key plugin-package və ya recovery key kimi qəbul edilmir (key-purpose separation).
5. Artifact-dakı `pluginId`, `customerId`, `deploymentId`, `deviceKeyHash` və `packageFingerprint` binding-i current host context-ə qarşı yoxlanılır.
6. `packageFingerprint` installed plugin package-in canonical fingerprint-i ilə tutuşdurulur (`ADR-0059` domain-separated manifest fingerprint; raw container byte-ları fingerprint girişi deyil).
7. Timestamp ordering (`issuedAt <= notBefore < expiresAt`), `gracePeriodDays` bound-u (`0..90` gün, aşağıda) və checked date arithmetic yoxlanılır; overflow malformed sayılır.
8. Anti-rollback: eyni `revisionSeriesId` üçün persisted revision yoxdursa positive `licenseRevision` qəbul edilir, varsa saxlanılmış maximum revision-dan strictly greater `licenseRevision` tələb olunur.
9. Bütün yoxlamalar keçdikdən sonra host license grant state-ini, import attestation/trusted-time state-ini, activation refresh request-ini və audit evidence-ini **bir unit of work** kimi commit edir (`ADR-0061`); commit uğursuz olarsa import success elan edilmir və heç bir tərəf logically applied qalmır.
10. Plugin license statusu commit-dən sonra derived state kimi refresh olunur.
11. Import nəticəsi (uğur və ya typed reject) audit trail-ə yazılır.

`deviceKeyHash` device license binding identifier-idir və derivation owner-i
`ADR-0063`-dür: dəyər `SHA-256( canonical-json{ algorithmVersion:
"dobby-license-device-fingerprint-v1", machineGuid, stableDeviceId } )`
lowercase-hex digest-idir. `machineGuid` normalizasiya olunmuş (trim,
lowercase, Unicode `FormC`) hardware dəyəri, `stableDeviceId` isə protected
material-dəki 32-byte random stable device id-nin lowercase hex
representasiyasıdır; canonical serialization `LicenseCanonicalJsonWriter` ilə
aparılır. Sahələrdən biri boş olarsa derivation fail-closed olur və binding
uğurlu sayılmır. Müqayisə exact və fixed-time-dır; fuzzy hardware quorum və
partial match qadağandır (`ADR-0058`).

Bu digest **device identity public key hash-ı deyil**: recovery device
public-key hash-ı ayrı purpose-dur, eyni cihazda da license binding ilə
əvəzlənə bilməz və tərsinə (`ADR-0058` protected material ownership, `ADR-0059`
key-purpose separation). Device private key license verification-da imza,
decrypt və ya possession proof üçün istifadə olunmur. Derivation selector
artifact-da daşınmır: `schemaVersion: 3` implicit olaraq `-v1` derivation
deməkdir və derivation dəyişməsi yeni schema version + yeni ADR tələb edir.
Raw device id, raw `MachineGuid` və full digest UI-a, audit-ə, log-a və export-a
düşmür; bu digest support code deyil (support code versioned keyed digest-dir,
`ADR-0058` § `Support identity`).

For a first valid import with no persisted revision for the same
`revisionSeriesId`, a positive `licenseRevision` is accepted. After persistence,
an artifact with an older or equal revision in the same series is rejected as a
replay/downgrade attempt per `ADR-0060`.

### Grace ceiling

`gracePeriodDays` `ADR-0060`-un "small explicit upper bound" tələbinin konkret
dəyəridir və bu spec ilə **`0..90` gün** kimi bağlanır (schema:
[`dobby-plugin-license-v3.schema.json`](../schemas/dobby-plugin-license-v3.schema.json)).
Grace pəncərəsi `FullAccess` verdiyi üçün ceiling license term-inin yerinə keçə
bilməz.

Host ceiling-i artifact dəyərinə güvənmədən özü tətbiq edir: `90`-dan böyük
`gracePeriodDays`, negative dəyər, non-integer dəyər və `expiresAt + grace`
overflow-u düzgün imzalanmış artifact-la gəlsə də `invalid-license-file` typed
reject-i alır və mövcud last-known-good grant-ı dəyişmir. Deployment config
ceiling-i aşağı sala bilər, yuxarı qaldıra bilməz.

Signed artifact-ın daşıdığı yeganə authoritative dəyər `payload.intent`-dir və o
`granted` və ya `revoked` olur. `Pending`, `Active`, `Grace` və
`ReadOnlyExpired` signed dəyər deyil; onlar hər evaluation zamanı signed time
bound-ları, binding, trusted time və policy üzərindən
[license v3 lifecycle truth table](../schemas/license-v3-lifecycle-truth-table.md)
ilə derive olunur. `NoLicense` persisted absence vocabulary-sidir, importable
artifact dəyəri deyil. Persisted lifecycle label müstəqil authority deyil və
truth table-ı override etmir.

### Legacy v2 artefaktları

`ADR-0060` P1 üçün v2 reader-i **istisna edir** və gələcək reader-i yalnız şərtli
icazə kimi saxlayır ("P1 ships **no** v2 compatibility reader ... Adding a v2
reader later is permitted only through an explicit, separately approved
compatibility path ... covered by fixed retirement dates and negative fixtures").
Bu spec həmin mövqeyi observable import davranışı səviyyəsində bağlayır:
**P1-də v2 compatibility reader implementasiya olunmur və import path-ı v3-only
fail-closed-dur.**

- `schemaVersion` `3`-dən fərqli olan hər artifact — v2 daxil olmaqla — heç bir
  state, attestation və ya activation dəyişikliyi etmədən `invalid-license-file`
  typed reject-i alır. v2 payload-un `status` field-i və `intent`-in yoxluğu ayrıca
  exact-shape yoxlamasından da keçmir.
- v2 `status` vocabulary-si (`active`, `pending`, `readonly`, `denied`) heç bir
  runtime mode-a normalize **edilmir**; belə normalization kodu mövcud deyil və
  əlavə edilməməlidir.
- Ona görə v2 artifact nə yeni grant, nə mövcud grant-ın uzadılması, nə də
  downgrade vasitəsi ola bilməz. Mövcud last-known-good v3 grant v2 faylı ilə
  toxunulmaz qalır.
- `docs/schemas/dobby-plugin-license.schema.json` yalnız frozen historical
  reference və negative-fixture mənbəyidir; live import authority deyil.

Reader gələcəkdə açılarsa, `ADR-0060`-un "fixed retirement dates" tələbi ona
qədər ödənilməlidir: bu bölmədə konkret retirement tarixi/host versiyası
elan olunmalı, matrix sətri yenilənməli və v2-nin escalation etmədiyini göstərən
fixture-lar əlavə olunmalıdır. Retirement bound-u olmayan compatibility reader
əlavə etmək qadağandır — bu, `ADR-0060`-un bağladığı downgrade səthini yenidən
açardı.

Yeni artifact-lar yalnız v3 buraxılır.

Cancellation import-un typed cancelled nəticəsidir: heç bir state, attestation
və ya activation dəyişikliyi qalmır və mövcud last-known-good grant toxunulmaz
saxlanılır.

Uğurlu import post-login runtime statusunu yeniləyə bilər, amma plugin raw license artifact detail almır.

## Reject Nəticələri

Reject səbəbləri user-facing səthdə typed və faydalı qalır, amma raw security/internal detail sızdırmır.

Minimum reject vocabulary:

| Səbəb | User-facing məna |
|---|---|
| `invalid-license-file` | Fayl Dobby license artifact-ı deyil. |
| `unsupported-license-version` | License faylının versiyası bu host tərəfindən dəstəklənmir. |
| `artifact-limit-exceeded` | License faylı gözlənilən ölçü/dəyər hədlərini aşır. |
| `invalid-signature` | License doğrulanmadı. |
| `untrusted-vendor` | License trusted vendor tərəfindən imzalanmayıb. |
| `binding-mismatch` | License bu cihaz/deployment/plugin üçün keçərli deyil. |
| `package-mismatch` | License installed plugin package ilə uyğun deyil. |
| `older-revision` | Daha köhnə və ya replay edilmiş eyni license revision import edilə bilməz. |
| `license-revoked` | License vendor tərəfindən ləğv edilib. |
| `trusted-time-unavailable` | Cihaz vaxtı etibarlı deyil; license yoxlanıla bilmədi. |
| `permission-denied` | User license import etmək üçün icazəli deyil. |
| `import-failed` | Əməliyyat tamamlanmadı; əvvəlki license vəziyyəti dəyişməyib. |

Bu table user-facing typed reason code vocabulary-sidir. Audit action ID-ləri
`docs/specs/audit-and-diagnostics.md` tərəfindən idarə olunur; dotted audit event
names license import user-facing reason code kimi istifadə edilmir.

**Last-known-good qorunması:** yuxarıdakı reject səbəblərinin heç biri mövcud
last-known-good grant-ı, runtime mode-u, plugin activation state-ini və ya
saxlanılmış revision-ı dəyişdirmir. `Denied` runtime mode yalnız (a) currently
valid signed revocation-dan və ya (b) saxlanılmış last-known-good artifact-ın
istifadə anında validation-dan keçməməsindən yaranır — heç vaxt uğursuz candidate
fayldan (`ADR-0060`). Malformed və ya oxunmayan persisted license state istifadə
üçün fail-closed olur, amma əvəz etməli olduğu last-known-good state-i məhv
etmir.

Older/equal revision checks use the v3 distinction between artifact binding and
revision series: artifact binding key `(pluginId, customerId, deploymentId,
deviceKeyHash, packageFingerprint)` decides whether an imported artifact matches
this host and this installed package, while the signed `revisionSeriesId` owns
monotonic revision continuity. Device rebind or package re-signing changes the
artifact binding key, but the next issued license for the same
`revisionSeriesId` must still carry a strictly greater `licenseRevision` than the
prior persisted maximum for that series.

Customer/deployment/device/package binding mismatch-ləri UI-a və plugin-ə yalnız non-disclosing typed reason kimi göstərilir; konkret customer id, deployment id, device fingerprint və package fingerprint açıqlanmır. Raw signature, local path, private key material, raw payload və stack trace UI-a/plugin-ə göstərilmir və raw audit/log payload kimi yazılmır. Diagnostics yalnız redacted support-safe summary saxlayır; audit isə action id, actor, target, typed reason code və correlation id kimi audit-safe field-lərlə məhdudlaşır.

## ReadOnlyExpired and Denied Recovery Exception

`ReadOnlyExpired` mode-da plugin/domain import, export, report/file output,
print-to-file, mutation və background task start qapalıdır. `.dobbylicense`
import, host support diagnostics export və support/recovery UI isə host-level
flow-lar olduğu üçün truth table-ın **hər** sətrində — `NoLicense`, `Pending`,
`Grace`, `ReadOnlyExpired` və `Denied` daxil — mümkün qalır (`ADR-0060`).

Bu exception permission-siz bypass deyil: bound, signature, key purpose, binding, package fingerprint, revision/anti-rollback və `plugins.license.import` permission yoxlamaları yenə tətbiq olunur.

## Status Surface

`Plugins > License` və Plugin Details plugin license statusunu broad, non-raw formada göstərə bilər:

Plugin-visible runtime mode-lar (`PluginRuntimeMode`, `ADR-0053`) yalnız bunlardır:

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
evaluation zamanı signed `expiresAt`, `gracePeriodDays` və trusted time üzərindən
derive olunur.

Status mapping-in canonical source-u
[`docs/schemas/license-v3-lifecycle-truth-table.md`](../schemas/license-v3-lifecycle-truth-table.md)-dir
(`ADR-0060` altında; bu mapping superseded `ADR-0051 § 8a`-dan gələn məsuliyyəti
əvəz edir). Lowercase lifecycle state-lər persisted/derived vocabulary-dir,
PascalCase adlar isə code-facing `PluginRuntimeMode` və broad UI-facing status
səthlərinə bölünür. Derived state hər evaluation-da yenidən hesablanır; UI
göstərilən status heç vaxt plugin-ə əlavə license detail sızdırmır.

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

Event ordering və reject mapping normativdir:

| Moment | Event | Result | Minimum details |
|---|---|---|---|
| Permission guard rədd edir (import başlamır) | `permission.denied` | `denied` | `target=plugins.license`, `verb=import` |
| Artifact seçilib, verification başlamazdan əvvəl | `license.import.attempted` | `success` | `pluginId` (məlumdursa) |
| Hər typed reject (§ `Reject Nəticələri`) | `license.import.rejected` | `failed` | `reasonCode` = həmin typed reason code |
| Commit uğurlu | `license.import.succeeded` | `success` | `pluginId`, `licenseId`, `revisionSeriesId`, `licenseRevision` |

`license.import.attempted` verification-dan **əvvəl** yazılır ki, uğursuz və ya
yarımçıq qalan import cəhdi audit-də iz qoysun; attempt qeydi persist edilə
bilmirsə import fail-closed dayanır. `permission-denied` reject-i
`license.import.rejected` yerinə `permission.denied` event-i ilə qeyd olunur və
`plugins.license.import` grant-ı olmayan çağırış üçün license verification
pipeline-ı heç vaxt işə düşmür. Typed reason code-lar audit `details` sahəsində
olduğu kimi saxlanılır; dotted audit event adları user-facing reason code kimi
istifadə edilmir.

`license.import.succeeded` event-i license grant state dəyişikliyi ilə eyni unit
of work-də commit olunur; commit uğursuz olarsa nə state, nə də success audit
qeydi qalır (`ADR-0061`, `NFR-025`). Audit append-only-dır və import audit qeydi
sonradan redaktə/silinmə ilə "təmizlənmir".

Audit qeydi yalnız audit-safe field-lərlə məhdudlaşır: action id, actor, target
plugin id, typed reason code, correlation id və derived state. Raw payload, raw
signature, artifact byte-ları, local file path, exception message/stack trace,
device binding fingerprint və key material audit-ə və exported diagnostics-ə
düşmür (`ADR-0019`, `NFR-026`).

Diagnostics import failure detail-i saxlaya bilər, amma `docs/specs/audit-and-diagnostics.md` redaction boundary-sinə tabedir. Host support diagnostics export license state-dən asılı deyil və `Denied`/`ReadOnlyExpired` vəziyyətində də işləyir (`ADR-0060`).

## Artifact Attestation Metadata

License artifact attestation derived lifecycle state-i dəyişmir və plugin-visible status-a çevrilmir. Import zamanı host audit/diagnostics metadata kimi artifact byte-ları üzərində domain-separated digest, `licenseId`, `revisionSeriesId`, `licenseRevision`, import timestamp, trusted-time snapshot, validation nəticəsi və correlation ID saxlayır. Attestation device binding fingerprint-ini və raw artifact-ı saxlamır.

Attestation metadata support və forensic diagnostics üçündür. User-facing və plugin-facing reject/status mesajları yenə non-disclosing qalır; raw license payload, raw signature, full local path və binding detail UI-a və plugin-ə göstərilmir.

## Acceptance Hooks

- `AC-006` lifecycle mode və raw detail boundary-ni yoxlayır.
- `AC-020` binding mismatch non-disclosing behavior-u yoxlayır.
- `AC-021` ReadOnlyExpired enforcement və license import recovery exception-ını yoxlayır.
- `AC-034` `Plugins > License` import flow-u, permission guard və reject nəticələrini yoxlayır.

## Branch Shared Setup Authorization Addendum

An ordinary `.dobbylicense` is **not** setup authorization and BranchSetup does
not read it as setup-time proof (`ADR-0060`; supersedes the earlier
license-as-setup-proof model in `ADR-0057` and related BranchSetup text). A
future BranchSetup flow uses a separate vendor-signed setup authorization
artifact with its own type, schema, key purpose, branch/deployment binding,
request id, nonce, expiry and one-use semantics; Windows administrator status and
the protected setup credential are additional gates. Machine-readable shape:
[`docs/schemas/dobby-branch-setup-authorization-v1.schema.json`](../schemas/dobby-branch-setup-authorization-v1.schema.json).

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

## Verification Obligations

Bu spec-in tələbləri behavior-first sübutla bağlanır (`ADR-0061`, `NFR-027`).
Source-text, regex, fayl mövcudluğu və ya "test list green" iddiası aşağıdakı
maddələri qapatmır:

| Yoxlama | Minimum sübut |
|---|---|
| Truth table-ın hər sətri (`NoLicense`, `Pending`, `Active`, `Grace`, `ReadOnlyExpired`, `Denied`) | Deterministik `TimeProvider` ilə boundary instant testləri (`notBefore`, `expiresAt`, `expiresAt + gracePeriodDays` dəqiq UTC sərhədləri) |
| Bound/limit yoxlaması allocation-dan əvvəl | Oversized/malformed artifact fixture-ları ilə runtime test; parse cəhdi limit-dən sonra davam etməməlidir |
| Signature/key purpose | Yanlış `purpose`, yanlış `keyId`, plugin-package key-i ilə imzalanmış license, encoding variant (padded base64, DER) fixture-larının fail-closed olması |
| `keyId` cross-field binding | `payload.keyId` ≠ `signature.keyId` fixture-ının `invalid-signature` ilə rədd olunduğunu göstərən test; out-of-range (`<3`, `>64`) və pattern-dən kənar `keyId` dəyərləri də rədd olunur. Artefakt səviyyəsində owning schema selector-un `license-<prod\|test>-<YYYY-MM>` namespace-ində olmasını tələb edir (`ADR-0062` § 1), yəni plugin-package və ya recovery ring-inin selector-u signature verification-dan əvvəl shape violation kimi rədd olunur; runtime validator-un generic bounded şəkli bu namespace yoxlamasını əvəz etmir və hər iki qat üçün committed negative fixture tələb olunur |
| Anti-rollback | Eyni `revisionSeriesId` üçün equal və older `licenseRevision` replay fixture-ları |
| Grace ceiling | `gracePeriodDays` = `0`, `90` accept; `91`, negative, non-integer və `expiresAt + grace` overflow fixture-larının düzgün imzalanmış artifact-la da `invalid-license-file` alması |
| Last-known-good | Hər reject səbəbindən sonra əvvəlki grant/runtime mode/revision-ın dəyişmədiyini göstərən test |
| Atomicity | Commit-in audit/outbox hissəsi uğursuz olanda nə state, nə success audit qalmadığını göstərən fault-injection testi |
| Non-disclosure | User-facing və plugin-facing mesajlarda konkret binding field, id, path, key və exception detail-in olmadığını yoxlayan test |
| Device binding derivation | Fixed input üçün stabil digest, normalizasiya ekvivalentliyi, boş `machineGuid`/stable device id-də fail-closed və bir simvol fərqli digest-in rədd olunması (`ADR-0063`) |
| Cancellation | Import cancel olunanda typed cancelled nəticə və dəyişməmiş state |
| v2 fail-closed (compatibility reader yoxdur) | `schemaVersion = 2` və v2-shaped payload (`status` var, `intent` yox) fixture-larının düzgün imzalanmış halda da `invalid-license-file` aldığını və mövcud v3 last-known-good grant-ı dəyişmədiyini göstərən test; v2 `status` dəyərinin heç bir runtime mode-a normalize olunmadığı |

## Cross-references

- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — controlling decision (supersedes `ADR-0051`)
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — signature/encoding profile, key purpose separation, package fingerprint
- `docs/adr/ADR-0062-signing-key-custody-and-trust-anchor-provisioning.md` — `keyId` selector, purpose-separated license leaf key, bounded verification ring (supersedes `ADR-0033`)
- `docs/adr/ADR-0063-device-license-binding-derivation.md` — `deviceKeyHash` derivation, versioning və digest-only vendor input qaydası
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — authenticated-session boundary, atomic commit, behavior-first proof
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — `PluginRuntimeMode`
- `docs/adr/ADR-0052-tenant-customer-context-v2.md` — customer/deployment context
- `docs/schemas/dobby-plugin-license-v3.schema.json` — artifact schema
- `docs/schemas/license-v3-lifecycle-truth-table.md` — derived state və runtime mode mapping
- `docs/schemas/dobby-plugin-license.schema.json` — frozen v2 schema; yalnız historical reference və negative-fixture mənbəyi, live import authority deyil
- `docs/specs/plugins-manager.md` — `Plugins > License` surface
- `docs/specs/audit-and-diagnostics.md` — audit action id-ləri və redaction boundary
- `docs/specs/storage-and-data-protection.md` — `CustomerDeploymentContext` və license state persistence

## Revision History

- **2026-08-27:** Legacy v2 handling shipped davranışla uzlaşdırıldı və
  `ADR-0060`-un "fixed retirement dates" tələbindəki boşluq bağlandı. Əvvəlki mətn
  v2 artefaktların "yalnız explicit compatibility reader vasitəsilə oxunduğunu" və
  `status` dəyərinin v3 model-ə normalize edildiyini iddia edirdi; belə reader
  repository-də mövcud deyil — `LicensePayloadValidator` `schemaVersion != 3` olan
  hər payload-u `invalid-license-file` ilə fail-closed rədd edir (behavioral proof:
  `tests/Dobby.UnitTests/Licensing/LicensePayloadValidatorTests.cs`
  `Validate_rejects_invalid_v3_authority_values`, `schemaVersion = 2` case-i).
  `ADR-0060` v2 reader-i icazə verir, tələb etmir, ona görə successor ADR lazım
  deyil. Yeni `### Legacy v2 artefaktları` bölməsi v3-only fail-closed davranışı
  normativ edir və reader-in gələcəkdə açılmasını konkret retirement
  tarixi/host versiyası elan edilməsi şərtinə bağlayır (bound-suz reader qadağandır,
  çünki `ADR-0060`-un bağladığı downgrade səthini yenidən açardı). Verification
  obligations cədvəlinə v2 fail-closed sətri əlavə olundu;
  `docs/schemas/artifact-compatibility-matrix.md` müvafiq olaraq propagate edildi.
- **2026-08-26:** `deviceKeyHash` derivation-ı `ADR-0063`-ə uyğunlaşdırıldı
  (protected stable device id + normalizasiya olunmuş `MachineGuid` üzərindən
  versioned canonical digest; "device identity public key hash-ı" mətni ləğv
  edildi), derivation versioning qaydası və device-binding verification obligation
  sətri əlavə olundu.
- **2026-08-26:** Grace ceiling normativ olaraq `0..90` gün kimi bağlandı;
  host-side re-application, typed reject davranışı, deployment policy istiqaməti,
  verification obligation sətri və `FR-016` / `AC-034` / truth table propagation-ı
  əlavə edildi (`ADR-0060` "small explicit upper bound" tələbinin konkretləşməsi).
- **2026-08-25:** v3 reconciliation. Artifact authority `ADR-0051`-dən `ADR-0060`-a
  keçdi; signed `status` vocabulary-si signed `intent` (`granted`/`revoked`) +
  derived truth table ilə əvəz olundu; bound-before-allocation, key-purpose
  separation, `revisionSeriesId` anti-rollback, last-known-good qorunması, atomic
  commit, `deviceKeyHash` tərifi, genişlənmiş reject vocabulary və behavior-first
  verification obligations əlavə edildi; license-as-setup-proof modeli silindi.
