# Session Lifecycle Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-023`, `FR-024`, `FR-031`, `AC-028`, `AC-029`, `ADR-0031`, `ADR-0038`, `ADR-0050`, `docs/specs/work-sessions.md`

## Məqsəd

Bu spec login sonrası user session davranışını, logout cleanup-ını, lock/auto-lock axınını və password change-in session-a təsirini tək yerdə bağlayır.

## Canonical Qaydalar

- Eyni Dobby prosesi daxilində yalnız bir active user session ola bilər.
- Login ekranı görünürsə, əvvəlki authenticated session artıq bitmiş sayılır.
- Remember-me authenticated session deyil; yalnız pre-auth login form rahatlığıdır.
- Logout current session-u bitirir; lock current session-u saxlayır.
- Work session-lar yalnız post-login process memory-də saxlanır; restart sonrası host restore P2-dir.
- Plugin-in real/draft/domain datası ayrıca plugin storage məsuliyyətidir.
- Single-instance coordinator login UI-dan əvvəl acquire olunur; pre-login consumer-lər direct SQLite/Dapper deyil, repository/application-service contract-ları istifadə edir.

## Session Açılışı

Uğurlu login zamanı host:

1. current user identity yaradır;
2. session material/protected session state yaradır;
3. permission cache-i həmin user üçün fresh başlayır;
4. plugin runtime və user-scoped plugin service state-i post-login mərhələdə açır;
5. failed-login throttle state-ini sıfırlayır.

First-run Owner bootstrap `FR-002` və `ADR-0041` ilə idarə olunur.

Login credential input validation `FR-032` və `docs/specs/user-management.md` Credential Validation section-ı ilə idarə olunur. Invalid username formatı və boş/uzun password login attempt başlamazdan əvvəl typed validation error verir; authentication failure-ları isə normal failed-login throttle qaydasına düşür.

## Pre-auth interaction details

Login surface açıldıqda username field fokuslana bilər. Enter yalnız form valid və submit enabled olduqda login attempt göndərir. Failed-login throttle countdown aktivdirsə submit disabled qalır və countdown localized text ilə göstərilir.

## Logout

Logout entry point-ləri:

- profile menu;
- lock screen-dəki logout action.

Logout app-i bağlamır; user-i login ekranına qaytarır.

Logout cleanup minimumu:

- current user identity silinir;
- session material/protected session state silinir;
- cached permission decisions təmizlənir;
- in-memory plugin services və user-scoped runtime state dispose/clear edilir;
- active work session varsa user-ə warning göstərilir;
- user təsdiqləyəndən sonra active work session-lar controlled close/cancel edilir.

Logout remember-me persistent username slot-unu silmir. Remembered username yalnız növbəti uğurlu login zamanı remember-me OFF seçildikdə persistent olaraq boşaldılır.

## Remember-Me Qarşılıqlı Təsiri

Remember-me qaydası `ADR-0058`-ə (və onun amend etdiyi `ADR-0038`-ə) tabedir:

- auto-login yoxdur;
- yalnız saved normalized username login formunda hazır görünə bilər; password heç vaxt saxlanmır, snapshot-lanmır və replay olunmur;
- password field hər process start-da boş başlayır; masked placeholder saxlanmış password-u əvəz edə bilməz;
- persistent slot yalnız uğurlu login zamanı remember-me OFF seçildikdə boşaldılır;
- hər uğurlu login saved slot-u replace edir: remember-me aktivdirsə yeni normalized username, passivdirsə boş dəyər.

Logout və auto-lock persistent clear etmir.

## Lock və Auto-Lock

Manual lock entry point-ləri:

- profile menu;
- status area və ya shell-in host-controlled quick action səthi.

Auto-lock default passivdir. User aktivləşdirəndə timeout dəyərini dəqiqə ilə verir.

Lock screen:

- current session context-i saxlayır;
- cari user üçün password field və unlock action göstərir;
- logout button göstərir;
- başqa user ilə birbaşa login flow-u göstərmir.

Başqa user login etmək üçün lock screen-də logout seçilir və login ekranına qayıdılır.

Advanced auto-lock preset/timer UI və lock screen visual polish P3 scope-dur; `docs/02_ARCHITECTURE_MAP.md` Future Scope cədvəlinə bax.

## Lock Screen Outcomes

Unlock uğursuz olarsa lock screen current session context-i saxlayır və typed, non-raw error göstərir. Deactivated user unlock edə bilməz; session invalid olur və login ekranına qaytarılır. Failed-attempt countdown varsa lock screen-də göstərilir.

