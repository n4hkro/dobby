# Dobby Product Scope

**Tarix:** 2026-04-23
**Status:** Təsdiqlənib
**Scope:** Məhsul kimliyi, əsas prinsiplər, istifadəçi modeli, shell davranışı, plugin modeli, licensing sərhədi, core vs plugin boundary

---

## Məqsəd

Bu sənəd Dobby məhsulunun nə olduğunu, nə üçün mövcud olduğunu və məhsul səviyyəsində hansı sərhədlərlə qurulduğunu müəyyən edir.

Bu sənədin məqsədi məhsul scope-u üçün tək, aydın və ziddiyyətsiz istinad verməkdir. Arxitektura, stack, error-handling policy, requirement və acceptance detalı ayrıca [docs/02_ARCHITECTURE_MAP.md](./02_ARCHITECTURE_MAP.md), [docs/03_TECH_STACK_AND_VERSIONS.md](./03_TECH_STACK_AND_VERSIONS.md), [docs/04_RESULT_VS_EXCEPTION_POLICY.md](./04_RESULT_VS_EXCEPTION_POLICY.md), [docs/05_FUNCTIONAL_REQUIREMENTS.md](./05_FUNCTIONAL_REQUIREMENTS.md), [docs/06_NON_FUNCTIONAL_REQUIREMENTS.md](./06_NON_FUNCTIONAL_REQUIREMENTS.md) və [docs/07_ACCEPTANCE_CRITERIA.md](./07_ACCEPTANCE_CRITERIA.md) sənədlərində bağlanır.

## Məhsul kimliyi

- Məhsulun adı: Dobby
- Məhsul tipi: plugin əsaslı Windows desktop application
- Dobby özü birbaşa konkret sahə problemini həll edən business modul deyil
- Dobby-nin əsas rolu: login, shell, navigation, plugin hosting, plugin install/lifecycle, licensing enforcement, localization, theme və ortaq platforma imkanlarını təqdim edən host olmaqdır
- Real business dəyər sonradan quraşdırılan plugin-lərlə yaranır

## Əsas Məhsul Prinsipləri

- Dobby plugin platformasıdır; host özü təkbaşına vergi, SQL query, satış və digər sahə problemlərini həll edən business məhsul kimi təqdim olunmur
- Məhsulun ən başlıca startup məqsədi login ekranını mümkün olan ən qısa vaxtda istifadəçiyə göstərməkdir
- Login ekranı görünənə qədər vacib olmayan yoxlama, import, scan, plugin activation və oxşar ağır işlər icra olunmamalıdır
- Məhsulun bütün əsas funksionallığı internetsiz işləməlidir
- İnternet heç bir halda məcburi deyil; gələcəkdə yalnız istifadəçi üçün əlavə dəyər yaradan opsional capability-lər bu prinsipi pozmadan əlavə oluna bilər
- Bütün istifadəçi məlumatları, hesablar, settings və tətbiq datası lokal cihazda saxlanılır
- String-lər kodun içinə səpələnmir; gələcək dil genişlənmələri üçün mərkəzləşdirilmiş şəkildə idarə olunur
- Phase 1 default UI culture-i `az-AZ`-dir; əlavə dil skeleton-ları gələcək genişlənmə üçün saxlanıla bilər, amma runtime language switching P2-yə qalır
- User-facing terminologiya canonical UI/code terminology mapping-ə tabedir; kod və ADR-lər stabil texniki adları saxlaya bilər, görünən UI isə lokalizə olunmuş termini ardıcıl istifadə etməlidir
- Theme sistemi ortaq olmalıdır; hər bölmə üçün ayrıca tema yazılmır, bütün elementlər vahid tema primitive-ləri üzərindən işləyir
- Dobby default olaraq vendor-a telemetry, usage analytics, crash report və diagnostics avtomatik göndərmir; support diagnostics yalnız explicit user/Owner action ilə local export package kimi çıxarıla bilər
- Core və plugin-lər üçün ortaq reusable elementlər olmalıdır ki, eyni UI və sistem davranışları təkrar-təkrar yazılmasın

## Məhsulun Dəyər Təklifi

Dobby müxtəlif sahələr üçün ayrıca business plugin-lərin işləyə biləcəyi host platformadır.

Platforma aşağıdakı tipdə plugin-ləri daşıya bilməlidir:

- vergi bəyannamələrinin yoxlanması və digər vergi yönümlü axınlar
- SQL database query və analiz plugin-ləri
- satış və əməliyyat yönümlü plugin-lər
- gələcəkdə əvvəlcədən düşünülməyən başqa sahə plugin-ləri

