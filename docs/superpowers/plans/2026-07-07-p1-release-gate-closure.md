# P1 Release Gate Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Track progress by updating the checkbox steps in this file.

**Goal:** Close the remaining P1 release gates without confusing authority approval with release-ready evidence.

**Architecture:** Treat Sprint 11 as an evidence closure pass over an already-green implementation baseline. Keep the remaining gates isolated: production recovery/key handover (`HD-S11-001`), ADR-0042 performance baseline (`HD-S11-002`), and MSIX installer evidence (`HD-S11-003`). `HD-S11-004` and `HD-S11-005` remain approved path decisions, but the final release claim changes only after all evidence gates and verification commands pass.

**Tech Stack:** .NET 10, WPF, xUnit/Shouldly, PowerShell, MSIX packaging tooling approved by `HD-S11-003`, and release evidence rooted at the Task 1 computed `artifacts/release-evidence/$buildId/` directory.

## Global Constraints

- Authority decisions approve the path; P1/release-ready is not closed until evidence passes.
- Production private key material, signing transport names/values, passphrases, private key paths, raw private material, raw/canonical recovery payloads, stack traces, raw Admin transcripts, and full local secret-bearing paths are forbidden in repo/source/tests/evidence.
- `HD-S11-002` is policy path accepted only. The current local machine is disqualified for release baseline evidence.
- ADR-0042 release performance closure requires an ADR-0042-compliant baseline machine/profile and N=100 cold, N=100 warm, and N=100 login-to-shell samples.
- `HD-S11-003` is MSIX-primary RC mechanics accepted only. Self-signed certificate evidence does not imply GA/customer production signing readiness unless release authority separately accepts that.
- Repository CI ownership is absent. Use the exact wording `repository CI ownership is absent; equivalent local release gate human-approved` in evidence instead of claiming CI success.
- Portable output remains support/dev/offline-test scope and is not a production installer substitute.
- Every forbidden-match `rg` scan must be fail-closed: exit code `0` means forbidden match found and fails the task; exit code `1` means no match and passes the scan; every other exit code is a scan error and fails the task.
- Do not add package references or installer tooling packages unless the package/version is already approved in `artifacts/release-evidence/skeleton-local/toolchain-pin-gate.md` or a new human approval row is recorded first.
- Do not commit unless the human explicitly requests a commit. Each task ends with verification and a diff checkpoint.

## File Structure

- Modify `src/Dobby.Host/Trust/vendor-master-public-key.pem`: public GA-candidate production key only after `HD-S11-001` source/test baseline approval is available.
- Modify `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`: expected production public-key hash and bootstrap negative-control assertions after key rotation.
- Modify `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`: published-artifact key extraction, trust profile, devQA/test fixture exclusion, and production recovery evidence closure assertions.
- Modify `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs`: gate-specific evidence assertions as each gate closes, and final release-ready assertions only in the final task.
- Modify the Task 1 computed evidence root files:
  - `production-recovery-handover.md`
  - `startup-performance.md`
  - `installer-verification.md`
  - `release-run-manifest.md`
  - `unit-integration-smoke-results.md`
  - `packaging-static-scan.md`
  - `auth-kdf-selection.md`
  - `release-smoke.md`
  - `performance/cold-start-samples.jsonl`
  - `performance/warm-start-samples.jsonl`
  - `performance/login-to-shell-samples.jsonl`
  - `performance/summary.json`
  - `performance/SHA256SUMS.txt`
- Create or modify MSIX packaging files under `installer/` only after `HD-S11-003` exact identity/tooling approval is recorded.

## Task 1: Release Run Preflight And Evidence Root

**Files:**
- Modify: `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs`
- Modify: Task 1 computed evidence root `release-run-manifest.md`
- Modify: Task 1 computed evidence root `unit-integration-smoke-results.md`

**Interfaces:**
- Consumes: current `Dobby.sln`, `global.json`, `Directory.Packages.props`, and decision-log D-35 through D-39.
- Produces: one current release evidence root and preflight manifest entries consumed by all later tasks.

- [x] **Step 1: Compute the release evidence root**

Run:

