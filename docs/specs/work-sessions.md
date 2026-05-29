# Work Sessions Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-015`, `AC-019`, `ADR-0050`, `ADR-0041`, `ADR-0031`, `docs/specs/shell-ui.md`, `docs/specs/session-lifecycle.md`

## Məqsəd

Bu spec Dobby-nin process-lifetime in-memory work session davranışını bağlayır: canlı iş ekranı açılır, user tərəfindən minimize/park edilir, eyni proses daxilində restore olunur və restart sonrası host tərəfindən avtomatik geri açılmır.

## Work Session Anlayışı

Work session user-in başlatdığı canlı iş ekranıdır. Bu ekran built-in və ya plugin-owned ola bilər və aşağıdakı state-ləri daşıya bilər:

- input seçimi;
- progress;
- validation error;
- result;
- transient UI state;
- cancellation/close state.

Work session browser-style tab chrome tələbi deyil. Canonical davranış minimize/restore edilə bilən live work screen-dir.

## Minimized Work Strip

Minimized work strip bottom status/progress sətrinin üstündə göstərilir.

Minimum item metadata:

- modul/plugin adı;
- icon;
- qısa iş təsviri;
- optional progress/status indicator.

Strip item activate ediləndə həmin live screen process memory-də qaldığı state ilə restore olunur.

## No Auto-Minimize

Host heç bir yarımçıq işi silent auto-minimize etmir. Minimize explicit user action-dır.

User yarımçıq work screen-i minimize etmədən başqa bölməyə keçmək istəyəndə host warning göstərir. Warning minimum bu seçimləri verir:

| Seçim | Davranış |
|---|---|
| Minimize et | Current work session minimized strip-ə keçir və navigation davam edir. |
| Qal | Navigation cancel olunur, user current work screen-də qalır. |
| Bağla | Work session controlled close/cancel edilir və navigation davam edir. |

Destructive close/cancel davranışı plugin-in domain qaydalarına hörmət etməlidir və raw exception/detail UI-a sızdırmamalıdır.

## Close Lifecycle

Work session bağlanarkən shell active content-dən close qərarı tələb edir. Content üç nəticədən birini qaytara bilər:

- close-a icazə verir;
- close-u ləğv edir;
- saxlanmamış plugin-owned state üçün confirmation tələb edir.

Shell session container-in sahibidir, domain data-nın sahibi deyil.

## Restart Boundary

P1-də work session state yalnız Dobby prosesi açıq olduğu müddətdə saxlanılır.

Restart və ya crash sonrası:

- host əvvəlki minimized work item-ları strip-də restore etmir;
- app login sonrası Home-dan başlayır;
- pre-login mərhələdə work-session restore/persistence I/O başlamır;
- persisted workspace restore P2 mövzusudur.

## Plugin Domain Data Boundary

Work session state host process memory-sindəki live screen state-dir; plugin domain data deyil.

Plugin işin real nəticəsini, draft-ını və ya domain record-unu saxlamaq istəyirsə, bunu öz storage/domain qaydası ilə edir:

- completed satış/import nəticəsi plugin domain datası kimi qalır;
- yarımçıq draft plugin istəyirsə `UserScopedStorage` və ya öz domain modelində saxlanır;
- restart sonrası həmin data-ya baxış yolu plugin-in öz ekranları və navigation-ı ilə təmin edilir;
- host minimized work strip-i plugin domain history/viewer əvəzinə istifadə olunmur.

## Logout, Lock və Plugin Lifecycle

Logout active user session-u bitirir. Active work session varsa warning göstərilir və təsdiqdən sonra work session-lar controlled close/cancel edilir.

Lock/manual lock/auto-lock current session-u saxlayır və work session-ları close etmir. Unlock sonrası process memory-də qalan session-lar görünə bilər.

Plugin disable/uninstall həmin plugin-ə aid work session-ları controlled close/cancel edir. Uninstall data saxlansın/silinsin seçimi `docs/specs/plugins-manager.md` ilə idarə olunur.

## Acceptance Hooks

- `AC-019` explicit minimize/restore, no-auto-minimize warning, minimized item metadata və restart boundary-ni yoxlayır.
- `AC-028` logout zamanı active work warning və cleanup behavior-u yoxlayır.
- `AC-003` minimized active work strip-in shell snapshot-da görünməsini yoxlayır.

## Branch Shared Availability Addendum

When Branch Shared PostgreSQL is unavailable, work session screen state may stay
in process memory so the user can retry or close gracefully. This is a UI/session
continuity rule only.

Any action requiring Branch Shared authoritative read/write must fail closed
until authority is reachable and revalidated. Work session memory is not a local
offline command queue and does not become authoritative Branch Shared data.
