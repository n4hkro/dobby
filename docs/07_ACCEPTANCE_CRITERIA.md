# Dobby Acceptance Criteria

**Tarix:** 2026-04-24
**Status:** Təsdiqlənib
**Scope:** Phase 1 üçün yoxlanıla bilən acceptance criteria və release blocker check-ləri

Bu sənəd Phase 1 acceptance yoxlamalarını qısa və test edilə bilən formada saxlayır. Məhsul və arxitektura izahı burada təkrarlanmır; yalnız observable release criteria verilir.

Acceptance item yalnız current implementation stage-də mövcud artifact-larla verify oluna bildikdə valid sayılır. Future CLI, schema, package, admin tool və ya başqa hələ açılmamış iş evidence kimi istifadə edilə bilməz.

## Acceptance Stage Tags

Acceptance items are interpreted by stage:

- `initial-skeleton`: minimum evidence expected from the first solution skeleton.
- `P1-release-gate`: evidence required before a P1 release candidate can pass.
- `non-blocking-P1`: useful P1 checks that do not block initial skeleton.
- `P2/future`: preserved future scope, not P1 skeleton work.
- `Branch-Shared/Tier-B`: preserved Branch Shared design or customer-release readiness, not initial P1 skeleton work.

Stage matrix:

| Acceptance item(s) | Stage interpretation |
|---|---|
| Release blocker table (`AC-001`-`AC-010`, `AC-014`-`AC-052`) | `P1-release-gate` unless an item-specific note below splits skeleton evidence from release evidence. |
| `AC-014` | `P1-release-gate`; ADR-0042 baseline hardware startup timing is not an initial-skeleton blocker. |
| `AC-051` | Split: KDF selection, minimum floors, salt requirements and fail-closed floor enforcement are `initial-skeleton`; ADR-0042 baseline-latency evidence used to justify PBKDF2 fallback is `P1-release-gate`. |
| Non-blocking Phase 1 checks (`AC-011`-`AC-013`) | `non-blocking-P1`. |
| Branch Shared Documentation Acceptance Notes | `Branch-Shared/Tier-B`; documentation acceptance only, not initial P1 skeleton runtime work. |

Future/P2 references inside an AC remain out of initial skeleton unless a
human-approved scope change moves them. Reopen only when a human-approved scope
change moves an acceptance item between stages.

## Release blocker acceptance

