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
tools/verify-plugin-package/
installer/
```

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
mövcuddur və package-bearing file creation stop condition-u Phase A/B approval-ları
ilə bağlanmışdır (`artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md`
§ `Resolved Stop Conditions`). Agent hələ də SDK patch-i və ya NuGet version-ı özü
seçmir: **yeni** package, version dəyişikliyi və ya package-source dəyişikliyi
ayrıca human approval tələb edir və approval gəlməyənə qədər stop condition-dur.
Package-bearing dəyişikliklər `docs/03_TECH_STACK_AND_VERSIONS.md`
`P1 Toolchain / Package Pin Gate` tier policy-sinə və `toolchain-pin-gate.md`
approval row-larına tabedir; həmin gate hansı pin-lərin approved olduğunu və hansı
foundation pin-lərinin hələ approval row-suz qaldığını (açıq external blocker)
göstərir.

| Path | Owner / purpose |
|---|---|
| `Dobby.sln` | Tək solution; bütün P1 source, test, sample və admin/tool project-lərini daşıyır. |
| `global.json` | .NET SDK feature-band pinning; cari pin `10.0.202` + `rollForward: latestPatch`. Exact patch üçün ayrıca human approval row-u yoxdur (`toolchain-pin-gate.md` external blocker); patch dəyişikliyi human approval tələb edir. |
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

The portable command writes a clean, versioned self-contained folder to
`artifacts/publish/portable/<version>-<commit>/Dobby.Host/`; `latest.json` points
to the exact current output. The artifact manifest records hashes and
`PortableSupport` mode, while the published configuration uses the isolated
`%LocalAppData%/Dobby/PortableSupport` data root. It does not produce an archive
and does not replace MSIX as the primary customer deployment format.

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
- `artifacts/release-evidence/<build-id>/storage-provider-proof.md` for `AC-042`, `AC-037` and `AC-038`; it must record the startup provider-enabled probe result against the installed/publish artifact, the byte-level sentinel scan over the DB file plus WAL/journal/rollback/checkpoint and temp artifacts, and the provider-disabled / provider-bypass / plain-DB / out-of-boundary WAL negative fixture outcomes. Recording only that a native provider package or binary is present does not satisfy this band (`NFR-027`);
- `artifacts/release-evidence/<build-id>/packaging-static-scan.md` for sample/dev/QA trust exclusion;
- `artifacts/release-evidence/<build-id>/startup-performance.md` for `AC-014`.

Skeleton-time local evidence may use `artifacts/release-evidence/skeleton-local/`, but release candidate acceptance must use the current CI/release build id.

Release candidate evidence is gate review input, so every file under `artifacts/release-evidence/<build-id>/` must be version-controlled and visible to `git status` in the candidate commit. Repository ignore rules must not exclude a build-id evidence root or its `.md` and `ui-snapshots/` files: an evidence file that exists on disk but is silently ignored does not count as recorded evidence, because the reviewer sees a clean working tree while a required gate artifact is absent from the commit. Transient local scratch roots that no gate cites may stay ignored, and generated log files inside an evidence root are not evidence — a claim only counts when it is written into one of the named evidence files above.


**Evidence change-trail qaydası.** Keçmiş build-id evidence root-u keçmiş release
candidate-in müşahidə olunmuş nəticəsini qeyd edir və **retroaktiv edit olunmur**.
Sonrakı bir pass həmin bandı yenidən sübut edirsə, nəticə cari build-id root-unda
yeni fayl kimi yazılır; köhnə fayl yerində qalır. Səbəb: gate review input-u
müqayisə üçün istifadə olunur, ona görə keçmiş evidence-in dəyişməsi review
tarixçəsini yoxa çıxarır və bəzi keçmiş fayllar `Sprint11ReleaseEvidenceTests` kimi
testlər tərəfindən verbatim assert olunur. Yalnız istisna: sızmış sensitive dəyər
(secret, private key material, full filesystem path) — bu halda redaction edilir və
redaction həm həmin faylda, həm də cari build-id root-unda qeyd olunur.

- Skipped test varsa səbəbi release evidence-də explicit qeyd edilir.
- App icon multi-size `.ico`, application logo və required host asset-lər publish/installer artifact-da mövcuddur.
- `.dobbyplugin` package validation, signature verification və optional `.dobbylicense` import smoke ayrı nəticələr kimi saxlanılır.

### Candidate release evidence set

Hər candidate release aşağıdakı bandlar üçün machine-readable evidence saxlamalıdır.
Bu siyahı release gate-in minimumudur; hər band üçün evidence current candidate
artifact-ına və exact candidate commit-ə bind olunur:

1. clean restore, və ya offline/no-restore halında sənədləşdirilmiş asset provenance;
2. format, build, unit, integration və smoke-test nəticələri;
3. negative və fault-injection nəticələri: password/throttle restart test-ləri,
   atomicity fault injection, malicious archive corpus və license/recovery
   negative case-ləri;
4. portable launch/smoke verification candidate bit-lərinə qarşı;
5. MSIX build, clean install, **installed** launch, upgrade, uninstall policy və
   user-data preservation — real package identity və real installed storage
   root-ları ilə;
6. production-appropriate certificate chain/timestamp/profile, artifact hash-ları,
   SBOM/dependency inventory və vulnerability/license review;
7. installable, düzgün imzalanmış sample plugin plus deterministic
   `verify-plugin-package` tool/command nəticəsi və negative fixture-lar;
8. customer artifact-lardan placeholder contact string, placeholder qrafika, dummy
   hash/signature, developer-only path və test key-lərin çıxarıldığının sübutu.

Self-signed RC yalnız packaging mechanics-i sübut edir; GA installed davranış,
upgrade/data preservation və signing/distribution evidence-i toplananadək bloklanır.
Evidence-only, file-existence və ya source-text test-ləri incomplete feature-i
implemented statusuna qaldırmır (`docs/02_ARCHITECTURE_MAP.md` §
`Architecture Evidence Rule`).

External installed-MSIX, production-signing və dependency-license evidence
`docs/schemas/release-external-evidence-v1.schema.json`-a uyğun olmalıdır: exact
candidate commit və gate-ə bind olunur, gate-specific required claim-ləri
enumerate edir, `attestedBy` ilə gate-i icra edən insanı adlandırır və hər
subject-i SHA-256 ilə identifikasiya edir. Faylın sadəcə mövcud olması heç bir
gate-i keçirmir.

`status` `passed`-ə pinned-dir, yəni conforming fayl yalnız bir approval qeyd
edir. Ona görə `attestedBy` məcburidir: identity daşımayan approval attribution
oluna bilmir və heç bir reviewer sonradan gate-i kimin icra etdiyini, nəyin real
işlədildiyini soruşa bilmir. Ad provenance-dır, verification deyil, və gate-in
tələb etdiyi behavioral sübutu əvəz etmir (`NFR-027`). Enforcement point-i
`tools/verify-release-candidate/validate-external-evidence.ps1`-dir.

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
  --input-path .\artifacts\sample-plugin\unsigned `
  --signing-profile dev-qa `
  --output-path .\artifacts\sample-plugin\signed\sample.dobbyplugin

.\tools\create-plugin-license\create-plugin-license.ps1 `
  --plugin-package-path .\artifacts\sample-plugin\signed\sample.dobbyplugin `
  --signing-profile dev-qa `
  --customer-id default `
  --deployment-id default `
  --device-binding-fixture-path .\artifacts\sample-plugin\smoke-device-binding.json `
  --valid-from 2026-06-18T00:00:00Z `
  --valid-until 2026-07-18T00:00:00Z `
  --revision 1 `
  --output-path .\artifacts\sample-plugin\license\sample.dobbylicense

