# 09. Build, Run və Release

**Tarix:** 2026-04-29
**Status:** P1 canonical support spec
**Scope:** Test layihə strukturu, developer command-ları, CI sequence, native encrypted SQLite provider proof, smoke-test release gate, publish/installer və upgrade/rollback policy

Bu spec Build/Run/Release üzrə test structure, developer command sequence, CI gate, native SQLite provider proof, smoke release gate və upgrade/rollback policy-nin canonical owner-idir.

## 1. Sənədin məqsədi

Bu sənəd Dobby solution-unun necə build, run, test və release olunduğunu canonicalise edir. `docs/03_TECH_STACK_AND_VERSIONS.md` tooling və versiya qərarlarının canonical owner-idir, `docs/07_ACCEPTANCE_CRITERIA.md` isə release davranışları üçün acceptance criteria saxlayır. Bu spec həmin sənədləri gündəlik developer flow və release gate-lərlə tamamlayır.

## 2. Sənədin authority-si

- Bu spec aşağıdakıların canonical owner-idir: test layihə strukturu (Unit/Integration/Smoke ayrıca), developer command-ları (restore, build, test, format, run, publish, installer), native encrypted SQLite provider proof, CI sequence, smoke-test release gate, upgrade və rollback policy.
- `docs/03_TECH_STACK_AND_VERSIONS.md` versiya/tooling qərarları üçün üstündür; bu sənəd həmin versiyaları işlədən komandaları və CI-i canonicalise edir.
- Acceptance criterion-lar `docs/07_ACCEPTANCE_CRITERIA.md`-də qalır; bu sənəd onların icra qaydasını və smoke-gate referansını verir.

## Repository / Solution Layout

Phase 1 skeleton physical layout bu sənədin canonical ownership-indədir. Project
ownership `docs/02_ARCHITECTURE_MAP.md`-də qalır; burada həmin ownership-in filesystem
path-ləri sadalanır.

Required P1 path groups:

```text
src/Dobby.Host/
src/Dobby.Host/Trust/
src/Dobby.Core/
src/Dobby.Contracts/
src/Dobby.PluginFramework/
src/Dobby.Security/
src/Dobby.Licensing/
src/Dobby.Configuration/
src/Dobby.Localization/
src/Dobby.Navigation/
src/Dobby.Messaging/
src/Dobby.Application/
src/Dobby.Diagnostics/
src/Dobby.Audit/
src/Dobby.UI/
src/Dobby.Shell/
src/Dobby.SDK/
src/Dobby.Admin/
src/Dobby.Infrastructure/
tests/Dobby.UnitTests/
tests/Dobby.IntegrationTests/
tests/Dobby.SmokeTests/
samples/Dobby.Sample.Plugin/
tools/sign-plugin-package/
tools/create-plugin-license/
tools/create-recovery-response/
installer/
```

Optional P1 debug/smoke wrapper:

```text
tools/verify-plugin-package/
```

`verify-plugin-package` is optional P1 debug/smoke support. Install-time package
verification remains the required trust gate. A standalone verifier becomes
required only if release automation explicitly requires standalone verification
evidence.

### Initial P1 Skeleton Exclusions

Unless a later human-approved implementation phase changes scope, the initial P1
skeleton must not create:

- `src/Dobby.Update/`
- `src/Dobby.BranchSetup/`
- `src/Dobby.Infrastructure.Postgres/`
- Npgsql runtime adapters or packages
- BranchSetup executables or installer flows
- live PostgreSQL integration tests
- Branch Shared runtime migrations or credential provisioning flows
- Branch Shared `ErrorCode` identifiers in the initial `Dobby.Core/ErrorCodes`
  registry

`Dobby.Update` is P2; Phase 1 supports manual local replacement only. Internet
update checks, signed self-update, rollback, and update polling are P2.

Branch Shared runtime and customer-release readiness remain future/Tier B under
ADR-0056, ADR-0057, and the Branch Shared specs.

