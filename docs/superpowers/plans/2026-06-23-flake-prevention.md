# Flake Prevention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce unexpected flaky test behavior by measuring the current failure shape first, avoiding speculative production changes, and adding deterministic guardrails for WPF and flake-sensitive verification.

**Architecture:** Keep production behavior untouched unless a fresh root-cause investigation reproduces a failure and proves a production defect. First rule out test harness leakage, WPF/STA/Dispatcher cleanup, pending action state, shared/static state, test ordering, and missing explicit completion signals. Use repeat verification as confidence evidence, not as proof that a root cause is fixed.

**Tech Stack:** .NET 10, xUnit, WPF/STA integration tests, PowerShell, `dotnet build --no-restore`, `dotnet test --no-build --no-restore`, `dotnet format --no-restore`.

---

## Operating Principle

Do not write another behavioral fix unless the current failure reproduces.
If it does not reproduce, preserve the evidence, add guardrails, and avoid speculative changes.

Passing once does not prove the flake is gone. It only establishes that the current baseline sample did not reproduce the failure.

---

### Task 0: Establish Current Flake Baseline

**Files:**
- Read: `tests/Dobby.IntegrationTests/Shell/WpfIntegrationTestCollection.cs`
- Read: `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`
- Read: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- Use: `tools/verify-flake-sensitive-tests/verify-flake-sensitive-tests.ps1`
- Output: `TestResults/flake-sensitive/<timestamp>/summary.md`

- [ ] **Step 1: Run a baseline sample**

Run from the repository root:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 `
  -FocusedIterations 1 `
  -IntegrationIterations 1 `
  -SolutionIterations 1
```

Expected: the script writes the same log format used by repeat verification:

```text
TestResults/flake-sensitive/<timestamp>/
- summary.md
- build-001.out.txt
- build-001.err.txt
- focused-001.out.txt
- focused-001.err.txt
- integration-001.out.txt
- integration-001.err.txt
- solution-001.out.txt
- solution-001.err.txt
```

This is a baseline sample, not proof that the flake is gone.

- [ ] **Step 2: If failure reproduces, stop speculative patching**

If any command fails, record the command, iteration, exit code, stack trace location, and stdout/stderr files from `summary.md`. Continue root-cause investigation before adding more fixes.

- [ ] **Step 3: If failure does not reproduce, keep the claim narrow**

If the baseline sample passes, classify it as:

```text
Currently not reproduced after existing WPF collection and TaskCompletionSource harness changes.
```

Do not claim:

```text
Fixed forever.
Root cause proven.
```

### Task 1: Audit Remaining Timing And WPF Harness Risk

**Files:**
- Read: `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs`
- Read: `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs`
- Read: `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs`
- Read: `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs`
- Read: `tests/Dobby.IntegrationTests/Startup/SingleInstanceCoordinatorTests.cs`
- Read: `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapCoordinatorTests.cs`
- Read: `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapTransactionTests.cs`
- Record: `docs/superpowers/flake-sensitive-wait-classification.md`

- [ ] **Step 1: Search for timing-sensitive waits**

Run:

```powershell
rg -n "SpinWait|Thread\.Sleep|Task\.Delay|TimeSpan\.FromSeconds|DispatcherPriority\.ApplicationIdle" tests
```

- [ ] **Step 2: Classify every hit before editing**

Each hit must be recorded as exactly one of:

```text
render/dispatcher pump acceptable
explicit async completion already present
deterministic fake-clock/domain-time advance
timing-risk requiring a reproduced failure before behavior changes
```

Do not mechanically replace every wait. WPF rendering pumps and bounded fail-safe timeouts can be legitimate test harness code.

- [ ] **Step 3: Confirm current C4/F1 harness state**

Current expected state:

```text
tests/Dobby.IntegrationTests/Shell/WpfIntegrationTestCollection.cs exists.
WorkSessionShellIntegrationTests uses WpfIntegrationTestCollection.
ShellSnapshotTests uses WpfIntegrationTestCollection.
WorkSessionShellIntegrationTests uses TaskCompletionSource for disable/uninstall lifecycle completion.
```

- [ ] **Step 4: Only patch timing-risk behavior after reproduction**

If the baseline reproduces a failure and the classification points to test harness state leakage, patch the narrowest test harness cause first. Check WPF/STA/Dispatcher cleanup, pending action reset, shared/static state, test ordering, and explicit completion signals before blaming production lifecycle logic.

### Task 2: Add Local Repeat Verification Guard

**Files:**
- Use: `tools/verify-flake-sensitive-tests/verify-flake-sensitive-tests.ps1`
- Use: `tools/verify-flake-sensitive-tests/README.md`
- Output: `TestResults/flake-sensitive/<timestamp>/summary.md`

- [ ] **Step 1: Use the default local profile for normal confidence checks**

