# ADR-0050: In-Memory Work Sessions

**Status:** Accepted
**Date:** 2026-04-25

## Context

Owner D-1 qərarı "browser kimi tab" istəyi kimi yox, canlı iş ekranının saxlanması
kimi dəqiqləşdirildi. Tipik axın: user vergi plugin-ində böyük Excel import işi
başladır, həmin ekranı minimize/park edir, başqa işə keçir və sonra eyni import
ekranına qayıdıb progress, validation error-lar, nəticə və completion state-i görür.

Mövcud shell scope `TopToolbar + Sidebar + Third Bar` modelini bağlamışdı, amma canlı
iş ekranı sessiyalarının lifecycle-ı canonical deyildi. `ADR-0041` login-first
startup qaydası isə pre-login mərhələdə background work, import/export və persistence
I/O başlamamasını tələb edir.

## Decision

Phase 1-də Dobby **process-lifetime in-memory work sessions** dəstəkləyir.

### 1. Work session anlayışı

Work session user-in başlatdığı canlı iş ekranıdır. Bu ekran:

- built-in və ya plugin-owned ola bilər;
- uzun iş, progress, validation state, input seçimi, result və error state daşıya bilər;
- minimize/park edilə bilər;
- user başqa ekrana keçdikdən sonra restore ediləndə eyni state-i göstərməlidir.

Bu qərar browser-style tab chrome tələb etmir. UI bunu tab, "Açıq işlər" siyahısı,
task strip və ya başqa shell surface ilə göstərə bilər. Canonical olan UI chrome deyil,
**minimize/restore edilə bilən live work screen behavior**-dur.

### 2. Minimize UX invariantları

- Work session silent auto-minimize edilmir; minimize explicit user action-dır.
- User yarımçıq live work screen-i minimize etmədən başqa bölməyə keçmək istəyəndə host
  xəbərdarlıq göstərir.
- Xəbərdarlıq minimum üç seçim verir: işi minimize et, həmin ekranda qal, işi bağla.
- Minimized work strip item-i modul/plugin adı, icon və qısa iş təsviri göstərir.
- Strip item click/activate həmin live screen-i process memory-də qaldığı state ilə
  restore edir.

### 3. Phase 1 persistence sərhədi

- Phase 1 work session state-i yalnız Dobby prosesi açıq olduğu müddətdə saxlanır.
- Dobby bağlandıqda və ya crash olduqda açıq work session-ların avtomatik restore-u
  Phase 1-də tələb olunmur.
- Restart sonrası workspace restore və persisted session recovery P2-dir.
- App hər açılışda login sonrası Home-dan başlayır; əvvəlki minimized work strip
  item-ları host tərəfindən geri açılmır.
- Pre-login mərhələdə work-session persistence I/O, restore, import/export start və
  background work kickoff qadağandır (`ADR-0041`).
- İşin completed/draft/domain datası work session state deyil. Plugin bu datanı öz
  domain storage qaydası ilə saxlayır və restartdan sonra ona baxış yolunu özü təmin
  edir.
- Logout active user session-u bitirdiyi üçün açıq work session varsa user-ə warning
  göstərilir; təsdiqdən sonra həmin session-lar controlled close/cancel edilir və
  in-memory user state təmizlənir (`ADR-0031`).
- Lock/manual lock/auto-lock work session-u avtomatik close etmir; lock screen current
  session-u qoruyur və unlock sonrası work session-lar process memory-də qaldığı yerdən
  görünə bilər.

### 4. Shell ownership

- Work session registry və minimize/restore orchestration `Dobby.Shell` məsuliyyətidir.
- Navigation mapping `Dobby.Navigation` altında qalır; actual screen hosting
  `Dobby.Shell` tərəfindən edilir.
- Long-running work progress/cancel/reporting üçün minimum integration
  `Dobby.Application / Background Tasks / Process Reporting` ilə bağlanır.
- Shell UI thread bloklanmamalıdır; uzun işlər host-un post-login orchestration və
  cancellation modelinə tabe olmalıdır.

### 5. Plugin boundary

- Plugin iş ekranı host implementation project-lərinə birbaşa bağlanmır.
- Plugin yalnız `Dobby.Contracts` / `Dobby.SDK` üzərindən work-screen surface təqdim
  edə bilər.
- Work session state host process memory-sində saxlanılır; plugin restart restore üçün
  host persistence detail-i görmür.
- Plugin işin real nəticəsini, draft-ını və ya domain record-unu saxlamaq istəyirsə,
  bunu öz `SharedDomainStorage` / `UserScopedStorage` qaydası ilə edir; host work
  session registry-si domain data store deyil.
- Plugin disable/uninstall zamanı həmin plugin-ə aid work session-lar host tərəfindən
  controlled şəkildə close/cancel edilir.

### 6. Rejected alternatives

- **Browser-style mandatory tabs:** rədd edildi — Owner tələbi tab chrome deyil, canlı iş
  ekranı sessiyasının minimize/restore davranışıdır.
- **Silent auto-minimize:** rədd edildi — yarımçıq işi gizlətmək əvəzinə user aydın
  seçim görməlidir.
- **Persisted workspace restore P1-də:** rədd edildi — restart sonrası restore storage,
  migration, plugin compatibility və partial-result recovery scope-u böyüdür.
- **Host work session registry-si domain data store olsun:** rədd edildi — host yalnız
  live screen orchestration saxlayır; satış/import/draft kimi real data plugin domain
  storage məsuliyyətidir.
- **Pre-login restore:** rədd edildi — login-first startup policy ilə ziddir.

## Consequences

- `Dobby.Shell / Shell Composition / Hosting` work session registry və restore
  orchestration daşıyır.
- Minimal background progress/reporting P1-core davranışa çevrilir; richer process
  reporting UI P1-if-time və ya P2-də dərinləşə bilər.
- Work session acceptance test-ləri browser tab UI yox, state-preserving
  minimize/restore və no-auto-minimize xəbərdarlıq davranışını yoxlayır.
- Restart restore ayrıca P2 ADR tələb edir.

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — Login sonrası shell və Phase 1 məhsul sərhədi
- `docs/02_ARCHITECTURE_MAP.md` — Row 63 `Background Tasks / Process Reporting`, Row 79 `Shell Composition / Hosting`, Cədvəl 3 `In-Memory Work Sessions`
- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-015`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-019`, `AC-028`
- `docs/specs/work-sessions.md`
- `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` — logout/lock session cleanup
- `docs/adr/ADR-0041-login-first-startup-policy.md` — pre-login forbidden workload
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` — shell navigation contribution model

## Branch Shared Availability Amendment

Branch Shared server unavailability does not silently discard process-lifetime
in-memory work session UI state. The shell may preserve visible in-memory state
so the user can retry or recover when authority returns.

Preserving in-memory UI state is not authorization. New shared commands,
mutations and authoritative reads fail closed until Branch Shared authority is
reachable and revalidated.