```powershell
$shortSha = (git rev-parse --short=12 HEAD).Trim()
$fullSha = (git rev-parse HEAD).Trim()
$branch = (git branch --show-current).Trim()
$utc = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$buildId = "local-rc-$utc-$shortSha"
$evidenceRoot = "artifacts\release-evidence\$buildId"
New-Item -ItemType Directory -Force -Path $evidenceRoot | Out-Null
$buildId
```

Expected: `$buildId` starts with `local-rc-`, ends with the current 12-character short SHA, and `$evidenceRoot` is the only evidence root used for the rest of this plan.

- [x] **Step 2: Capture source state**

Run:

```powershell
git status --short --branch
git diff --stat
```

Expected: Record existing uncommitted files in the manifest as pre-existing source state. Do not reset or revert unrelated changes.

- [x] **Step 3: Add preflight assertions**

Update `Sprint11ReleaseEvidenceTests.cs` so it discovers or pins the Task 1 computed evidence root and asserts:

- `release-run-manifest.md` exists in that root.
- Manifest build id equals the directory name.
- Manifest includes full SHA, short SHA, branch, pre-run source state, and the anchor sentence: `Authority decisions approve the path; P1/release-ready is not closed until evidence passes.`
- Manifest says `repository CI ownership is absent; equivalent local release gate human-approved`.
- Manifest does not claim final readiness yet.

- [x] **Step 4: Verify the preflight test fails before evidence exists**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=p1-preflight-red.trx"
```

Expected: FAIL only on the newly added preflight evidence assertion.

- [x] **Step 5: Create the preflight manifest**

Create `release-run-manifest.md` and `unit-integration-smoke-results.md` inside `$evidenceRoot`.

`release-run-manifest.md` must include:

```markdown
# Sprint 11 Release Run Manifest

Build ID: value of $buildId
Evidence root: value of $evidenceRoot
Source commit full SHA: value of $fullSha
Source commit short SHA: value of $shortSha
Source branch: value of $branch
Pre-run source config state: exact summary from git status

Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

Repository CI status: repository CI ownership is absent; equivalent local release gate human-approved.

Release readiness: BLOCKED UNTIL EVIDENCE PASSES
```

`unit-integration-smoke-results.md` must state that final verification has not run yet and must not imply release closure.

- [x] **Step 6: Verify green and checkpoint**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=p1-preflight-green.trx"
git diff --check
git diff --stat
```

Expected: focused test PASS, no whitespace errors, and diff limited to preflight test/evidence changes.

## Task 2: HD-S11-001 Source/Test Baseline For Production Public Key

**Files:**
- Modify: `src/Dobby.Host/Trust/vendor-master-public-key.pem`
- Modify: `tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs`
- Modify: `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`
- Modify: Task 1 computed evidence root `production-recovery-handover.md`

**Interfaces:**
- Consumes: release-authority-approved public PEM, public SHA-256, custodian label, and rotation disposition.
- Produces: source-level production public-key hash equality and bootstrap-key negative-control evidence.

- [x] **Step 1: Stop unless non-secret approval inputs are complete**

Proceed only when all of these non-secret fields are available:

- Approved GA-candidate production public PEM.
- Approved public-key SHA-256 as a 64-character lowercase hex string.
- Custodian label suitable for redacted evidence.
- Rotation disposition suitable for public evidence.
- Confirmation that private key material and private signing transport details are out of scope for repo/evidence.

Expected: If any field is missing, stop this task and record the missing non-secret field in the working notes. Do not invent values.

- [x] **Step 2: Add failing source-level assertions**

Update `PluginTrustResourcePackagingTests.cs` and/or `ProductionReleasePackagingStaticScanTests.cs` so they assert:

- `vendor-master-public-key.pem` parses as PEM.
- SHA-256 of the source public PEM equals the approved public SHA-256 value.
- Bootstrap/dev/test public key hash remains different from the approved production public SHA-256 value.
- No private key blocks exist in source/resources/tests/evidence.

Use the exact approved public SHA-256 literal once it is available.

