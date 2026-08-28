# ADR-0008: Deferred Plugin Activation

**Status:** Accepted
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Row 32 `Deferred Plugin Activation` Phase 1-də P1-core statusda idi. `ADR-0041 Login-First Startup Policy` pre-login forbidden work siyahısına `plugin activation`-ı daxil edir, amma deferred activation model-i (nə zaman və necə aktivləşməsi) canonical olaraq bağlanmamışdı.

Master plan login-first startup sərhədini daha dəqiqləşdirir: login ekranı görünənə qədər plugin runtime, full plugin scan və manifest directory walk başlamamalıdır. Login ekranı görünəndən sonra, amma authenticated session yaranmamışdan əvvəl yalnız təhlükəsiz, yüngül və non-blocking cached plugin metadata oxuna bilər.

Açıq suallar:
- Plugin cached metadata nə zaman oxuna bilər?
- Fresh manifest discovery və plugin assembly yüklənməsi nə zaman baş verir?
- Plugin service-ləri (DI registration) nə zaman aktivləşir?
- Plugin activation fail olarsa, digər plugin-lərin activation-ı davam etməlidirmi?
- Plugin "lazy activation" (ilk istifadədə açılma) ilə "eager post-login activation" arasında hansı model seçilir?

## Decision

### 1. Login-visible cached metadata

Login ekranı görünənə qədər plugin manifest scan, plugin assembly load və plugin runtime activation başlamır.

Login ekranı artıq görünəndən sonra, amma user credential submit etməmişdən əvvəl host yalnız əvvəlcədən saxlanmış yüngül cached plugin metadata oxuya bilər. Bu metadata aşağıdakı kimi təhlükəsiz summary məlumatla məhdud qalmalıdır:

- Plugin ID, version və display name
- son məlum enable/disable statusu
- son məlum cached signature/license summary statusu
- shell-də loading/skeleton göstərmək üçün lazım olan minimal descriptor cache

Bu mərhələdə fresh `manifest.json` directory walk, signature verification, license resolution, assembly load və plugin code execution qadağandır. Cached metadata oxunuşu login UI və credential submit-i bloklamamalıdır; tamamlanması login submit-dən əvvəl zəmanət verilmir.

### 2. Post-login activation (explicit phase)

Login uğurlu olduqdan sonra `PostLoginPluginActivator` fresh discovery və activation cycle-ı başladır:

1. **Fresh manifest discovery**: plugin package/manifest descriptor-ları oxunur və cached metadata yenilənir
2. **Dependency graph sort** (`ADR-0009`): topological sırada plugin siyahısı hazırlanır
3. **Per-plugin activation loop**:
   - Package completeness, hash, signature və trust verification (`ADR-0032`, `ADR-0045`) — fail olarsa plugin activation-a buraxılmır və `trust-failed`, `signature-failed`, `hash-mismatch` və ya uyğun typed reason ilə `Rejected` / `Skipped` statusuna düşür
   - Compatibility check (`ADR-0037`) — fail olarsa plugin skip olunur
   - License mode check (`ADR-0051`, `ADR-0053`) — `Denied` olarsa plugin skip olunur; `ReadOnlyExpired` olarsa plugin host enforcement gates ilə read-only mode-da açılır
   - Assembly load (`ADR-0007` plugin ALC)
   - `IPluginModule.RegisterServices` invocation (`ADR-0006`)
   - Lifecycle hook: `IPluginModule.OnActivated()`

Activation `IHostedService.StartAsync`-in post-login ekvivalentidir — `ADR-0041`-in `IHostedService baseline` qaydası burada pozulmur, çünki fresh discovery və activation yalnız post-login phase-ində baş verir.

### 2.1 Cancellation / Session-End Behavior

Logout, lock, session expiry və ya session disposal post-login activation zamanı
baş verərsə, activation operation mövcud Result/cancellation policy-yə uyğun
cooperatively cancel olunur. Cancellation plugin activation step-i başlamazdan
əvvəl, step-lər arasında və ya step qayıtdıqdan sonra müşahidə olunur; artıq
başlamış synchronous plugin hook preemptively dayandırılmır.

