# Settings and About Spec

**Status:** P1 canonical spec
**Phase:** P1-core
**Sources:** `FR-003`, `FR-010`, `FR-024`, `FR-028`, `AC-016`, `AC-029`, `AC-030`, `AC-034`, `ADR-0028`, `ADR-0031`, `ADR-0038`, `ADR-0048`, `docs/specs/shell-ui.md`, `docs/specs/session-lifecycle.md`, `docs/specs/user-management.md`, `docs/specs/license-import.md`

## Məqsəd

Bu spec Settings bölməsinin minimum P1 category tree-sini, action entry point-lərini, permission gating qaydalarını və About support/privacy field-lərini bağlayır.

## Category Tree

Minimum P1 Settings tree:

| Category | P1 status | Canonical content |
|---|---|---|
| Appearance | P1-core | Theme detailed setting; TopToolbar quick switch eyni preference-i dəyişir. |
| Account | P1-core | Password change shortcut, saved login-i unut shortcut. Manual lock/logout burada duplicate edilmir. |
| Security / Session | P1-core | Auto-lock toggle və minute input. |
| User Management | P1-core | Permission-gated entry to User Management. |
| About | P1-core | Host/support məlumatları. License import/status yeri deyil. |
| Backup / Restore | P2 placeholder | Self-service copy/import deyil; P2 backup/restore spec-ə pointer. |

Backup/Restore P2 behavior is owned by `docs/specs/backup-restore-migration.md`; Settings/About may show safe status/metadata only and does not expose raw backup artifact content or key material.

## Settings apply model

| Category | Apply model | Notes |
|---|---|---|
| Appearance | Immediate apply | Theme və visual preference dəyişiklikləri eyni theme preference-i yeniləyir. |
| Account | Action-based | Password change və saved-login actions ayrıca command-dır. |
| Security / Session | Immediate or command-specific | Auto-lock/session preference dəyişiklikləri explicit control state-inə bağlıdır. |
| User Management | Action-based | CRUD və grant/revoke əməliyyatları explicit command və audit tələb edir. |
| About | Read-only | Support metadata və copy actions xaricində mutable setting yoxdur. |
| Backup/Restore | P2 placeholder | P1-də yalnız pointer və safe metadata ola bilər. |

## Entry Point Rules

- `Settings` authenticated shell bölməsidir və login olmadan açılmır.
- Settings core page bütün authenticated user-lərə görünür.
- Subpage/action-lar permission və session state qaydalarına görə görünür və ya disabled/denied olur.
- `Plugins` list/import üçün canonical yer TopToolbar `Plugins` bölməsidir.
- License import üçün canonical yer `Plugins > License`-dır; About və Settings license import yeri deyil.

## Appearance

TopToolbar theme switch quick action-dır. `Settings > Appearance` eyni theme preference üçün daha detallı/canonical ayar yeridir. P1-də light/dark theme mövcuddur; advanced palette və per-plugin theme P3-ə saxlanılır.

## Account

`Settings > Account` password change və saved login-i unut action-larına shortcut verə bilər. Bu action-ların canonical behavior-u `docs/specs/session-lifecycle.md` və `ADR-0038` ilə idarə olunur.

Manual lock və logout Profile menu-da qalır; Settings onları duplicate etmir.

## Security / Session

Auto-lock default passivdir. User aktivləşdirəndə timeout dəyərini dəqiqə ilə daxil edir. P1-də preset list məcburi deyil; numeric minute input kifayətdir.

## User Management

`Settings > User Management` yalnız uyğun permission/capability olan user üçün active entry olur. Owner all-grants semantics daşıyır. Managed user permission-u yoxdursa entry non-disclosing disabled/hidden ola bilər; actual operation guard `ADR-0048` və `docs/specs/user-management.md` ilə enforce olunur.

## About

About minimum fields:

| Field | Visible to Owner | Visible to managed user |
|---|---|---|
| App version | yes | yes |
| Build number/date | yes | yes |
| Host edition/channel | yes | yes |
| Device code redacted form | yes | yes |
| Installed plugin versions + status | yes | yes |
| Vendor/support contact | yes | yes |
| Copyright/legal line | yes | yes |
| Owner identity username/display | yes | neutral `About.OwnerStatus.Registered` resource key only |

About Dobby host üçün license status göstərmir və license import yeri deyil. Plugin license statusu `Plugins > License` və Plugin Details səthlərində qalır.

## Support code copy

About surface redacted support/device code göstərə və copy action təqdim edə bilər. Copy action raw hardware fingerprint, protected key material, token, session material, raw license payload və plugin secret məlumatı clipboard-a yazmamalıdır.

## About Version Source

About-da göstərilən host version build/project metadata-sından gəlir. `appsettings.json` bu dəyəri define edə bilməz; yalnız cached/display copy kimi istifadə oluna bilər. Eyni source export metadata, backup metadata və plugin-facing host info surface-ləri üçün də canonical qalır.

Settings və About ekranları loading, empty, success, error və confirmation feedback state-lərini shell-wide UI state qaydası ilə göstərir; raw internal error detail user-facing səthə çıxmır.

## Acceptance Hooks

- `AC-016` Settings theme/layout preference baseline-ını yoxlayır.
- `AC-029` Account/session actions ilə bağlı behavior-u yoxlayır.
- `AC-030` User Management entry və permission guard-ları yoxlayır.
- `AC-035` minimum category tree və About privacy field-lərini yoxlayır.

## Branch Shared Settings / About Addendum

Settings/About may show support-safe Branch Shared status such as deployment
mode, redacted device/support code, server profile display name and high-level
connection health when future runtime implementation opens.

It must not show PostgreSQL passwords, protected credential values, raw license
payloads, raw hardware identifiers, full local paths or raw TLS pin secret
material. License import/status remains under `Plugins > License`, not About.
