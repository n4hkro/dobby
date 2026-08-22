# Flake-Sensitive Verification

This tool runs the flake-sensitive test matrix with repeat counts and a shared log format. Use it before release/handover, after fixing suspected flakes, or before accepting sprint closure.

Do not treat a passing run as proof that flakes can never recur. A passing run means the selected profile did not reproduce the failure.

## Profiles

Default local profile:

```text
Focused WorkSessionShellIntegrationTests: 5 iterations
Full Dobby.IntegrationTests project: 2 iterations
Sequential unit/integration/smoke matrix: 1 iteration
```

Release profile:

```text
Focused WorkSessionShellIntegrationTests: 20 iterations
Full Dobby.IntegrationTests project: 10 iterations
Sequential unit/integration/smoke matrix: 5 iterations
```

## Commands

Baseline sample:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 `
  -FocusedIterations 1 `
  -IntegrationIterations 1 `
  -SolutionIterations 1
```

Local confidence check:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1
```

Release/handover check:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 -Profile Release
```

The script runs this build once before no-build test loops unless `-NoBuild` is passed:

```powershell
dotnet build .\Dobby.sln -c Release --no-restore -warnaserror
```

Use `-NoBuild` only when intentionally testing existing Release artifacts.

At least one test iteration is required. The script rejects build-only runs and `-NoBuild` runs where focused, integration, and solution iteration counts are all `0`; those runs are smoke checks for the script itself, not valid flake-sensitive verification evidence.

## Logs

Every run writes:

```text
TestResults/flake-sensitive/<timestamp>/
- summary.md
- build-001.out.txt
- build-001.err.txt
- focused-001.out.txt
- focused-001.err.txt
- integration-001.out.txt
- integration-001.err.txt
- matrix-unit-001.out.txt
- matrix-unit-001.err.txt
- matrix-integration-001.out.txt
- matrix-integration-001.err.txt
- matrix-smoke-001.out.txt
- matrix-smoke-001.err.txt
```

`summary.md` records the profile, command, iteration, start/end time, duration, exit code, stdout/stderr files, and result.
Each matrix iteration deliberately runs the three test projects sequentially. Running
`dotnet test Dobby.sln` starts separate testhosts concurrently and can turn host-process,
SQLite, dispatcher, and KDF scheduling contention into false timeout/file-lock failures.

## CI Ownership

Do not create a new CI platform or workflow only for this guard unless repository CI ownership already exists or a human decision authorizes it. If CI ownership exists, prefer a manual or nightly guard first and preserve `TestResults/flake-sensitive/<timestamp>/` as an artifact on failure.
