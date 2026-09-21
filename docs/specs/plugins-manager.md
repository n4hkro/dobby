# Plugins Manager Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-007`, `FR-008`, `FR-025`, `FR-026`, `AC-004`, `AC-005`, `AC-015`, `AC-031`, `AC-032`, `AC-049`, `NFR-008`, `NFR-009`, `NFR-014`, `NFR-025`, `NFR-026`, `NFR-027`, `ADR-0059`, `ADR-0060`, `ADR-0061`, `ADR-0001`, `ADR-0008`, `ADR-0009`, `ADR-0011`, `ADR-0012`, `ADR-0032`, `ADR-0036`, `ADR-0037`, `ADR-0045`, `ADR-0048`
**Controlling decisions:** package trust, manifest v2, package validation limits, lifecycle contract və declarative plugin UI üçün `ADR-0059`; license runtime mode/lifecycle üçün `ADR-0060` (`ADR-0051`-i supersede edir) + `ADR-0053`; pre-auth boundary və atomic mutation üçün `ADR-0061`. `ADR-0011`/`ADR-0012` sandbox iddiaları, `ADR-0006`/`ADR-0021`/`ADR-0036`/`ADR-0037`/`ADR-0049`-un minimum page/lifecycle hissələri `ADR-0059` ilə əvəz olunur.

## Məqsəd

Bu spec `Plugins` bölməsinin P1 davranışını, plugin install/lifecycle əməliyyatlarını, permission mapping-i və plugin runtime fault isolation qaydasını bağlayır.

## P1 Scope

P1-də Plugins Manager aşağıdakıları edir:

- installed plugin-ləri siyahıda göstərir;
- local `.dobbyplugin` file install flow-u təqdim edir;
- install validation nəticəsini user-a typed və non-raw mesajla göstərir;
- enable/disable əməliyyatlarını idarə edir;
- uninstall üçün confirmation və data saxla/sil seçimi göstərir;
- plugin activation/runtime statuslarını göstərir;
- runtime fault baş verəndə host process-i qoruyur.

P1-də aşağıdakılar yoxdur:

- remote marketplace/catalog;
- online plugin store;
- plugin update/repair/downgrade flow-u;
- hot-reload və background eager hot-activation zəmanəti.

Update/repair/downgrade P2 lifecycle dizaynına saxlanılır. Local marketplace/catalog P3 və ya store/update modelindən sonra açılır.

Bu deferred plugin lifecycle/catalog mövzuları üçün `docs/02_ARCHITECTURE_MAP.md` Future Scope cədvəlinə bax.

## Future Update Scope

Plugin update P2-yə daxil olanda update command selected installed plugin card-dan başlayır və imported package yalnız həmin `pluginId` üçün update kimi qəbul edilir. Same-version repair və downgrade ayrıca explicit qərar tələb edir; P1-də bu əmrlər gizli/disabled qalır.

Plugin restore source rules are owned by `docs/specs/backup-restore-migration.md`: backup artifacts do not blindly embed plugin binaries; restore verifies signed package source and registry metadata before plugin code is trusted again.

## Plugin List

Plugin list minimum bu məlumatları göstərir:

- plugin display name;
- plugin version;
- plugin status;
- short description.

`displayName`, `version` və `description` manifest/cached metadata-dan gəlir. Bu məlumatları göstərmək üçün plugin code execution tələb olunmur.

## Plugin Icons

Plugin icon-ları package/manifest metadata-sından gələn host-validated PNG asset-ləridir. Minimum source ölçüsü `64x64` olmalıdır; missing, invalid, unsupported və ya validation-dan keçməyən icon host default plugin icon-u ilə əvəz olunur.

Runtime plugin SVG loader P1-də açılmır. Icon render-i shell/navigation səthində host-controlled qalır və plugin code execution tələb etmir.

Minimum status vocabulary:

| Status | Məna |
|---|---|
| `Enabled` | Plugin installed və lifecycle olaraq aktivdir; post-login activation cycle-a daxil olur. |
| `Disabled` | Plugin installed qalır, amma navigation entry və runtime activation almır. |
| `Skipped` | Dependency və ya compatible olmayan şərait səbəbi ilə activation-a buraxılmayıb. |
| `Rejected` | Trust, manifest, signature, forbidden content, compatibility və ya package validation səbəbi ilə qəbul edilməyib. |
| `Faulted` | Runtime screen/task/lifecycle hook fault verib və həmin instance/screen/task dayandırılıb. |
| `Quarantined` | Təkrarlanan runtime fault səbəbi ilə plugin process-local quarantine-ə alınıb; bu proses ömrü boyu yenidən activate edilmir (`ADR-0059`). |
| `Pending uninstall` | Activated plugin uninstall təsdiqlənib; plugin disabled/hidden qalır və restart runtime fayl deletion üçün required-dır. |