- [x] **Step 3: Verify red**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~PluginTrustResourcePackagingTests|FullyQualifiedName~ProductionReleasePackagingStaticScanTests" --logger "trx;LogFileName=hd-s11-001-source-red.trx"
```

Expected: FAIL only because the current source PEM/hash has not yet been rotated to the approved production value.

- [x] **Step 4: Rotate only the public PEM**

Replace `src/Dobby.Host/Trust/vendor-master-public-key.pem` with the approved public PEM. Do not add private key material, signing transport details, passphrases, or secret-bearing paths.

- [x] **Step 5: Verify green**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~PluginTrustResourcePackagingTests|FullyQualifiedName~ProductionReleasePackagingStaticScanTests" --logger "trx;LogFileName=hd-s11-001-source-green.trx"
```

Expected: PASS.

- [x] **Step 6: Update source baseline evidence**

Update `production-recovery-handover.md` in `$evidenceRoot` with:

- Source public-key SHA-256: exact approved value.
- Custody public-key SHA-256: exact approved value.
- Custodian: approved redacted label.
- Rotation disposition: approved public wording.
- Bootstrap public-key negative control: PASS, hash differs from production public key.
- Private key handling: PASS, not present in repo/source/tests/evidence.

- [x] **Step 7: Leak scan and checkpoint**

Run:

```powershell
function Invoke-ForbiddenMatchScan {
    param(
        [Parameter(Mandatory=$true)][string]$Pattern,
        [Parameter(Mandatory=$true)][string[]]$Paths
    )

    rg -n $Pattern @Paths
    $code = $LASTEXITCODE
    if ($code -eq 0) { throw "Forbidden match found." }
    if ($code -eq 1) { "No forbidden matches."; return }
    throw "Scan error from rg, exit code $code."
}

Invoke-ForbiddenMatchScan -Pattern "BEGIN .*PRIVATE KEY|passphrase|private key path|signing transport|raw canonical|raw/canonical" -Paths @("src", "tests", "artifacts\release-evidence")
git diff --check
git diff -- src/Dobby.Host/Trust/vendor-master-public-key.pem tests/Dobby.IntegrationTests/Architecture/PluginTrustResourcePackagingTests.cs tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs
```

Expected: forbidden-match scan prints `No forbidden matches.`, `git diff --check` passes.

## Task 3: HD-S11-001 Published Artifact And Recovery Import Evidence

**Files:**
- Modify: `tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs`
- Modify: Task 1 computed evidence root `production-recovery-handover.md`
- Modify: Task 1 computed evidence root `packaging-static-scan.md`
- Modify: Task 1 computed evidence root `release-run-manifest.md`

**Interfaces:**
- Consumes: approved production public-key baseline from Task 2 and Admin-generated production `.dobbyrecovery` artifact evidence.
- Produces: published-artifact key equality, redacted recovery artifact evidence, and Host import evidence.

- [x] **Step 1: Add failing published-artifact evidence assertions**

Update `ProductionReleasePackagingStaticScanTests.cs` so it requires:

- Published `Dobby.Security.dll` contains the approved production public-key hash when built with `DobbyPluginTrustProfile=Production`.
- Source public-key SHA-256, embedded public-key SHA-256, and custody public-key SHA-256 are equal.
- Published Host import result is recorded for a production `.dobbyrecovery` artifact.
- `production-recovery-handover.md` contains redacted Admin report hash and redacted transcript hash-chain references.
- Evidence contains no raw/canonical recovery payload, private key material, passphrase, secret path, stack trace, or raw Admin transcript.