Run:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1
```

Expected default local profile:

```text
Focused WorkSessionShellIntegrationTests: 5 iterations
Full Dobby.IntegrationTests project: 2 iterations
Full Dobby.sln: 1 iteration
```

- [ ] **Step 2: Use the release profile for handover or sprint closure**

Run:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 -Profile Release
```

Expected release profile:

```text
Focused WorkSessionShellIntegrationTests: 20 iterations
Full Dobby.IntegrationTests project: 10 iterations
Full Dobby.sln: 5 iterations
```

- [ ] **Step 3: Keep build safety on by default**

The script must run this once before no-build loops unless `-NoBuild` is explicitly passed:

```powershell
dotnet build .\Dobby.sln -c Release --no-restore -warnaserror
```

Use `-NoBuild` only when intentionally testing existing Release artifacts.

- [ ] **Step 4: Reject no-op and build-only verification runs**

The script must require at least one test iteration after profile defaults and explicit overrides are resolved:

```powershell
$totalTestIterations = $FocusedIterations + $IntegrationIterations + $SolutionIterations
if ($totalTestIterations -le 0) {
    throw 'At least one test iteration is required for flake-sensitive verification.'
}
```

This check must run before `TestResults/flake-sensitive/<timestamp>/` is created so invalid runs do not produce misleading `summary.md` artifacts.

- [ ] **Step 5: Preserve identical log format for baseline and repeat runs**

Every run writes:

```text
TestResults/flake-sensitive/<timestamp>/
- summary.md
- <phase>-<iteration>.out.txt
- <phase>-<iteration>.err.txt
```

`summary.md` must record:

```text
profile
command
iteration
start time
end time
duration
exit code
stdout/stderr file names
result
```

If a command fails, the script exits non-zero after recording the failed phase and iteration.

### Task 3: Keep CI Guard Manual/Nightly Unless Ownership Exists

**Files:**
- Read: repository CI configuration if it exists
- Modify only with ownership: existing CI workflow file
- Otherwise document: `tools/verify-flake-sensitive-tests/README.md`

- [ ] **Step 1: Do not create a new CI platform without authorization**

Current repository state may have no CI workflow. Do not create `.github`, Azure Pipelines, or any new CI platform/workflow unless repository CI ownership already exists or a human decision authorizes it.

- [ ] **Step 2: If CI ownership exists, add manual/nightly guard only**

Recommended policy:

```text
Normal PR: build + standard tests + format.
Release/nightly/manual: repeat flake-sensitive matrix.
Escalate to required PR gate only if flakes return.
```

- [ ] **Step 3: Upload or preserve failure logs**

If CI is later added, preserve `TestResults/flake-sensitive/<timestamp>/` as a build artifact when the guard fails.

### Task 4: Add Future Flake Triage Rule To Agent Instructions

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Add a narrow WPF/STA rule**

Add this instruction under quality guidance:

```markdown
For flaky WPF/STA/Dispatcher tests, first rule out test harness state leakage before blaming production logic: Window.Close/dispatcher cleanup, pending action reset, shared/static state, test ordering, and explicit completion signals must be checked.
```

- [ ] **Step 2: Add the no-speculative-fix rule**

Add:

```markdown
Do not write behavioral fixes for flakes unless the current failure reproduces. If it does not reproduce, preserve evidence, add guardrails, and avoid speculative changes.
```

### Task 5: Use Precise Review Status Language

**Files:**
- Use in sprint review notes or cross-review responses

- [ ] **Step 1: Separate review convergence from sprint acceptance**

Use:

```text
review convergence = reviewers agree on the active findings and limitations.
sprint acceptance = the defects/gaps are fixed and the verification matrix is green.
```

- [ ] **Step 2: Keep CONFIRMED FAIL wording clear**

Use:

```text
A CONFIRMED FAIL commitment can still be review-converged; it means reviewers agree the sprint commitment is currently failing, not that the sprint is accepted.
```

- [ ] **Step 3: Do not overstate C4/F1 evidence**

Use:

```text
Cross-round verification strongly indicates suite/order/timing/isolation sensitivity because broad solution, direct integration project, and focused class results disagreed across rounds.
```

Avoid:

```text
This proves the root cause.
This command always fails.
This command always passes.
```

---

## Completion Criteria

- Task 0 baseline sample has been run or explicitly deferred with reason.
- Every timing hit from Task 1 has a classification table entry.
- No new behavioral fix was written unless a fresh failure reproduced.
- Local repeat guard script exists, builds by default, supports `-NoBuild`, and writes baseline/repeat logs in one format.
- The repeat guard rejects no-op and build-only runs instead of writing misleading PASS summaries.
- CI workflow is not created unless repository ownership exists or a human authorizes it.
- `AGENTS.md` includes the narrow WPF/STA flake triage rule.
- Review wording separates `review convergence` from `sprint acceptance`.

## Recommended Next Execution

1. Run Task 0 baseline sample.
2. If it fails, debug that exact failure shape before changing behavior.
3. If it passes, keep the claim to "currently not reproduced" and rely on Task 2 for release/handover confidence.