Yəni məhsulun dəyəri konkret bir business vertical-da sərt bağlanmaq yox, müxtəlif sahələrin plugin vasitəsilə host olunmasına imkan verməkdir.

## İstifadəçi Modeli

- Sistem local multi-user modeli dəstəkləyir
- İlk açılışda istifadəçi `username + password` ilə giriş etməyə cəhd etdikdə sistemdə heç bir istifadəçi yoxdursa, həmin hesab avtomatik `Owner` olur
- `Owner` sonradan əlavə istifadəçilər yarada, redaktə edə, password reset edə, deactivate/reactivate edə və registry-dəki built-in/plugin bölmələri üzrə icazələr verə bilər
- Permission modeli universal icazə felləri ilə işləyir; managed user explicit grant olmadan həmin əməliyyatı icra edə bilmir və `Owner` məlumatına, statusuna və icazələrinə toxuna, `Owner`-i deactivate edə, özünə permission verə və ya özündə olmayan permission-u başqasına ötürə bilmir
- Sonradan yaranan istifadəçilər `Owner` deyil, normal idarə olunan local istifadəçilərdir
- İstifadəçi və icazə modeli lokal cihaz daxilində işləyir; cloud identity provider məhsulun default hissəsi deyil
- Eyni Dobby prosesində yalnız bir active user session olur; yeni user login-i əvvəlki session logout/cleanup olandan sonra başlayır
- Logout cari session-u və in-memory user state-i bitirir, amma remember-me saved username slot-unu silmir; slot yalnız növbəti uğurlu login zamanı remember-me OFF seçildikdə boşaldılır (remember-me yalnız non-secret username saxlayır, password saxlamır)
- Manual lock və auto-lock logout deyil: lock screen cari user üçün unlock və ya logout seçimləri göstərir, başqa user login-i üçün logout vasitəsilə login ekranına qayıtmaq lazımdır

## Owner Recovery / Reset Password

Login ekranında `Owner` hesabı üçün ayrıca `Reset Password` bölməsi olmalıdır.

Bu axının məhsul səviyyəli davranışı:

- reset ekranında istifadəçi `Export Recovery Request` basır və cihaza aid request faylı yaradılır
- istifadəçi həmin faylı vendor-a out-of-band kanalla (məs. email, USB) göndərir
- vendor ayrıca admin tool vasitəsilə imzalanmış response artefaktı hazırlayır
- müştəri `Import Recovery Response` basıb cavab faylını proqrama təqdim edir
- artefakt yalnız həmin cihazda işləyir, yaradıldıqdan sonra 24 saat qüvvədədir və yalnız bir dəfə istifadə oluna bilir
- `Owner` şifrəsi sıfırlanır və yenidən giriş mümkündür

Bu axın offline məhsul prinsipini pozmamalıdır; vendor ilə əlaqə insan/proses səviyyəsində ola bilər, amma məhsulun runtime-ı məcburi online xidmətə bağlı olmur.

## Login Sonrası Shell

Uğurlu login-dən sonra istifadəçini əsas shell qarşılayır.

Shell-in məhsul səviyyəli davranışı:

- `TopToolbar`, `Sidebar`, `Third Bar`, `Main Content Area` və bottom status/progress sətri mövcuddur
- əlavə child navigation üçün `Third Bar` mövcuddur
- `TopToolbar` daxilində məhsulla birgə gələn built-in elementlər minimum olaraq `Home`, `Plugins`, `Settings` olur; active plugin entry-ləri Home ilə Settings arasında görünür
- `Home` üçün sidebar başlanğıcda boşdur; yalnız favoritlənən elementlər burada görünür
- `Plugins` və `Settings` kimi digər bölmələrin öz alt elementləri sidebar-da görünür
- `Settings` authenticated category tree-i (`Appearance`, `Account`, `Security / Session`, `User Management`, `About`) host shell altında saxlayır; About host/support/privacy field-lərini göstərir, license import yeri deyil və license import canonical olaraq `Plugins > License`-da qalır
- sidebar elementlərinin alt elementləri Third Bar-da görünür
- bar elementlərinin default sırası canonical `group + weight + stable tiebreak` modelinə (`ADR-0005`, `ADR-0028`) əsasən sabit qalır; Sidebar/Third Bar drag-and-drop override-ları user preference kimi saxlanır və default sıraya qaytarıla bilir
- TopToolbar solda Dobby logo/ad, mərkəzdə navigation, sağda tək theme switch və profile button göstərir
- Built-in və plugin navigation contribution-ları deklarativ descriptor modeli ilə shell-ə qoşulur; parent/child hierarchy dəstəklənir, amma extension target vocabulary host-defined və qapalı qalır
- Phase 1-də user canlı iş ekranını in-memory work session kimi yalnız explicit action ilə minimize/park edə, başqa işə keçə və sonra eyni ekran vəziyyətini, progress-i, error-ları və nəticəni restore edə bilər; bu browser-style tab chrome tələbi deyil, silent auto-minimize yoxdur və restart sonrası host restore P2-yə qalır