Reopen only if human-approved scope explicitly includes host update or Branch
Shared runtime implementation in the current phase.

### Root files

Skeleton commit 1 repo root-da aşağıdakı faylları yaratmalıdır. Tooling və version qərarlarının canonical owner-i `docs/03_TECH_STACK_AND_VERSIONS.md` olaraq qalır; bu section yalnız physical path ownership-i bağlayır.

`global.json`, `Directory.Packages.props` və package-bearing `.csproj` faylları
human-approved toolchain/package pin list olmadan yaradılmamalıdır. Pin list
verilməyibsə skeleton həmin addımda stop condition yazır; agent SDK patch-i və ya
NuGet version-ları özü seçmir. Package-bearing file creation
`docs/03_TECH_STACK_AND_VERSIONS.md` `P1 Toolchain / Package Pin Gate` tier
policy-sinə və hər row üçün approved `ReferenceStance` / `ApprovedVersion`
qaydalarına tabedir.

| Path | Owner / purpose |
|---|---|
| `Dobby.sln` | Tək solution; bütün P1 source, test, sample və admin/tool project-lərini daşıyır. |
| `global.json` | .NET SDK feature-band pinning; exact patch human-approved toolchain/package pin list-dən götürülür. |
| `Directory.Build.props` | Shared build properties: nullable, implicit usings, warnings-as-errors; TFM per-project `.csproj`-larda qalır. |
| `Directory.Packages.props` | Central Package Management; NuGet version-larının yeganə repo-level source-u. Skeleton commit 1 bu faylı yalnız `docs/03_TECH_STACK_AND_VERSIONS.md` human-approved pin list və package-reference stance-lərinə əsasən yarada bilər. |
| `.editorconfig` | Formatting, analyzer və naming convention-lar üçün IDE/CI source. |
| `.gitignore` | Build output, local profile/test artifacts, package output və non-canonical scratch folders üçün ignore qaydaları. |
| `README.md` | Repo entry point; docs index və local developer command-larına yönləndirir. |

Test layout exactly three evidence-split projects uses `tests/Dobby.UnitTests/`,
`tests/Dobby.IntegrationTests/` and `tests/Dobby.SmokeTests/`. Tests may mirror source
ownership with folders/classes inside those projects, but the physical test project
layout is not one test project per production project.

## 3. Test Project Structure

Phase 1 test projects stay split by evidence type:

| Project kind | Purpose | May touch real host runtime? | Release evidence |
|---|---|---:|---|
| Unit | Pure business rules, DTO mapping, validators, command handlers, small service rules | no | required for every PR |
| Integration | SQLite/provider, storage migration, auth/session, plugin package validation, host service boundaries | yes, test instance only | required before release candidate |
| Smoke | Installed/published host launch, login-first path, shell first paint, sample plugin/package path, installer upgrade path | yes, release-like artifact | required for release acceptance |

Unit tests must not depend on local user profile state. Integration and smoke tests use isolated temp/profile roots and clean up after the run.

## 4. Developer Commands

Canonical local command order:

Bu command-lar yalnız `Dobby.sln`, project files, `global.json` və package
metadata human-approved pin list və package-reference stance-lərinə uyğun
yaradılandan sonra runnable sayılır. Pin list yoxdursa, docs command order
canonical qalır, amma skeleton agent package-bearing file yaratmadan stop
condition yazmalıdır.

```powershell
dotnet --info
dotnet restore .\Dobby.sln
dotnet build .\Dobby.sln -c Debug --no-restore
dotnet test .\Dobby.sln -c Debug --no-build
dotnet format .\Dobby.sln --verify-no-changes
dotnet run --project .\src\Dobby.Host\Dobby.Host.csproj -c Debug
```

Release candidate command order:

```powershell
dotnet restore .\Dobby.sln
dotnet build .\Dobby.sln -c Release --no-restore
dotnet test .\Dobby.sln -c Release --no-build
dotnet publish .\src\Dobby.Host\Dobby.Host.csproj -c Release
```

Portable support/dev folder command:

```powershell
.\tools\publish-portable\publish-portable.ps1
```

The portable command writes an ordinary self-contained folder to
`artifacts/publish/portable/Dobby.Host/` for offline/manual testing. It does not
produce an archive and does not replace MSIX as the primary customer deployment
format.

If a command path changes during implementation, this document is updated in the same change that changes the project path.

## 5. CI Sequence

CI runs the same evidence classes in order:

1. SDK/tooling check (`global.json`, .NET SDK, package restore).
2. Static checks (`dotnet format --verify-no-changes`, warning-as-error build).
3. Unit tests.
4. Integration tests with isolated storage root.
5. Native encrypted SQLite provider proof.
6. Minimal scripted operator artifact production for sample `.dobbyplugin` and
   detached `.dobbylicense` smoke fixtures when plugin/license gates are in
   scope.
7. Automated static packaging scan verifies customer artifacts do not contain dev/QA
   trust material, dev sample plugin packages or dev sample licenses.
8. Smoke tests against the publish/installer artifact selected for the candidate.
9. Artifact packaging and evidence archive.

Later steps do not run if an earlier gate fails. A release candidate cannot pass using an artifact that was not produced by the current CI run.

## 6. Native Encrypted SQLite Provider Proof

Release verification must prove the `ADR-0054` selected DB-at-rest provider is the provider actually loaded by the host. The proof includes:

- package/native dependency inventory for the release artifact;
- startup-time provider-enabled activation probe;
- SQLite connection guardrail check from `AC-038`;
- failure when provider-disabled, provider-bypass or unprotected WAL/journal/temp configuration is detected.

The proof may be implemented as an integration test, smoke probe or startup diagnostic assertion, but it must run before release acceptance is marked complete. Initial skeleton commit 1 may remain provider-neutral; in that case it must not claim native encrypted SQLite provider proof or production DB-at-rest provider package evidence.

The proof must also confirm that release artifacts do not rely on
`SQLitePCLRaw.bundle_*` encrypted bundle packages as the default provider path.
If skeleton commit 1 does not pin `Microsoft.Data.Sqlite.Core` +
`SQLitePCLRaw.core`, the package set remains an explicit deferred
`P1 Persistence Materialization Pass` / security-round item and cannot be used as
release evidence until the probe passes. If skeleton commit 1 is provider-neutral,
it must not add production DB-at-rest provider packages to `Directory.Packages.props`.

## 7. Smoke-Test Release Gate

Release smoke evidence includes:

- host process starts and shows login without plugin activation or heavy background work before login;
- first-user/Owner bootstrap path works on a clean profile;
- login success opens shell Home with built-in navigation;
- sample/test plugin package can be installed and activated only through host-approved contracts;
- installer upgrade from the previous release candidate preserves `%LocalAppData%/Dobby` user data, audit data, license cache, permissions and user preferences.

Customer production packages must not include the sample plugin by default. The sample plugin is produced as a separate dev/QA artifact and may be installed only through explicit test/smoke flow. Release packaging verification must fail if the customer MSIX/portable artifact contains the sample plugin package, sample license, preinstalled sample plugin runtime folder, dev/QA public key or dev/QA trust profile metadata. This verification is an automated static scan in CI; manual reviewer attestation is not sufficient release evidence.

Admin Tool smoke tests are required only when an Admin Tool artifact is in the current release scope. Future Admin Tool behavior cannot be used as evidence for the current host release.

## Release evidence additions

Release candidate evidence mövcud current-CI artifact və warning-as-error gate-lərini təkrarlamadan aşağıdakı əlavə boşluqları da göstərməlidir:

Acceptance stage vocabulary is owned by `docs/07_ACCEPTANCE_CRITERIA.md`.
Skeleton-local evidence is only `initial-skeleton` evidence; P1 release
acceptance uses the current release candidate artifact and `P1-release-gate`
evidence.

Canonical generated evidence root is `artifacts/release-evidence/<build-id>/`. Release candidate evidence files are generated artifacts, not hand-written canonical docs. Minimum named evidence files:

- `artifacts/release-evidence/<build-id>/auth-kdf-selection.md` for `AC-051`;
- `artifacts/release-evidence/<build-id>/storage-provider-proof.md` for `AC-042`, `AC-037` and `AC-038`;
- `artifacts/release-evidence/<build-id>/packaging-static-scan.md` for sample/dev/QA trust exclusion;
- `artifacts/release-evidence/<build-id>/startup-performance.md` for `AC-014`.

Skeleton-time local evidence may use `artifacts/release-evidence/skeleton-local/`, but release candidate acceptance must use the current CI/release build id.

- Skipped test varsa səbəbi release evidence-də explicit qeyd edilir.
- App icon multi-size `.ico`, application logo və required host asset-lər publish/installer artifact-da mövcuddur.
- `.dobbyplugin` package validation, signature verification və optional `.dobbylicense` import smoke ayrı nəticələr kimi saxlanılır.

## 8. Publish və Installer

MSIX is the primary installer path. MSI is fallback only when MSIX cannot satisfy the deployment scenario and the fallback is explicitly approved for that release.

Publish artifacts include:

- host binaries and managed dependencies;
- native provider binaries required by `ADR-0054`;
- manifest/package metadata needed for support;
- no user profile data, no license artifacts, no device secrets and no local DB files.

Installer signing, package identity and artifact naming are release evidence and are stored with the build output.

## Operator artifact flow notes

Plugin package və license flow cari artifact modelindən istifadə etməlidir:

1. plugin publish;
2. manifest schema validation;
3. `.dobbyplugin` build;
4. package signature verification;
5. detached `.dobbylicense` creation/import smoke yalnız həmin release scope-da varsa.

Köhnə `.sppkg`, `.sppkg.sig` və `.entitlement` artifact adları release flow-a qaytarılmır.

### Minimal scripted operator artifact handoff

P1 smoke/release evidence üçün full Admin UI tələb olunmur, amma artifact path
derivable olmalıdır. CI və ya release operator aşağıdakı scripted flow-dan
istifadə edir:

```powershell
.\tools\sign-plugin-package\sign-plugin-package.ps1 `
  -InputPath .\artifacts\sample-plugin\unsigned `
  -SigningProfile dev-qa `
  -OutputPath .\artifacts\sample-plugin\signed\sample.dobbyplugin

.\tools\create-plugin-license\create-plugin-license.ps1 `
  -PluginPackage .\artifacts\sample-plugin\signed\sample.dobbyplugin `
  -SigningProfile dev-qa `
  -CustomerId default `
  -DeploymentId default `
  -DeviceBindingFixture smoke `
  -OutputPath .\artifacts\sample-plugin\license\sample.dobbylicense