| ID | Acceptance criteria | Type | Source |
|---|---|---|---|
| AC-001 | Tətbiq açıldıqda login UI görünənə qədər yalnız son theme snapshot-u, DPAPI CurrentUser ilə qorunan remember-me startup snapshot-u və login pəncərəsinin renderi üçün minimum theme/localization məlumatı oxunur; credential store, audit storage, plugin runtime/activation, full plugin scan, online check, update check, import/export, heavy migration və license-heavy verification başlamır. | integration | `ADR-0041`, `ADR-0008`, `NFR-002` |
| AC-002 | Sistemdə heç bir user yoxdursa, ilk uğurlu login edən hesab `Owner` kimi yaradılır və sonrakı login-lərdə eyni bootstrap təkrarlanmır. | integration | `FR-002` |
| AC-003 | Uğurlu login-dən sonra shell `Home`, `Plugins`, `Settings` built-in-ləri ilə açılır; TopToolbar solda Dobby logo/ad, mərkəzdə icon/ad navigation, sağda tək theme switch və profile button göstərir. Sidebar geniş/yığcam rejimləri, Third Bar Main Content Area-nın yuxarı-mərkəzindəki yumşaq panel kəsiyi slotunda, Main Content Area, bottom status/progress sətri və minimized active work strip snapshot-da görünür. Shell snapshot-ları minimum `1024×680` və baseline `1920×1080` resolution-larında 100% DPI altında yoxlanılır; əlavə olaraq 125% və 150% DPI manual smoke test baseline `1920×1080` resolution-da release blocker kimi yoxlanılır. 175%/200% DPI və `PerMonitorV2` advanced polish P2/P3-dədir. | integration + snapshot + manual | `FR-005` |
| AC-004 | `Plugins` bölməsində local file picker ilə signed `.dobbyplugin` seçiləndə trusted package uğurla install olunur; unsigned, tampered, untrusted, forbidden content və ya invalid manifest package install olunmur və istifadəçiyə raw signature/key/path detail sızdırmayan typed səbəb göstərilir. | integration | `FR-007`, `NFR-004`, `NFR-014` |
| AC-005 | Missing dependency, circular dependency, compatibility failure və activation failure hallarında plugin activation-a buraxılmır və `Skipped`/`Rejected` kimi statuslanır; runtime fault hallarında plugin `Faulted` vəziyyətə keçir. Hər iki halda host prosesi və digər müstəqil plugin-lər işləməyə davam edir. | integration | `FR-008`, `NFR-009` |
| AC-006 | License `FullAccess` olduqda plugin normal işləyir; `grace` vəziyyətində plugin işləyir və istifadəçiyə xəbərdarlıq/audit warning yaranır; `ReadOnlyExpired` vəziyyətində köhnə nəticələr görünür, amma mutating/data-extraction əməliyyatları host tərəfindən deny olunur və `.dobbylicense` host import flow-u permission yoxlaması ilə mümkün qalır; `Denied` vəziyyətində plugin runtime access almır. Plugin heç bir halda raw artifact detail, expiry, grace, revision və binding detail almır. | integration | `FR-009`, `FR-017`, `NFR-005`, `NFR-016`, `ADR-0051`, `ADR-0053` |
| AC-007 | `Owner` recovery flow `Export Recovery Request` ilə `.dobbyrecoveryrequest` faylı yaradır, vendor Admin Tool həmin request-dən ayrıca user seçmədən signed `.dobbyrecovery` response yaradır və `Import Recovery Response` həmin response-u yalnız uyğun cihazda, məhdud müddətdə və bir dəfə istifadə olunan artefakt kimi qəbul edir. Request/response içində `targetUserId` və `targetUsername` kimi managed-user target field-ləri olmur; managed user password reset recovery flow-a düşmür. | integration + manual | `FR-004` |
| AC-008 | Core runtime internet bağlantısı olmadan login, shell və lokal plugin-host foundation davranışlarını saxlayır. | integration + manual | `FR-001`, `NFR-001` |
| AC-009 | Plugin boundary-də atılan expected/domain/infrastructure failure-lar `Result<T>` və well-known error code-larla görünür; raw plugin exception istifadəçiyə və cross-project contract-a sızmır. | unit + integration | `NFR-006` |
| AC-010 | Aşağıdakı əməliyyatlar audit trail-də görünür: login/logout, permission grant/revoke, password change, user create/update/password reset/activation/deactivation, plugin install/enable/disable/uninstall, plugin activation skipped/rejected və lifecycle/security baxımından əhəmiyyətli runtime fault-lar, recovery import/export, license import/enforcement, security verification failure və Owner-protection violation cəhdləri. Exact audit action ID-ləri `docs/specs/audit-and-diagnostics.md` tərəfindən idarə olunur. | integration | `FR-011`, `NFR-011` |
| AC-014 | Release build instrumented startup ölçmələri `ADR-0042` baseline hardware-də aşağıdakı thresholds-ı göstərir: cold start pre-login duration ≤ `3000ms` P95, warm start pre-login duration ≤ `1000ms` P95, login submit → shell first paint ≤ `800ms` P95. Baseline build-dən > `20%` regression hard fail sayılır. Ölçmələr pre-login, login-visible pre-auth və post-login mərhələ sərhədlərini ayrıca qeyd edir; plugin activation shell first paint metric-ini bloklamır. | integration + manual | `NFR-003`, `ADR-0041`, `ADR-0008`, `ADR-0042` |
| AC-015 | Plugin runtime login success-dən əvvəl başlamır; login success-dən sonra plugin runtime yalnız `Dobby.Contracts` və `Dobby.SDK` public surface-lərini görür, host implementation project-lərinə birbaşa referans (reflection və ya dynamic load daxil) bloklanır. Plugin ekranı Main Content Area-da yalnız official page/extension contract vasitəsilə host olunur və plugin internal WPF/host implementation detail-lərinə toxuna bilmir. Plugin-host boundary contract test-ləri bu surface-in qapalı siyahısını yoxlayır. | contract + integration | `NFR-008`, `ADR-0001`, `ADR-0008`, `ADR-0012` |
| AC-016 | Shell built-in ekranları (`Home`, `Plugins`, `Settings`) minimum theme foundation-dan (design tokens, spacing scale, base typography) istifadə edir; hardcoded color/spacing/font qiymətləri shell səviyyəsində tapılmır. Light/dark theme mövcuddur, TopToolbar tək theme switch ilə dəyişir, sidebar collapse/window position/navigation ordering state user preference kimi saxlanır və startup yenə Home-dan başlayır. | snapshot + manual | `FR-010`, `NFR-010`, `02_ARCHITECTURE_MAP.md` Row 72/73 |
| AC-017 | Shell və built-in UI-da user-facing hardcoded string scan release gate-də fail verir; bütün visible strings localization key üzərindən gəlir. P1 default culture `az-AZ`-dir, missing key development-də görünən olur, production-da fallback + `localization.missing-key` diagnostics yaranır və audit-ə yazılmır. | integration + static-scan | `FR-013`, `NFR-013` |
| AC-018 | Signed `.dobbyplugin` package install olunur; package içində `.dobbylicense`, customer-specific license payload, customer data, device fingerprint, vendor private key, host internal config və host persistence migration script yoxdur. `.dobbylicense` ayrıca vendor-signed detached artifact kimi import edilir və package binding raw zip/container bytes-a yox, canonical signed content fingerprint-ə bağlanır. Package reject edir: unsigned extra file, manifest/package missing entry, hash mismatch, duplicate/case-collision, symlink, forbidden path, raw container metadata fingerprint dependency. | integration | `FR-014`, `NFR-014` |
| AC-019 | User böyük Excel import və ya satış tipli işi plugin work session kimi başladır, explicit minimize action ilə live screen-i park edir və minimized work strip-də modul/plugin adı, icon və qısa iş təsviri görünür; restore etdikdə progress/result/error/input state-i itmədən qayıdır. User yarımçıq work screen-i minimize etmədən başqa bölməyə keçmək istəyəndə silent auto-minimize baş vermir; modal/banner xəbərdarlığı minimize et, qal və ya bağla seçimləri verir. Dobby restartından sonra host həmin work session-u strip-də restore etmir və pre-login work-session persistence I/O yoxdur; həmin ana qədər plugin tərəfindən saxlanmış real/draft/domain data plugin-in öz ekranlarından görünə bilər. | integration + snapshot | `FR-015`, `ADR-0050`, `ADR-0041`, `docs/specs/work-sessions.md` |
| AC-020 | Customer/deployment/device/package mismatch license plugin activation/read access-i non-disclosing deny edir və audit/diagnostics-də detallı host-side səbəb saxlanılır. UI və plugin mismatch-in hansı binding sahəsindən gəldiyini görmür. | integration | `FR-016`, `NFR-015`, `ADR-0051`, `ADR-0052` |
| AC-021 | Expired plugin `ReadOnlyExpired` mode-da açılır, history/on-screen render görünür; structured export, report file output, print-to-PDF, print-to-file, mutation operations, plugin/domain import və background task start deny olunur. `.dobbylicense` import expired vəziyyətdən çıxmaq üçün host-level flow kimi mümkün qalır və ayrıca permission yoxlamasından keçir. Print classification test double physical target allow, virtual/unknown target deny davranışını yoxlayır. | integration | `FR-017`, `NFR-016`, `ADR-0051`, `ADR-0053` |
| AC-022 | Permission registry-də ən azı bir built-in target və validated signed manifest `operationTargets` metadata-sından gələn bir plugin-declared target görünür; hər target yalnız dəstəklədiyi canonical verb toggle-larını göstərir və explicit deny toggle-u olmur. `Owner` managed user-ə yalnız `view` grant verdikdə user həmin target-i görə bilir, amma `create`, `update`, `delete`, `execute`, `import`, `export`, `print`, `configure` və `grant` əməliyyatları non-disclosing deny olur və audit entry yaranır. Unknown target, unknown verb və plugin target-inin manifestdə elan etmədiyi verb üçün authorization fail-closed deny edir, user/plugin-facing reason non-disclosing qalır və audit warning yaranır. `grant` icazəsi olan managed user `Owner`-ə permission/status/data dəyişikliyi edə bilmir, özünə permission əlavə edə bilmir və özündə olmayan permission-u başqasına grant edə bilmir. | integration | `FR-003`, `FR-018`, `NFR-017`, `ADR-0036`, `ADR-0048` |
| AC-023 | `Settings > Settings 2 > Settings 2.1` navigation descriptor əlavə ediləndə shell layout code dəyişmədən TopToolbar/Sidebar/ThirdBar-da düzgün render olunur. Aktiv plugin top-level contribution-u TopToolbar-da, onun child-ları Sidebar-da, seçilmiş child-ın daha dərin child-ları Third Bar-da görünür; Sidebar/Third Bar drag-drop ordering per-user saxlanır, default reset işləyir, Home birinci və Settings sonuncu/fixed qalır. | snapshot + integration | `FR-019` |
| AC-024 | MSIX install/uninstall və `v(N) → v(N+1)` upgrade clean işləyir; upgrade host data, audit log, license cache, users/permissions və user preferences-i silmir. Portable artifact varsa support/dev build kimi yoxlanır və release blocker sayılmır. | manual | `ADR-0047`, `NFR-024` |
| AC-025 | Remember-me aktiv olan son remembered user üçün tətbiq yenidən açıldıqda username və masked password hazır görünür, auto-login baş vermir və stored password üçün reveal button görünmür. Password field edit ediləndə hazır password cari formdan təmizlənir, uğurlu login isə saved slot-u remember-me ON olduqda yeni user/credential ilə replace edir, OFF olduqda boşaldır. | integration + manual | `FR-021`, `ADR-0038` |
| AC-026 | Login səhvləri ardıcıl simulyasiya ediləndə 1-ci və 2-ci səhvdə login button aktiv qalır, 3-cü səhvdən sonra 10 saniyə, 4-cü səhvdən sonra 30 saniyə, 5-ci və sonrakı səhvlərdən sonra 60 saniyə countdown göstərilir və login button disabled olur. Countdown aktivkən username dəyişmək cəzanı sıfırlamır; uğurlu login failed-attempt state-i təmizləyir. | integration | `FR-022`, `ADR-0038` |
| AC-027 | Shell və built-in ekranlar keyboard-only navigation, görünən focus indicator, minimum contrast və əsas control-lar üçün accessibility role/label mapping baseline-ını saxlayır; full WCAG/screen reader polish P3-ə saxlanılsa da P1-də əsas shell workflow-ları mouse tələb etməməlidir. | integration + manual | `NFR-019` |
| AC-028 | User A ilə login edilib work session açıldıqdan sonra logout çağırılanda active work xəbərdarlığı göstərilir; təsdiqdən sonra login ekranı görünür, User A identity/session material/cached permission decision/in-memory plugin service state-i istifadə edilmir və User B yalnız yeni uğurlu login-dən sonra fresh session alır. Remember-me aktivdirsə logout-dan sonra saved masked login qalır; slot növbəti uğurlu login zamanı remember-me OFF seçildikdə boşaldılır. | integration | `FR-023`, `ADR-0031`, `ADR-0050` |
| AC-029 | Current user profile/account səthindən password dəyişəndə cari password və eyni new-password confirmation tələb olunur; yanlış proof non-disclosing deny və durable throttle yaradır. Uğurlu atomik commit cari session-u açıq saxlayır, başqa active session-ları invalid edir və növbəti login yalnız yeni password ilə uğurlu olur; injected write/commit failure köhnə credential/session/audit vəziyyətini saxlayır. Manual lock və auto-lock eyni lock screen-i açır; lock screen cari user password-u ilə unlock edir və ya logout button-u ilə login ekranına qayıdır, amma həmin ekranda başqa user login flow-u göstərilmir. Auto-lock default passivdir və aktivləşdirildikdə minute timeout dəyəri ilə işləyir. | integration + manual | `FR-024`, `ADR-0031`, `ADR-0058`, `ADR-0061` |
| AC-030 | Owner yeni managed user yarada, məlumatını dəyişə, password reset edə, deactivate/reactivate edə və permission verə bilir. Uyğun permission verilmiş managed user yalnız həmin əməliyyatları icra edə bilir; Owner-i deactivate edə, Owner məlumat/status/permission-larını dəyişə, özünə permission əlavə edə və ya özündə olmayan permission-u başqasına grant edə bilmir. Managed user password reset recovery flow-a düşmür və User Management audit entry yaradır. | integration | `FR-003`, `FR-018`, `ADR-0048` |
| AC-031 | `Plugins` list plugin name, version, status və short description göstərir. `import` permission olmayan user install edə bilmir; `configure` permission olmayan user enable/disable edə bilmir; `delete` permission olmayan user uninstall edə bilmir. Disable edilən plugin navigation-dan və runtime activation-dan çıxır. Uninstall təsdiq modalı göstərir və istifadəçi data saxla/sil seçimi etmədən uninstall davam etmir. Cari process-də aktivləşdirilmiş plugin uninstall ediləndə plugin `Pending uninstall` state-inə keçir, dərhal disabled/hidden olur, navigation/runtime activation-dan çıxır, runtime faylları növbəti app start-da fresh discovery-dən əvvəl silinir və restart tamamlanana qədər list restart-required state göstərir. Heç vaxt aktivləşdirilməmiş plugin uninstall flow-u runtime fayllarını host nəzarəti ilə dərhal silə bilər. | integration + snapshot | `FR-025`, `FR-018`, `ADR-0007`, `ADR-0048`, `docs/specs/plugins-manager.md` |
| AC-032 | Runtime-da plugin screen/task unhandled fault verdikdə host app çökmür, həmin plugin instance/screen/task faulted/stopped vəziyyətə keçir, başqa plugin-lər və shell işləməyə davam edir, user non-technical error görür, raw exception detail diagnostics-only qalır və audit-də yalnız lifecycle/security-significant fault event + typed reason/correlation saxlanılır. | integration | `FR-026`, `NFR-006`, `NFR-008`, `ADR-0012` |
| AC-033 | Fresh install/default config ilə Dobby usage analytics, crash report, telemetry və diagnostics-i vendor-a avtomatik göndərmir. User/Owner explicit diagnostics export action-ı işlədildikdə local support package yaranır; package raw password/token/private key/session material/remember-me secret/raw license artifact detail daşımır və export action audit trail-də görünür. | integration + manual | `FR-027`, `NFR-020`, `ADR-0019` |
| AC-034 | `Plugins > License` daxilində `import` permission-u olan user `.dobbylicense` seçəndə host signature, customer/deployment/device/package binding və license revision yoxlamalarını aparır, valid license-i saxlayır, plugin statusunu yeniləyir və audit entry yaradır. Permission olmayan user import edə bilmir; `.entitlement` və ya yanlış extension seçimi, invalid signature, binding mismatch, package fingerprint mismatch və older revision non-disclosing typed nəticə ilə reject olunur. `ReadOnlyExpired` vəziyyətində plugin/domain import qapalı qalsa da bu host-level license import flow-u işləyir. Plugin Details `Import license` shortcut-u varsa eyni flow-a gedir; About import yeri kimi çıxmır. | integration + manual | `FR-028`, `FR-016`, `FR-017`, `FR-018`, `NFR-015`, `NFR-016`, `ADR-0051`, `ADR-0052`, `ADR-0053`, `ADR-0048` |
| AC-035 | Settings açıldıqda minimum `Appearance`, `Account`, `Security / Session`, `User Management` və `About` category-ləri görünür. TopToolbar theme switch və `Settings > Appearance` eyni theme preference-i dəyişir; auto-lock default passive olur və aktivləşdirildikdə minute input qəbul edir; User Management entry-si permission guard-a tabedir; About app version/build/device code/plugin versions/support/legal fields göstərir, license import/status göstərmir və managed user Owner identity-ni yalnız neutral `About.OwnerStatus.Registered` resource key-i ilə görür. | integration + snapshot + manual | `FR-029`, `FR-010`, `FR-024`, `FR-028`, `docs/specs/settings-and-about.md` |
| AC-036 | Sample/test plugin test artifact kimi signed `.dobbyplugin` formatında install olunur, manifest `operationTargets` metadata-sında `com.dobby.sample.greet` + `execute` + `labelKey` elan edir, plugin list-də generic sample kimi görünür, Main Content page-i yalnız Contracts/SDK surface ilə host olunur, permission grant olmadıqda sample execute action-u deny alır, success/invalid-input action-ları `Result<T>` boundary ilə qayıdır, fault action host-u çökdürmədən plugin instance/screen/task fault isolation yaradır və progress work session explicit minimize/restore zamanı state-i saxlayır. Production customer install-da sample plugin default visible/shipped feature kimi görünmür. | integration + contract + snapshot | `FR-030`, `AC-004`, `AC-009`, `AC-015`, `AC-019`, `AC-022`, `AC-032`, `docs/specs/sample-plugin.md` |
| AC-037 | Production build local persistence açılışında `ADR-0054` ilə seçilmiş DB-at-rest provider-in host və plugin persistence faylları üçün aktiv olduğu yoxlanılır; unprotected production DB, WAL/journal/temp artifact və ya provider-disabled konfiqurasiya release verification-da fail verir. Secret material yenə field-level/protected-storage yoxlamasından keçir və plugin provider/key/backing path detail-larını görmür. | integration + static/dependency + manual | `NFR-007`, `ADR-0030`, `ADR-0054`, `ADR-0055` |
| AC-038 | Production storage açılışında SQLite connection guardrails-i tətbiq olunur: hər connection üçün `foreign_keys=ON` enabled, bounded `busy_timeout` set olunur və WAL/journal/temp artifact-ları DB-at-rest protection sərhədi daxilində qalır. Guardrail-less, missing-pragma və ya bypassed-protection konfiqurasiya release verification-da fail verir. | integration | `NFR-007`, `ADR-0054`, `ADR-0055`, `docs/specs/storage-and-data-protection.md` |
| AC-039 | Active session-ı olan managed user deactivate ediləndə həmin user-in növbəti authorization-required əməliyyatı icra olunmur; session invalid edilir, user login ekranına qaytarılır və session invalidation audit trail-də görünür. Future login deactivate state-i aradan qalxana qədər deny olunur. | integration | `FR-003`, `FR-018`, `docs/specs/user-management.md`, `docs/specs/session-lifecycle.md` |
| AC-040 | Keyboard-only smoke test `Ctrl+1`, `Ctrl+2`, `Ctrl+3`, `Esc`, `Enter`, `Tab` və `Shift+Tab` davranışlarını yoxlayır: top-level shell navigation işləyir, transient UI bağlanır, focused primary action icra olunur və focus order visible indicator ilə kəsilmədən davam edir. | integration + manual | `NFR-019`, `docs/specs/shell-ui.md` |
| AC-041 | Release candidate evidence includes separate unit, integration and smoke test results. Unit tests run without local profile state; integration/smoke tests use isolated profile roots; smoke tests execute against the publish/installer artifact from the same CI run. | unit + integration + smoke + manual | `docs/09_BUILD_RUN_AND_RELEASE.md` |
| AC-042 | Release candidate native storage proof verifies the `ADR-0054` DB-at-rest provider is packaged, loaded and active, and fails when provider-disabled, provider-bypass, unprotected WAL/journal/temp or missing connection guardrail configuration is detected. | integration + smoke + static/dependency | `ADR-0054`, `AC-037`, `AC-038`, `docs/09_BUILD_RUN_AND_RELEASE.md` |
| AC-043 | Static dependency scan proves plugin projects reference only `Dobby.Contracts`/`Dobby.SDK`; integration test proves plugin-to-plugin communication uses the host event/contract boundary and not direct implementation references. | static/dependency + integration | `FR-012` |
| AC-044 | Launching a second host instance focuses or hands off to the first active instance and does not open a second writable host persistence session. | integration + manual | `FR-031` |
| AC-045 | Username and password validation rejects empty, over-limit, malformed and normalization-conflicting input; accepted values round-trip through storage and login without changing identity semantics. | unit + integration | `FR-032` |
| AC-046 | Long-running plugin install, license import and storage operations keep the UI responsive; automated smoke verifies no blocking work runs on the UI thread for those flows. | integration + smoke | `NFR-021` |
| AC-047 | Static scan verifies committed text artifacts under docs/source/resource folders are UTF-8 and do not introduce unsupported encodings. | static-scan | `NFR-022` |
| AC-048 | Azerbaijani diacritics round-trip through user input, storage, audit and UI display; identifier comparisons use documented ordinal/normalized behavior instead of culture-sensitive surprises. | integration + localization | `NFR-023` |
| AC-049 | Installed plugin package activation zamanı yenidən package completeness, hash, signature və trust verification-dan keçir. Install-dan sonra plugin runtime file, manifest, `files` hash entry-si və ya signature block-u dəyişdirilərsə plugin assembly load olunmur, activation-a buraxılmır, typed non-raw reason ilə `Rejected`/`Skipped` statusuna düşür və audit/diagnostics raw key/path/stack detail sızdırmadan səbəbi saxlayır. | integration + static/dependency | `NFR-004`, `ADR-0032`, `ADR-0045`, `ADR-0008` |
| AC-050 | `SessionAbsoluteTimeout` və `SessionIdleTimeout` `FakeTimeProvider` və ya ekvivalent test time provider ilə yoxlanılır: session yaradıldığı andan 8 saat keçəndə absolute timeout session-u `expired` edir; son aktivlikdən 30 dəqiqə keçəndə idle timeout session-u `expired` edir; expired session authenticated context-i təmizləyir, plugin-visible detail sızdırmır və user-i login flow-a qaytarır. | integration | `ADR-0031`, `FR-023` |
| AC-051 | Skeleton-time auth KDF evidence seçilmiş password hashing yolunu göstərir: default `Argon2id` seçilərsə minimum `m=19 MiB`, `t=2`, `p=1` və hər credential üçün minimum 128-bit random salt yoxlanılır; PBKDF2 fallback seçilərsə səbəb package health, license compatibility və ya `AC-014` baseline latency evidence-i ilə əsaslandırılır, `HashAlgorithmName.SHA256`, minimum `600_000` iteration və minimum 128-bit random salt yoxlanılır. Config bu floor-ları aşağı sala bilmir və zəiflətmə cəhdi fail-closed davranış yaradır. | unit + integration + manual evidence | `ADR-0038`, `ADR-0042`, `docs/03_TECH_STACK_AND_VERSIONS.md` |
| AC-052 | Audit retention floor və diagnostics retention yoxlanılır: audit log default minimum 365 gün saxlanır və config bu dəyəri aşağı sala bilmir; technical diagnostics default retention 30 gündür və finite total size cap konfiqurasiyası olmadan release verification fail verir. Retention cleanup login-first first-paint path-ini bloklamır və raw secret redaction boundary-ni zəiflətmir. | integration + config + manual evidence | `FR-011`, `FR-027`, `NFR-011`, `NFR-020`, `docs/specs/audit-and-diagnostics.md` |

