# User Management Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-003`, `FR-018`, `FR-024`, `AC-022`, `AC-029`, `AC-030`, `NFR-025`,
`NFR-026`, `ADR-0048`, `ADR-0058`, `ADR-0061`

**Controlling decisions:** Password proof tələbi, self-service change ilə
privileged reset arasındakı fərq və credential state-in leak olunmaması
`ADR-0058`-in öhdəliyindədir; user/permission/audit mutation-larının atomic commit
qaydası `ADR-0061` ilə bağlanır. Owner recovery flow-unun normativ detalı
`docs/specs/session-lifecycle.md` § `Owner Recovery (v2)` bölməsindədir; bu spec
onu təkrar tərif etmir.

## Məqsəd

Bu spec local user-lərin idarə olunması, permission guard-ları və Owner qorunması üçün P1 minimum davranışını bağlayır.

## Prinsiplər

- `Owner` all-grants semantics daşıyır və protected principal-dır.
- Managed user default-deny modelindədir.
- User Management ayrıca bypass admin surface deyil; operation-based authorization modelinə qoşulan built-in registry target-dir.
- Permission modeli grant-only/on-off işləyir; explicit deny record və deny toggle yoxdur.
- Managed user password reset Owner recovery flow deyil.
- Self-service password change ilə privileged password reset iki ayrı əməliyyatdır:
  birincisi cari user-in password proof-unu tələb edir, ikincisi target user-in
  köhnə password-unu tələb etmir, lakin `execute` grant-i və audit tələb edir.
- User state, permission state və məcburi audit evidence-i tək atomic commit kimi
  yazılır; commit uğursuz olarsa əməliyyat success kimi publish edilmir
  (`ADR-0061`, `NFR-025`).
- Password, password proof, hash və verifier heç bir result detail-ində, audit
  payload-ında, diagnostics-də, log-da və UI notification-ında görünmür
  (`ADR-0058`, `NFR-026`).

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

Password policy P1-də deliberately simple-dır: boş password qəbul edilmir, minimum 1 simvol və maksimum 128 simvol tələb olunur. Composition/complexity rule və məcburi password rotation P1 scope-unda yoxdur; password storage və hashing floor-ları `docs/03_TECH_STACK_AND_VERSIONS.md` Security Floors section-ına tabedir.

Password proof tələbi əməliyyat tipinə görə fərqlənir (`ADR-0058`, `FR-024`,
`AC-029`):

| Əməliyyat | Proof tələbi |
|---|---|
| Self-service password change (cari user öz password-unu dəyişir) | cari password (və ya explicit təsdiqlənmiş ekvivalent reauthentication ceremony) **plus** yeni-password confirmation; UI-level onay bunu əvəz etmir |
| Privileged managed user password reset (`execute` grant) | target user-in köhnə password-u tələb olunmur; grant yoxlaması, audit event və target session invalidation tələb olunur |
| Owner recovery | password proof yerinə vendor-signed recovery artifact zənciri; detalı `docs/specs/session-lifecycle.md` § `Owner Recovery (v2)` |

Yanlış self-service proof non-disclosing deny qaytarır, durable login/unlock
throttle ledger-inə yazılır (`FR-022`) və hansı hissənin yanlış olduğunu
açıqlamır.

## Owner Guard

Managed user heç bir permission kombinasiyası ilə:

- `Owner`-i deactivate/reactivate edə bilməz;
- `Owner` password reset edə bilməz;
- `Owner` məlumatını, statusunu və permission-larını dəyişə bilməz;
- Owner transfer/replacement flow-u aça bilməz.

Owner transfer/replacement P2/P3 vendor-assisted provisioning/migration mövzusudur.

Owner Guard yalnız managed user-lərin Owner-ə toxunmasını deyil, Owner-in özünü
deactivate etməsini də bağlayır (`Self-Target Guard`); Owner heç bir yolla
login-ə buraxılmayan state-ə keçirilə bilməz.

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

## Self-Target Guard

Grant yoxlaması "kim" sualını cavablandırır, "kimə" sualını yox. Aşağıdakı
self-target halları grant mövcud olsa belə service boundary-də fail-closed deny
alır:

| Cəhd | Nəticə | Səbəb |
|---|---|---|
| `Owner` öz account-unu deactivate etmək | deny (`owner.protection.denied`) | Deactivated Owner login edə bilmir; Owner recovery yalnız credential reset edir, account state-i deyil, buna görə self-deactivation host-u bərpa olunmaz lockout-a salardı |
| Managed user öz account-unu deactivate etmək (`delete` grant olsa belə) | deny (`permission.denied`) | Self-lockout əməliyyat xətası kimi qarşılanır; deactivation başqa principal tərəfindən icra olunur |
| Caller-in özünü privileged password reset target-i seçməsi (`execute` grant olsa belə) | deny (`permission.denied`) | `execute` grant-ı self-service current-password proof tələbinin bypass-ına çevrilə bilməz (`ADR-0058`, `AC-029`); öz password-unu dəyişmək yalnız self-service change yolu ilə mümkündür |
| Managed user öz permission-larını dəyişmək (grant və ya revoke) | deny (`permission.denied`) | Self-escalation qadağası simmetrikdir; self-revoke da recoverable olmayan operational vəziyyət yaradır və yalnız başqa principal tərəfindən icra olunur |

Bu guard grant yoxlamasından **sonra**, mutation-dan **əvvəl** işləyir və UI-da
target-in gizlədilməsi ilə əvəz edilə bilməz. Deny non-disclosing qalır: caller
hansı guard-ın işlədiyini user-facing mesajdan çıxara bilmir, typed reason isə
audit trail-də saxlanılır.

Owner-in öz password-unu dəyişməsi self-service change yolu ilə mümkündür və bu
guard-a düşmür; Owner-in öz display/account metadata-sını redaktə etməsi də icazə
verilir.

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

## Password Change, Reset və Recovery Ayrımı

Self-service password change:

- profile/account səthindən cari user üçün işləyir və permission grant tələb etmir;
- cari password proof-u (və ya explicit təsdiqlənmiş reauthentication ceremony) və
  yeni-password confirmation tələb edir;
- uğurlu dəyişiklik cari session-u saxlayır, həmin user-in digər active session-larını
  invalid edir və `user.password.change` audit event-i yaradır (`FR-024`);
- credential update, session-version/disposition dəyişikliyi və audit evidence tək
  atomic commit kimi yazılır (`ADR-0061`).

Managed user password reset:

- User Management əməliyyatıdır;
- uyğun `execute` permission tələb edir;
- target user-in köhnə password-unu tələb etmir və Owner-i target seçə bilməz
  (`Owner Guard`);
- target user active session-ı varsa invalid edir;
- `user.password.reset` audit event-i yaradır;
- credential update, session invalidation və audit evidence eyni atomic commit-dədir.

Owner recovery:

- login/recovery səthindən başlayır və köhnə password tələb etmir;
- device-generated recovery key material, nonce/`requestId` binding və vendor-signed
  response tələb edir; normativ ardıcıllıq, reject vocabulary və audit sahələri
  `docs/specs/session-lifecycle.md` § `Owner Recovery (v2)` bölməsindədir;
- `.dobbyrecoveryrequest` və `.dobbyrecovery` artifact flow-u istifadə edir
  (`docs/schemas/dobby-recovery-request-v2.schema.json`,
  `docs/schemas/dobby-recovery-response-v2.schema.json`);
- Admin Tool və purpose-specific vendor recovery signing key tələb edir
  (`docs/specs/admin-tool.md`);
- User Management içində managed user reset ilə qarışdırılmır və managed user
  permission-ları ilə əldə edilə bilməz.

## Audit

Minimum audit event-lər:

- `user.create`;
- `user.update`;
- `user.password.change`;
- `user.password.reset`;
- `user.activate`;
- `user.deactivate`;
- `permission.grant`;
- `permission.revoke`;
- `permission.denied`;
- `owner.protection.denied`.

Audit payload minimumu `ADR-0019`-a tabedir. Password, password proof, hash,
verifier və recovery secret material audit payload-ına daxil edilmir; deny
event-ləri səbəbi typed reason kimi saxlayır, user-facing mesajda isə
non-disclosing qalır.

## Acceptance Hooks