- [x] **Step 2: Verify red**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~ProductionReleasePackagingStaticScanTests" --logger "trx;LogFileName=hd-s11-001-published-red.trx"
```

Expected: FAIL only on missing published-artifact/recovery-import evidence.

- [x] **Step 3: Publish production trust profile**

Run:

```powershell
dotnet publish .\src\Dobby.Host\Dobby.Host.csproj -c Release --no-restore -p:DobbyPluginTrustProfile=Production
```

Expected: PASS, and the publish output includes `Dobby.Host.exe` and `Dobby.Security.dll`.

- [x] **Step 4: Extract and compare embedded public key**

Extract/hash the embedded public key from the published `Dobby.Security.dll` using the existing test helper or a small throwaway local inspection command. Record only:

- Source public-key SHA-256.
- Embedded public-key SHA-256.
- Custody public-key SHA-256.
- PASS/FAIL equality result.

Expected: all three public SHA-256 values match exactly.

- [x] **Step 5: Generate or receive production `.dobbyrecovery` evidence**

Use the approved Admin `create-recovery-response` flow. Evidence may record only:

- Command name.
- Signing profile: production.
- Exit code/result.
- `.dobbyrecovery` artifact SHA-256.
- Redacted Admin report SHA-256.
- Redacted transcript hash-chain digest.

Expected: no raw recovery payload, raw Admin transcript, private signing details, or secret-bearing paths are written to repo/evidence.

- [x] **Step 6: Verify published Host import**

Run the published `Dobby.Host.exe` recovery import on a clean temp/profile context and record:

- Published Host path as a non-secret repository-relative or redacted path.
- Import result.
- Exit code or equivalent deterministic success signal.
- Imported artifact SHA-256.

Expected: import succeeds using Production trust profile.

- [x] **Step 7: Update evidence and verify green**

Update `production-recovery-handover.md`, `packaging-static-scan.md`, and `release-run-manifest.md` in `$evidenceRoot`.

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~ProductionReleasePackagingStaticScanTests|FullyQualifiedName~RecoveryResponseImport" --logger "trx;LogFileName=hd-s11-001-published-green.trx"
function Invoke-ForbiddenMatchScan {
    param(
        [Parameter(Mandatory=$true)][string]$Pattern,
        [Parameter(Mandatory=$true)][string[]]$Paths
    )

    rg -n $Pattern @Paths
    $code = $LASTEXITCODE
    if ($code -eq 0) { throw "Forbidden match found." }
    if ($code -eq 1) { "No forbidden matches."; return }
    throw "Scan error from rg, exit code $code."
}

Invoke-ForbiddenMatchScan -Pattern "BEGIN .*PRIVATE KEY|passphrase|private key path|signing transport|raw canonical|raw/canonical|stack trace" -Paths @("artifacts\release-evidence", "src", "tests")
git diff --check
```

Expected: tests PASS, forbidden-match scan prints `No forbidden matches.`, and whitespace check passes.

## Task 4: HD-S11-002 ADR-0042 Performance Baseline Evidence

**Files:**
- Modify: `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs`
- Modify: Task 1 computed evidence root `startup-performance.md`
- Optional modify: `docs/_perf/baselines/v1.0.0.json` only if release authority accepts this as the baseline registry file.

**Interfaces:**
- Consumes: ADR-0042-compliant baseline machine/profile and N=100 timing samples.
- Produces: AC-014/AC-051 release timing closure evidence.

- [ ] **Step 1: Stop unless baseline machine/profile is compliant**

Proceed only when the named machine/profile satisfies all ADR-0042 minimums:

- CPU class at least Intel i5-10xxx or AMD Ryzen 5 5xxx equivalent.
- 4 cores / 8 threads.
- 2.5 GHz or higher base clock under strict reading.
- 16 GB DDR4 RAM or better.
- SATA SSD or faster.
- 1920x1080 display.
- Windows 11 22H2 or Windows 10 22H2 ESU.
- Offline test conditions.

Expected: The current local machine remains recorded only as baseline-disqualification evidence, not performance closure evidence.

- [ ] **Step 2: Add failing performance evidence assertions**

Update `Sprint11ReleaseEvidenceTests.cs` so it requires `startup-performance.md` to state:

- Named ADR-0042-compliant machine/profile.
- Current local machine evidence is baseline-disqualification evidence only.
- N=100 cold, N=100 warm, and N=100 login-to-shell samples.
- Raw sample artifacts exist under `$evidenceRoot\performance\` as `cold-start-samples.jsonl`, `warm-start-samples.jsonl`, and `login-to-shell-samples.jsonl`.
- `summary.json` exists under `$evidenceRoot\performance\` and records the P95 method, exact thresholds, measured P95 values, and baseline comparison source/hash.
- `SHA256SUMS.txt` exists under `$evidenceRoot\performance\` and records SHA-256 values for all three raw sample files and `summary.json`.
- P95 cold start pre-login is at or below 3000ms.
- P95 warm start pre-login is at or below 1000ms.
- P95 login submit to shell first paint is at or below 800ms.
- Regression delta is not above 20%.
- AC-014 and AC-051 are closed only by the compliant baseline evidence.

- [ ] **Step 3: Verify red**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=hd-s11-002-red.trx"
```