Plugin öz cari `Enabled` state-ində qalır. Persistent plugin status transition
yaranmır. Diagnostics cancellation-ı yaza bilər. Audit event yazılmır. Növbəti
post-login activation cycle normal qaydada retry edir.

Bu hal `plugin.runtime.faulted` deyil və P1-də `cancelled` plugin status-u əlavə
etmir.

Reopen only if a required plugin needs async/cancellable activation that cannot
be background-deferred, product behavior requires a user-visible
activation-cancelled state, or audit policy reclassifies cancellation events.

### 3. Lazy extension point resolution

Plugin activation edildikdən sonra belə, **extension point** (məs., plugin səhifəsi) ilk istifadə olunan anda (user navigation trigger) tam initialize edilə bilər. Bu performansı artırır amma Phase 1-də məcburi deyil — plugin author `OnActivated` hook-da ağır iş etmirsə bu lazy pattern optional qalır.

### 4. Per-plugin failure isolation

Bir plugin-in activation failure-ı **digər plugin-lərin activation-ını dayandırmır**. Skipped/failed plugin `ADR-0009` dependency semantics-inə uyğun işarələnir:

- **Dependency-dən asılı olan plugin-lər skipped** (transitive)
- **Digərləri normal aktivləşir**

Plugin status user-visible-dir (FR-008); failure reason typed-dir (`trust-failed`,
`runtime-denied`, `compatibility-mismatch`, `missing-dependency`, `internal-error`).
`ReadOnlyExpired` failure reason deyil; plugin həmin runtime mode-da host enforcement
gates ilə read-only açılır.

Runtime-da activation sonrası plugin screen/task/lifecycle hook fault verərsə, bu
activation cycle-ın qlobal fail-fast hadisəsinə çevrilmir. Host həmin plugin
instance/screen/task sərhədini `Faulted` kimi işarələyir, user-a non-technical error
göstərir və digər müstəqil plugin-lərin işləməsini davam etdirir (`ADR-0012`).

### 5. No background hot-reload

Plugin install/enable/disable **restart tələb etmir** Phase 1-də, amma hot-reload eager background activation da yaratmır — yeni enable olunan plugin next activation cycle-a qədər gözləyir (və ya user explicit "restart application" edir). P2-də hot-activation model yenidən qiymətləndirilir.

### 6. Rejected alternatives

- **Eager pre-login activation:** rədd edildi — `ADR-0041` pozur, login UI blocking riski
- **Fresh manifest scan before login UI:** rədd edildi — full directory walk və manifest parse login-visible deadline-ını zəiflədir
- **Fail-fast activation (ilk plugin fail-ində bütün cycle dayansın):** rədd edildi — single-plugin problem user-i bütün platformdan məhrum edir
- **Manual activation (user explicit clicks "activate"):** rədd edildi — UX burden və user error potensialı; invariant "enabled plugin aktivdir" pozulur
- **Lazy activation exclusively (yalnız ilk istifadədə aktivləş):** rədd edildi — plugin extension point-ləri (məs., shell top-toolbar icon) activation ilk anda görünməlidir

## Consequences

- `Dobby.PluginFramework / Deferred Plugin Activation` (Row 32) `PostLoginPluginActivator` + cached metadata + fresh post-login discovery olaraq ayrılır
- `Dobby.Startup / Startup Bootstrap` (Row 2) login UI görünənə qədər plugin discovery trigger etmir; login-visible pre-auth mərhələdə yalnız cached metadata oxuna bilər
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
- `docs/adr/ADR-0051-license-lifecycle-v2.md` — license state machine (superseded by `ADR-0060`; state names preserved as derived states)
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md` — plugin runtime mode
- `docs/adr/ADR-0006-plugin-di-registration.md` — `IPluginModule.RegisterServices` və `OnActivated` activation steps
- `docs/adr/ADR-0007-plugin-load-context-strategy.md` — plugin ALC assembly load step
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — signature verification step
- `docs/adr/ADR-0012-plugin-capability-boundary.md` — runtime fault isolation və capability boundary
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` NFR-003 — startup ölçmə isolation
