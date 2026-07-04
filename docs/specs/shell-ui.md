# Shell UI Spec

**Tarix:** 2026-04-27
**Status:** P1 canonical spec
**Scope:** Shell visual layout, navigation surfaces, profile menu, bottom status/progress, notification taxonomy, theme/localization baseline və accessibility minimumu

Bu spec `FR-005`, `FR-010`, `FR-013`, `FR-019`, `AC-003`, `AC-016`, `AC-017`, `AC-023`, `AC-027`, `NFR-010`, `NFR-013`, `NFR-019`, `ADR-0005`, `ADR-0010`, `ADR-0013`, `ADR-0028` və `ADR-0049` üçün icra dilini saxlayır.

## 1. Visual Reference

Shell üçün bir canonical UI design direction var. `ui_design_light_theme.jpg` faylı
light theme visual direction və composition intent üçün Dobby-owned visual
reference-dir. O, measurements, layout constants, spacing scale, exact proportions,
typography sizes, radius values, icon sizes və token values üçün təkbaşına
normative authority deyil; bu dəyərlər yalnız textual token/constant kimi
`docs/specs/design-system-assets.md` və ya bu spec-də yazılanda canonical sayılır.
Asset yalnız host/platform labels daşımalıdır; business-domain content və
third-party branding daşımır.

Bu visual reference light-theme direction üçün support verir. O, labels, default
runtime state, plugin visibility, navigation behavior, permission behavior,
measurements, token values və release requirements üçün ADR/spec mətnini override
etmir.

Theme variantları həmin eyni dizaynı istifadə edir və yalnız color/brush token-lərini dəyişir. Light theme visual reference-in rəng ailəsini saxlayır; dark theme `docs/specs/design-system-assets.md` içindəki Solarized Dark token mapping-i istifadə edir. Gələcək theme-lər də eyni token adlarını doldurmalı və layout/control variant yaratmamalıdır.

## 2. Shell Layout

Shell bu səthlərdən ibarətdir:

- TopToolbar
- Sidebar
- Third Bar
- Main Content Area
- minimized active work strip
- bottom status/progress sətri

TopToolbar:

- solda Dobby logo + ad göstərir;
- navigation elementləri ekranın mərkəzində icon/logo + ad formatında yerləşir;
- sağda tək theme switch button var, iki ayrı light/dark button yoxdur;
- theme switch-in sağında profile button/logo yerləşir.

Sidebar:

- geniş rejimdə icon/logo + ad göstərir;
- yuxarıda collapse/expand button saxlayır;
- yığcam rejimdə yalnız icon/logo göstərir;
- yığcam rejimdə hover zamanı ad tooltip/popover kimi görünə bilər, sidebar avtomatik genişlənmir.

Collapsed Sidebar parent icon-ları visible saxlayır; collapsed mode flyout açmır və hover yalnız qısa label/tooltip göstərə bilər. Expand/collapse state, open node state və ordering per-user saxlanır.

Third Bar:

- Main Content Area-nın yuxarı-mərkəz hissəsində yerləşir;
- visual olaraq island/kəsik təsiri verməlidir;
- sağ və sol tərəflər bütöv main content səthi kimi davam edir;
- selected Sidebar item-in daha dərin child navigation səviyyəsini göstərir.

Minimized active work strip bottom status/progress sətrinin üstündə tab/chip kimi görünür. Hər work item modul/plugin adı, icon və qısa iş təsviri göstərir. Strip yalnız explicit minimize edilmiş work session-ları göstərir; host yarımçıq işi silent auto-minimize etmir.

Bottom status/progress sətri host-controlled status, background progress, plugin job progress və qısa status mesajları üçündür. Plugin buraya arbitrary UI yerləşdirmir; yalnız host-un verdiyi descriptor/capability modeli ilə qısa indicator və progress məlumatı təklif edə bilər.

### Window Sizing və Resolution

