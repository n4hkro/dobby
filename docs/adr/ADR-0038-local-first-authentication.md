# ADR-0038: Local-First Authentication

**Status:** Accepted (credential-storage and throttle details amended by `ADR-0058` — see ## Revision History)
**Date:** 2026-04-23

## Context

Dobby məhsulunun əsas prinsipi offline-first işləməkdir: məhsul heç bir halda
internet bağlantısına ehtiyac duymur və istifadəçi məhsulun bütün funksionallığını
internetsiz kamil şəkildə istifadə edə bilir. İnternet istifadəsi heç bir halda
məcburi deyil; yalnız istifadəçi tərəfindən açılan opsional funksiya kimi mövcud
ola bilər. `docs/01_PRODUCT_SCOPE.md` istifadəçi hesablarının və istifadəçi
məlumatlarının yalnız lokal cihazda saxlanacağını təsbit edir.

Əvvəlki stack qərarı (`03_TECH_STACK_AND_VERSIONS.md`) default auth-u MSAL.NET + WAM
+ Entra ID / OIDC kimi bağlayırdı və local auth-u yalnız `offline/air-gap`
exception kimi saxlayırdı. Bu, məhsulun offline-first prinsipi ilə birbaşa
ziddiyyət təşkil edirdi: Entra ID / OIDC initial auth üçün internet tələb edir,
token expiry dövriyyə ilə bağlıdır və identity source cihazdan kənardadır.

Bu ADR həmin ziddiyyəti bağlayır və auth strategiyasını məhsulun əsas prinsipi
ilə uzlaşdırır.

## Decision

Dobby-nin core auth yolu bütün phase-lərdə `device-bound local password-based
authentication` olaraq təsbit edilir. Host heç bir cloud identity provider-ə
bağlı olmur; identity cihazdadır və cihazla bağlıdır.

Bağlayıcı qaydalar:

- Core auth yalnız local password-based auth-dur; MSAL.NET, WAM və Entra ID /
  OIDC Phase 1 scope-una daxil deyil və default yolun hissəsi kimi nəzərdə
  tutulmur
- Password hash və credential material cihazda DPAPI-encrypted saxlanır
  (`System.Security.Cryptography.ProtectedData`)
- Remember-me auto-login deyil: yalnız cihazdakı son remembered user üçün login
  formunda username və masked password hazır doldurula bilər
- Remember-me credential material plain text faylda saxlanmır; OS secure
  credential store / protected storage ilə qorunur
- Password field proses restart-dan sonra boş açılır və reveal action yoxdur: retained
  password mövcud olmadığı üçün reveal edilə bilən stored material da yoxdur. Masked
  placeholder retained password yerinə keçmir (`ADR-0058` § `Remembered login`)
- Field-level clear yalnız cari form state-ini təmizləyir. Persistent slot yalnız növbəti
  uğurlu login zamanı remember-me OFF seçildikdə, həmçinin account silinəndə/deaktiv
  ediləndə boşaldılır; logout və auto-lock slot-u silmir
- Phase 1 remember-me modeli single slot daşıyır və bu slot secret DAŞIMIR: persist olunan
  yeganə material normalize olunmuş username və non-secret slot identifier-dir. Password,
  reversible credential, password-equivalent verifier, startup snapshot və ya bunların
  masked surrogate-i saxlanmır, ona görə slot üzərində reveal action da yoxdur və password
  field proses restart-dan sonra boş açılır. Hər uğurlu login slot-u yeni normalize olunmuş
  username ilə replace edir. Legacy saved-password qeydləri və duplicate startup snapshot
  idempotent migration ilə silinir (`ADR-0058` § `Remembered login`; observable qaydalar
  `docs/specs/session-lifecycle.md` § `Remembered Login Qarşılıqlı Təsiri`, migration
  `docs/specs/storage-and-data-protection.md` § `Remembered-login slot legacy migration`)
- Password hashing üçün Phase 1 seçimi skeleton/toolchain decision gate-də explicit
  bağlanır və `ADR-0027` içindəki `Dobby:Auth:PasswordKdfMode` config key-i ilə
  ifadə olunur. `Argon2id` default niyyətdir; BCL `Rfc2898DeriveBytes` ilə
  PBKDF2-HMAC-SHA256 yalnız package health, license compatibility və ya `ADR-0042`
  baseline-da ölçülmüş login latency `AC-014` gate-ini pozarsa dependency-minimal
  fallback kimi seçilir.
  Əvvəlki third-party hashing package candidate Phase 1 canonical siyahısından
  çıxarılır
- `Argon2id` istifadə olunarsa starter floor `m=19 MiB`, `t=2`, `p=1`
  olmalıdır; bu dəyərlər skeleton benchmark nəticəsi ilə yalnız yuxarı
  tune edilə bilər
- PBKDF2 fallback istifadə olunarsa `Rfc2898DeriveBytes` konstruksiyasında
  `HashAlgorithmName.SHA256` explicit verilməli və iteration sayı minimum
  `600_000` olmalıdır
- Hər credential üçün salt minimum 128-bit cryptographically random olmalıdır;
  salt reuse və deterministic salt qəbul edilmir
- Password hash/verify parametrləri `ADR-0042` baseline hardware-də ölçülür;
  selected KDF, package-health/license check nəticəsi və ölçülmüş login latency
  skeleton evidence-də qeyd olunur. Tuning OWASP floor dəyərlərindən aşağı düşməməli və `AC-014`
  `login submit → shell first paint` release budget-ini pozmamalıdır
- Phase 1 namizəd siyahısından çıxarılan algoritm: `BCrypt.Net-Next` Phase 1
  canonical hashing siyahısından çıxarıldı; `Argon2id` memory-hard qoruma
  profili və PBKDF2-HMAC-SHA256 BCL-only fallback ilə üst-üstə üstün boundary
  verir, üçüncü tərəf hashing package dependency-si ehtiyac olmadan saxlanılır.
  PBKDF2 fallback rolu üçün BCL `Rfc2898DeriveBytes` seçilməsinin səbəbi əlavə
  package tələb etməməsi və OWASP iteration floor (`≥600_000`) ilə canonical
  fallback olmasıdır
- Uğursuz login throttle app-wide state kimi işləyir: ilk 2 səhv cəhddə cəza
  yoxdur, 3-cü səhvdən sonra 10 saniyə, 4-cü səhvdən sonra 30 saniyə, 5-ci və
  sonrakı hər səhvdən sonra 60 saniyə gözləmə tətbiq olunur. Username dəyişmək
  aktiv cəzanı sıfırlamır; uğurlu login failed-attempt state-i təmizləyir.
  Canonical config key `ADR-0027` içindəki `Dobby:Auth:LoginThrottleStepsSeconds`-dir
  və bu floor-ları aşağı sala bilməz
- Throttle aktivdirsə login button disabled olur və UI qalan saniyəni göstərir
- Session semantics ADR-0031 ilə tənzimlənir: invalid/expired session user-i
  login flow-a qaytarır
- Login ekranında görünən recovery/about kimi public
  funksiyalar login olmadan əlçatandır; login ekranında görünməyən shell,
  settings, plugins, plugin runtime və əsas app funksiyaları yalnız
  authenticated session-dan sonra açılır
- Cloud identity (Entra / OIDC və ya digər) gələcəkdə yalnız opsional
  plugin və ya modul kimi qoşula bilər; core host məcburi internet-dən asılı
  olmur
- Authorization policy engine-i (`Microsoft.AspNetCore.Authorization` — yalnız
  `IAuthorizationService`, requirement və handler səthi) bu ADR ilə əvəz
  olunmur; authorization və authentication ayrı layerdir

### First-user Owner bootstrap invariant

Clean install zamanı ilk uğurlu login `Owner` yaradırsa, `Users`, `Permissions` və
`AuditLog` seeding tək transaction kimi commit/rollback olunmalıdır. Partial state
qəbul edilmir: Owner user yaranıb all-grants və audit entry yaranmasa, yaxud audit
yazılıb Owner permission state yaranmasa bootstrap uğursuz sayılır və növbəti cəhd
idempotent şəkildə eyni invariantı yenidən yoxlayır.

Phase 1-də `Permissions` state seed olunur tələbi ayrıca `Permissions` row
yaratmaqla yox, `Users.IsOwner=1` row-u ilə təmin olunur; bu row Owner
all-grants short-circuit-i təmsil edir və transaction invariant-ının tərkib
hissəsidir. Managed-user permission rows və operation-target registry Phase 1
Owner bootstrap invariant-ının hissəsi deyil.

## Consequences

- Host auth səthi Phase 1-də kiçik, sabit və internet-dən asılı olmayan qalır
- Remember-me rahatlıq yaradır, amma auto-login və stored-password reveal
  yaratmadığı üçün authentication boundary-ni zəiflətmir
- Login throttle username bazasında deyil, app-wide state kimi işlədiyi üçün
  username dəyişməklə cəzanı bypass etmək olmur
- MSAL ekosistemindən (`Microsoft.Identity.Client`,
  `Microsoft.Identity.Client.Broker`,
  `Microsoft.Identity.Client.Extensions.Msal`) package dependency yoxdur;
  skeleton package dəstindən çıxarılır
- `Dobby.Security / Authentication / Session` namespace-inin implementasiyası
  cloud identity provider inteqrasiyasına açıq deyil; belə bir inteqrasiya
  lazım olarsa ayrıca ADR tələb olunur
- Password hashing seçimi human-approved toolchain/package decision gate-də
  `Argon2id` default və PBKDF2-SHA256 BCL fallback gate-i çərçivəsində təsbit
  edilir; əvvəlki third-party package dependency-si Phase 1 stack-dən çıxarılır
- `Argon2id` seçilərsə `Konscious.Security.Cryptography.Argon2` package-i
  human-approved package pin list-də version-pin edilir; hazırkı docs-only revision
  `Directory.Packages.props` yaratmır
- `03_TECH_STACK_AND_VERSIONS.md § 6. Authentication Strategy` və `§ 2. Infrastructure
  Packages` bu ADR-ə uyğun şəkildə yenilənir
- `02_ARCHITECTURE_MAP.md` Row 35 `Authentication / Session` description-i device-bound
  local auth dili ilə dəqiqləşdirilir; phase və status dəyişmir
  (`P1-core agreed`)
- `docs/01_PRODUCT_SCOPE.md` offline-first yanaşması phase-scoped tələb deyil, əsas
  məhsul prinsipi kimi ifadə edilir

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 35 `Authentication / Session`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `## 2. DI, Hosting & Infrastructure`, Authentication row
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `## 6. Authentication Strategy`
- `docs/03_TECH_STACK_AND_VERSIONS.md` — `Directory.Packages.props` / CPM enforcement section
- `docs/adr/ADR-0031-session-lifecycle-and-refresh.md` — Phase 1 session semantics
- `docs/adr/ADR-0041-login-first-startup-policy.md` — login-first startup boundary
- `docs/adr/ADR-0008-deferred-plugin-activation.md` — plugin runtime login success-dən sonra
- `docs/adr/ADR-0042-release-gate-hardware-baseline.md` — password hash benchmark üçün baseline hardware
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-014` login submit → shell first paint release gate budget
- `docs/01_PRODUCT_SCOPE.md` — `## Əsas Məhsul Prinsipləri`
- `docs/01_PRODUCT_SCOPE.md` — `## İstifadəçi Modeli`
- `docs/adr/ADR-0055-protected-storage-scope-and-key-wrapping.md` — password hash, remember-me və session material üçün protected-storage scope
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — bu ADR-in credential
  storage və throttle detallarını amend edir və həmin bölmələr üçün current
  authority-dir; qalan local-first authentication qərarı bu ADR-də qalır

## Revision History

- **2026-04-28:** Password hashing kandidat siyahısı yenidən tərtib edildi
  (əvvəlki third-party package candidate removed; `Argon2id` default +
  PBKDF2-HMAC-SHA256 fallback; starter parameter floor, salt invariant and
  benchmark/latency guardrail əlavə olundu). Mövzu və local-first authentication
  invariantları dəyişmir.
- **2026-04-28 (hardening):** Decision section-a explicit `BCrypt.Net-Next`
  rejection rationale və PBKDF2 fallback seçim əsaslandırması əlavə olundu;
  review hardening zamanı rationale canonical ADR-ə köçürüldü.
- **2026-05-16:** Phase 1 first-user Owner bootstrap invariantı dəqiqləşdirildi:
  Owner all-grants permission state ayrıca `Permissions` row-u ilə yox,
  `Users.IsOwner=1` row-u ilə seed olunur; managed-user permission rows və
  operation registry sonrakı authorization scope-da qalır.

## 2026-08-31 Remembered-Login Amendment

**Tarix:** 2026-08-31 — **Authority:** `ADR-0058` § `Remembered login`

Bu ADR-in local-first authentication qərarı dəyişmir. `**Status:**` sətri artıq
credential-storage detallarının `ADR-0058` ilə amend olunduğunu bildirirdi, lakin
§ `Decision` daxilindəki retired cümlələr normativ prose kimi qalmışdı: "Phase 1-də
remember-me modeli single saved credential slot daşıyır", "slot yeni user/credential ilə
yazılır" və stored password üçün reveal semantics. Bölmə oxuyan reader Status-a deyil,
həmin cümlələrə əməl edir, ona görə mətn indi enforce olunan qaydaya uyğunlaşdırılıb:
slot yalnız normalize olunmuş username və non-secret slot identifier saxlayır, reveal
action yoxdur, legacy saved-password qeydləri idempotent migration ilə silinir.

Dəyişmir: throttle modeli, password hashing seçimi və parametrləri, first-user Owner
bootstrap invariantı, session semantics delegation-ı. Bu bölmə mövcud və enforce olunan
davranışın dəqiq ifadəsidir (`docs/specs/session-lifecycle.md`
§ `Remembered Login Qarşılıqlı Təsiri`), yeni qərar deyil.

## Branch Shared Mode Amendment

This ADR remains canonical for Standalone local/device-bound authentication.
Branch Shared authentication is owned by
`ADR-0057-mode-aware-authentication-boundary.md`.

Branch Shared mode does not create local credential hash mirrors or stale
permission caches. Login uses Branch Shared credential authority after server
identity, `branchDeploymentId`, schema and grant validation. First-login
Owner bootstrap is Standalone-only; Branch Shared first Owner provisioning is
performed during main-device BranchSetup flow.