.\tools\verify-plugin-package\verify-plugin-package.ps1 `
  --input-path .\artifacts\sample-plugin\signed\sample.dobbyplugin `
  --trust-profile dev-qa `
  --host-version 1.0.0
```

Script adları və folder-lər implementation phase-də bu sənədlə eyni patch-də
yaradılmalıdır. Flow yalnız dev/QA smoke materialı üçündür; customer production
MSIX/portable artifact sample plugin-i və ya sample license-i default şəkildə
daşımamalıdır.

Wrapper-lər arqumentləri dəyişmədən ötürür (`ValueFromRemainingArguments`), buna
görə yuxarıdakı option adları PowerShell parametr adları deyil, birbaşa CLI
option-larıdır və canonical yazılış `docs/specs/admin-tool.md` § 3 `Approved P1
CLI contract` cədvəlindəki formadır. PowerShell-style `-PluginPackage` kimi ad
wrapper tərəfindən CLI-a olduğu kimi ötürülür və unknown option olaraq `64` verir.
Həmin cədvəl `HD-S10-001` ilə approve olunmuş **canonical** public option
contract-dır: option adları, required/optional bölgüsü, default-lar və exit
code-lar oradan oxunur, bu sənəddə dublikat edilmir. Folder/script adları,
`Dobby.Admin.exe --cli <command>` invocation modeli, exit code, stdout və stderr
davranışı da stable-dir. Bu blokdaki dəyərlər (tarixlər, revision, fixture yolu)
nümunədir; option adları isə deyil.

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