Shell pəncərəsinin canonical minimum ölçüsü `1024×680` piksel-dir. Bu ölçüdən aşağıda layout qırılmamalıdır; pəncərə daha kiçik ölçülməyə çalışılırsa, host minimum constraint tətbiq edir.

Shell layout `1024×680` ilə `4K` (`3840×2160`) arası standart Windows resolution-larında qırılmadan render olunmalıdır. Test minimum resolution kümələri: `1366×768` (köhnə laptop), `1920×1080` (FullHD), `2560×1440` (QHD), `3840×2160` (4K).

100% DPI primary snapshot baseline-dır. 125% və 150% DPI baseline `1920×1080` resolution-da manual smoke test kimi yoxlanılır. 175%/200% DPI və `PerMonitorV2` advanced polish P2/P3 scope-undadır.

## 3. Navigation Model

Target dili:

- TopToolbar
- Sidebar
- Third Bar
- plugin page region

Product sənədlərində DTO/enum adları requirement dili kimi istifadə olunmur.

Navigation mapping:

- Home fixed first qalır və gizlədilmir.
- Settings fixed last qalır və gizlədilmir.
- Active plugin top-level entries Home ilə Settings arasında TopToolbar-da görünür.
- Selected TopToolbar entry child-ları Sidebar-da görünür.
- Selected Sidebar entry child-ları Third Bar-da görünür.
- Plugin page daxilində daha lokal composition üçün `plugin page region` saxlanılır.

Default ordering `group + weight + stable tiebreak` modelindən gəlir. Sidebar və Third Bar drag/drop ordering per-user saxlanır. TopToolbar-da plugin entries üçün user ordering/hidden state saxlanır. Default sıraya qaytar action-ı override-ları təmizləyir.

### Navigation Failure Handling

Persisted və ya contributed navigation route resolve edilə, authorization-dan keçə və ya aktivləşdirilə bilmirsə, shell fail-closed şəkildə `Home`-a qayıdır və technical diagnostic warning yazır. Protected operation cəhdi yoxdursa, bu fallback audit event yaratmır.

Plugin navigation icon-ları üçün render surface host-controlled qalır. Broken/missing plugin icon default fallback icon-a düşür; canonical icon asset qaydası `docs/specs/plugins-manager.md` içindədir.

Plugin page rendering contract `ADR-0049` tərəfindən idarə olunur:
`IPluginPage` / `IPluginPageModel` host-owned rendering surface-dir. Shell
plugin WPF controls, host implementation types or arbitrary plugin UI objects
render etmir; plugin page region yalnız official page/extension contract
vasitəsilə compose olunur. Plugin manifest yalnız navigation/route metadata
daşıyır; shell host-owned page render registry-si ilə approved
`IPluginPageModel` contract type-larını host views-lara map edir və manifest
schema-ya `viewKey` əlavə edilmir.

## Responsive navigation details

Home, Plugins və Settings top toolbar entry-ləri core navigation sayılır və responsive overflow-da gizlədilməməlidir. Overflow yalnız dynamic plugin entries və aşağı prioritet host actions üçün istifadə edilməlidir.

Collapsed sidebar-da parent icon kliklənəndə sidebar açılır və həmin parent navigation context-i seçilir.

## 4. Profile Menu

Profile menu shell-də user-ə aid əsas əmrlərin entry point-idir.

Minimum inventory:

- profile/account info
- password change
- manual lock
- logout
Profile menu item-ları permission və session state qaydalarına uyğun göstərilir.

## 5. Notification Surface

Notification taxonomy:

| Surface | Semantika |
|---|---|
| Toast | Qısa, non-blocking məlumat |
| Banner | Persistent warning/status |
| Modal | User təsdiqi tələb edən və ya riskli əməliyyatı dayandıran hal |

Plugin install validation, expiry warning, runtime fault, work-session warning və destructive action confirm-ləri bu taxonomy-yə bağlanmalıdır. Work-session warning user-ə minimum minimize et, qal və bağla seçimləri verməlidir.

### Screen Feedback States

