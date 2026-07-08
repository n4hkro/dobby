# Sprint 11 Startup Performance Evidence

Status: HD-S11-002 policy path accepted only; closure blocked until an ADR-0042-compliant baseline machine/profile is named and N=100 evidence is collected.

## AC-014

AC-014 requires ADR-0042 baseline hardware, methodology, artifact format, and accepted P95 evidence for startup and login-to-shell paths.

Authority decisions approve the path; P1/release-ready is not closed until evidence passes.

## Baseline Disqualification Evidence

Current local machine evidence: i5-1135G7 @ 2.40GHz, 4C/8T, ~8.3GB RAM; disqualified for ADR-0042 release baseline because it fails the 16GB RAM minimum and, under strict reading, the 2.5GHz base-clock threshold. Informational measurements on this machine do not close AC-014/AC-051.

This is baseline disqualification evidence only, not release timing/performance evidence.

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
