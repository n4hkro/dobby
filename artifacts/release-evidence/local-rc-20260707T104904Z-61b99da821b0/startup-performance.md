# Sprint 11 Startup Performance Evidence

Status: HD-S11-002 policy path accepted only; Task 4 authority is durable, but closure remains blocked until an ADR-0042-compliant baseline machine/profile is named and N=100 evidence is collected.

## Task 4 Authority Stance

- Approved combination: `D1-1, D2-1-after-measurement, D3-1R, D4-1`.
- Authority decisions approve the path; P1/release-ready is not closed until evidence passes.
- `docs/_perf/baselines/v1.0.0.json` may be created only after measurement completes and release authority accepts the baseline artifact.
- The future accepted baseline artifact must include profile id, hardware/OS specification, measurement method, raw artifact hashes, P95 values, and baseline comparison source/hash.
- Measurement must bind to the existing startup instrumentation and milestones.
- Raw JSONL samples and `summary.json` must be hashed.
- Manual or video fallback is not sufficient for the release gate.

## Baseline Disqualification Evidence

Current local machine evidence remains disqualification evidence only: `i5-1135G7 @ 2.40GHz`, `4C/8T`, approximately `8.3GB RAM`. It fails the ADR-0042 16GB RAM minimum and, under strict reading, the 2.5GHz base-clock threshold. Informational measurements on this machine do not close AC-014/AC-051.

## Current Automated Guards

- `tests/Dobby.IntegrationTests/Startup/PreLoginAllowedWorkloadTests.cs`
- `tests/Dobby.IntegrationTests/Startup/StartupBootstrapWiringTests.cs`
- `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/ShellUiStaticScanTests.cs`

## Blocked Performance Fields

| Field | Status |
| --- | --- |
| ADR-0042 hardware profile | BLOCKED by HD-S11-002 |
| Measurement process | BLOCKED pending approved baseline machine/profile |
| Artifact format/location | BLOCKED pending accepted baseline artifact |
| `docs/_perf/baselines/v1.0.0.json` | NOT CREATED |
| Raw JSONL sample hashes | NOT COLLECTED |
| `summary.json` hash | NOT COLLECTED |
| N=100 cold samples | NOT COLLECTED |
| N=100 warm samples | NOT COLLECTED |
| N=100 login-to-shell samples | NOT COLLECTED |
| P95 regression decision | NOT CLAIMED |

Developer workstation, high-end machine, or CI-runner timings are informational only and are not used here to claim release readiness.