Expected: FAIL only on missing ADR-0042 baseline evidence.

- [ ] **Step 4: Collect N=100 samples on the approved baseline**

Run the approved measurement procedure on the compliant baseline machine/profile:

- 100 cold start pre-login samples.
- 100 warm start pre-login samples.
- 100 login submit to shell first-paint samples.
- Write raw samples to `$evidenceRoot\performance\cold-start-samples.jsonl`, `$evidenceRoot\performance\warm-start-samples.jsonl`, and `$evidenceRoot\performance\login-to-shell-samples.jsonl`.
- Each JSONL row must include `sampleIndex`, `scenario`, `durationMs`, `capturedAtUtc`, and the approved baseline profile id.

Expected: each raw sample file has exactly 100 rows, is deterministic, is traceable to the named baseline profile, and contains no secret paths or personal data.

- [ ] **Step 5: Calculate and record P95 evidence**

Calculate P95 values and regression delta. Use sorted nearest-rank P95: sort the 100 `durationMs` values ascending and take the 95th sample using 1-based indexing, which is zero-based index 94 in PowerShell. Update `startup-performance.md` in `$evidenceRoot` with:

```markdown
## ADR-0042 Baseline

Current local machine evidence: i5-1135G7 @ 2.40GHz, 4C/8T, ~8.3GB RAM; disqualified for ADR-0042 release baseline because it fails the 16GB RAM minimum and, under strict reading, the 2.5GHz base-clock threshold. Informational measurements on this machine do not close AC-014/AC-051.

## Release Timing Gate

AC-014: CLOSED by ADR-0042-compliant baseline evidence.
AC-051: CLOSED by ADR-0042-compliant baseline evidence.
```

Create `$evidenceRoot\performance\summary.json` with:

- Baseline profile id.
- Baseline comparison source and SHA-256.
- P95 method: sorted nearest-rank, N=100, 1-based index 95.
- Exact P95 values for cold start, warm start, and login-to-shell.
- Thresholds: 3000ms, 1000ms, and 800ms.
- Regression delta and PASS/FAIL result.

Create `$evidenceRoot\performance\SHA256SUMS.txt` with SHA-256 values for `cold-start-samples.jsonl`, `warm-start-samples.jsonl`, `login-to-shell-samples.jsonl`, and `summary.json`.

Add a results table to `startup-performance.md` with the exact measured P95 values, all marked PASS only if thresholds are met.

- [ ] **Step 6: Verify green and checkpoint**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=hd-s11-002-green.trx"
git diff --check
git diff -- tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs
git diff -- artifacts\release-evidence
```

Expected: focused test PASS and evidence does not treat the current local machine as timing closure evidence.

## Task 5: HD-S11-003 MSIX Installer Evidence

**Files:**
- Modify: `installer/README.md`
- Modify: approved MSIX packaging files under `installer/`
- Modify: `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs`
- Modify: Task 1 computed evidence root `installer-verification.md`
- Modify: Task 1 computed evidence root `release-run-manifest.md`

**Interfaces:**
- Consumes: approved MSIX identity, publisher, versioning policy, signing certificate disposition, packaging tooling, and self-signed RC/GA stance.
- Produces: MSIX RC mechanics evidence for install/uninstall/upgrade/data preservation.

- [ ] **Step 1: Stop unless installer authority inputs are complete**

Proceed only when all of these non-secret fields are available:

- MSIX package identity.
- Publisher string.
- Versioning policy.
- Packaging tooling and version.
- Signing certificate disposition.
- Explicit statement that self-signed signing is RC mechanics only unless separately approved for GA/customer signing readiness.
- MSI fallback disposition.

Expected: If any field is missing, stop this task and record the missing non-secret field in the working notes. Do not infer identity/publisher/versioning policy.

- [ ] **Step 2: Add failing installer evidence assertions**

Update `Sprint11ReleaseEvidenceTests.cs` so it requires `installer-verification.md` to include:

- MSIX package identity PASS.
- Signing certificate disposition PASS.
- Versioning policy PASS.
- Packaging tooling PASS.
- vN installer artifact SHA-256 PASS.
- vN+1 installer artifact SHA-256 PASS.
- Upgrade evidence binds the run to those two exact artifact hashes.
- vN and vN+1 use the same package identity/family.
- vN+1 package version monotonically increases according to the approved versioning policy.
- Clean install PASS.
- Clean uninstall PASS.
- Upgrade from vN to vN+1 PASS.
- Data preservation PASS for app data, audit log, license cache, users/permissions, and preferences.
- Self-signed certificate GA/customer readiness is not claimed unless separately approved.

- [ ] **Step 3: Verify red**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=hd-s11-003-red.trx"
```

