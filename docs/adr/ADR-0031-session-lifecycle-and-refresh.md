# ADR-0031: Session Lifecycle and Refresh

**Status:** Accepted
**Date:** 2026-04-22

## Context

Authentication və storage scope-ları üçün "session" sözünün sabit mənaya malik
olması vacibdir. Bu mərhələdə məqsəd tam auth məhsulu dizaynı deyil; Phase 1
minimum session lifecycle qərarını bağlamaqdır ki, refresh, remember-me və
session-derived storage kimi mövzular yanlış prioritet qazanmasın.

Arxitektura xəritəsində `active session` anlayışı artıq müəyyən olunub: host
tərəfindən etibarlı sayılan current authenticated session.

## Decision

`active session` — host tərəfindən etibarlı sayılan current authenticated
session-dır.

Phase 1 session modeli:

- refresh yoxdur
- invalid/expired session authenticated context-i təmizləyir
- session state etibarsız sayılır
- token varsa o da etibarsız sayılır
- user login flow-a qaytarılır

Bu minimum model Phase 1 üçün canonical session semantics-dir. Remember-me session refresh və ya long-lived authenticated session deyil; yalnız pre-auth login form rahatlığıdır və `ADR-0038` ilə tənzimlənir.

### Session expiry trigger semantics

`invalid/expired session` sözü aşağıdakı tetikleyicilərlə ifadə olunur:

1. **Absolute timeout:** session yaradıldığı andan `SessionAbsoluteTimeout` dəyəri keçdikdə `expired` hesab olunur. Phase 1 default: `8 saat`. Canonical config key `ADR-0027` içindəki `Dobby:Session:AbsoluteTimeoutMinutes`-dır.
2. **Idle timeout:** istifadəçinin son aktivlik qeydindən `SessionIdleTimeout` dəyəri keçdikdə `expired` hesab olunur. Phase 1 default: `30 dəqiqə`. Canonical config key `ADR-0027` içindəki `Dobby:Session:IdleTimeoutMinutes`-dır. Aktivlik qeydi host-level user input və ya explicit host API çağırışı ilə yenilənir.
3. **Explicit invalidation:** `Owner` password recovery uğurla başa çatdıqda, başqa user üçün admin/user-management password reset həmin user-in mövcud session-ını etibarsız etdikdə, istifadəçi logout edəndə və ya session material revoke edildikdə session dərhal `invalid` hesab olunur; timeout gözlənilmir.
4. **Storage tampering:** DPAPI-encrypted session material decrypt edilə bilmədikdə session `invalid` hesab olunur.

Expiry check-i `TimeProvider` üzərindən aparılır; test üçün `FakeTimeProvider` istifadə olunur. Expiry detection plugin-visible deyil — plugin boundary-də yalnız `ErrorCode.Unauthorized` və ya session-less execution contracts görünür.

### Logout and lock semantics

Phase 1-də eyni Dobby prosesi daxilində yalnız bir active user session ola bilər.
Yeni user login-i yalnız əvvəlki session logout/cleanup olandan sonra başlayır.

Logout app-i bağlamır; current authenticated context-i təmizləyir və user-i login
ekranına qaytarır. Cleanup minimumu:

- current user identity və session material silinir;
- cached permission decisions təmizlənir;
- in-memory plugin services və user-scoped runtime state dispose/clear edilir;
- active work session varsa user-ə warning göstərilir və təsdiqdən sonra həmin
  session-lar controlled close/cancel edilir.

Logout və auto-lock remember-me persistent slot-unu silmir. Saved credential yalnız
növbəti uğurlu login zamanı remember-me OFF seçildikdə persistent olaraq boşaldılır (`ADR-0038`).

Manual lock və auto-lock eyni lock screen-i istifadə edir. Lock screen current
session context-i saxlayır, amma interaction-u password unlock və ya logout ilə
məhdudlaşdırır. Lock screen-də ayrıca "başqa user ilə login" flow-u yoxdur; başqa
user login-i üçün user logout action-ı ilə login ekranına qayıtmalıdır.

Auto-lock default passivdir (`Dobby:Session:AutoLockEnabledByDefault = false`,
`ADR-0027`). User aktivləşdirəndə timeout dəyərini dəqiqə ilə verir; timeout expiry
lock screen açır, logout etmədiyi üçün remember-me və current session identity
persistent olaraq dəyişmir.

### Password change semantics

Self-service password change current authenticated user-in profile/account
səthindən aparılır, cari password tələb etmir və uğurlu dəyişiklik cari session-u
invalid etmir. Növbəti login yeni password tələb edir.

Bu qərar yalnız active authenticated session daxilində keçərlidir; login/pre-auth
səthindən self-service password change yoxdur. Cari password tələb edilməməsinin
kompensasiya nəzarətləri:

- operation current authenticated user identity-si ilə bind olunur;
- action `user.password.change` audit event-i yaradır;
- stored remember-me material köhnə password materialı ilə silent auto-relogin
  yaratmamalıdır; saved credential slot yeni credential ilə replace edilir və ya
  clear olunur;
- current session davam edir, amma session material yeni credential version metadata
  ilə rebind olunur;
- error-lar non-disclosing qalır və raw password/hash detail diagnostics/audit-ə
  yazılmır.

User Management daxilində başqa managed user üçün password reset recovery flow deyil.
Həmin user-in mövcud active session-ı varsa invalid edilir. `Owner` password
recovery isə Owner session materialını və recovery nonce state-ini ayrıca
invalid/rebind edir (`ADR-0039`).

## Consequences

- Storage və capability qərarları session-a bağlı əlavə scope güman etmir
- Auth/session implementation Phase 1-də daha kiçik və sabit qalır
- Logout və lock ayrı concern-lərdir: logout session cleanup edir, lock isə current
  session-u qoruyub unlock/logout seçimləri göstərir
- Remember-me authenticated session deyil və logout/auto-lock tərəfindən persistent
  clear edilmir
- Refresh və long-lived session davranışı lazım olarsa ayrıca sonrakı qərar kimi
  açılır, bu sənəd implicit genişlənmir

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — `Authentication / Session` row
- `docs/02_ARCHITECTURE_MAP.md` — `Session Lifecycle and Refresh` decision row
- `docs/02_ARCHITECTURE_MAP.md` — `Session Lifecycle Minimumu` convention
- `docs/adr/ADR-0030-host-persistence-and-plugin-storage-boundary.md` — session scope-un Phase 1-dən kənarda qalması
- `docs/adr/ADR-0038-local-first-authentication.md` — remember-me və login throttle auth rahatlığı
- `docs/adr/ADR-0050-in-memory-work-sessions.md` — logout zamanı active work session warning/cleanup boundary
- `docs/specs/session-lifecycle.md` — logout, lock, auto-lock və password change observable flow-ları
