# Sprint 11 Release Run Manifest

Build ID: `local-rc-20260624T124146Z-f153ca9c373f`
Sprint: Sprint 11, Release Gates, Installer, Evidence, and Smoke
Release readiness: BLOCKED UNTIL EVIDENCE PASSES

## Source Binding

- sourceCommitFullSha: `f153ca9c373fc30d1316df680f315b1e1f7fce9e`
- sourceCommitShortSha: `f153ca9c373f`
- sourceBranch: `codex/sprint-11-release-gates`
- preRunSourceConfigState=clean
- preRunCleanCheck: `git status --short --branch` returned only branch header before this evidence root was generated.
- postRunDiffPolicy=allowlisted-generated-evidence-only
- evidenceRoot: `artifacts/release-evidence/local-rc-20260624T124146Z-f153ca9c373f/`
- repository CI ownership is absent; equivalent local release gate human-approved.
- Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

## Environment

- UTC timestamp: `2026-06-24T13:45:28Z`
- OS: `Microsoft Windows 10.0.26200`
- Machine: `SHIRVANBAR`
- SDK: `.NET SDK 10.0.202`
- Runtime architecture: `win-x64`
- DobbyPluginTrustProfile=Production

## Commands And Results

| Gate | Command | Result |
| --- | --- | --- |
| SDK | `dotnet --info` | PASS; SDK `10.0.202` selected by `global.json`. |
| Restore | `dotnet restore .\Dobby.sln` | PASS; all projects up-to-date. |
| Release build | `dotnet build .\Dobby.sln -c Release --no-restore` | PASS; 0 warnings, 0 errors. |
| Publish | `dotnet publish .\src\Dobby.Host\Dobby.Host.csproj -c Release --no-restore` | PASS; host publish output produced. |
| Unit tests | `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=baseline-unit.trx"` | PASS; 622 passed, 0 failed, 0 skipped. |
| Integration baseline | `dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=baseline-integration.trx"` | PASS before Sprint 11 evidence contract; 241 passed, 0 failed, 0 skipped. |
| Smoke baseline | `dotnet test tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj -c Release --no-build --logger "trx;LogFileName=baseline-smoke.trx"` | PASS; 11 passed, 0 failed, 0 skipped. |
| AC-046 license heartbeat RED | `dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~ShellUiStaticScanTests.License_import_long_running_action_reports_background_operations"` | EXPECTED FAIL before `MainWindow.xaml.cs` change; missing `ReportLicenseImportBackgroundOperation(`. |
| AC-046 license heartbeat GREEN | same filtered command | PASS after `MainWindow.xaml.cs` change. |

Final no-restore verification after evidence generation is recorded in `unit-integration-smoke-results.md`.

## Artifact Hashes

| Artifact | SHA-256 |
| --- | --- |
| `src/Dobby.Host/bin/Release/net10.0-windows/publish/Dobby.Host.exe` | `0cbf66dc862b7e69ab5402789d546b2060b951bca6e6718260a06c2c525e1ce3` |
| `src/Dobby.Host/bin/Release/net10.0-windows/publish/Dobby.Host.dll` | `d29f6ff1e8801c9f808cb790ceb4db475b41314858fd8e6bcef78f01764c11a3` |
| `src/Dobby.Host/bin/Release/net10.0-windows/publish/Dobby.Security.dll` | `1c936aba16078b4150b98f71f58bd44ed01906e07512f290825a2d9a35db80a4` |
| `src/Dobby.Infrastructure/native/win-x64/sqlite3mc_x64.dll` | `3744689bf8427b40b222c1ec046ebc5e4f08c5c9898d56573112006ff2c4def8` |
| installer artifact | BLOCKED by HD-S11-003; no approved MSIX identity/signing/tooling exists in this repository. |

## Trust And Recovery Gate