Bu cədvəl plugin **lifecycle** oxudur. License statusu ayrı, ortoqonal oxdur və
onun vocabulary-si (`NoLicense`, `Pending`, `Grace`, `FullAccess`,
`ReadOnlyExpired`, `Denied`) `docs/specs/license-import.md` § `Status Surface` və
`docs/schemas/license-v3-lifecycle-truth-table.md` tərəfindən idarə olunur. Plugin
card iki oxu göstərir və onları tək enum kimi birləşdirmir; `ReadOnlyExpired`
license oxunun dəyəridir və həmin plugin lifecycle olaraq `Enabled` qalır (köhnə
data/render read-only görünə bilər, mutation isə bloklanır).

Lifecycle oxunda display precedence normativdir və yuxarıdan aşağı tətbiq olunur:
`Pending uninstall`, `Rejected`, `Quarantined`, `Faulted`, `Disabled`, `Skipped`,
`Enabled`. License oxu yalnız lifecycle statusu `Enabled` olan plugin üçün
göstərilir; digər hallarda plugin runtime access almadığı üçün license statusunun
göstərilməsi yanlış siqnal olardı. Heç bir halda iki ox eyni badge-də
birləşdirilmir və license axis dəyəri lifecycle `Rejected` kimi təqdim edilmir.

Expected validation, authorization, compatibility, license/lifecycle və
cancellation nəticələri plugin defect deyil: onlar runtime fault counter-ini
artırmır və quarantine tətbiq etmir (`ADR-0059`). Quarantine yalnız real plugin
runtime fault-larından yaranır və process-local qalır — disk-ə "permanently
banned" kimi yazılmır.

License runtime mode-ları `ADR-0060` (lifecycle/derived state) və `ADR-0053`
(`PluginRuntimeMode` surface) ilə idarə olunur; plugin raw license detail görmür.

## UI States

Plugins Manager list/detail/install ekranları loading, empty, success, error və confirmation state-lərini explicit göstərir. Empty state installed plugin olmadıqda local install action-ını göstərə bilər; error state raw verifier, path, stack trace və key material detail-ı göstərmir.

## Permission Mapping

Plugins Manager built-in target kimi `plugins.management` target id-si ilə permission
registry-yə yazılır. Operation key-lər `<targetId>.<verb>` formasındadır; məsələn
`plugins.management.view`, `plugins.management.import`, `plugins.management.configure`
və `plugins.management.delete`.

| Əməliyyat | Canonical verb |
|---|---|
| Plugin list/detail görmək | `view` |
| Plugin install | `import` |
| Plugin enable/disable | `configure` |
| Plugin uninstall | `delete` |
| Plugin update/repair | `import` (P2) |

Grant yoxdursa əməliyyat deny edilir. Deny səbəbi user-facing səthdə non-disclosing qalır və audit trail-ə yazılır.

Plugin-specific permission UI runtime plugin code-dan target discovery etmir.
Host permission registry-si package validation zamanı signed manifest
`operationTargets = { id, verbs, labelKey }` metadata-sından yaranır. Plugins
Manager yalnız həmin validated registry target-lərini və supported verb-ləri
toggle kimi göstərir; unknown target/verb və manifestdə elan edilməmiş verb
fail-closed deny edilir.

## Local File Install

Install flow:

1. User `Plugins` bölməsində local file picker açır.
2. Yalnız `.dobbyplugin` package seçilir.
3. Host package-i **streaming** oxuyur və hər hard limit-i allocation/extraction-dan
   **əvvəl** yoxlayır: compressed bytes, manifest bytes, entry count, per-entry
   uncompressed bytes, aggregate uncompressed bytes, path length və compression
   ratio (`ADR-0059`). Limit aşılırsa parse dayanır.
