# ADR-0041: Login-First Startup Policy

**Status:** Accepted (runtime boundary and atomicity amended by `ADR-0061`; pre-login
remember-me snapshot removed by `ADR-0058` — see ## Revision History)
**Date:** 2026-04-24

## Context

`01_PRODUCT_SCOPE.md` `## Əsas Məhsul Prinsipləri` login ekranının mümkün qədər qısa vaxtda istifadəçiyə göstərilməsini core məhsul prinsipi kimi tərif edir və login-ə qədər `plugin scan, plugin activation, online check, update check, import, heavy migration` və bənzər ağır işlərin icra olunmamasını tələb edir.

Bu prinsip sənəddə ölçülmür və pre-login forbidden-work canonical siyahısı yoxdur. `03_TECH_STACK_AND_VERSIONS.md` `## 7. Core Primitives (Dobby-owned)`, Background tasks row `IHostedService`-i `plugin activation, health check` üçün də sadalayır, lakin `IHostedService` baseline-ın login-ə qədər işləməməsi üçün explicit guard yazılmayıb.

`02_ARCHITECTURE_MAP.md` Row 2 (`Startup / Bootstrap`) və Row 32 (`Deferred Plugin Activation`) bu prinsipi namespace səviyyəsində tutur, amma enforcement qaydası rəsmiləşməyib. Audit `Missing M-02`, `Missing M-10`, `H-03` tapıntıları eyni boşluğu fərqli zaviyələrdən göstərirdi.

Bu ADR həmin boşluğu tək canonical sənəd kimi bağlayır: login UI görünənə qədər icazəli minimal workload siyahısı, pre-login forbidden workload qapalı siyahısı, `IHostedService` post-login gating və startup measurement sərhədi.

## Decision

Phase 1 login-first startup policy:

### 1. Login-visible allowed workload (closed set)

Login ekranı render olunana qədər yalnız aşağıdakı işlər icazəlidir:

- host composition root: DI container qurulması və `IHost` build
- startup theme snapshot load: son theme üçün kiçik `startup-theme.json` oxunuşu
- startup remembered-login metadata load: yalnız normalize olunmuş username və non-secret
  slot identifier oxunur. Ayrı `startup-remember-me.json` DPAPI snapshot-u mövcud deyil --
  duplicate startup snapshot və saved-password qeydləri `ADR-0058` § `Remembered login`
  ilə idempotent migration vasitəsilə silinib; bu closed set onların yenidən yaradılmasına
  icazə vermir
- minimal theme/localization load: login ekranını render etmək üçün lazım olan theme token-ləri və `Dobby.Localization` default culture string-ləri
- shell window construction: login page-in minimum WPF window və theme render

Bu siyahı qapalıdır (closed set). Yeni pre-login workload yalnız bu ADR-in amendment-i ilə
əlavə oluna bilər. `ADR-0061` § 1 həmin amendment-dir və siyahını genişləndirir:
single-instance coordinator, credential/throttle store-unu açmaq üçün lazım olan protected
material, credential/throttle sorğuları, login UI və Owner recovery request/response
surface-i pre-auth icazəlidir. Row-level authority `docs/specs/storage-and-data-protection.md`
§ pre-login closed table set-indədir.

### 2. Pre-login forbidden workload (closed set)

Login ekranı render olunana qədər aşağıdakı işlər qadağandır:

- plugin scan (full directory walk və manifest load)
- plugin activation (plugin assembly load + initialization)
- plugin runtime binding və ya plugin code execution
- plugin signature verification (heavy crypto işi)
- online check (hər cür network request, DNS lookup, HTTP ping)
- update check (host və ya plugin update polling)
- configuration graph load
- audit storage open, `ADR-0061`-in closed pre-auth audit set-indən kənarda
- credential/throttle store-dan kənar hər cür store open (business data, cache, attestation)
- credential/throttle store-unu açmaq üçün lazım olmayan protected material probe/load
- database migration (schema upgrade, data transformation)
- import/export pipeline start
- license verification (plugin entitlement resolution)
- cache warm-up (pre-load large data sets)
- background task kicking-off (IHostedService.StartAsync calls)

### 3. Login-visible pre-auth metadata window

Login ekranı artıq görünəndən sonra, amma authenticated session yaranmamışdan əvvəl host yalnız təhlükəsiz, yüngül və non-blocking cached plugin metadata oxuya bilər. Bu cached metadata user credential submit-ni bloklamamalıdır və plugin assembly/runtime load yaratmamalıdır.

Fresh plugin scan, manifest directory walk, signature verification, license resolution və plugin activation yalnız login success-dən sonra `ADR-0008` qaydasına uyğun işləyir.

### 4. `IHostedService` baseline

- Dobby-də heavy background work başladan `IHostedService` implementation-ları login-ə qədər faktiki işə başlamır.
- Authenticated session qurulana qədər login-visible allowed workload siyahısından kənar `StartAsync` davranışı qadağandır.
- Login uğurla başa çatdıqdan sonra plugin activation, health check, background reporting və digər heavy işlər post-login orchestration ilə açılır.
- Bu qayda `Dobby.Host / Startup / Bootstrap` namespace-ində enforcement-lə bağlanır; konkret wrapper və ya orchestrator shape-i implementation detail-dir, amma pre-login guard özü canonical-dir.

### 5. Startup measurement və release gate

- Phase 1-də aşağıdakı mərhələlər ayrıca ölçülür: `cold start -> login visible`, `warm start -> login visible`, `login visible pre-auth metadata window`, `login submit -> shell first paint`.
- Bu ölçmələr structured log milestone-ları və integration acceptance yoxlamaları ilə toplanır.
- Release gate məntiqi NFR və acceptance sənədlərində saxlanılır; bu ADR konkret rəqəm seçimi deyil, ölçülən və regressiya-görünən startup policy-ni bağlayır.

### 6. Single-instance and pre-login storage cross-reference

Single-instance behavior is bound by `FR-031`, `AC-044` and the Architecture Map
engineering convention `Startup Single-Instance Sərhədi`: the coordinator is
acquired before login UI first paint, a second instance does not open a second
writable host persistence session, and the existing instance receives focus or
activation handoff.

The login-window first-paint path opens no business-data persistence. Remember-me
prefill reads the canonical pre-login remembered-account slot directly -- a username-only,
non-secret row inside the `ADR-0061` § 1 pre-auth allow-list and the
`docs/specs/storage-and-data-protection.md` pre-login closed table set. No
`startup-remember-me.json` or `startup-remembered-account.json` snapshot file
participates: both are legacy artifacts deleted best-effort at startup under
`ADR-0058` § `Remembered login`, and the canonical preference lives in the encrypted
database. Full migrations remain outside the login-first first-paint path.

### 7. Bootstrap Sequence and DI Rationale

The login-first closed allowed/forbidden lists remain canonical. This section
adds service-start ordering only; it does not permit diagnostics sink
initialization, auth/session storage open, credential store open, or audit
storage open before login first paint.

Root DI may build the host and inert service graph before login. User-scoped
storage, migrations, plugin discovery, plugin activation, and user-scoped
background services must not start until an authenticated session scope exists.

Default DI wiring for infrastructure adapters belongs to `Dobby.Host`
composition root. Owner projects do not register infrastructure adapters
themselves unless a later ADR explicitly changes the composition model.

Reopen only if a new accepted ADR changes login-first startup policy.

### 8. Rejected alternatives

- **Soft pre-login guard (`allowed if fast`):** rədd edildi — "fast" subyektivdir; qapalı siyahı deterministik enforcement verir.
- **Fresh plugin scan pre-login, activation post-login:** rədd edildi — plugin scan özü full I/O və manifest JSON parse-i nəzərdə tutur; login-first prinsipini zəiflədir.
- **Login ekranı əvəzinə splash screen with progress bar:** rədd edildi — splash işin icrasını gizlədir, amma hazırki prinsip iş miqdarını azaltmaqdır, maskalamaq deyil.

## Consequences

- `Dobby.Host / Startup / Bootstrap` namespace-i bu ADR-i canonical qaydaları kimi qəbul edir.
- `IHostedService` registration-ları pre-login guard qaydasına tabe olur; legacy `AddHostedService` istifadəsi bu guard-ı bypass edə bilməz.
- Login-visible allowed siyahısından kənar iş əlavə etmək ADR amendment tələb edir.
- Startup ölçmələri regressiya görünürlüğü verir və acceptance gate-lərlə birlikdə release qərarına daxil edilir.
- Plugin activation, license check, online operations-lar login-dən sonra lazy mode-da işləyir.
- `Dobby.Diagnostics / Logging` namespace-i startup milestone-larını structured log kimi yazır (`startup.host-built`, `startup.config-loaded`, `startup.login-visible`, `startup.pre-auth-metadata-complete`, `startup.session-authenticated`, `startup.shell-first-paint`).

## Cross-references

- `docs/01_PRODUCT_SCOPE.md` — `## Əsas Məhsul Prinsipləri`
- `docs/02_ARCHITECTURE_MAP.md` — Row 2 `Startup / Bootstrap`, Row 32 `Deferred Plugin Activation`, Cədvəl 3 `Login-First Startup Policy` risk sətri
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `## 7. Core Primitives (Dobby-owned)`, Background tasks row
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — startup və offline-first NFR-lər
- `docs/07_ACCEPTANCE_CRITERIA.md` — startup və offline acceptance check-ləri
- `docs/adr/ADR-0008-deferred-plugin-activation.md` — plugin activation login success-dən sonra
- `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` — session semantics, expiry trigger
- `docs/adr/ADR-0038-local-first-authentication.md` — device-bound local auth
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — § 1 pre-login allowed
  set-i amend edir: duplicate `startup-remember-me.json` DPAPI snapshot-ı silinib,
  sadəcə istifadəsiz qalmayıb
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — pre-auth
  allowed/forbidden qapalı set-lərini, post-auth boundary point-i və mutation
  atomicity-ni amend edir; həmin bölmələr üçün current authority

## Branch Shared Pre-Auth Amendment

Branch Shared mode adds a narrow PostgreSQL exception to the pre-auth workload:
server identity, `branchDeploymentId`, schema/grant probe and canonical
pre-auth audit INSERT may run before full authenticated shell startup.

The allowed pre-auth audit event set is closed:

- `user.login.failed`;
- `recovery.export`;
- `recovery.import`;
- `security.verification.failed`.

This does not permit plugin activation, full migration, import/export, online
checks or arbitrary PostgreSQL reads before login. If PostgreSQL is unreachable
before the audit insert can occur, the event is DeviceLocal diagnostics/setup
evidence only.

## Revision History

- **2026-08-31:** `ADR-0061` amendment-i dated record kimi yazıldı və ziddiyyətli closed
  set-lər düzəldildi. `**Status:**` sətri amendment-i bildirirdi, lakin sənəddəki yeganə
  `## ... Amendment` bölməsi Branch Shared pre-auth (`ADR-0056`/`ADR-0057`) haqqındadır və
  tarixsizdir. İki düzəliş: (1) § 2 forbidden closed set hələ də "credential store, session
  store və audit storage open", "protected material probe/load" və "SQLite connection open"
  qadağalarını daşıyırdı -- `ADR-0061` § 1 isə məhz bunları pre-auth icazə verir, yəni
  qadağa siyahısı cari authority ilə birbaşa ziddiyyətdə idi; § 1-in özü yeni pre-login
  workload-un yalnız bu ADR-in amendment-i ilə əlavə olunmasını tələb edir, ona görə
  ziddiyyət düz həmin bölmədə həll olunub. (2) § 1 allowed set "startup remember-me
  snapshot load: `DPAPI CurrentUser` ilə qorunan `startup-remember-me.json`" sətrini
  daşıyırdı -- bu, `ADR-0058` § `Remembered login`-in idempotent migration ilə silinməsini
  qərara aldığı duplicate startup snapshot-dur; retired secret-bearing faylı closed
  allow-list-də saxlamaq onun yenidən yaradılması üçün göstərişdir. (3) § 6 hələ də
  "Remember-me prefill uses only the `startup-remember-me.json` snapshot, which is refreshed
  from the canonical DB slot" deyirdi -- bu, data axınının istiqamətini tərsinə çevirirdi:
  silinmiş snapshot-u prefill MƏNBƏYİ, canonical DB slot-unu isə onun refresh hədəfi kimi
  göstərirdi. HEAD-də əksi doğrudur: `HostLoginRuntimeFactory`
  `rememberMeSlotService.LoadRememberedAccount()` ilə slot-u birbaşa oxuyur (cədvəl
  `PreLoginRememberedAccountMigrationV7` ilə yaradılır) və `StartupRememberMeLegacyCleaner`
  həm `startup-remember-me.json`, həm də obsolete `startup-remembered-account.json` faylını
  best-effort silir. Bu, § 1 allow-list düzəlişi ilə eyni retired snapshot-a aiddir, lakin
  ayrı bölmədə qalmışdı; slot-un pre-auth oxunuşu `ADR-0061` § 1 icazəsi daxilindədir.
  Login-first startup
  qərarı, `IHostedService` baseline-ı və bootstrap rationale dəyişmir; yeni öhdəlik əlavə
  edilmir, `ADR-0058` və `ADR-0061`-in mövcud qərarları dəqiq ifadə olunur.