## Non-blocking Phase 1 checks

| ID | Acceptance criteria | Type | Phase | Source |
|---|---|---|---|---|
| AC-011 | `Home` sidebar boş vəziyyətdən favorited item-lər görünən vəziyyətə keçir və favorit siyahısı istifadəçi preference kimi saxlanılır. | integration | P1-if-time | `FR-006` |
| AC-012 | Extended background process reporting UI uzun əməliyyatlar üçün richer progress/cancel surface göstərir; minimum work-session restore/progress behavior `AC-019` ilə release blocker kimi yoxlanılır. | integration | P1-if-time | `NFR-012`, `AC-019`, `ADR-0015` |
| AC-013 | Shared UI infrastructure (`dialog`, `notification`, `forms`, `grid`) yalnız ehtiyac olan built-in ekranlarda istifadə olunur; release blocker deyil. | manual | P1-if-time | `NFR-012`, `docs/specs/shell-ui.md` |

## Branch Shared Documentation Acceptance Notes

The current Mərhələ 1 pass is accepted when Branch Shared decisions live in
`ADR-0056`, `ADR-0057`, the affected existing ADR/spec/docs and
`docs/schemas/branchshared.schema.json`. This is a documentation acceptance
check only; it does not require a C# solution, Npgsql runtime adapter,
BranchSetup executable or live PostgreSQL tests.

