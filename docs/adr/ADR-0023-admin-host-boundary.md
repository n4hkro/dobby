# ADR-0023: Admin-Host Boundary

**Status:** Accepted (recovery-artifact and license-generation references now execute under ADR-0058 recovery v2 and ADR-0060 license v3; the admin-host boundary itself is unchanged)
**Date:** 2026-04-24

## Context

`02_ARCHITECTURE_MAP.md` Row 84 (`Dobby.Admin`), Row 85 (`Admin Application Composition`), Row 86 (`Owner Recovery Artefakt Generation`) və Cədvəl 3 `Dobby.Admin` risk sətri admin application-in host ilə boundary-sini Phase 1 blocker kimi göstərir. Engineering Convention `Admin DRY Qaydası` business logic duplication-i qadağan edir və `Admin-in Təbiəti` convention-ı `Dobby.Admin ayrıca UI-dir, ayrıca domain deyil` qaydasını qoyur.

Audit `C-02` tapıntısı göstərdi ki, `ADR-0039 Owner Password Recovery` recovery artefakt generation-ını `Dobby.Admin`-ə bağlayır və recovery Phase 1 məhsul minimumu olduğu üçün minimal admin component P1-core-dur. Full admin UI P1-if-time qalır.

Bu ADR admin-host boundary-ni və Phase 1 scope split-ni rəsmiləşdirir.

## Decision

Phase 1 admin-host boundary modeli:

### 1. `Dobby.Admin` standalone executable

- `Dobby.Admin` ayrıca WPF executable-dır (`WinExe`), host ilə deploy olunmur.
- Admin build artifact vendor tərəfdə operator maşınında işləyir; müştəri maşınlarında mövcud deyil.
- Admin runtime data və konfiqurasiya host-dan ayrıdır; ortaq state yoxdur.

### 2. Admin vs Host responsibility split

Admin-in **unique** responsibility-ləri (host-da yoxdur):

- Customer CRUD (add, edit, delete customer records — vendor internal)
- Plugin package signing flow (plugin binary-ni vendor master key ilə imzalamaq — `ADR-0032`)
- License artifact generation flow (plugin-specific license artifact-ların yaradılması — `ADR-0051`)
- Owner Recovery Artefakt Generation (`.dobbyrecoveryrequest` inputundan user seçmədən signed `.dobbyrecovery` response artifact-ın imzalanması — `ADR-0039`)

Admin-in **shared** responsibility-ləri (host ilə eyni kod):

- Plugin manifest validation (`ADR-0036` schema)
- Signature verification (`ADR-0032`)
- Audit trail (`ADR-0019`)
- Localization (`Dobby.Localization`)
- Theme və UI primitives (`Dobby.UI`)

Shared behavior duplication deyil, reuse ilə təmin olunur: Admin və Host eyni `Dobby.Contracts`, `Dobby.Application`, `Dobby.Security`, `Dobby.UI`, `Dobby.Localization` project-lərinə reference saxlayır.

### 3. Phase 1 scope split (C-02 consensus)

- **Phase 1 P1-core** (məcburi): `Dobby.Admin / Owner Recovery Artefakt Generation` namespace-i (Row 86). Recovery artefakt signing minimum CLI və ya basit UI path-i `P1-core`-dur çünki `01_PRODUCT_SCOPE.md` `## Core vs Plugin Boundary` və `## Phase 1 Məhsul Sərhədi` owner recovery-ni Phase 1 məhsul minimumunda saxlayır. Bu flow input `.dobbyrecoveryrequest` faylından signed `.dobbyrecovery` response yaradır və operatora ayrıca user seçdirmir.
- **Phase 1 P1-if-time**: `Dobby.Admin / Admin Application Composition` namespace-i (Row 85). Full admin UI (customer CRUD, plugin signing wizard, license generator wizard və s.) vaxt qalsa Phase 1-də tamamlanır; release blocker deyil.
- **P2**: Admin UI polish, batch operations, audit viewer, çox vendor dəstəyi.

### 4. Admin-in host-un biznes məntiqini kopyalamaması