Expected: FAIL only on missing installer evidence.

- [ ] **Step 4: Add or update MSIX packaging files**

Add the approved MSIX packaging files under `installer/`. Keep MSI fallback dormant unless explicit fallback evidence is required. Do not set `%LocalAppData%\Dobby` as the production installer target.

- [ ] **Step 5: Build and sign the MSIX**

Run the approved packaging command recorded in `installer/README.md`.

Expected: both vN and vN+1 MSIX packages exist, each has a recorded SHA-256, both share the same package identity/family, vN+1 has a monotonically higher package version, and signature disposition matches the approved RC wording.

- [ ] **Step 6: Verify install, uninstall, and upgrade**

On a clean Windows profile or test VM:

- Install vN using the MSIX artifact whose SHA-256 is recorded as the vN hash.
- Launch and verify expected app state.
- Create or seed app data, audit log, license cache, users/permissions, and preferences.
- Upgrade to vN+1 using the MSIX artifact whose SHA-256 is recorded as the vN+1 hash.
- Verify all required data survives.
- Uninstall.
- Verify uninstall behavior matches the approved policy.

Expected: all operations PASS, upgrade evidence names the two artifact hashes, package identity/family remains the same, package version increases monotonically, and evidence contains no secret-bearing local paths.

- [ ] **Step 7: Update evidence and verify green**

Update `installer-verification.md` and `release-run-manifest.md` in `$evidenceRoot`.

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=hd-s11-003-green.trx"
function Invoke-ForbiddenMatchScan {
    param(
        [Parameter(Mandatory=$true)][string]$Pattern,
        [Parameter(Mandatory=$true)][string[]]$Paths
    )

    rg -n $Pattern @Paths
    $code = $LASTEXITCODE
    if ($code -eq 0) { throw "Forbidden match found." }
    if ($code -eq 1) { "No forbidden matches."; return }
    throw "Scan error from rg, exit code $code."
}

Invoke-ForbiddenMatchScan -Pattern "%LocalAppData%\\Dobby|LocalAppData\\\\Dobby" -Paths @("installer", "artifacts\release-evidence")
git diff --check
```

Expected: focused test PASS, forbidden-match scan prints `No forbidden matches.`, and whitespace check passes.

## Task 6: Final Release Evidence Rerun And Manifest Closure

**Files:**
- Modify: `tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs`
- Modify: Task 1 computed evidence root `release-run-manifest.md`
- Modify: Task 1 computed evidence root `unit-integration-smoke-results.md`
- Modify: Task 1 computed evidence root `auth-kdf-selection.md`
- Modify: Task 1 computed evidence root `packaging-static-scan.md`
- Modify: Task 1 computed evidence root `release-smoke.md`

**Interfaces:**
- Consumes: closed evidence from Tasks 1-5.
- Produces: final release-review claim, final verification results, and refreshed portable support/dev build.

- [ ] **Step 1: Add final release-readiness assertions**

Update `Sprint11ReleaseEvidenceTests.cs` so final readiness requires:

- `Release readiness: READY FOR RELEASE REVIEW`.
- `HD-S11-001`, `HD-S11-002`, `HD-S11-003`, `HD-S11-004`, and `HD-S11-005` are all closed.
- No stale blocker text in the final evidence root.
- Manifest still includes the anchor sentence about authority decisions versus evidence closure.
- Manifest still uses `repository CI ownership is absent; equivalent local release gate human-approved`.
- Portable output is identified as support/dev/offline-test only.

- [ ] **Step 2: Verify final assertions red**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests" --logger "trx;LogFileName=p1-final-readiness-red.trx"
```