Mode-aware interpretations:

- `AC-008` means no mandatory internet. Branch Shared shared authority still
  requires LAN PostgreSQL availability.
- `AC-037`, `AC-038` and `AC-042` continue to prove SQLite/DeviceLocal
  protection. PostgreSQL operational protection evidence belongs to
  `docs/specs/postgres-deployment-ops.md` and Tier B customer release.
- `AC-044` remains the Host single-instance gate. BranchSetup has a separate
  future coordinator and must not run concurrently with `Dobby.Host` on the same
  device.

Branch Shared runtime acceptance criteria are intentionally not added to P1-core
until a later human-approved skeleton/runtime phase opens.

## 2026-08 Acceptance Amendment (Controlling)

The following criteria replace conflicting earlier interpretations:

- **AC-001:** a behavior test proves that no shell, plugin scan/activation,
  license maintenance, retention job or business repository starts before a
  successful session; logout disposes the authenticated scope.
- **AC-004/AC-014:** restart never restores a password-equivalent secret. Durable
  account/device throttles survive restart, isolate accounts and bound concurrent
  password verification work.
- **AC-007:** recovery rejects replay, wrong request/nonce/device/key-purpose,
  expired response and key rotation mismatch; no fuzzy hardware match can
  authorize recovery.
- **AC-015:** a valid license import commits state, attestation and audit/outbox
  atomically; malformed/invalid input and injected commit failures preserve LKG.
  Pending/active/grace/read-only states are derived at every evaluation.
- **AC-025/AC-033/AC-034:** archive limit, path traversal, Windows name,
  collision, compression-bomb, signature-purpose and key-rotation cases are
  executable tests. In-process plugin trust is explicitly disclosed.
- **AC-046:** install/import/verify/activate actions do not block the dispatcher,
  expose cancellable progress and clean staging deterministically. Expected
  validation or license rejection does not count as a plugin runtime fault.
- Host diagnostics export succeeds in every license state while plugin/domain
  export gates remain enforced.
- Package/MSIX release acceptance requires actual install, installed launch,
  upgrade and data-preservation evidence with a production-appropriate signing
  profile; build output alone cannot close the gate.
- Architecture/source assertions supplement but never replace end-to-end,
  restart, crash/fault-injection and packaged-install behavior tests.
