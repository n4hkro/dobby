# Work Sessions Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-015`, `AC-003`, `AC-019`, `AC-028`, `ADR-0050`, `ADR-0041`, `ADR-0031`, `ADR-0010`, `ADR-0012`, `NFR-027`, `docs/specs/shell-ui.md`, `docs/specs/session-lifecycle.md`

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

## Owner Isolation

Work session identifikatoru capability deyil. Session id time-ordered GUID-dir və
guessable/enumerable sayılır, ona görə də id-nin bilinməsi həmin session üzərində
əməliyyat hüququ vermir.

Normativ qaydalar:

- host hər work session-u onu açan owner-a (plugin id və ya host area) bağlayır;
- `UpdateProgress`, `RecordResult`, `RecordError` və `Close` çağırışları yalnız
  həmin owner-a issue edilmiş session id-lər üçün icra olunur; başqa owner-ın
  session id-si ilə edilən çağırış `Forbidden` qaytarır və inner registry-yə
  ötürülmür;
- close uğurla tamamlandıqdan sonra owner grant-ı ləğv olunur: eyni id ilə təkrar
  əməliyyat `Forbidden` verir (close sonrası replay yolu qalmır);
- plugin disable/uninstall/deactivation zamanı owner-a aid bütün grant-lar silinir;
- ownership yoxlaması host tərəfindədir; plugin-in ötürdüyü `ownerId` arqumenti
  tək başına etibar mənbəyi deyil (`Open` fərqli owner id ilə çağırılarsa
  `Forbidden` qaytarılır);
- host-driven close (logout, plugin disable/uninstall, host shutdown) bu
  yoxlamadan asılı deyil: plugin öz session-unun host tərəfindən bağlanmasını
  veto edə və ya gecikdirə bilmir.

Bu qayda `ADR-0010` plugin communication policy və `ADR-0012` capability boundary
ilə eyni istiqamətdədir: plugin-lər bir-birinin UI/session state-inə birbaşa təsir
edə bilməz.

## Resource Limits

Work session registry bounded resursdur. Aşağıdakı limitlər host tərəfindən
enforce olunur (advisory deyil):

| Limit | Dəyər | Limit aşıldıqda |
|---|---|---|
| Bütün owner-lar üzrə eyni vaxtda açıq session | 64 | `ResourceBusy` |
| Bir owner üzrə eyni vaxtda açıq session | 16 | `ResourceBusy` |
| Route/label/summary/state key uzunluğu | 256 simvol | `InvalidInput` |
| Session üzrə retained state entry (input və screen ayrı-ayrı) | 64 | `ResourceBusy` |
| Retained state value uzunluğu | 4096 simvol | `InvalidInput` |

Əlavə normativ davranış:

- per-owner limit global limitdən əvvəl tətbiq olunur, beləliklə bir plugin
  bütün registry tutumunu tutub digər owner-ları starve edə bilmir;
- limit aşılması exception kimi UI-a sızmır, `Result` failure kimi qaytarılır;
- rədd edilmiş çağırış heç bir qismən mutasiya buraxmır: session-un progress,
  result/error summary və retained state dəyərləri dəyişməz qalır;
- mövcud key-in dəyərini yeniləmək entry limitini artırmır və limit dolu olsa da
  icazəlidir.

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

Lock/manual lock/auto-lock current session-u saxlayır və work session-ları close etmir. Lock ekranı aktiv olduğu müddətdə minimized work strip label-ları və live work screen məzmunu gizlədilir (bax `docs/specs/session-lifecycle.md` → Lock və Auto-Lock); unlock sonrası eyni session-lar əvvəlki state və görünüşü ilə qayıdır.

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

## Verification Obligations

`NFR-027` behavior-first proof qaydası bu spec-ə də aiddir: source text, regex scan
və ya "test green" ifadəsi aşağıdakı maddələri qapatmır.

| Maddə | Tələb olunan sübut |
|---|---|
| Cross-owner isolation | Plugin B-nin plugin A-ya issue edilmiş session id ilə `UpdateProgress`/`RecordResult`/`RecordError`/`Close` çağırışlarının `Forbidden` aldığını və A-nın session-unun toxunulmaz qaldığını göstərən runtime test |
| Close sonrası replay | Öz session-unu bağlayan owner-ın eyni id ilə təkrar əməliyyat cəhdinin `Forbidden` aldığını göstərən test |
| Owner grant cleanup | Plugin deactivation/uninstall sonrası owner grant-larının silindiyini və köhnə id ilə əməliyyatın `Forbidden` verdiyini göstərən test |
| Session flood | Per-owner limitin aşılmasının `ResourceBusy` verdiyini, digər owner-ın hələ də session aça bildiyini və global limitin ayrıca tətbiq olunduğunu göstərən test |
| Bounded payload | Oversize key/state value-nun `InvalidInput` verdiyini və session state-inin dəyişməz qaldığını göstərən test |
| State entry cap | Entry limiti dolduqdan sonra yeni key-in `ResourceBusy`, mövcud key-in update-inin isə uğurlu olduğunu göstərən test |
| Lock concealment | Lock aktiv olarkən minimized work strip label-larının render olunmadığını, unlock sonrası bərpa olunduğunu göstərən runtime UI testi |
| Restart boundary | Yeni registry instance-ın əvvəlki session-ları restore etmədiyini göstərən test |

## Revision History

| Tarix | Dəyişiklik |
|---|---|
| 2026-08-27 | Sources sətrinə `AC-003`, `AC-028`, `ADR-0010`, `ADR-0012`, `NFR-027` əlavə edildi. Owner Isolation bölməsi (session id capability deyil, foreign id → `Forbidden`, close sonrası grant revocation, host-driven close veto edilə bilməz) və Resource Limits bölməsi (global/per-owner session limiti, key/state bound-ları, `ResourceBusy`/`InvalidInput` mapping) normativ olaraq yazıldı. Lock bölməsi concealment qaydası ilə uzlaşdırıldı. Verification Obligations cədvəli əlavə edildi. |