- `AC-022` registry/verb/no-deny/Owner-protection permission behavior-u yoxlayır.
- `AC-029` self-service password change proof, confirmation, session davranışı və
  non-disclosing deny-i yoxlayır.
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

## Verification Obligations

Aşağıdakı maddələr behavior-first sübut tələb edir; source-text axtarışı, regex və
ya fayl mövcudluğu yoxlaması onları qapatmır (`ADR-0061`, `NFR-027`):

| Yoxlama | Minimum sübut |
|---|---|
| Self-service proof | Yanlış cari password ilə change cəhdi non-disclosing deny verir, password dəyişmir və throttle ledger-i artır |
| Privileged reset boundary | `execute` grant-i olmayan managed user-in direct service invocation-ı reject olunur; UI-nin command-i gizlətməsi sübut sayılmır |
| Owner guard | Managed user-in hər permission kombinasiyası ilə Owner-i reset/deactivate/dəyişmə cəhdinin service boundary-də fail-closed olması |
| Self-escalation | Managed user-in özünə və ya özündə olmayan permission-u başqasına verə bilməməsi |
| Self-target guard | `delete` grant-ı olan managed user-in öz account-unu deactivate edə bilməməsi, `execute` grant-ı olan user-in özünü privileged reset target-i seçə bilməməsi və Owner-in öz account-unu deactivate edə bilməməsi — hər üçü service boundary-də, UI gizlətməsindən asılı olmayaraq |
| Atomicity | Create/change/reset/deactivate və session-invalidation flow-larında injected commit failure-ın köhnə effective state-i saxlaması və success publish etməməsi |
| Bootstrap atomicity | First-login Owner bootstrap-ında `Users`/permission state/audit seed-inin qismən qalmaması |
| Session invalidation | Reset və deactivation-dan sonra target user-in active session-ının növbəti protected operation-dan gec olmayaraq invalid olması |
| Leak yoxlaması | Password/hash/verifier-in audit payload, diagnostics export, result detail və UI notification-larında olmaması |

## Cross-references

- `docs/05_FUNCTIONAL_REQUIREMENTS.md` — `FR-003`, `FR-018`, `FR-022`, `FR-024`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-022`, `AC-029`, `AC-030`
- `docs/adr/ADR-0048-operation-based-authorization.md` — permission/authorization modeli
- `docs/adr/ADR-0058-credential-and-recovery-hardening-v2.md` — password proof, throttle, recovery hardening
- `docs/adr/ADR-0061-login-first-runtime-and-atomic-mutation-boundary.md` — atomic mutation və behavior-first proof
- `docs/adr/ADR-0019-audit-vs-diagnostics-boundary.md` — audit payload boundary
- `docs/specs/session-lifecycle.md` — login/logout, throttle və Owner Recovery (v2)
- `docs/specs/storage-and-data-protection.md` — credential/permission persistence
- `docs/specs/admin-tool.md` — vendor-side recovery response generation
- `docs/03_TECH_STACK_AND_VERSIONS.md` — password hashing/security floor-ları

## Revision History

- **2026-08-27:** `Self-Target Guard` bölməsi əlavə edildi. Əvvəlki mətn grant
  yoxlamasını tərif edirdi, lakin caller-in özünü target seçdiyi halları
  bağlamırdı: `execute` grant-ı self-service current-password proof tələbinin
  bypass-ına çevrilə bilirdi, Owner öz account-unu deactivate edib host-u bərpa
  olunmaz lockout-a sala bilirdi və self-deactivate/self-revoke halları normativ
  deyildi. Owner Guard-a self-deactivation qadağası, Verification Obligations-a
  behavior-first self-target sətri əlavə edildi.
- **2026-08-25:** `ADR-0058` reconciliation. "current-password-on-reset tələbi
  yoxdur" mətni silindi və self-service change / privileged reset / Owner recovery
  üçün ayrı proof cədvəli əlavə edildi; atomic commit, leak prohibition,
  `user.password.change` audit event-i, `AC-029` hook-u və behavior-first
  Verification Obligations yazıldı; `2026-08 Hardening Amendment` bölməsi ləğv
  edildi, çünki qərarları normativ mətnə köçürüldü.
