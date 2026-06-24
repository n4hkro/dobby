# Sprint 11 Startup Performance Evidence

Status: HD-S11-002 BLOCKED PENDING HUMAN DECISION.

## AC-014

AC-014 requires ADR-0042 baseline hardware, methodology, artifact format, and accepted P95 evidence for startup and login-to-shell paths.

## Current Automated Guards

- `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`
- `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs`
- `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`

## Blocked Performance Fields

| Field | Status |
| --- | --- |
| ADR-0042 hardware profile | BLOCKED by HD-S11-002 |
| Measurement process | BLOCKED by HD-S11-002 |
| Artifact format/location | BLOCKED by HD-S11-002 |
| N=100 cold samples | NOT COLLECTED |
| N=100 warm samples | NOT COLLECTED |
| N=100 login-to-shell samples | NOT COLLECTED |
| P95 regression decision | NOT CLAIMED |

Developer workstation, high-end machine, or CI-runner timings are informational only and are not used here to claim release readiness.