## Plugins və Licensing

Dobby özü pulsuz platformadır; business dəyər verən modullar plugin kimi quraşdırılır.

Plugin modeli üçün məhsul səviyyəli qaydalar:

- plugin-lər lokal olaraq quraşdırılır
- plugin install axını `Plugins` bölməsindən local file picker ilə seçilən signed `.dobbyplugin` package üzərindən idarə olunur
- `Plugins` bölməsi plugin adı, versiyası, statusu və qısa təsvirini göstərir
- plugin installasiya artefaktı vendor-admin tərəfindən hazırlanmış imzalanmış plugin paketidir; quraşdırma zamanı vendor, paket imzası və manifest/trust yoxlamaları keçirilir
- plugin install və plugin activation ayrı məhsul anlayışlarıdır; install artefaktı vendor-created signed `.dobbyplugin` package-dir, activation/license artefaktı vendor-signed detached `.dobbylicense` faylıdır və install package-ə embed edilmir
- license import Settings və About altında deyil, `Plugins > License` flow-u ilə aparılır; Plugin Details sadəcə eyni flow-a aparan shortcut verə bilər
- plugin package customer data, device fingerprint, vendor private key, host internal configuration və host persistence migration script daşımır
- plugin enable/disable edilə bilər; disabled plugin navigation entry və runtime activation almır
- plugin uninstall confirmation tələb edir və istifadəçiyə plugin datasını saxlamaq və ya silmək seçimi verir
- plugin runtime fault verdikdə host app çökməməli, fault həmin plugin instance/screen/task sərhədində isolate edilməli və istifadəçiyə non-technical error göstərilməlidir
- sample/test plugin Dobby plugin platformasının release verification artifact-ıdır; production customer install-da default görünmür və konkret business vertical (`vergi`, `satış`, `SQL` və s.) kimi təqdim edilmir
- plugin aktivasiyası ayrıca müddətli lisenziya artefaktı ilə idarə olunur: məsələn 1 həftə, 1 ay və s.; aktivasiya artefaktı activation/not-before, expiry, customer/deployment/device binding və plugin package fingerprint binding daşıyır; installasiya paketindən ayrı hazırlanır və istifadəçiyə ayrıca təqdim oluna bilər
- license import yalnız `.dobbylicense` file picker ilə başlayır, import permission-u tələb edir, signature/binding/package fingerprint/revision yoxlamalarından keçir və nəticəni audit trail-ə yazır
- lisenziya bitdikdə plugin tam yox olmur: host köhnə nəticələrin read-only görünməsinə icazə verə bilər, amma yeni əməliyyat, dəyişiklik, plugin/domain import/export, report/file output, print-to-file/PDF və background task start fail-closed bloklanır; yeni `.dobbylicense` import-u expired vəziyyətdən çıxmaq üçün host-level flow kimi mümkün qalır
- plugin raw license detail görmür; plugin-ə yalnız non-disclosing runtime mode (`FullAccess`, `ReadOnlyExpired`, `Denied`) görünür
- Dobby core host-u pulsuz qalır; licensing plugin səviyyəsində tətbiq olunur
- remote plugin store, cloud repository və oxşar online mexanizmlər məhsulun core funksionallığına daxil deyil; sonradan əlavə olunarsa yalnız opsional capability kimi mövcud ola bilər və offline işləməyi bloklamır
- local marketplace catalog, plugin update, repair və downgrade Phase 1 scope-una daxil deyil; bu axınlar gələcək lifecycle/update dizaynına saxlanılır

## Core vs Plugin Boundary

Core host-un məsuliyyətləri:

- startup və login axını
- local user/session idarəsi
- shell və navigation mühiti
- in-memory work session minimize/restore orchestration
- plugin install, verify, load və lifecycle orchestration
- plugin runtime fault isolation və plugin page hosting boundary
- licensing import və enforcement boundary
- localization, theme və shared UI/system primitives
- audit/diagnostics boundary və privacy-default-local support export davranışı

