# Sprint 11 Unit Integration Smoke Results

Build ID: local-rc-20260707T104904Z-61b99da821b0
Evidence root: artifacts\release-evidence\local-rc-20260707T104904Z-61b99da821b0
Acceptance criteria: AC-041

Final local verification status: PASSED for build, format, unit, integration, smoke, and portable publish checks in this evidence pass.

## Commands

| Command | Result |
| --- | --- |
| `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal` | PASS |
| `dotnet build .\Dobby.sln -c Release --no-restore` | PASS: 0 warnings, 0 errors |
| `dotnet test tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-build --logger "trx;LogFileName=main-update-final-unit.trx"` | PASS: 664 passed, 0 failed, 0 skipped |
| `dotnet test tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-build --logger "trx;LogFileName=main-update-final2-integration.trx"` | PASS: 295 passed, 0 failed, 0 skipped |
| `dotnet test tests\Dobby.SmokeTests\Dobby.SmokeTests.csproj -c Release --no-build --logger "trx;LogFileName=main-update-final-smoke.trx"` | PASS: 20 passed, 0 failed, 0 skipped |
| `.\tools\publish-portable\publish-portable.ps1 -NoRestore` | PASS |

## Artifacts

- Unit TRX: `tests\Dobby.UnitTests\TestResults\main-update-final-unit.trx`
- Integration TRX: `tests\Dobby.IntegrationTests\TestResults\main-update-final2-integration.trx`
- Smoke TRX: `tests\Dobby.SmokeTests\TestResults\main-update-final-smoke.trx`
- Portable executable: `artifacts\publish\portable\Dobby.Host\Dobby.Host.exe`

## Release Readiness Boundary

These verification results prove the local build/test/publish gate for this evidence pass only.
They do not close release readiness while Task 4 remains blocked on an ADR-0042-compliant baseline machine/profile with N=100 startup samples and Task 5 remains blocked on snapshot-based clean VM/profile install, uninstall, upgrade, and data-preservation evidence.