- sourceVendorPublicKeyHash: `00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f`
- oldBootstrapProductionHash=00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f
- oldBootstrapNegativeControlResult: BLOCKING; the current source PEM still equals the Sprint 8 bootstrap hash, so HD-S11-001 cannot close.
- embeddedVendorPublicKeyHash: not claimed for production handover; byte-level extraction and custody comparison are blocked by HD-S11-001.
- custodyVendorPublicKeyHash: BLOCKED by HD-S11-001.
- redactionGate: not executed for production/Admin recovery because no approved production signing material or redacted Admin transcript exists.
- rawCapturePresentInEvidenceRoot=false
- No production private key, signing transport, passphrase, raw recovery payload, or raw Admin transcript is stored in this evidence root.

## Acceptance Criteria Map

| AC | Evidence |
| --- | --- |
| AC-008 | `release-smoke.md`; startup tests `PreLoginAllowedWorkloadTests.cs` and `StartupBootstrapWiringTests.cs`. |
| AC-014 | `startup-performance.md`; `ui-snapshots/README.md`; `ShellSnapshotTests.cs`. |
| AC-024 | `installer-verification.md`; blocked pending HD-S11-003. |
| AC-041 | `unit-integration-smoke-results.md`; unit/integration/smoke result separation. |
| AC-046 | `release-smoke.md`; `ShellUiStaticScanTests.License_import_long_running_action_reports_background_operations`; `ShellBackgroundOperationCoordinatorTests`; `PluginIntegrationSuiteTests`; `HostLoginRuntimeFactoryTests`. |
| AC-047 | EncodingBoundaryTests covers `.md`, `.json`, `.cs`, `.xaml`, and repo text files including this evidence root. |
| AC-051 | `auth-kdf-selection.md`; PBKDF2 fallback remains selected while ADR-0042 latency evidence is blocked. |

## Blocked Decisions

- HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES. Production vendor key handover may proceed only with approved GA-candidate public PEM/hash/custodian evidence, source/embedded/custody hash equality, Sprint 8 bootstrap negative control, redacted Admin report/transcript hashes, and published Host import evidence. Production private material remains forbidden in repo/source/tests/evidence.
- HD-S11-002: POLICY PATH ACCEPTED ONLY; CLOSURE BLOCKED. Current local machine is disqualified for release baseline. Current local machine evidence: i5-1135G7 @ 2.40GHz, 4C/8T, ~8.3GB RAM; disqualified for ADR-0042 release baseline because it fails the 16GB RAM minimum and, under strict reading, the 2.5GHz base-clock threshold. Informational measurements on this machine do not close AC-014/AC-051.
- HD-S11-003: MSIX-PRIMARY RC MECHANICS ACCEPTED ONLY. Self-signed certificate evidence may support RC installer mechanics only; it does not imply GA/customer production signing readiness unless release authority separately accepts that. MSIX identity/signing/versioning/tooling and install/upgrade evidence remain required before AC-024 release-ready closure.
- HD-S11-004: APPROVED. This run uses approved local fallback naming `local-rc-<UTC>-<shortsha>`; `skeleton-local` is not release-candidate evidence.
- HD-S11-005: APPROVED. Repository CI ownership is absent; equivalent local release gate human-approved. Scripted local build/test/static-scan/smoke/hash evidence remains required, and this manifest must not use CI-success wording.

## Release Claim

This evidence package is suitable for blocked Sprint 11 handoff review only. Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

## Final Verification Addendum

- Post-format Release build: PASS; 0 warnings, 0 errors.
- Post-format unit tests: PASS; 622 passed, 0 failed, 0 skipped (`postformat-unit.trx`).
- Post-format integration tests: PASS; 245 passed, 0 failed, 0 skipped (`postformat-integration.trx`).
- Post-format smoke tests: PASS; 11 passed, 0 failed, 0 skipped (`postformat-smoke.trx`).
- Format verification: PASS; `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal`.
- Flake-sensitive Release profile: PASS; summary `TestResults/flake-sensitive/20260624-165531/summary.md`; 20 focused, 10 full integration, and 5 full solution iterations; total duration `00:49:08.6053220`.