- Admin `Dobby.Admin` daxilində business rule yazmır; `Dobby.Application` use case-lərini reuse edir.
- Admin-in UI layer-i (`Dobby.Admin.Composition`) yalnız user interaction + flow orchestration-ı saxlayır; domain logic `Dobby.Application`-dadır.
- Plugin signing və license generation kimi cryptographic işlər `Dobby.Security / Plugin Trust Verification` namespace-indəki shared flow-ları istifadə edir (yeni private key access capability ilə ADR-0033 uyğun).

### 5. Private key access

- Admin build vendor private signing key-ə runtime-da access edə bilən yeganə Dobby application-dır.
- Private key path (`appsettings.json` və ya CLI arg) admin-in özündə konfiqurasiyadadır; hardcoded deyil.
- Host build isə yalnız vendor public key-ə access edir (`ADR-0033`).

### 6. Rejected alternatives

- **Admin as host plugin**: rədd edildi — admin müştəri maşınlarında deploy olunmur; plugin model deployment sadəliyinə xələl gətirir.
- **Admin as web app**: rədd edildi — offline-first prinsipi və solo vendor konteksti üçün WPF desktop standalone sadə və sufficient-dir.
- **Recovery generator not in admin (standalone CLI)**: rədd edildi — recovery artefakt generation plugin signing və license generation ilə eyni cryptographic workflow-u paylaşır; ayrıca CLI DRY pozar.

## Consequences

- `Dobby.Admin` proje `P1-core` statusuna yüksəlir, amma namespace səviyyəsində phase split saxlanılır (Row 85 `P1-if-time`, Row 86 `P1-core`).
- Phase 1 release-də minimal recovery generator (CLI və ya minimal UI) vendor tərəfdə istifadəyə hazır olmalıdır.
- Minimal recovery generator `.dobbyrecoveryrequest` faylını input kimi qəbul edib `.dobbyrecovery` response yaradır; generic target user picker və ya managed user reset flow-u daşımır.
- Full admin UI P1-if-time-dır; vendor operator Phase 1-də recovery üçün minimal CLI-dən istifadə edə bilər, plugin signing və license generation əllə PowerShell script-ləri və ya P1-if-time UI ilə icra edilir.
- Admin build pipeline vendor private key access tələb edir; admin build vendor tərəfdə tutulur, müştəri maşınlarında yoxdur.
- Admin UI-dəki business logic duplication code review-da bloklanır (Engineering Convention `Admin DRY Qaydası`).

## Cross-references

- `docs/02_ARCHITECTURE_MAP.md` — Row 84 `Dobby.Admin`, Row 85 `Admin Application Composition`, Row 86 `Owner Recovery Artefakt Generation`, Cədvəl 3 `Dobby.Admin` risk sətri, Engineering Conventions `Admin DRY Qaydası`, `Admin-in Təbiəti`
- `docs/adr/ADR-0032-plugin-signature-trust-model.md` — plugin signing cryptographic flow
- `docs/adr/ADR-0033-signing-key-ownership.md` — vendor private key mülkiyyəti
- `docs/adr/ADR-0051-license-lifecycle-v2.md` — license artifact generation (superseded by `ADR-0060` license v3)
- `docs/adr/ADR-0039-owner-password-recovery.md` — recovery artefakt generation ownership (superseded by `ADR-0058` recovery v2)

## BranchSetup Boundary Amendment

`Dobby.Admin` remains vendor-side admin/signing/recovery tooling. It is not a
customer branch setup device and does not become the Branch Shared setup host.

Branch Shared setup is a separate customer-side context named
`Dobby.BranchSetup.exe` in `ADR-0057` and `docs/specs/onboarding-and-provisioning.md`.
BranchSetup may apply vendor/admin-signed restore or setup packages, but it does
not own vendor private signing keys.

Any statement that `Dobby.Host.exe` and `Dobby.BranchSetup.exe` ship together is
Tier B/customer-release packaging scope. The current Mərhələ 1 document pass
does not create BranchSetup implementation or installer artifacts.
