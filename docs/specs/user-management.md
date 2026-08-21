# User Management Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-003`, `FR-018`, `AC-022`, `AC-030`, `ADR-0048`

## Məqsəd

Bu spec local user-lərin idarə olunması, permission guard-ları və Owner qorunması üçün P1 minimum davranışını bağlayır.

## Prinsiplər

- `Owner` all-grants semantics daşıyır və protected principal-dır.
- Managed user default-deny modelindədir.
- User Management ayrıca bypass admin surface deyil; operation-based authorization modelinə qoşulan built-in registry target-dir.
- Permission modeli grant-only/on-off işləyir; explicit deny record və deny toggle yoxdur.
- Managed user password reset Owner recovery flow deyil.

## Visibility

`Settings > İstifadəçilər` yalnız aşağıdakılara görünür:

- `Owner`;
- User Management target-i üçün uyğun `view` grant-i olan managed user.

Profile/account səthi bütün authenticated user-lər üçün yalnız öz account məlumatları və self-service password change kimi şəxsi əmrləri göstərir.

## Operation Mapping

Built-in User Management target-i minimum bu canonical verb-ləri elan edir:

| Əməliyyat | Canonical verb |
|---|---|
| User siyahısını və detail-i görmək | `view` |
| Managed user yaratmaq | `create` |
| Managed user məlumatını redaktə etmək | `update` |
| Managed user deactivate/reactivate etmək | `delete` |
| Managed user password reset etmək | `execute` |
| Managed user-ə permission vermək və ya söndürmək | `grant` |

Unsupported verb UI-da toggle kimi göstərilmir.

## Validation Layering

User Management əməliyyatları üçün validation üç qatda baş verir:

- **ViewModel validation** UX rahatlığı üçündür: form input-larını dərhal göstərir, lakin yeganə müdafiə xətti deyil və bypass edilə bilər;
- **Service validation** məcburidir: hər User Management service operation-i input-ları öz daxilində yenidən validate edir (məcburi sahələr, format, length, business rule); mutating/security-sensitive əməliyyat (`create`, `update`, `delete`, `execute`, `grant`) yalnız service validation keçəndən sonra icra olunur;
- **DB constraints** son müdafiə xəttidir: schema-level NOT NULL, UNIQUE, foreign key və check constraint-ləri yuxarı qatların boşluqları üçün backstop verir.

UI validation-a güvənmək service-level qoruma yerinə keçə bilməz; service validation atlana bilməz.

## Credential Validation

Local username login identifier-i ASCII-only canonical formadadır: `a-z`, `0-9`, `_`, `-`, `.` qəbul edilir. Username trim edilir, lowercase normalize olunur, case-insensitive unique saxlanır və 3-64 simvol aralığında olmalıdır.

Azərbaycan hərfləri və tam lokal adlar display name/profile metadata field-lərində dəstəklənir; login username-i lokal display name deyil.

Password policy P1-də deliberately simple-dır: boş password qəbul edilmir, minimum 1 simvol və maksimum 128 simvol tələb olunur. Composition/complexity rule, password rotation və current-password-on-reset tələbi yoxdur; password storage və hashing floor-ları `docs/03_TECH_STACK_AND_VERSIONS.md` Security Floors section-ına tabedir.

## Owner Guard

Managed user heç bir permission kombinasiyası ilə:

- `Owner`-i deactivate/reactivate edə bilməz;
- `Owner` password reset edə bilməz;
- `Owner` məlumatını, statusunu və permission-larını dəyişə bilməz;
- Owner transfer/replacement flow-u aça bilməz.

Owner transfer/replacement P2/P3 vendor-assisted provisioning/migration mövzusudur.

## Owner UI protection reminder

Owner protected principal-dır. Owner sətri seçildikdə permission editor info-only panel kimi render olunur; grant/revoke action UI-da göstərilmir.

## Permission Guard

Managed user:

- özünə permission əlavə edə bilməz;
- özündə olmayan permission-u başqasına verə bilməz;
- unknown target və ya unsupported verb üçün fail-closed deny alır;
- deny səbəbinin detallı host-side izahını UI/plugin-facing mesajda görmür.

