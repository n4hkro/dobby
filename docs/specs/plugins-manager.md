# Plugins Manager Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-007`, `FR-008`, `FR-025`, `FR-026`, `AC-004`, `AC-005`, `AC-015`, `AC-031`, `AC-032`, `ADR-0001`, `ADR-0008`, `ADR-0009`, `ADR-0011`, `ADR-0012`, `ADR-0032`, `ADR-0036`, `ADR-0037`, `ADR-0045`, `ADR-0048`

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
| `ReadOnlyExpired` | License expired olsa da read-only mode-da köhnə data/render görünə bilər. |
| `Pending uninstall` | Activated plugin uninstall təsdiqlənib; plugin disabled/hidden qalır və restart runtime fayl deletion üçün required-dır. |

License runtime mode-ları `ADR-0051` və `ADR-0053` ilə idarə olunur; plugin raw license detail görmür.

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
3. Host package-i parse edir və `docs/schemas/dobby-plugin-manifest.schema.json`
   üzrə manifest schema validation aparır.
4. Package completeness, forbidden content, hash, signature və trust verification
   yoxlanılır; JSON Schema ilə ifadə olunmayan parent cycle, duplicate id,
   namespace ownership və trust-store lookup qaydaları ADR-0036/ADR-0045/ADR-0048
   əsasında procedural fail-closed validation kimi qalır.
5. Verification uğurludursa package installed state-ə keçir və metadata cache yenilənir.
6. Verification uğursuzdursa install dayanır; partial install qalmır.

Install validation user-facing typed səbəb göstərir:

- `invalid-manifest`;
- `signature-failed`;
- `trust-failed`;
- `forbidden-content`;
- `hash-mismatch`;
- `compatibility-mismatch`.

Bu list user-facing typed reason code vocabulary-sidir. Audit action ID-ləri
`docs/specs/audit-and-diagnostics.md` tərəfindən idarə olunur və
`plugin.install.signature-failed`, `plugin.install.trust-failed` kimi dotted
event names bu user-facing reason list-ə qaytarılmır.

Raw key material, full local path, stack trace və internal verifier detail UI-da göstərilmir və raw audit payload kimi yazılmır. Diagnostics redacted support-safe detail saxlayır; audit yalnız action id, status, typed reason və correlation id kimi audit-safe field-lərlə məhdudlaşır.

License denial install validation səbəbi deyil. Structurally valid, signed və trusted
package install oluna bilər; license state post-install runtime/license status
surface-ində `ADR-0051` və `ADR-0053` qaydaları ilə `Denied`, `ReadOnlyExpired` və ya
UI status kimi görünür.

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

## Runtime və Main Content Hosting

Plugin runtime yalnız uğurlu login-dən sonra real assembly/code execution edə bilər. Login ekranı görünənə qədər plugin discovery/manifest scan başlamır; login-visible pre-auth mərhələdə yalnız yüngül cached metadata oxuna bilər.

Plugin ekranları Dobby Main Content Area-da yalnız official `Dobby.Contracts` / `Dobby.SDK` page/extension contract-ları ilə host olunur. Plugin:

- host shell view model-inə toxunmur;
- WPF/internal implementation service almır;
- `IServiceProvider` və internal DI container görmür;
- host implementation project-lərinə direct reference saxlamır;
- route/navigation integration-ı declared descriptor/extension point ilə edir.

## Runtime Fault Isolation

Plugin screen, task və ya lifecycle hook unhandled fault verərsə:

- host app çökmür;
- həmin plugin instance/screen/task stopped və ya `Faulted` state-ə keçir;
- digər plugin-lər və shell işləməyə davam edir;
- user non-technical error görür;
- raw exception detail yalnız diagnostics-də saxlanılır;
- lifecycle/security baxımından əhəmiyyətli fault-lar audit trail-ə yazılır.

Session-end zamanı post-login activation operation-ın cooperative cancellation-ı
runtime fault deyil: `plugin.runtime.faulted` audit event-i yaratmır, `Faulted`
state-ə keçirmir və P1-də ayrıca `cancelled` status əlavə etmir.

Fatal corrupted-state exception-lar `04_RESULT_VS_EXCEPTION_POLICY.md` qaydasına tabedir.

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
- `plugin.runtime.faulted`.

Diagnostics raw technical detail daşıya bilər. Audit user/security/compliance yönümlü minimum payload saxlayır və `ADR-0019` boundary-sinə tabedir.

## Acceptance Hooks

- `AC-004` local file install və validation failure davranışını yoxlayır.
- `AC-005` dependency/compatibility/activation failure isolation davranışını yoxlayır.
- `AC-015` plugin runtime post-login və SDK/Contracts-only boundary-ni yoxlayır.
- `AC-031` plugin list, permission mapping, enable/disable və uninstall data choice davranışını yoxlayır.
- `AC-032` runtime fault isolation davranışını yoxlayır.

## Branch Shared Plugin Storage Addendum

Branch Shared does not add plugin-visible deployment topology. Plugin manifests
do not receive `StorageScope`, provider, branch or database fields.

Host may place host-owned plugin shared key/value data in Branch Shared
PostgreSQL, but the plugin contract remains `SharedDomainStorage` /
`UserScopedStorage` with host-enforced authorization and single-key atomic
semantics. Plugin-owned relational schemas, plugin migrations, CAS/versioned
writes and cross-device plugin package delivery are Phase 2/future topics unless
a later ADR opens them.