Cancel current lock screen-i bypass etmir; user ya unlock edir, ya logout seçir.

## Lock screen interaction details

Password field ilkin focus ala bilər. Unlock primary action, logout secondary action kimi render olunmalıdır; action priority lock bypass yaratmamalıdır.

## Password Change

Self-service password change profile/account səthindən cari authenticated user üçün aparılır.

Uğurlu self-service password change:

- cari password və eyni new-password confirmation tələb edir;
- cari session-u invalid etmir;
- həmin user-in başqa active session-larını `password-change` səbəbi ilə invalid edir;
- current user-i login ekranına məcburi qaytarmır;
- növbəti login üçün yeni password tələb edir;
- active authenticated session tələb edir; login/pre-auth səthindən self-service
  password change yoxdur;
- remember-me slot yalnız username/account metadata saxlayır və seçimə görə həmin
  account ilə replace edilir və ya clear olunur; password materialı saxlanmır;
- session material yeni credential version metadata ilə rebind olunur;
- failure mesajları non-disclosing qalır və raw password/hash detail göstərmir;
- credential, current/other session, remembered-account, audit və outbox state-i
  bir atomik commit sərhədində dəyişir.

User Management daxilində başqa managed user üçün password reset recovery deyil. Əgər target user-in active session-ı varsa, həmin session invalid edilir.

Owner password recovery ayrıca vendor-assisted recovery flow-dur və `ADR-0058` ilə idarə olunur.

## Active User Deactivation

Managed user deactivate ediləndə həmin user üçün future login deny edilir. Əgər həmin user-in active session-ı varsa, session növbəti authorization-required əməliyyatdan gec olmayaraq invalid edilir və protected əməliyyat davam etmir.

Invalidated session user-i login ekranına qaytarır və session invalidation audit trail-ə düşür.

## Work Session Davranışı

Logout zamanı active work session varsa modal warning göstərilir. Təsdiqdən sonra:

- work session-lar close/cancel edilir;
- process-lifetime state təmizlənir;
- login ekranı açılır.

Lock/manual lock/auto-lock work session-ları close etmir. Unlock sonrası session-lar process memory-də qaldığı yerdən görünə bilər.

Minimize behavior, minimized strip metadata, navigation warning və restart boundary `docs/specs/work-sessions.md` ilə bağlanır.

## Audit

Minimum audit event-lər:

- `user.login`;
- `user.login.failed`;
- `user.logout`;
- `user.password.change`;
- `user.password.reset`;
- `session.lock`;
- `session.unlock`;
- `session.expired`;
- `session.invalidated`.

Diagnostics detallarını audit payload-u ilə qarışdırmaq olmaz; boundary `ADR-0019`-a tabedir.

## Acceptance Hooks

- `AC-028` logout cleanup, remember-me interaction və single active session behavior-u yoxlayır.
- `AC-029` password change, manual lock, auto-lock və lock screen behavior-u yoxlayır.
- `AC-019` work session minimize/restore, no-auto-minimize warning və restart boundary-ni yoxlayır.

## Branch Shared Session Addendum

Standalone session lifecycle remains local-first.

Branch Shared login/session creation requires Branch Shared authority to be
reachable and validated. The app validates server identity, `branchDeploymentId`,
schema/grants and credential authority before creating an authenticated shared
session.

If Branch Shared authority becomes unavailable during a session, protected
shared commands fail closed until authority is reachable and revalidated. The UI
may preserve in-memory state and show retry/reconnect status, but this does not
authorize new shared writes.

Initial reconnect policy values are owned by `ADR-0056` /
`docs/specs/postgres-deployment-ops.md` and remain Tier B placeholders until
runtime implementation opens.

## 2026-08 Hardening Amendment (Controlling)

- Remembered login persists only the normalized username/account selection. It
  never persists or reconstructs a password or password-equivalent proof.
- Process restart requires fresh authentication. A masked placeholder must not
  conceal a retained credential.
- Throttle state is durable and separately accounts for normalized-account and
  device/global abuse. Successful authentication clears only the policy-owned
  buckets; one account cannot reset or lock every other account.
- Credential warm-up, if retained, is cancellable, concurrency-bounded,
  short-lived and charged to the same attempt policy as final verification.
- The authenticated runtime is created only after session creation commits and
  is disposed on logout/expiry. Pre-auth work follows the closed boundary in
  `ADR-0061`.