Expected: FAIL only because the manifest has not yet been promoted to final release-review wording.

- [ ] **Step 3: Run full no-restore verification**

Run:

```powershell
dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal
dotnet build .\Dobby.sln -c Release --no-restore
dotnet test tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=p1-final-unit.trx"
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=p1-final-integration.trx"
dotnet test tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj -c Release --no-build --logger "trx;LogFileName=p1-final-smoke.trx"
```

Expected:

```text
format: no changes
build: 0 errors
unit: 0 failed
integration: 0 failed
smoke: 0 failed
```

- [ ] **Step 4: Refresh portable support/dev artifact**

Run:

```powershell
.\tools\publish-portable\publish-portable.ps1 -NoRestore
```

Expected:

```text
Executable: artifacts\publish\portable\Dobby.Host\Dobby.Host.exe
```

- [ ] **Step 5: Update final evidence files**

Update `unit-integration-smoke-results.md` with final counts and TRX names.

Update `release-run-manifest.md`:

```markdown
Release readiness: READY FOR RELEASE REVIEW

## Closed Decisions

- HD-S11-001: CLOSED.
- HD-S11-002: CLOSED.
- HD-S11-003: CLOSED.
- HD-S11-004: CLOSED.
- HD-S11-005: CLOSED.
```

Update `auth-kdf-selection.md` so AC-051 references the accepted ADR-0042 latency evidence from `startup-performance.md`.

Update `release-smoke.md` so it references final plugin install/license/storage heartbeat evidence and installer evidence.

- [ ] **Step 6: Run final evidence tests and verify green**

Run:

```powershell
dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Sprint11ReleaseEvidenceTests|FullyQualifiedName~ProductionReleasePackagingStaticScanTests" --logger "trx;LogFileName=p1-final-evidence-green.trx"
```

Expected: PASS.

- [ ] **Step 7: Final leak and stale-blocker scans**

Run:

```powershell
function Invoke-ForbiddenMatchScan {
    param(
        [Parameter(Mandatory=$true)][string]$Pattern,
        [Parameter(Mandatory=$true)][string[]]$Paths
    )

    rg -n $Pattern @Paths
    $code = $LASTEXITCODE
    if ($code -eq 0) { throw "Forbidden match found." }
    if ($code -eq 1) { "No forbidden matches."; return }
    throw "Scan error from rg, exit code $code."
}

Invoke-ForbiddenMatchScan -Pattern "BLOCKED UNTIL EVIDENCE PASSES|CLOSURE BLOCKED|NOT COLLECTED|NOT RUN|PENDING HUMAN|CI passed" -Paths @($evidenceRoot, "tests\Dobby.IntegrationTests\Architecture")
Invoke-ForbiddenMatchScan -Pattern "BEGIN .*PRIVATE KEY|passphrase|private key path|signing transport|raw canonical|raw/canonical|stack trace" -Paths @($evidenceRoot, "src", "tests")
git diff --check
```

Expected: both forbidden-match scans print `No forbidden matches.`, stale blocker text is absent from the final evidence root and final tests, leak scan finds no forbidden secret material, and whitespace check passes.

- [ ] **Step 8: Final checkpoint**

Run:

```powershell
git status --short --branch
git diff --stat
Get-Item -LiteralPath 'artifacts\publish\portable\Dobby.Host\Dobby.Host.exe' | Select-Object FullName,Length,LastWriteTime
```

Expected: changed files are limited to approved source/test/evidence/installer files, and portable executable timestamp reflects the final publish.

## Self-Review

- Spec coverage: the plan covers all remaining items from the audit list: production recovery/key evidence, ADR-0042 baseline/N=100 performance evidence, MSIX installer evidence, final release manifest rerun, and portable refresh.
- Placeholder scan: the plan uses Task 1 computed runtime values and explicit stop conditions instead of unknown claims. It intentionally keeps private signing inputs out of repo/evidence and requires public/non-secret fields before edits.
- Type consistency: tasks refer to existing tests, evidence files, Admin command name `create-recovery-response`, `Dobby.Security.dll`, and `Dobby.Host.exe` paths consistently.