```

Script adları və folder-lər implementation phase-də bu sənədlə eyni patch-də
yaradılmalıdır. Flow yalnız dev/QA smoke materialı üçündür; customer production
MSIX/portable artifact sample plugin-i və ya sample license-i default şəkildə
daşımamalıdır.

Yuxarıdakı PowerShell option adları illustrative smoke flow-dur, canonical public
option contract deyil. Folder/script adları, `Dobby.Admin.exe --cli <command>`
invocation modeli, exit code, stdout və stderr davranışı stable-dir; exact option
names və option sets yalnız `docs/specs/admin-tool.md`-də future
human/tooling-approved option table bağlandıqda freeze olunur.

Skeleton-time auth KDF selection evidence must be written to `artifacts/release-evidence/skeleton-local/auth-kdf-selection.md` and later to `artifacts/release-evidence/<build-id>/auth-kdf-selection.md` for release candidates. It records the selected algorithm (`Argon2id` or PBKDF2 fallback), package-health/license check result, configured floor values and measured login latency on the `ADR-0042` baseline. Open-ended tentative wording is not release evidence.

## 9. Static and Dynamic Scan Tooling

Release-blocking static/dynamic gates concrete commands saxlamalıdır:

| AC | Gate | Canonical command/evidence |
|---|---|---|
| `AC-043` | Plugin reference boundary | `dotnet list .\Dobby.sln reference` və ya skeleton-da yaradılan assembly-reference walker plugin project-lərinin yalnız `Dobby.Contracts` / `Dobby.SDK` reference etdiyini sübut edir. |
| Architecture map dependency boundary | Host/internal reference graph | Skeleton-time assembly-reference walker verifies `docs/02_ARCHITECTURE_MAP.md` dependency table and `ADR-0040` `HD-008` / `DM-095` per-row infrastructure ownership agree: plugin-visible assemblies do not reference `Dobby.Infrastructure`, and any owner-project direct reference to `Dobby.Infrastructure` is explicit in the architecture map. |
| `AC-046` | UI responsiveness / no UI-thread blocking | Smoke instrumentation long-running plugin install, license import və storage operation-ları zamanı UI dispatcher heartbeat-in kəsilmədiyini və operation-ların background operation contract-dan keçdiyini sübut edir. |
| `AC-047` | UTF-8 committed text scan | Repository text scan docs/source/resource folder-larında UTF-8 olmayan faylları fail edir; binary allow-list yalnız image/icon/package artifact-ları üçündür. |
| Dev/QA trust exclusion | Customer artifact trust hygiene | Automated static scan verifies customer MSIX/portable artifacts do not include dev/QA trust material, dev sample plugin package, dev sample license or preinstalled sample runtime folder. |

Skeleton round bu gate-lər üçün exact script path-lərini `tools/` altında
yaradırsa, bu section eyni patch-də path-lərlə yenilənməlidir.

## 10. Upgrade və Rollback Policy

Installer upgrade replaces installer-managed application artifacts only. `%LocalAppData%/Dobby` is not an installer target and must not be deleted or overwritten by upgrade, repair or uninstall.

Rollback scope is limited to installer-managed binaries and packaged config. User data recovery belongs to backup/restore scope, not installer rollback.

Upgrade verification must show that host data, audit log, license cache, users/permissions and user preferences survive `v(N) -> v(N+1)` upgrade.

## 11. References

- Sections 3, 5 and 7 — Unit/Integration/Smoke test split, CI sequence and smoke-test release gate.
- Repository / Solution Layout — physical `src/`, `tests/`, `samples/`, `tools/` and `installer/` path ownership.
- Sections 4 and 6 — canonical developer commands and native encrypted SQLite provider proof, including SQLite connection guardrail evidence.
- Section 10 — installer upgrade and rollback policy.
- `docs/03_TECH_STACK_AND_VERSIONS.md` (tooling və versiya canonical owner)
- `docs/07_ACCEPTANCE_CRITERIA.md` — release acceptance criteria, including SQLite guardrails and test evidence gates.

## Branch Shared Release Readiness Addendum

Branch Shared Mərhələ 1 is documentation-only. It creates/updates ADRs, specs
and schemas but does not create `src/Dobby.Infrastructure.Postgres`,
`src/Dobby.BranchSetup`, an Npgsql adapter, a BranchSetup executable or live
PostgreSQL tests.

Repository / Solution Layout may list future rows for:

- `Dobby.Infrastructure.Postgres` as deferred Branch Shared runtime adapter;
- `Dobby.BranchSetup` as deferred customer-side setup executable;
- `Dobby.BranchSetup.Core` as namespace/library ownership only, not a separate
  project unless later approved.

Branch Shared customer-release readiness is Tier B and requires evidence from
`docs/specs/postgres-deployment-ops.md`: selected setup artifact/runbook,
PostgreSQL 16.x policy, firewall/port guidance, TLS pin proof, schema/grant
smoke, backup/restore rehearsal, credential rotation and secret-redaction
evidence.

`HD-005` is resolved by `DM-092`: release docs must say "selected PostgreSQL
setup artifact/runbook" unless Tier B customer-release work explicitly chooses
and evidences a bundled PostgreSQL packaging model.
