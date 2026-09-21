# ADR-0008: Deferred Plugin Activation

**Status:** Accepted (license authority is `ADR-0060`; § 1 pre-auth construction/I-O boundary and § 2.1 cancellation semantics amended by `ADR-0061`, and § 2 activation-loop hook shapes, the compatibility gate and § 4 failure classification amended by `ADR-0059` — see ## Revision History)
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Row 32 `Deferred Plugin Activation` Phase 1-də P1-core statusda idi. `ADR-0041 Login-First Startup Policy` pre-login forbidden work siyahısına `plugin activation`-ı daxil edir, amma deferred activation model-i (nə zaman və necə aktivləşməsi) canonical olaraq bağlanmamışdı.

Master plan login-first startup sərhədini daha dəqiqləşdirir: login ekranı görünənə qədər plugin runtime, full plugin scan və manifest directory walk başlamamalıdır. Bu ADR ilk yazılanda login ekranı ilə credential submit arasında yüngül cached plugin metadata oxunuşuna icazə verirdi; `ADR-0061` § `Strict pre-auth boundary` sonradan pre-auth allow-list-i qapadıb həmin pəncərəni tamamilə bağladı (§ 1).

Açıq suallar:
- Plugin metadata (cached və ya fresh) nə zaman oxuna bilər?
- Fresh manifest discovery və plugin assembly yüklənməsi nə zaman baş verir?
- Plugin service-ləri (DI registration) nə zaman aktivləşir?
- Plugin activation fail olarsa, digər plugin-lərin activation-ı davam etməlidirmi?
- Plugin "lazy activation" (ilk istifadədə açılma) ilə "eager post-login activation" arasında hansı model seçilir?

## Decision

### 1. Pre-login plugin boundary

Login ekranı görünənə qədər plugin manifest scan, plugin assembly load və plugin runtime
activation başlamır.

Successful authentication-a qədər **heç bir** plugin işi -- fresh və ya cached -- icra
edilmir. `ADR-0061` § `Strict pre-auth boundary` qapalı allow-list qoyur və orada plugin
metadata oxunuşu yoxdur; həmin ADR "plugin state/cache/staging construction or recovery"
və "plugin discovery/verification/activation" işini açıq şəkildə post-auth commit
point-dən sonraya aid edir. Bu ADR-in əvvəlki mətni login ekranı ilə credential submit
arasında yüngül cached plugin metadata (plugin ID/version/display name, son enable/disable
statusu, cached signature/license summary, skeleton descriptor cache) oxumağa icazə
verirdi; həmin pəncərə `ADR-0061` ilə bağlanıb və o icazə artıq mövcud deyil.

Praktiki nəticə: plugin descriptor cache-ini oxuyan service login window-un first-paint
path-ində **construct edilmir**. `ADR-0061` "Construction of a service that performs
forbidden I/O counts as work; merely delaying its public method call is insufficient"
qaydasını qoyur, ona görə lazy field, `Lazy<T>` wrapper və ya sonraya qoyulmuş method
call kifayət etmir -- pre-auth object graph həmin service-i heç saxlamamalıdır.

Login ekranında plugin siyahısı, plugin sayı və ya plugin status summary göstərilmir;
bu məlumat post-login shell-in § 2 activation cycle-ından sonra ilk dəfə görünür. Login
UI-ın pre-auth-da oxumağa icazəsi olan yeganə persisted user məlumatı `ADR-0061` § 1
allow-list-indəki username-only remembered-login slot-udur (`ADR-0041` § 6, `ADR-0058`).


### 2. Post-login activation (explicit phase)

Login uğurlu olduqdan sonra `PostLoginPluginActivator` fresh discovery və activation cycle-ı başladır:

1. **Fresh manifest discovery**: plugin package/manifest descriptor-ları oxunur və cached metadata yenilənir
2. **Dependency graph sort** (`ADR-0009`): topological sırada plugin siyahısı hazırlanır
3. **Per-plugin activation loop** — hər plugin üçün host boundary-də async/cancellable icra olunur (`ADR-0059` § `Compatibility and lifecycle`):
   - Package completeness, hash, signature və trust verification (`ADR-0032`, `ADR-0045`, `ADR-0059`) — fail olarsa plugin activation-a buraxılmır və `trust-failed`, `signature-failed`, `hash-mismatch` və ya uyğun typed reason ilə `Rejected` / `Skipped` statusuna düşür
   - Compatibility check (`ADR-0037`, `ADR-0059`) — manifest `schemaVersion`, package format version, required `hostApiVersion` (`MAJOR.MINOR`) və `minHostVersion` table-driven compatibility matrix-i ilə qiymətləndirilir: `hostApiVersion` major mismatch activation-ı reject edir, host-un dəstəklədiyindən böyük minor reject olunur (minor uyğunluq explicit backward-compatibility matrisinə tabedir), `minHostVersion` isə product/build floor kimi qalır və API compatibility-nin əvəzi deyil; prerelease və ya build-metadata daşıyan plugin `version` dəyəri `invalid-manifest` ilə reject olunur (`ADR-0036`). Hər fail typed reason code ilə fail-closed-dur (`unsupported-manifest-version`, `unsupported-package-format-version`, `invalid-host-api-version`, `unsupported-host-api-major`, `unsupported-host-api-minor`, `invalid-minimum-host-version`, `host-version-below-minimum`, `invalid-manifest`) — unknown və ya parse olunmayan dəyər üçün permissive fallback yoxdur. Compatibility fail edən plugin özü `Rejected` olur; ondan asılı plugin-lər `ADR-0009` dependency semantics-i ilə `Skipped` olur.
   - License mode check (`ADR-0060`, `ADR-0053`) — `Denied` olarsa plugin skip olunur; `ReadOnlyExpired` olarsa plugin host enforcement gates ilə read-only mode-da açılır
   - Assembly load və module construction (`ADR-0007` plugin ALC)
   - Service registration (`ADR-0006`) iki fazalıdır: əvvəlcə cancellable **prepare** (validation + izolə edilmiş registration planı), plugin lifecycle uğurla bitəndən sonra **commit**. Prepare və ya lifecycle fail edərsə heç bir registration commit olunmur və hazırlanmış plan cleanup ilə atılır — yarı-registrasiya olunmuş plugin state qalmır.
   - Lifecycle activation (`ADR-0059`): module lifecycle hook-ları host boundary-də async/cancellable-dır və hər biri bounded timeout altında çağırılır — `IPluginModuleV2.InitializeAsync`, sonra `IPluginModuleV2.ActivateAsync`. Instance başına yalnız bir aktiv əməliyyat olur. Hook fail, timeout və ya cancel olarsa host deterministik cleanup icra edir (`DeactivateAsync` və `UnloadAsync`, öz bounded timeout-ları ilə) və uncommitted registration-lar geri alınır.
   - Yalnız `IPluginModule` implement edən legacy v1 module host boundary-də eyni bounded timeout və cancellation observation altında adapt olunur və typed `legacy-v1-adapter-upgrade-required` warning-i daşıyır. Bu backward-compatibility adapter-dir; yeni plugin-lər üçün governing shape async `IPluginModuleV2`-dir (`ADR-0059`, `ADR-0021`).

Lifecycle timeout-ları host-owned konfiqurasiyadır (initialize, activate, deactivate və
unload üçün ayrı dəyərlər), validated bounded aralıqda olur və plugin tərəfindən
oxunmur/dəyişdirilmir. Timeout aşılması § 4 mənasında plugin defect origin-idir.

Activation `IHostedService.StartAsync`-in post-login ekvivalentidir — `ADR-0041`-in `IHostedService baseline` qaydası burada pozulmur, çünki fresh discovery və activation yalnız post-login phase-ində baş verir.

### 2.1 Cancellation / Session-End Behavior

Logout, lock, session expiry və ya session disposal post-login activation zamanı
baş verərsə, activation operation mövcud Result/cancellation policy-yə uyğun
cooperatively cancel olunur. Host cancellation token-i hər lifecycle hook-a ötürür
(`ADR-0059`), ona görə cancellation plugin activation step-i başlamazdan əvvəl,
step-lər arasında və step qayıtdıqdan sonra müşahidə olunur. Cooperating hook token-i
müşahidə edib qayıtmalıdır; token-i ignore edən hook preemptively dayandırılmır — host
onu bounded timeout ilə kəsir və nəticə § 4 mənasında `timeout` origin-i ilə plugin
defect kimi təsnif olunur. Hər iki halda host deterministik cleanup icra edir və
uncommitted registration-ları commit etmir.

Plugin öz cari `Enabled` state-ində qalır. Persistent plugin status transition
yaranmır. Diagnostics cancellation-ı yaza bilər. Audit event yazılmır. Növbəti
post-login activation cycle normal qaydada retry edir.

Bu hal `plugin.runtime.faulted` deyil və P1-də `cancelled` plugin status-u əlavə
etmir.

Reopen only if product behavior requires a user-visible activation-cancelled state or
audit policy reclassifies cancellation events. Async/cancellable activation is no longer
a reopen trigger: `ADR-0059` § `Compatibility and lifecycle` made module registration and
activation asynchronous/cancellable at the host boundary, and § 2 above records the
resulting shape.

### 3. Lazy extension point resolution

Plugin activation edildikdən sonra belə, **extension point** (məs., plugin səhifəsi) ilk istifadə olunan anda (user navigation trigger) tam initialize edilə bilər. Bu performansı artırır amma Phase 1-də məcburi deyil — plugin author activation hook-larında (`InitializeAsync` / `ActivateAsync`) ağır iş etmirsə bu lazy pattern optional qalır. Activation hook-ları bounded timeout altında icra olunduğu üçün ağır işi activation-a yığmaq timeout defect-i riski yaradır (§ 4); uzun sürən iş host-owned background operation contract-ına (`ADR-0015`) və ya lazy extension point initialize-ə köçürülməlidir.

### 4. Per-plugin failure isolation

Bir plugin-in activation failure-ı **digər plugin-lərin activation-ını dayandırmır**. Skipped/failed plugin `ADR-0009` dependency semantics-inə uyğun işarələnir:

- **Dependency-dən asılı olan plugin-lər skipped** (transitive)
- **Digərləri normal aktivləşir**

Plugin status user-visible-dir (FR-008); failure reason typed-dir və heç bir halda raw
exception, stack və ya path mətni daşımır. Phase 1 reason vocabulary-si: trust/package
verification üçün `trust-failed`, `signature-failed`, `hash-mismatch`; compatibility üçün
§ 2-də sadalanan matrix reason code-ları; dependency üçün `missing-dependency`; license
üçün `runtime-denied`; lifecycle hook-ları üçün `lifecycle-initialize-<code>` /
`lifecycle-activate-<code>`; qalan gözlənilməyən hallar üçün `internal-error`.
`ReadOnlyExpired` failure reason deyil; plugin həmin runtime mode-da host enforcement
gates ilə read-only açılır.

Safe error mapping-dən sonra da hər nəticə explicit **outcome origin** saxlayır
(`ADR-0059` § `Compatibility and lifecycle`): expected rejection, authorization/license
denial, cancellation, timeout, plugin-declared failure, thrown plugin exception, contract
breach, host infrastructure failure. Bu ayrım məcburidir — plugin-in qaytardığı validation
error host infrastructure failure və ya plugin-in atdığı exception ilə eyni kateqoriyaya
yığılmır. Status origin-dən törəyir: plugin defect origin-ləri (timeout, plugin-declared
failure, thrown exception, contract breach) `Faulted`, qalan uyğunsuzluqlar `Rejected`
verir. `Forbidden`, `InvalidInput`, `LicenseReadOnly` və cancellation kimi expected
nəticələr plugin fault sayılmır.

Runtime-da activation sonrası plugin screen/task/lifecycle hook fault verərsə, bu
activation cycle-ın qlobal fail-fast hadisəsinə çevrilmir. Host həmin plugin
instance/screen/task sərhədini `Faulted` kimi işarələyir, user-a non-technical error
göstərir və digər müstəqil plugin-lərin işləməsini davam etdirir (`ADR-0012`).

Təkrarlanan plugin defect-ləri **process-local quarantine** ilə nəticələnir (`ADR-0059`):
yalnız yuxarıdakı dörd defect origin-i sayılır, tamamlanmış əməliyyat defect streak-ini
sıfırlayır, expected və host infrastructure failure-ları neytraldır. Bounded threshold-a
çatanda plugin-in route-ları, event subscription-ları, service-ləri, operation-ları və
work session-ları normal lifecycle cleanup boundary-si ilə çıxarılır və plugin bu proses
ömrü boyunca yeni command/navigation qəbul etmir. Quarantine state process-local və
in-memory-dir: persist olunmur, plugin-in `Enabled` state-ini dəyişmir və növbəti host
start-ının post-login activation cycle-ında sıfırdan başlayır — yəni permanent disable
deyil. Quarantine-in başlanğıcı audit-də bir dəfə `plugin.runtime.quarantined` event-i
ilə qeyd olunur (audit ownership `ADR-0019` / `ADR-0061`); event yalnız bounded field set
daşıyır (`pluginId`, `status`, `reasonCode`, `defectCount`, `diagnosticMarker`) və raw
detail sızdırmır.

### 5. No background hot-reload

Plugin install/enable/disable **restart tələb etmir** Phase 1-də, amma hot-reload eager background activation da yaratmır — yeni enable olunan plugin next activation cycle-a qədər gözləyir (və ya user explicit "restart application" edir). P2-də hot-activation model yenidən qiymətləndirilir.

### 6. Rejected alternatives

- **Eager pre-login activation:** rədd edildi — `ADR-0041` pozur, login UI blocking riski
- **Fresh manifest scan before login UI:** rədd edildi — full directory walk və manifest parse login-visible deadline-ını zəiflədir
- **Fail-fast activation (ilk plugin fail-ində bütün cycle dayansın):** rədd edildi — single-plugin problem user-i bütün platformdan məhrum edir
- **Manual activation (user explicit clicks "activate"):** rədd edildi — UX burden və user error potensialı; invariant "enabled plugin aktivdir" pozulur
- **Lazy activation exclusively (yalnız ilk istifadədə aktivləş):** rədd edildi — plugin extension point-ləri (məs., shell top-toolbar icon) activation ilk anda görünməlidir

## Consequences

- `Dobby.PluginFramework / Deferred Plugin Activation` (Row 32) `PostLoginPluginActivator` + post-login fresh discovery + post-login descriptor cache refresh olaraq ayrılır
- `Dobby.Startup / Startup Bootstrap` (Row 2) login UI görünənə qədər plugin discovery trigger etmir və pre-auth mərhələdə heç bir plugin metadata -- cached daxil olmaqla -- oxumur; plugin descriptor cache-i oxuyan service pre-auth object graph-da construct edilmir (`ADR-0061` § 1)
- Login flow-u uğurlu olduqdan sonra shell render-i activation tamamlanmasını gözləmir — shell immediate açılır, plugin extension point-ləri asenkron gəlir və UI uyğun "loading" göstərir
- Plugin failure audit entry-ləri `FR-011 plugin lifecycle` audit skopunda qeyd olunur
- Runtime fault-lar plugin boundary-də isolate edilir; raw exception detail UI-a
  çıxmır və diagnostics/audit boundary-də qalır
- NFR-003 startup ölçmələri `login submit → shell first paint` metric-ini plugin activation-dan ayrıca sayır (AC-014 thresholds isolated)
- Plugin activation order deterministikdir (`ADR-0009` dependency graph); testability qorunur

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` Row 32 `Deferred Plugin Activation`, Row 2 `Startup / Bootstrap`
- `docs/adr/ADR-0041-login-first-startup-policy.md` — pre-login forbidden work
- `docs/adr/ADR-0009-plugin-dependency-graph.md` — dependency sort və failure propagation
- `docs/adr/ADR-0037-plugin-compatibility-and-versioning.md` — compatibility check gate
- `docs/adr/ADR-0060-license-lifecycle-and-import-v3.md` — current license lifecycle authority (derived state, runtime mode gate)
- `docs/schemas/license-v3-lifecycle-truth-table.md` — derived state → runtime mode → UI status mapping
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — plugin runtime mode
- `docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md` — current plugin package validation, trust posture and async/cancellable lifecycle contract
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — strict pre-authentication runtime boundary; closes the former login-visible cached plugin-metadata window and is the current authority for § 1
- `docs/adr/ADR-0006-plugin-di-registration.md` — plugin service registration contract (§ 2 prepare/commit step)
- `docs/adr/ADR-0021-sdk-surface-area.md` — `IPluginModuleV2` / `PluginBaseV2` lifecycle surface exposed to plugin authors
- `docs/adr/ADR-0015-background-operation-reporting.md` — long-running iş üçün host-owned background operation contract-ı (§ 3)
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` — quarantine/fault audit vs diagnostics ownership (§ 4)
- `docs/adr/ADR-0007-plugin-load-context-strategy.md` — plugin ALC assembly load step
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — signature verification step
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — runtime fault isolation və capability boundary
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` NFR-003 — startup ölçmə isolation

## Revision History

- **2026-08-31:** Amendment record yazıldı və relation vocabulary düzəldildi.
  `**Status:**` sətri əvvəl "pre-auth construction/I-O boundary superseded by ADR-0061;
  plugin lifecycle hook shapes superseded by ADR-0059" deyirdi. `docs/adr/README.md`
  yalnız iki relation tanıyır: tam supersession (`**Supersedes:**`, orada adlanan ADR-in
  `Status`-u `Superseded` olmalıdır) və partial amendment (`**Amends:**`, amendee
  `Accepted` qalır və toxunulmamış bölmələri üçün authority olaraq qalır). "Accepted
  (portions superseded)" üçüncü, define olunmamış vocabulary idi və README-nin öz
  Supersession Index-i bu ADR-i `Accepted (partial)` kimi göstərirdi -- iki authority
  bir-birini təkzib edirdi. Bu ADR tam retire olunmayıb: deferred-activation timing modeli
  (pre-login scan/load/activation qadağası, bir explicit post-login phase, dependency-graph
  sırası, per-plugin failure isolation, P1-də hot-reload yoxluğu) hələ də bu sənədin
  authority-sindədir; `ADR-0059` yalnız hook shape-lərini, compatibility gate-i və failure
  classification-ı, `ADR-0061` isə § 1 pre-auth pəncərəsini və § 2.1 cancellation
  semantikasını amend edir. `**Status:**` amend wording-inə keçirildi, license pointer-i
  amendment clause-undan kənarda saxlanıldı (guard clause-u "amended" sözündən sonra parse
  edir, ona görə orada adlanan hər ADR reverse `**Amends:**` edge tələb edir) və
  `ADR-0059`/`ADR-0061` bu ADR-i öz `**Amends:**` header-lərində geri claim etdi -- əvvəl
  relation tək-istiqamətli idi.
- **2026-08-31:** § 1 tam əvəz olundu (`### 1. Login-visible cached metadata` →
  `### 1. Pre-login plugin boundary`). Retire olunan mətn login paint ilə credential
  submit arasında "yüngül cached plugin metadata" oxumağa icazə verirdi (plugin
  ID/version/display name, enable/disable status, cached signature/license summary,
  skeleton descriptor cache). `ADR-0061` § `Strict pre-auth boundary` allow-list-i
  qapalıdır və orada plugin metadata oxusu yoxdur; "plugin state/cache/staging
  construction or recovery" və "plugin discovery/verification/activation" post-auth-dır.
  Bu, sadəcə stale wording deyil, canlı pre-auth icazəsi idi: § 1-ə əsaslanaraq login
  pəncərəsi quran developer məhz qadağan olunmuş davranışı ship edərdi. Yeni mətn
  `ADR-0061`-in construction qaydasını da kodlaşdırır (qadağan olunmuş I-O edən
  service-in construct edilməsi işin özü sayılır, public metodun çağırışını gecikdirmək
  kifayət deyil), yəni `Lazy<T>` və ya deferred call bəs etmir — pre-auth object graph
  həmin service-i heç saxlamamalıdır. Dəyişiklik § Context premise-inə, open-question
  bullet-inə, iki § Consequences sətrinə (Row 32 / Row 2) və `ADR-0061`
  cross-reference sətrinə propagate olundu; `ADR-0036`-nın caching icazəsini bu ADR-ə
  delegate edən cümləsi də eyni səbəblə əvəzləndi.
