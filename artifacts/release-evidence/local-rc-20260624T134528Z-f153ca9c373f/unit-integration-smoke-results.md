# Sprint 11 Unit, Integration, and Smoke Results

Status: LOCAL BASELINE PASSED; final no-restore verification appended during Sprint 11 closure.

## Baseline Release Runs

| Project | Command | Passed | Failed | Skipped | Notes |
| --- | --- | ---: | ---: | ---: | --- |
| Unit | `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=baseline-unit.trx"` | 622 | 0 | 0 | Baseline before Sprint 11 evidence edits. |
| Integration | `dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=baseline-integration.trx"` | 241 | 0 | 0 | Baseline before Sprint 11 evidence contract. |
| Smoke | `dotnet test tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj -c Release --no-build --logger "trx;LogFileName=baseline-smoke.trx"` | 11 | 0 | 0 | Baseline before Sprint 11 evidence edits. |

## Sprint 11 Targeted RED/GREEN Evidence

| Test | RED | GREEN |
| --- | --- | --- |
| `Sprint11ReleaseEvidenceTests` | Failed because no Sprint 11 `local-rc-*` evidence root existed. | Expected to pass after this evidence root is generated. |
| `ShellUiStaticScanTests.License_import_long_running_action_reports_background_operations` | Failed because `LicenseImport_Click` did not call `ReportLicenseImportBackgroundOperation(`. | Passed after `MainWindow.xaml.cs` emitted Running and terminal background-operation snapshots for license import. |

## AC-041

AC-041 separation is preserved: unit, integration, and smoke evidence are recorded separately and use isolated result files. This file does not merge smoke or WPF integration evidence into unit evidence.

## Final Verification Placeholder

Final no-restore verification commands are run after evidence generation:

- `dotnet build .\Dobby.sln -c Release --no-restore`
- `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj -c Release --no-build`
- `dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj -c Release --no-build`
- `dotnet test tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj -c Release --no-build`
- `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal`

If any final command fails, this Sprint 11 package remains blocked.

## Final No-Restore Verification

| Gate | Result |
| --- | --- |
| Release build | PASS; 0 warnings, 0 errors. |
| Unit tests | PASS; 622 passed, 0 failed, 0 skipped. |
| Integration tests | PASS; 245 passed, 0 failed, 0 skipped. |
| Smoke tests | PASS; 11 passed, 0 failed, 0 skipped. |
| Format verify | PASS. |
| Flake-sensitive Release profile | PASS; `TestResults/flake-sensitive/20260624-165531/summary.md`; 20 focused, 10 integration, 5 solution iterations; `00:49:08.6053220`. |