4. Manifest `docs/schemas/dobby-plugin-manifest-v2.schema.json` üzrə validate olunur
   (`schemaVersion: 2`, `purpose: dobby.plugin-package`, required `hostApiVersion`
   `MAJOR.MINOR`, bounded collection limitləri). v1 schema
   (`dobby-plugin-manifest.schema.json`) heç bir read path-da yüklənmir: `schemaVersion`
   2-dən fərqli olan manifest həm install validation-da, həm activation
   re-verification-da `unsupported-manifest-version` ilə rədd olunur.
5. Hər entry adı bir dəfə normalize olunur; empty segment, absolute/drive/UNC path,
   alternate separator, trailing dot/space alias, ADS syntax, Windows device name
   (extension variantları daxil), symlink/reparse point, duplicate və case collision
   fail-closed reject edilir.
6. Extraction destination üçün `Path.GetFullPath` hesablanır, separator ilə bitən
   staging root altında containment yoxlanılır, overwrite rədd edilir və write
   zamanı no-follow yoxlaması aparılır; validation və materialization eyni canonical
   path obyektindən istifadə edir.
7. Package completeness, forbidden content, per-file hash, canonical signature və
   trust verification yoxlanılır: `ECDSA-P256-SHA256`,
   `IeeeP1363FixedFieldConcatenation`, padding-siz base64url, domain-separated
   versioned signed bytes və package fingerprint
   `SHA-256(domain ‖ canonical-manifest-without-signature)`. Plugin-package key-i
   license və recovery key-lərindən ayrıdır; provider default signature format-ına
   güvənilmir. Static forbidden-content scan defense-in-depth-dir, containment
   sübutu deyil.
8. `hostApiVersion` major mismatch activation-a icazə vermir; minor uyğunluq
   explicit backward-compatibility matrix ilə yoxlanılır. `minHostVersion`
   product/build floor olaraq qalır və API compatibility-ni əvəz etmir.
9. JSON Schema ilə ifadə olunmayan parent cycle, duplicate id, namespace ownership
   və trust-store lookup qaydaları ADR-0036/ADR-0045/ADR-0048 əsasında procedural
   fail-closed validation kimi qalır.
10. Verification uğurludursa promotion crash-consistent şəkildə aparılır: installed
    state/last-known-good və metadata-cache projection-ı bir transaction/outbox
    unit-i kimi commit olunur (`ADR-0061`); startup recovery yarımçıq transaction-ı
    deterministik tamamlayır və ya rollback edir, projection-ı authoritative
    state-dən yenidən qurur.
11. Verification uğursuzdursa install dayanır; partial install, promoted staging
    qalığı və dəyişmiş metadata cache qalmır və mövcud installed plugin state-i
    toxunulmaz saxlanılır.

Install validation user-facing typed səbəb göstərir:

- `invalid-manifest`;
- `unsupported-manifest-version`;
- `package-limit-exceeded`;
- `unsafe-entry-path`;
- `signature-failed`;
- `trust-failed`;
- `forbidden-content`;
- `hash-mismatch`;
- `compatibility-mismatch`;
- `host-api-mismatch`;
- `install-failed`.

`package-limit-exceeded` archive/decompression limit, unbounded allocation və
integer/range overflow hallarını, `unsafe-entry-path` isə traversal, absolute/UNC,
device name, ADS, symlink/reparse, duplicate/case-collision və containment
pozuntularını əhatə edir. `install-failed` promotion/commit uğursuzluğunun
non-disclosing nəticəsidir və əvvəlki installed state-in dəyişməz qaldığını bildirir.

Bu list user-facing typed reason code vocabulary-sidir. Audit action ID-ləri
`docs/specs/audit-and-diagnostics.md` tərəfindən idarə olunur və
`plugin.install.signature-failed`, `plugin.install.trust-failed` kimi dotted
event names bu user-facing reason list-ə qaytarılmır.

Raw key material, full local path, stack trace və internal verifier detail UI-da göstərilmir və raw audit payload kimi yazılmır. Diagnostics redacted support-safe detail saxlayır; audit yalnız action id, status, typed reason və correlation id kimi audit-safe field-lərlə məhdudlaşır.

License/runtime-mode denial package validation səbəbi deyil. Structurally valid,
signed və trusted package `package-invalid` kimi göstərilmir. Bununla belə,
`ReadOnlyExpired` mode-da local `.dobbyplugin` package install/import host
installed plugin state, install staging/runtime files və installed plugin
metadata/cache mutation etdiyi üçün policy/license-mode denial kimi bloklanır.
User-facing `license-read-only` / `LicenseReadOnly` mapping-i package validation
failure kimi göstərilmir. `.dobbylicense` import `ReadOnlyExpired`
mutation/import block-un recovery exception-ı olaraq qalır. License/runtime mode
host mutation-a icazə verdikdə structurally valid, signed və trusted package
install oluna bilər; post-install runtime/license status surface-ində
`ADR-0060` və `ADR-0053` qaydaları ilə `Denied`, `ReadOnlyExpired` və ya UI
status kimi görünür.