- **2026-08-31:** §§ 2, 2.1, 3 və 4 `ADR-0059` § `Compatibility and lifecycle` shape-inə
  gətirildi. Əvvəl § 2 activation loop-u `IPluginModule.RegisterServices` invocation-ı və
  `IPluginModule.OnActivated()` hook-unu synchronous, unbounded host step-ləri kimi
  sadalayırdı; § 2.1 "artıq başlamış synchronous plugin hook" ifadəsini işlədirdi; § 3
  `OnActivated` hook-una istinad edirdi; § 4 isə typed failure reason set-ini outcome
  origin və quarantine modeli olmadan verirdi. Yeni mətn iki fazalı (prepare/commit)
  service registration-ı, async/cancellable `IPluginModuleV2.InitializeAsync` və
  `ActivateAsync` hook-larını, per-instance bir aktiv əməliyyat qaydasını, bounded
  host-owned timeout-ları, deterministik cleanup-ı (`DeactivateAsync`/`UnloadAsync`),
  `legacy-v1-adapter-upgrade-required` warning-i daşıyan v1 adapter-i, `hostApiVersion`
  `MAJOR.MINOR` compatibility gate-ini (major mismatch reject, host-dan böyük minor
  reject, `minHostVersion` yalnız product/build floor) və səkkiz üzvlü outcome origin
  taxonomiyası ilə bounded process-local quarantine-i yazır. § 2.1-in Reopen clause-unun
  birinci şərti (`async/cancellable activation`) `ADR-0059` ilə artıq ödənildiyi üçün
  clause-dan çıxarıldı və qalan iki canlı şərt saxlanıldı. Compatibility fail-inin
  statusu da dəqiqləşdirildi: fail edən plugin özü `Rejected` olur (əvvəl "skip olunur"
  yazılmışdı), ondan asılı plugin-lər isə `ADR-0009` semantics-i ilə `Skipped` olur.