Deny qərarları audit trail-ə yazılır.

UI-da command gizlətmək, disable etmək və ya entry-ni göstərməmək yalnız UX rahatlığıdır. Hər protected User Management operation-i service/operation boundary-də grant yoxlamasından keçməlidir; direct service invocation grant olmadan reject olunur.

## User CRUD

P1 minimum əməliyyatlar:

- create managed user;
- update managed user display/account metadata;
- reset managed user password;
- deactivate managed user;
- reactivate managed user;
- grant/revoke permission toggle-ları.

Managed user deactivate ediləndə həmin user üçün future login deny edilir və active session varsa növbəti protected operation-dan gec olmayaraq invalid edilir. Deactivation Owner protection qaydalarını bypass etmir.

User silinməsi fiziki data delete kimi yox, lifecycle/state dəyişikliyi kimi dizayn edilməlidir. Hard delete ayrıca data-retention qərarı tələb edir.

## First-User Owner Bootstrap

Clean install zamanı heç bir local user yoxdursa, ilk uğurlu login həmin user-i
`Owner` edir. Bu bootstrap `Users`, `Permissions` və `AuditLog` state-lərini tək
transaction kimi seed etməlidir:

- Owner user record-u yaranır;
- Owner all-grants/protected-principal permission state-i yaranır;
- `user.create` və ya `user.activate` audit trail entry-si yaranır.

Phase 1-də Owner all-grants/protected-principal permission state-i ayrıca
`Permissions` row yaratmaqla yox, `Users.IsOwner=1` row-u ilə təmin olunur.
Owner Guard, bootstrap və permission editor-in info-only davranışı eyni
`Users.IsOwner=1` protected-principal mənbəyindən oxunmalıdır; managed-user
permission rows, registry/verb inventory və runtime operation authorization
sonrakı permission-runtime scope-da qalır.

Bu üç addımdan hər hansı biri uğursuz olarsa transaction rollback olunur və partial
Owner/security state saxlanılmır.

## Password Reset və Recovery Ayrımı

Managed user password reset:

- User Management əməliyyatıdır;
- uyğun `execute` permission tələb edir;
- target user active session-ı varsa invalid edir;
- audit event yaradır.

Owner recovery:

- login/recovery səthindən başlayır;
- `.dobbyrecoveryrequest` və `.dobbyrecovery` artifact flow-u istifadə edir;
- Admin Tool və vendor signature tələb edir;
- User Management içində managed user reset ilə qarışdırılmır.

## Audit

Minimum audit event-lər:

- `user.create`;
- `user.update`;
- `user.password.reset`;
- `user.activate`;
- `user.deactivate`;
- `permission.grant`;
- `permission.revoke`;
- `permission.denied`;
- `owner.protection.denied`.

Audit payload minimumu `ADR-0019`-a tabedir.

## Acceptance Hooks

- `AC-022` registry/verb/no-deny/Owner-protection permission behavior-u yoxlayır.
- `AC-030` User Management CRUD, Owner guard və managed user permission guard-larını yoxlayır.

## Branch Shared User / Permission Addendum

Standalone clean install first-login Owner behavior remains unchanged.

Branch Shared users, credentials and permissions are BranchShared authoritative
data. First Owner is provisioned during main-device BranchSetup flow; seller
devices cannot create the first Owner through local first-login.

Branch Shared devices do not keep local credential mirrors or stale permission
caches. Authorization decisions for shared operations require current Branch
Shared authority. When PostgreSQL is unavailable or validation fails, protected
shared operations fail closed.

## 2026-08 Hardening Amendment (Controlling)

- Self-service password change requires the current password or a separately
  approved reauthentication ceremony plus new-password confirmation.
- Owner/admin reset is a distinct audited privileged operation and invalidates
  affected sessions according to policy.
- Create, change/reset password, deactivate/reactivate and session-invalidation
  workflows commit the user state and mandatory audit/outbox evidence atomically.
  Injected commit failure must preserve the prior effective state.
- Password input and hashes never appear in audit, diagnostics, result details or
  UI notifications.