Missing dependency install validation failure deyil. Structurally valid, signed və
trusted package install oluna bilər; missing/disabled dependency yalnız
post-login activation cycle zamanı `Skipped` state-i yaradır və ADR-0009
semantics-i ilə `plugin.activation.skipped` audit səbəbi kimi saxlanılır.

## Enable / Disable

Enable/disable `configure` permission tələb edir.

Disable davranışı:

- plugin navigation entry aktiv işləməz;
- plugin runtime activation-a daxil olmaz;
- həmin plugin-ə aid active work session varsa host controlled close/cancel edir;
- dependency olan plugin-lər növbəti activation cycle-da dependency semantics-ə görə skipped ola bilər.

Enable davranışı:

- plugin lifecycle state-i aktivləşdirir;
- plugin növbəti post-login activation cycle-a daxil olur;
- P1 hot-reload/eager background activation zəmanəti vermir.

## Uninstall

Uninstall `delete` permission tələb edir və confirmation modalı olmadan davam etmir.

Uninstall zamanı user iki seçimdən birini etməlidir:

- plugin datasını saxla;
- plugin datasını sil.

Runtime faylları host nəzarəti ilə silinir. Data saxlanılırsa plugin domain/user-scoped data host storage boundary-də qalır, amma plugin installed/runtime state-i artıq yoxdur. Data silinirsə host plugin storage contract-ları üzərindən həmin plugin-ə aid data-nı silir. Retention, backup və forensic detallar P2 data-retention spec-də dərinləşə bilər.

If a plugin has been activated in the current process, uninstall enters `Pending uninstall` after confirmation. The plugin is disabled immediately, hidden from activation/navigation, and scheduled for physical runtime file deletion on next app start before fresh plugin discovery. The plugin list must show restart-required state until restart completes the deletion.

Uninstall intent-i, keep/delete data seçimi, installed-state removal-ı, metadata-cache
projection dəyişikliyi və `plugin.uninstall` audit event-i bir unit of work kimi commit
olunur (`ADR-0061`, `NFR-025`). Commit uğursuz olarsa plugin əvvəlki installed/enabled
vəziyyətində qalır və uninstall success kimi publish edilmir.

`Pending uninstall` state-i durable-dır və restart-a qədər gözləmək crash-safe
qalmalıdır: intent commit olunandan sonra host çökərsə, process kill olunarsa və ya
maşın söndürülərsə plugin növbəti start-da yenidən discovery-yə düşmür və activation
almır. Runtime fayl deletion idempotentdir və hər start-da uğurlu olana qədər təkrar
cəhd edilir; deletion locked file, permission və ya I/O səbəbi ilə tamamlanmazsa plugin
`Pending uninstall` state-ində və restart-required göstərişi ilə qalır — silent
reactivation, discovery-yə qaytarılma və ya intent-in düşməsi qadağandır. Bu fail-closed
davranış uninstall edilmiş plugin code-unun crash timing-i ilə yenidən trusted olmasının
qarşısını alır.

## Activation Verification

Installed olmaq activation üçün kifayət deyil. Hər post-login activation cycle-ı
package-i yenidən verify edir (`ADR-0008` § 2, `ADR-0032`, `ADR-0045`, `ADR-0059`):

- package completeness və `files` per-entry `sha256` dəyərləri installed layout
  üzərində yenidən hesablanır;
- manifest signature block-u, `purpose`, `algorithm`, `format` və string `keyId`
  trust store-a görə yenidən yoxlanılır; `keyId` seçimi exact match-dir və
  başqa purpose-un key-i ilə, naməlum/retired key ilə və ya artıq trusted olmayan
  vendor key-i ilə imzalanmış package reject olunur (`ADR-0062` § 4). Selector-un
  `plugin-<prod|test>-<YYYY-MM>` namespace formasında olması manifest v2 schema-sı
  tərəfindən package read zamanı, yəni signature verification-dan əvvəl tələb
  olunur (`ADR-0062` § 1); format uyğunluğu key-in trusted olduğunu sübut etmir və
  exact ring match-i əvəz etmir. Numeric
  `keyVersion` selector-u istifadə olunmur;