Plugin-lərin məsuliyyətləri:

- konkret sahə/business problemi həll etmək
- öz domain qaydalarını və business axınlarını daşımaq
- istifadəçiyə real iş görən capability vermək

Core daxilində aşağıdakılar sərt şəkildə business modul kimi qurulmamalıdır:

- sahəyə xas business qaydaları
- vertical-specific data model-lər
- ayrıca plugin-in həll etməli olduğu domain prosesləri

## Phase 1 Məhsul Sərhədi

Phase 1-də məqsəd tam business ekosistemi vermək deyil, düzgün foundation qurmaqdır.

Phase 1-də məhsul səviyyəsində gözlənilən minimum:

- sürətli login-first startup
- first-user-becomes-owner davranışı
- `Owner` user-management capability üçün foundation
- `Owner` password recovery axınının məhsul səviyyəli mövcudluğu
- built-in `Home`, `Plugins`, `Settings` ilə əsas shell
- `TopToolbar + Sidebar + Third Bar` navigation modeli
- process-lifetime in-memory work sessions: canlı iş ekranı explicit minimize/park edilir, minimized work strip-də modul/plugin adı, icon və qısa iş təsviri görünür, başqa işə keçilir və eyni ekran state-i restore olunur; restart sonrası host restore P2-dir və real/draft/domain data plugin məsuliyyətində qalır
- signed/licensed plugin install foundation
- customer/deployment/device/package-bound plugin licensing, where Phase 1 customer/deployment context is the single local `default`/`default` context created before license import per `ADR-0052`; device/package binding, read-only expired mode və non-disclosing runtime mode surface P1-də enforce olunur
- `Plugins > License` altında `.dobbylicense` import flow-u və permission guard
- offline işləyən host və plugin runtime modeli
- localization/theme/shared component foundation

Phase 1 self-service customer/device data-folder migration-ı dəstəkləmir. Tam vendor-signed customer/device migration və rebind axını P2/later capability-dir və ayrıca qərarla açılır.

## Out Of Scope

Aşağıdakılar bu sənəddə bağlanmır:

- texniki layer, namespace və dependency xəritəsi
- konkret package seçimi və version qərarları
- error/result policy detalı
- plugin manifest schema və low-level packaging format detail-ları
- licensing artefaktlarının kriptoqrafik formatı
- vendor admin tool-un daxili texniki quruluşu
- hər ayrıca business plugin-in funksional detalları

Bu mövzular aidiyyəti design və ADR sənədlərində ayrıca bağlanır.

## Storage / Deployment Mode Addendum

Bu sənədin local/offline ifadələri Standalone Installation üçün canonical qalır.
Branch Shared Installation əlavə mode kimi yalnız eyni branch LAN daxilində
PostgreSQL authority istifadə edir və mandatory internet dependency yaratmır.

Branch Shared mode-da "offline işləyən host" ifadəsi internet tələb olunmaması
deməkdir; Branch Shared authoritative əməliyyatları LAN PostgreSQL unavailable
olanda fail-closed davranır. Local diagnostics və setup evidence DeviceLocal
qala bilər, amma Branch Shared authoritative audit/data kimi sayılmır.

Mərhələ 1 yalnız sənəd işidir: `ADR-0056`, `ADR-0057`, bağlı spec-lər və schema
yaradılır və mövcud sənədlər mode-aware edilir. Bu mərhələ code skeleton,
`Dobby.BranchSetup` implementation, Npgsql runtime adapter və installer işi
başlatmır.

## 2026-08 Security and Completion Amendment (Controlling)

This amendment overrides conflicting earlier wording and is governed by
`ADR-0058` through `ADR-0061`.

- Standalone completion and production-release evidence are the current product
  milestone. Branch Shared remains a separately approved future track.
- "Remember me" may retain only a non-secret account identifier. A password,
  password-equivalent verifier or replayable login proof is never persisted for
  one-click login.
- Login-first means the pre-authentication process is deliberately small. Full
  shell, plugin, licensing, retention and business runtimes begin only after an
  authenticated session exists.
- In-process plugins are trusted, reviewed, signed product code. Dobby does not
  claim that `AssemblyLoadContext`, capability wrappers or namespace scanning
  sandbox hostile code. A future untrusted-plugin product requires a separate
  process/OS isolation design.
- Host-owned support diagnostics remain available in every license lifecycle
  state. License gates still protect plugin/domain exports and mutations.
- Security-relevant state changes and their audit/outbox records form one
  durable unit; success is never reported for a partially committed mutation.