Shell və built-in ekranlar long-running və data-dependent hallarda explicit feedback state göstərir: loading, empty, success, error və confirmation. Empty state action-a imkan verirsə primary action göstərir; error state raw exception/detail sızdırmır və recovery action varsa onu ayrıca command kimi verir.

### Background Operation Status

Background operation statusları host-owned progress modelindən gəlir. Shell progress surface-i operation adı, status (`queued`, `running`, `cancelling`, `failed`, `completed`), percent/indeterminate progress, cancel availability və short non-technical failure text göstərə bilər; retry qərarı operation owner contract-ında qalır və plugin arbitrary progress UI yerləşdirmir.

### UI Thread / Dispatch Boundary

Shell və built-in UI update-ləri UI thread-də host-owned dispatcher adapter-i ilə
icra olunur. Messaging UI Bridge public plugin API deyil; integration event və ya
background operation status UI feedback-ə çevriləndə host daxilində
`Dobby.Messaging` / `Dobby.Shell` orchestration-u UI dispatcher-ə keçir.

Plugin `Application.Current.Dispatcher`, shell view model-ləri və ya WPF control
instance-larını direct access kimi almır. Plugin yalnız declared page/extension,
notification və progress descriptor-ları verir; host həmin descriptor-ları UI
thread-də render edir. UI-local event visibility və bridge sərhədi `ADR-0010`
tərəfindən idarə olunur.

## 6. Theme And Localization

P1-də light və dark theme mövcud olmalıdır. Shell və built-in ekranlar design tokens, spacing scale və base typography-dən istifadə edir; hardcoded color/spacing/font dəyərləri shell səviyyəsində release gate-də qəbul edilmir.

Theme switch yalnız color/brush resource dictionary-ni dəyişir. Layout, spacing, typography, radius, motion, icon size, navigation model və screen composition theme-ə görə dəyişə bilməz. Shell screen-ləri per-screen dark palette define etmir; bütün theme-lər `docs/specs/design-system-assets.md` semantic color token-lərini consume edir.

P1 default UI culture `az-AZ`-dir. User-facing host UI string-ləri localization resource-larından gəlməlidir. Plugin resource-ları plugin package/assembly sərhədində qalır və host yalnız declared provider/hook vasitəsilə oxuyur.

Missing-key davranışı:

- development/debug mühitində missing key görünən olur;
- production-da fallback culture istifadə olunur;
- `localization.missing-key` diagnostics event/log yaranır;
- missing key audit event deyil.

Runtime language switching P2 scope-una saxlanılır. Full multi-language UI və RTL polish P3 scope-una saxlanılır.

Localization resource scope shell və built-in UI string-ləri ilə məhdudlaşır: navigation label, command name, dialog/notification text, surface chrome və oxşar UI mesajlar. Biznes, qanun, qayda və plugin domain text-ləri `.resx` resource-u kimi avtomatik UI material sayılmır; həmin material plugin-owned data, validation, storage və migration modelinə tabedir. Canonical principle: `docs/adr/ADR-0044-localization-resource-organization.md`.

Shell visual tokens, asset naming, app icon and plugin icon resource rules are owned by `docs/specs/design-system-assets.md`. Shell screens consume those tokens through shared resources; hardcoded spacing, radius, typography and motion values should not be introduced in individual screens.

## 7. UI State Persistence

Per-user saxlanılan shell state:

- Sidebar collapse/expand state
- Sidebar ordering
- Sidebar open node state
- Third Bar ordering
- TopToolbar plugin ordering/hidden state
- window size/position
- last active module

Last active module startup restore üçün istifadə olunmur. App açıldıqdan və login success-dən sonra shell Home-dan başlayır.

Favorites ayrıca navigation/personalization modelidir; adi layout state kimi qarışdırılmır.

Favorites per-user UI state-dir. Navigation contribution source yalnız provenance bildirir və permission scope yaratmır; favorite aktivləndiriləndə canonical route yenidən resolve olunur və authorization yoxlanılır.

Favorites parent-child semantics:

- child favorite yalnız həmin child route-a təsir edir;
- parent favorite parent-i və bütün child route-ları favorite set-ə əlavə edir;
- parent unfavorite parent-i və child route-ları favorite set-dən çıxarır;
- parent partial state bəzi, amma bütün child-lar favorite olmadıqda göstərilir.

## Favorites and route preference repair

Home/favorites surface boş olduqda localized empty-state message göstərməlidir. Favorite əlavə və silmə əməliyyatları toast feedback verə bilər. Favorite record plugin business data-nı kopyalamır.

Persisted route artıq mövcud deyilsə, disabled plugin-ə aiddirsə və ya current user üçün authorized deyilsə, Home fallback və diagnostic warning-dən sonra stale preference təmizlənməli və ya safe default-a normalize edilməlidir.

Default layout reset per-user layout override-larını təmizləyir, amma Favorite records-u silmir.

## 8. Accessibility Minimumu

P1 minimum:

- shell və built-in ekranlarda keyboard navigation;
- visible focus indicator;
- kifayət qədər contrast;
- əsas control-lar üçün role/label mapping;
- əsas shell workflow-larında mouse məcburiyyəti olmaması.

Full WCAG və screen reader polish P3 scope-una saxlanılır.

### Keyboard Shortcuts And Focus

P1 shell shortcut-ları:

- `Ctrl+1`, `Ctrl+2`, `Ctrl+3` əsas TopToolbar target-ləri arasında keçid edir;
- `Esc` modal, flyout və transient UI state-ləri bağlayır, protected operation-u silent cancel etmir;
- `Enter` focused primary action-u icra edir;
- `Tab` və `Shift+Tab` visible focus order boyunca hərəkət edir.

Lock screen UI outcome-ları `docs/specs/session-lifecycle.md` içində bağlanır; shell həmin outcome-ları typed error, failed-attempt countdown və logout-vs-unlock action ayrımı ilə render edir.

## 9. Verification

Minimum verification:

- shell snapshot TopToolbar/Sidebar/Third Bar/Main Content/bottom status səthlərini yoxlayır;
- ordering testləri `group + weight + stable tiebreak` və per-user override/reset davranışını yoxlayır;
- static scan hardcoded visible UI string-ləri tutmalıdır;
- static scan shell-level hardcoded color/spacing/font dəyərlərini tutmalıdır;
- keyboard-only smoke test Home, Plugins, Settings, Sidebar collapse, theme switch və profile menu workflow-larını yoxlayır.
- shell snapshot minimum `1024×680` və baseline `1920×1080` resolution-larında 100% DPI altında yoxlanılır;
- 125% və 150% DPI manual smoke test baseline `1920×1080` resolution-da shell layout-un overlap/kəsilmə yaratmadığını yoxlayır.

## Cross-references

- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-005`, `FR-010`, `FR-013`, `FR-019`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-003`, `AC-016`, `AC-017`, `AC-023`, `AC-027`
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-010`, `NFR-013`, `NFR-019`
- `docs/adr/ADR-0005-ui-ordering-model.md`
- `docs/adr/ADR-0010-plugin-communication-policy.md`
- `docs/adr/ADR-0028-shell-composition-ordering-render.md`
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`
- `docs/adr/ADR-0050-in-memory-work-sessions.md`
- `docs/specs/work-sessions.md`
- `docs/adr/ADR-0043-initial-ui-culture-and-fallback-chain.md`
- `docs/adr/ADR-0044-localization-resource-organization.md`
- `docs/adr/ADR-0046-ui-and-code-terminology-mapping.md`

## Branch Shared Shell Status Addendum

Branch Shared unavailable, wrong-server, wrong-deployment, schema/grant mismatch
and not-provisioned states should be represented as typed shell/status outcomes
when future runtime implementation opens.

The shell may render local diagnostics/setup evidence status, but it must not
claim unsent DeviceLocal evidence is authoritative Branch Shared audit. Any
Branch Shared setup/admin surface belongs to future BranchSetup flow, not normal
authenticated shell navigation by default.