- `hostApiVersion` major uyğunsuzluğu və `minHostVersion` floor pozuntusu
  activation-ı reject edir; minor uyğunluq explicit backward-compatibility matrix
  ilə qiymətləndirilir.

Install-dan sonra plugin runtime file, manifest, `files` hash entry-si və ya
signature block-u dəyişdirilərsə plugin assembly **load olunmur**: verification
assembly load-dan və istənilən plugin code execution-dan əvvəl tamamlanır, plugin
typed non-raw reason ilə `Rejected` (dependency zəncirində isə `Skipped`) statusuna
düşür və `plugin.activation.rejected` audit event-i yazılır. Audit/diagnostics raw
key material, signature dəyəri, full local path və stack trace sızdırmır.

Activation verification cached install-time nəticəsinə güvənmir. Cached
signature/license summary yalnız UI status göstərişi üçündür və trust qərarı deyil.

## Runtime və Main Content Hosting

Plugin runtime yalnız uğurlu authentication-dan sonra işə düşür. `ADR-0061` strict
pre-auth allow-list-inə görə pre-auth mərhələdə plugin discovery, manifest scan,
package I/O və plugin metadata cache oxunuşu yoxdur; forbidden I/O edən service-in
**qurulması** da iş sayılır, yalnız metod çağırışını gecikdirmək kifayət deyil.
Plugin registry/metadata oxunuşu post-login scope-da başlayır (session əvvəlcə
commit olunur, sonra post-login scope yaradılır).

Plugin ekranları Dobby Main Content Area-da yalnız official `Dobby.Contracts` / `Dobby.SDK` page/extension contract-ları ilə host olunur. Plugin:

- host shell view model-inə toxunmur;
- WPF/internal implementation service almır;
- `IServiceProvider` və internal DI container görmür;
- host implementation project-lərinə direct reference saxlamır;
- route/navigation integration-ı declared descriptor/extension point ilə edir;
- UI-nı versioned declarative component tree ilə (localized text, form field,
  validation, command, table, list, status/progress, layout primitive) təsvir edir;
  WPF tipi və ya view model-ə embed edilmiş delegate qaytarmır (`ADR-0059`);
- action-ları id ilə göstərir; dispatch async host command boundary-də cancellation,
  authorization, license mode, timeout, correlation və result mapping ilə gedir;
- localization-ı plugin resource provider (plugin id/culture/key) ilə həll edir;
  resource key user-facing text kimi render olunmur;
- unsupported component/schema version page activation-dan əvvəl reject olunur və
  safe localized fallback göstərilir.

Lifecycle və package I/O asynchronous, cancellable və timeout-governed-dır; hər
instance üçün bir aktiv əməliyyat olur və cleanup deterministikdir (`ADR-0059`).

## Runtime Fault Isolation

Plugin screen, task və ya lifecycle hook unhandled fault verərsə:

- host app çökmür;
- həmin plugin instance/screen/task stopped və ya `Faulted` state-ə keçir;
- digər plugin-lər və shell işləməyə davam edir;
- user non-technical error görür;
- raw exception detail yalnız diagnostics-də saxlanılır;
- lifecycle/security baxımından əhəmiyyətli fault-lar audit trail-ə yazılır.

Hər runtime call üçün safe error mapping-dən sonra explicit **outcome origin**
saxlanılır: expected rejection, authorization/license denial, cancellation,
timeout, plugin-declared failure, thrown plugin exception, contract breach və ya
host infrastructure failure (`ADR-0059`). Bu ayrım sayəsində qaytarılan validation
error-u plugin-in atdığı argument exception-ı ilə qarışdırılmır.

Process-local quarantine policy yalnız timeout, plugin-declared failure, thrown
exception və contract-breach outcome-larını sayır. Tamamlanmış əməliyyat defect
streak-i sıfırlayır; expected və host failure-lar neytraldır. Bounded threshold-a
çatanda plugin-in route-ları, event subscription-ları, service-ləri, operation-ları
və work session-ları normal lifecycle cleanup boundary-si ilə çıxarılır.

Session-end zamanı post-login activation operation-ın cooperative cancellation-ı
runtime fault deyil: `plugin.runtime.faulted` audit event-i yaratmır, `Faulted`
state-ə keçirmir və P1-də ayrıca `cancelled` status əlavə etmir.