Cari release track Standalone-dur. Branch Shared future work olaraq qalır və
explicit human-approved scope reopen etmədən Standalone hardening/release
gate-lərini bloklaya bilməz.

The Branch Shared documentation tier (pre-Tier A, `ADR-0056`) is documentation-only.
It creates/updates ADRs, specs
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

## Revision History

- **2026-08-27 (scripted operator path executability):** `Minimal scripted
  operator artifact handoff` bloku icra olunmayan command-lər göstərirdi.
  Wrapper-lər `ValueFromRemainingArguments` ilə arqumentləri dəyişmədən
  `Dobby.Admin.exe --cli <command>`-ə ötürür, buna görə blokda yazılmış
  `-InputPath`, `-PluginPackage`, `-DeviceBindingFixture` kimi PowerShell-style
  adlar CLI-a olduğu kimi çatır və unknown option olaraq `64` verirdi. Blok
  canonical `--option` adlarına keçirildi, `create-plugin-license` üçün əskik
  required option-lar (`--valid-from`, `--valid-until`, `--revision`) əlavə edildi,
  `verify-plugin-package` addımı göstərildi. "Exact option names … future
  human/tooling-approved option table bağlandıqda freeze olunur" iddiası ləğv
  edildi, çünki `docs/specs/admin-tool.md` § 3 `Approved P1 CLI contract` cədvəli
  `HD-S10-001` ilə artıq approve olunmuş canonical contract-dır; option adları
  burada dublikat edilmir, oradan oxunur.
- **2026-08-26 (storage evidence hardening):** `storage-provider-proof.md`
  bandının minimum məzmunu explicit edildi — startup provider-enabled probe
  nəticəsi, byte-level sentinel scan və provider-disabled / provider-bypass /
  plain-DB / out-of-boundary WAL negative fixture nəticələri tələb olunur;
  native provider package/binary mövcudluğunun qeydi bu bandı qapatmır
  (`AC-037`, `AC-042`, `NFR-027`).
- **2026-08-26:** `ADR-0058`…`ADR-0061` reconciliation. `2026-08 Release Evidence
  Amendment` bölməsi ləğv edildi; Standalone release-track ifadəsi
  `Branch Shared Release Readiness Addendum`-a, candidate evidence bandları,
  self-signed RC limiti, evidence-only test qadağası və
  `release-external-evidence-v1.schema.json` bind qaydası isə
  `Release evidence additions` § `Candidate release evidence set` bölməsinə
  köçürüldü.
- **2026-08-29 (pin gate approval reality):** Skeleton commit 1 package-bearing file
  creation paraqrafı və `global.json` table sətri hələ də "pin list verilməyibsə
  stop condition" prospektiv formasında idi, halbuki həmin fayllar mövcuddur, build
  onlarla verify olunur və stop condition Phase A/B approval-ları ilə bağlanmışdır.
  Prose faktiki vəziyyətə uyğunlaşdırıldı: canonical approval source
  `toolchain-pin-gate.md`-dir, `global.json` `10.0.202` + `latestPatch` ilə
  pinned-dir, exact SDK patch üçün ayrıca approval row-u yoxdur (external blocker),
  və **yeni** package/version/source dəyişikliyi hələ də human approval tələb edir.
  Version/approval authority `docs/03_TECH_STACK_AND_VERSIONS.md`-də qalır.