Fatal corrupted-state exception-lar `04_RESULT_VS_EXCEPTION_POLICY.md` qaydasına tabedir.

Host in-process plugin-lər üçün hostile-code containment iddia etmir:
`AssemblyLoadContext`, assembly-reference scan və contract convention-ları
compatibility/hygiene mexanizmləridir, sandbox deyil (`ADR-0059`). Fully trusted
olmayan package host process-də icra edilmir; untrusted/semi-trusted vendor
dəstəyi ayrıca təsdiqlənmiş out-of-process worker (OS-enforced identity, IPC,
resource və file/network boundary) tələb edir.

## Audit və Diagnostics

Minimum audit event-lər:

- `plugin.install.attempted`;
- `plugin.install.verified`;
- `plugin.install.rejected`;
- `plugin.install.signature-failed`;
- `plugin.install.trust-failed`;
- `plugin.enable`;
- `plugin.disable`;
- `plugin.uninstall`;
- `plugin.activation.skipped`;
- `plugin.activation.rejected`;
- `plugin.runtime.faulted`;
- `plugin.runtime.quarantined`.

Diagnostics raw technical detail daşıya bilər. Audit user/security/compliance yönümlü minimum payload saxlayır və `ADR-0019` boundary-sinə tabedir. Audit və exported diagnostics-ə raw manifest/package byte-ları, entry adlarının tam local path-ı, key material, signature dəyəri, exception message və stack trace düşmür (`NFR-026`).

`plugin.install.verified` audit qeydi installed-state promotion-u ilə eyni unit of
work-də commit olunur; commit uğursuz olarsa nə promotion, nə success audit qalır
(`ADR-0061`, `NFR-025`).

## Acceptance Hooks

- `AC-004` local file install və validation failure davranışını yoxlayır.
- `AC-005` dependency/compatibility/activation failure isolation davranışını yoxlayır.
- `AC-015` plugin runtime post-login və SDK/Contracts-only boundary-ni yoxlayır.
- `AC-031` plugin list, permission mapping, enable/disable və uninstall data choice davranışını yoxlayır.
- `AC-032` runtime fault isolation davranışını yoxlayır.
- `AC-049` installed package-in activation-time yenidən verification-ını və
  post-install tamper/rotated-key fixture-larının assembly load olmadan reject
  olunmasını yoxlayır.

## Branch Shared Plugin Storage Addendum

Branch Shared does not add plugin-visible deployment topology. Plugin manifests
do not receive `StorageScope`, provider, branch or database fields.

Host may place host-owned plugin shared key/value data in Branch Shared
PostgreSQL, but the plugin contract remains `SharedDomainStorage` /
`UserScopedStorage` with host-enforced authorization and single-key atomic
semantics. Plugin-owned relational schemas, plugin migrations, CAS/versioned
writes and cross-device plugin package delivery are Phase 2/future topics unless
a later ADR opens them.

## Verification Obligations

Bu spec-in tələbləri behavior-first sübutla bağlanır (`ADR-0061`, `NFR-027`).
Source-text/regex/fayl mövcudluğu yoxlaması və ya "test list green" iddiası
aşağıdakı maddələri qapatmır:

| Yoxlama | Minimum sübut |
|---|---|
| Archive limitləri | Flat ZIP bomb, nested/high-ratio compression, entry-count overflow, per-entry və aggregate byte overflow, integer overflow fixture-larının allocation-dan əvvəl reject olunması |
| Entry name normalization | Traversal, absolute/drive/UNC, alternate separator, trailing dot/space, ADS, `CON`/`NUL`/`COM1` və extension variantları, symlink/reparse, duplicate və case-collision fixture-ları |
| Containment | Staging root-dan kənara çıxan resolved full path və overwrite cəhdinin write anında rədd olunması |
| Signature profile | Yanlış `purpose`/`keyId`, license və ya recovery key-i ilə imzalanmış package, DER/padded base64 encoding variantlarının fail-closed olması |
| Fingerprint | `SHA-256(domain ‖ canonical-manifest-without-signature)` dəyərinin canonical `files` map-ini tam bir dəfə əhatə etdiyini göstərən test |
| `hostApiVersion` | Major mismatch-in activation-ı bloklaması və minor matrix davranışı |
| Activation re-verification | Install-dan sonra dəyişdirilmiş runtime file, manifest, `files` hash entry-si və signature block fixture-larının, həmçinin rotated/retired və cross-purpose key fixture-larının activation-da plugin assembly load olunmadan reject edilməsi |
| Crash consistency | Promotion/metadata-cache commit-i arasında fault injection; startup recovery-nin deterministik tamamlaması və ya rollback etməsi |
| Uninstall durability | `Pending uninstall` intent commit olunduqdan sonra simulated crash/restart-da plugin-in discovery-yə qaytarılmadığını, activation almadığını və deletion uğursuz olduqda restart-required state-in qaldığını göstərən test |
| Fault vs expected | `Forbidden`, `InvalidInput`, `LicenseReadOnly` və cancellation nəticələrinin quarantine counter-ini artırmadığını göstərən test |
| Quarantine | Bounded threshold-a çatanda route/subscription/service/operation/work-session cleanup-ının real icrası |
| Quarantine audit | Threshold-a çatan real plugin invocation ardıcıllığından sonra `plugin.runtime.quarantined` event-inin audit trail-ə dəqiq bir dəfə (`QuarantineStarted` kənarında) düşdüyünü, `pluginId`/`status`/`reasonCode`/`defectCount` field-lərini daşıdığını, raw plugin exception mətnini daşımadığını və lifecycle cleanup wired olmadıqda da yazıldığını göstərən test |
| Declarative UI | WPF tipi/delegate qaytarılmadığını, resource key-in user-facing render olunmadığını və unsupported component-in activation-dan əvvəl reject olunduğunu göstərən test |
| Pre-auth boundary | Pre-auth mərhələdə plugin discovery/manifest/package I/O və plugin metadata oxunuşunun baş vermədiyini runtime davranışı ilə göstərən test |

## Cross-references

- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — controlling trust/package/UI decision
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — license lifecycle (supersedes `ADR-0051`)
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — pre-auth allow-list, atomic promotion, behavior-first proof
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — `PluginRuntimeMode`
- `docs/schemas/dobby-plugin-manifest-v2.schema.json` — manifest v2 authority
- `docs/schemas/dobby-plugin-manifest.schema.json` — frozen v1; heç bir read path-da yüklənmir
- `docs/schemas/artifact-compatibility-matrix.md` — artifact/host compatibility
- `docs/specs/license-import.md` — `Plugins > License` import flow
- `docs/specs/sample-plugin.md` — v2 contract release verification artifact
- `docs/specs/audit-and-diagnostics.md` — audit action id-ləri və redaction boundary
- `docs/specs/backup-restore-migration.md` — plugin restore source rules

## Revision History

- **2026-08-27:** Status oxlarının qarışması aradan qaldırıldı: `ReadOnlyExpired`
  lifecycle vocabulary cədvəlindən çıxarıldı və license oxunun dəyəri kimi
  göstərildi, iki ox arasında normativ display precedence yazıldı (əvvəllər
  `Denied`/`Pending`/`Grace`/`NoLicense` üçün plugin card davranışı təyin edilməmişdi).
  Uninstall üçün atomic commit və `Pending uninstall` crash-safety/idempotent
  deletion qaydası əlavə edildi (əvvəlki mətn restart-a qədər intent-in durable
  qalmasını tələb etmirdi). `AC-049` Sources sətrinə əlavə olundu.
- **2026-08-25:** v2 reconciliation. Manifest authority v1 schema-dan
  `dobby-plugin-manifest-v2.schema.json`-a keçdi; streaming/bounded package
  validation, entry-name normalization, containment, canonical signature profile,
  `hostApiVersion`, crash-consistent promotion, outcome origin, process-local
  quarantine (`Quarantined` status) və declarative host-rendered UI qaydaları
  body-yə yazıldı; superseded pre-auth "cached metadata oxuna bilər" cümləsi
  `ADR-0061` strict allow-list-i ilə əvəz olundu; license mode referansı
  `ADR-0051` → `ADR-0060`; genişlənmiş reject vocabulary və behavior-first
  verification obligations əlavə edildi.
- **2026-08-26:** `AC-049` coverage gap-i bağlandı: `Activation Verification`
  bölməsi əlavə olundu (activation-time completeness/hash/signature/key
  purpose-version/trust re-verification, assembly load-dan əvvəl fail-closed,
  cached summary trust qərarı deyil), `AC-049` acceptance hook-u və activation
  re-verification sətri `Verification Obligations` cədvəlinə yazıldı.
