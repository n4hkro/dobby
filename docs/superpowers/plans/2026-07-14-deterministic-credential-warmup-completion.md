# Deterministic Credential Warmup Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the credential-warmup test's scheduling-dependent spin waits with an observable task completion contract, verify the merged product at release strength, and leave local and GitHub state with only `main`.

**Architecture:** `ILoginCredentialWarmupWorkflow` will expose `Task WarmCredentialsAsync(LoginCredentialWarmup warmup)`. The direct and deferred workflows will return the existing background operation, the WPF login window will explicitly discard it to remain non-blocking, and tests will await that exact operation instead of polling private state.

**Tech Stack:** .NET SDK 10.0.202, C# 14, WPF on `net10.0-windows`, xUnit 2.9.3, Shouldly 4.3.0, PowerShell, Git.

## Global Constraints

- Follow `AGENTS.md`: correctness, deterministic synchronization, strongest relevant no-restore verification, and refreshed portable output take priority over elapsed time.
- Do not change credential validation, fingerprinting, cache version semantics, dispatcher debounce behavior, login submit behavior, or non-fatal exception containment.
- Do not add production cancellation, retries, timeouts, credential logging, task storage, or global xUnit/thread-pool configuration.
- Keep `ArgumentNullException.ThrowIfNull(warmup)` synchronous at each public warmup boundary.
- Tests may use `WaitAsync(TimeSpan.FromSeconds(10))` only as a stuck-test guard around an explicit completion task; do not poll counters or private fields.
- Run restore-free commands only; local restore assets are already present.
- Treat warnings as errors and preserve the repository's CRLF checkout policy.
- Do not push or delete a branch until the merged `main` release verification and portable publish both succeed.

---

### Task 1: Make credential warmup completion observable

**Files:**
- Modify: `src/Dobby.Host/Dobby.Host.csproj`
- Modify: `tests/Dobby.UnitTests/Host/LoginWindowRuntimeWiringTests.cs:1-3,155-169,375-403`
- Modify: `src/Dobby.Host/Login/LoginRuntimeWorkflow.cs:51-54,128-134`
- Modify: `src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs:24-42`
- Modify: `src/Dobby.Host/Login/LoginWindow.xaml.cs:279-299`
- Modify: `tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs:122-165`
- Reference: `docs/superpowers/specs/2026-07-14-credential-warmup-completion-design.md`

**Interfaces:**
- Consumes: `LoginCredentialWarmup`, `PreparedLoginEntryRuntime`, `HostPreLoginRuntime`, and the existing versioned warmed-credential cache.
- Produces: `Task ILoginCredentialWarmupWorkflow.WarmCredentialsAsync(LoginCredentialWarmup warmup)`; callers can observe completion without changing cache semantics.
- `LoginWindow.QueueCredentialWarmup` consumes the task-returning contract but deliberately discards the returned task to keep the WPF event path non-blocking.

- [ ] **Step 1: Add a test-only visibility seam and write RED contract/completion tests**

Add this item group to `src/Dobby.Host/Dobby.Host.csproj` after the first `PropertyGroup`:

```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>Dobby.UnitTests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

Add these usings to `tests/Dobby.UnitTests/Host/LoginWindowRuntimeWiringTests.cs`:

```csharp
using Dobby.Core;
using Dobby.Host;
using Dobby.Host.Login;
using System.IO;
using Shouldly;
```

Replace `Login_window_warms_available_credentials_before_submit` with:

```csharp
[Fact]
public void Login_window_warms_available_credentials_before_submit()
{
    var codeBehind = ReadSource("src", "Dobby.Host", "Login", "LoginWindow.xaml.cs");
    var workflow = ReadSource("src", "Dobby.Host", "Login", "LoginRuntimeWorkflow.cs");
    var deferredWorkflow = ReadSource("src", "Dobby.Host", "Login", "DeferredLoginRuntimeWorkflow.cs");

    codeBehind.ShouldContain("QueueCredentialWarmup();");
    codeBehind.ShouldContain("ScheduleCredentialWarmup();");
    codeBehind.ShouldContain("ILoginCredentialWarmupWorkflow warmupWorkflow");
    codeBehind.ShouldContain("_ = warmupWorkflow.WarmCredentialsAsync(new LoginCredentialWarmup(");
    codeBehind.ShouldContain("viewModel.Password");
    workflow.ShouldContain("public interface ILoginCredentialWarmupWorkflow");
    workflow.ShouldContain("Task WarmCredentialsAsync(LoginCredentialWarmup warmup)");
    workflow.ShouldContain("TryUseWarmedCredential(submission");
    workflow.ShouldContain("LoginWithValidatedCredential(");
    deferredWorkflow.ShouldContain("public Task WarmCredentialsAsync(LoginCredentialWarmup warmup)");
    deferredWorkflow.ShouldContain("await warmupWorkflow.WarmCredentialsAsync(warmup).ConfigureAwait(false);");
}
```

Add this test before `Login_window_can_render_before_runtime_is_attached`:

```csharp
[Fact]
public async Task Deferred_login_warmup_task_completes_only_after_inner_warmup()
{
    var inner = new BlockingCredentialWarmupWorkflow();
    var preparedRuntime = new PreparedLoginEntryRuntime(
        new HostPreLoginRuntime(inner, null!, null!),
        Task.FromResult<PreparedLoginRuntime?>(null));
    var deferred = new DeferredLoginRuntimeWorkflow(
        () => Task.FromResult<PreparedLoginEntryRuntime?>(preparedRuntime));
    var method = typeof(DeferredLoginRuntimeWorkflow).GetMethod(
        nameof(BlockingCredentialWarmupWorkflow.WarmCredentialsAsync));

    method.ShouldNotBeNull();
    var warmupTask = method.Invoke(deferred, [new LoginCredentialWarmup(
        "owner",
        "correct password",
        CorrelationId.New())])
        .ShouldBeOfType<Task>();

    await inner.WarmupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    warmupTask.IsCompleted.ShouldBeFalse();
    inner.AllowCompletion.TrySetResult(true).ShouldBeTrue();
    await warmupTask.WaitAsync(TimeSpan.FromSeconds(10));
}
```

Add this temporary dual-contract fake before `ReadSource`:

```csharp
private sealed class BlockingCredentialWarmupWorkflow :
    ILoginRuntimeWorkflow,
    ILoginCredentialWarmupWorkflow
{
    public TaskCompletionSource<bool> WarmupEntered { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> AllowCompletion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LoginRuntimeWorkflowResult Submit(LoginSubmission submission)
    {
        throw new NotSupportedException();
    }

    public void WarmCredentials(LoginCredentialWarmup warmup)
    {
        throw new InvalidOperationException("The legacy synchronous warmup contract must not be invoked.");
    }

    public async Task WarmCredentialsAsync(LoginCredentialWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);
        WarmupEntered.TrySetResult(true);
        await AllowCompletion.Task.ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify RED for the missing async contract**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj `
  -c Debug `
  --no-restore `
  --filter "FullyQualifiedName~LoginWindowRuntimeWiringTests.Login_window_warms_available_credentials_before_submit|FullyQualifiedName~LoginWindowRuntimeWiringTests.Deferred_login_warmup_task_completes_only_after_inner_warmup"
```

Expected: FAIL. The static wiring test must report the missing `WarmCredentialsAsync` source text, and the deferred behavior test must report that the reflected method is null. If either test passes before production changes, stop and correct the RED test.

- [ ] **Step 3: Implement the task-returning production contract**

Replace the interface and direct workflow method in `src/Dobby.Host/Login/LoginRuntimeWorkflow.cs` with:

```csharp
public interface ILoginCredentialWarmupWorkflow
{
    Task WarmCredentialsAsync(LoginCredentialWarmup warmup);
}

public Task WarmCredentialsAsync(LoginCredentialWarmup warmup)
{
    ArgumentNullException.ThrowIfNull(warmup);

    var version = Interlocked.Increment(ref credentialWarmupVersion);
    return Task.Run(() => WarmCredentialsCore(warmup, version));
}
```

Replace the warmup method in `src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs` with:

```csharp
public Task WarmCredentialsAsync(LoginCredentialWarmup warmup)
{
    ArgumentNullException.ThrowIfNull(warmup);

    return Task.Run(async () =>
    {
        try
        {
            var runtime = await runtimeTaskProvider().ConfigureAwait(false);
            if (runtime?.PreLoginRuntime.LoginWorkflow is ILoginCredentialWarmupWorkflow warmupWorkflow)
            {
                await warmupWorkflow.WarmCredentialsAsync(warmup).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    });
}
```

Replace the invocation in `src/Dobby.Host/Login/LoginWindow.xaml.cs` with:

```csharp
_ = warmupWorkflow.WarmCredentialsAsync(new LoginCredentialWarmup(
    username,
    credential,
    CorrelationId.New()));
```

- [ ] **Step 4: Refactor the RED test to direct task usage and remove the legacy fake method**

Replace the reflective invocation block in `Deferred_login_warmup_task_completes_only_after_inner_warmup` with:

```csharp
var warmupTask = deferred.WarmCredentialsAsync(new LoginCredentialWarmup(
    "owner",
    "correct password",
    CorrelationId.New()));

await inner.WarmupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
warmupTask.IsCompleted.ShouldBeFalse();
inner.AllowCompletion.TrySetResult(true).ShouldBeTrue();
await warmupTask.WaitAsync(TimeSpan.FromSeconds(10));
```

Delete this temporary method from `BlockingCredentialWarmupWorkflow`:

```csharp
public void WarmCredentials(LoginCredentialWarmup warmup)
{
    throw new InvalidOperationException("The legacy synchronous warmup contract must not be invoked.");
}
```

The remaining fake must implement only `Task WarmCredentialsAsync(LoginCredentialWarmup warmup)`.

- [ ] **Step 5: Replace the flaky integration wait with the observable completion task**

Replace the integration test and delete `WaitForCompletedCredentialWarmup` from `tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs`:

```csharp
[Fact]
public async Task Submit_reuses_completed_credential_warmup_without_repeating_password_verification()
{
    using var fixture = LoginRuntimeFixture.Create();
    fixture.InsertUser("user-1", "owner", isActive: true);
    var verifier = new CountingCredentialVerifier(succeeds: true);
    var workflow = fixture.CreateWorkflow(credentialVerifier: verifier);

    await workflow.WarmCredentialsAsync(new LoginCredentialWarmup(
            "owner",
            "correct password",
            CorrelationId.New()))
        .WaitAsync(TimeSpan.FromSeconds(10));

    verifier.VerifyCount.ShouldBe(1);

    var result = workflow.Submit(new LoginSubmission(
        "owner",
        "correct password",
        DisplayName: null,
        RememberMe: true,
        CorrelationId.New()));

    result.IsAuthenticated.ShouldBeTrue();
    verifier.VerifyCount.ShouldBe(1);
    fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM Sessions WHERE UserId = 'user-1' AND InvalidatedAtUtc IS NULL;").ShouldBe(1);
    fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM AuditLog WHERE EventName = 'user.login' AND ActorId = 'user-1' AND Result = 'success';").ShouldBe(1);
}
```

- [ ] **Step 6: Prove that the timing and reflection waits were removed**

Run:

```powershell
rg -n "SpinWait|WaitForCompletedCredentialWarmup|warmedCredential" .\tests\Dobby.IntegrationTests\Host\LoginRuntimeWorkflowTests.cs
if ($LASTEXITCODE -eq 0) {
    throw 'The credential warmup integration test still contains timing or private-state polling.'
}
if ($LASTEXITCODE -ne 1) {
    throw "rg failed with exit code $LASTEXITCODE."
}
```

Expected: `rg` emits no match, returns `1`, and the guard script exits without throwing.

- [ ] **Step 7: Run focused GREEN verification**

Run:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj `
  -c Debug `
  --no-restore `
  --filter "FullyQualifiedName~LoginWindowRuntimeWiringTests.Login_window_warms_available_credentials_before_submit|FullyQualifiedName~LoginWindowRuntimeWiringTests.Deferred_login_warmup_task_completes_only_after_inner_warmup"

dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj `
  -c Debug `
  --no-restore `
  --filter "FullyQualifiedName=Dobby.IntegrationTests.Host.LoginRuntimeWorkflowTests.Submit_reuses_completed_credential_warmup_without_repeating_password_verification"
```

Expected: both commands exit `0`; unit result is 2 passed and integration result is 1 passed, with no warnings or errors.

- [ ] **Step 8: Repeat the former flake 20 times without rebuilding or restoring**

Run:

```powershell
$filter = 'FullyQualifiedName=Dobby.IntegrationTests.Host.LoginRuntimeWorkflowTests.Submit_reuses_completed_credential_warmup_without_repeating_password_verification'
for ($iteration = 1; $iteration -le 20; $iteration++) {
    dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj `
      -c Debug `
      --no-build `
      --no-restore `
      --filter $filter
    if ($LASTEXITCODE -ne 0) {
        throw "Credential warmup repetition $iteration failed with exit code $LASTEXITCODE."
    }
}
```

Expected: all 20 invocations exit `0` with one passed test each.

- [ ] **Step 9: Verify the task diff and commit the behavior change**

Run:

```powershell
dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror
git diff --check
git diff -- src/Dobby.Host/Dobby.Host.csproj src/Dobby.Host/Login/LoginRuntimeWorkflow.cs src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs src/Dobby.Host/Login/LoginWindow.xaml.cs tests/Dobby.UnitTests/Host/LoginWindowRuntimeWiringTests.cs tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs
```

Expected: build exits `0`, `git diff --check` emits no errors, and the diff contains only the approved contract, call-site, test seam, and deterministic tests.

Commit:

```powershell
git add -- src/Dobby.Host/Dobby.Host.csproj src/Dobby.Host/Login/LoginRuntimeWorkflow.cs src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs src/Dobby.Host/Login/LoginWindow.xaml.cs tests/Dobby.UnitTests/Host/LoginWindowRuntimeWiringTests.cs tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs
git commit -m "fix: make credential warmup completion observable"
```

---

### Task 2: Verify the feature branch before integration

**Files:**
- Verify: `Dobby.sln`
- Verify: `src/Dobby.Host/Login/LoginRuntimeWorkflow.cs`
- Verify: `src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs`
- Verify: `src/Dobby.Host/Login/LoginWindow.xaml.cs`
- Verify: `tests/Dobby.UnitTests/Host/LoginWindowRuntimeWiringTests.cs`
- Verify: `tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs`

**Interfaces:**
- Consumes: the committed `WarmCredentialsAsync` contract from Task 1.
- Produces: a clean, warning-free feature-branch commit ready for fast-forward integration.

- [ ] **Step 1: Run the affected test classes in Release configuration**

Run:

```powershell
dotnet build .\Dobby.sln -c Release --no-restore -warnaserror

dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj `
  -c Release `
  --no-build `
  --no-restore `
  --filter "FullyQualifiedName~LoginWindowRuntimeWiringTests"

dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj `
  -c Release `
  --no-build `
  --no-restore `
  --filter "FullyQualifiedName~LoginRuntimeWorkflowTests"
```

Expected: build and both test commands exit `0` with zero failures.

- [ ] **Step 2: Run one complete solution test pass and formatting verification**

Run:

```powershell
dotnet test .\Dobby.sln -c Release --no-build --no-restore
dotnet format .\Dobby.sln --verify-no-changes --no-restore
git diff --check
git status --short --branch
```

Expected: solution tests and format verification exit `0`, `git diff --check` emits no errors, and status shows a clean `codex/remaining-product-work` branch.

- [ ] **Step 3: Review the committed change against the approved design**

Run:

```powershell
git diff --stat main...codex/remaining-product-work
git diff main...codex/remaining-product-work -- src/Dobby.Host/Dobby.Host.csproj src/Dobby.Host/Login/LoginRuntimeWorkflow.cs src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs src/Dobby.Host/Login/LoginWindow.xaml.cs tests/Dobby.UnitTests/Host/LoginWindowRuntimeWiringTests.cs tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs docs/superpowers/specs/2026-07-14-credential-warmup-completion-design.md docs/superpowers/plans/2026-07-14-deterministic-credential-warmup-completion.md
```

Expected: no task storage, production timeout, retry, cancellation, cache semantic change, global test configuration, unrelated refactor, or credential logging is present.

---

### Task 3: Fast-forward `main`, run the release gate, and publish portable output

**Files:**
- Verify: `Dobby.sln`
- Execute: `tools/verify-flake-sensitive-tests/verify-flake-sensitive-tests.ps1`
- Execute: `tools/publish-portable/publish-portable.ps1`
- Produce: `artifacts/publish/portable/Dobby.Host/Dobby.Host.exe`

**Interfaces:**
- Consumes: the clean, reviewed `codex/remaining-product-work` branch from Task 2.
- Produces: locally verified `main` at the exact feature-branch commit and a refreshed portable executable.

- [ ] **Step 1: Refresh Git state and prove fast-forward safety**

Run:

```powershell
git fetch --all --prune --tags
git status --porcelain=v2 --branch --untracked-files=all
git worktree list --porcelain
git branch -a -vv --no-abbrev
git merge-base --is-ancestor main codex/remaining-product-work
```

Expected: fetch exits `0`; the worktree is clean; the only registered worktree is `C:/Users/Evaks/Desktop/dobby`; local branches are `main` and `codex/remaining-product-work`; remote heads contain only `origin/main`; and `merge-base --is-ancestor` exits `0`.

- [ ] **Step 2: Fast-forward local `main`**

Run:

```powershell
git switch main
git merge --ff-only codex/remaining-product-work
```

Expected: both commands exit `0`; `main` points to the feature branch's commit without a merge commit or conflict.

- [ ] **Step 3: Run the full Release flake-sensitive profile on merged `main`**

Run:

```powershell
.\tools\verify-flake-sensitive-tests\verify-flake-sensitive-tests.ps1 -Profile Release
```

Expected: exit `0`; one Release warning-as-error build, 20 focused `WorkSessionShellIntegrationTests` iterations, 10 full integration-project iterations, and 5 full-solution iterations all report `PASS`. If any run fails, preserve its `TestResults/flake-sensitive` log directory and return to systematic debugging before push or cleanup.

- [ ] **Step 4: Verify formatting and refresh the portable test build**

Run:

```powershell
dotnet format .\Dobby.sln --verify-no-changes --no-restore
.\tools\publish-portable\publish-portable.ps1 -NoRestore
Test-Path -LiteralPath '.\artifacts\publish\portable\Dobby.Host\Dobby.Host.exe' -PathType Leaf
```

Expected: format exits `0`, publish exits `0`, and `Test-Path` prints `True`. The executable path is `C:\Users\Evaks\Desktop\dobby\artifacts\publish\portable\Dobby.Host\Dobby.Host.exe`.

---

### Task 4: Push `main` and remove every remaining non-main branch/worktree reference

**Files:**
- Update remote ref: `refs/heads/main` on `origin`
- Delete local ref after verification: `refs/heads/codex/remaining-product-work`
- Prune stale worktree metadata only after branch integration succeeds

**Interfaces:**
- Consumes: release-verified local `main` and refreshed portable output from Task 3.
- Produces: matching local/GitHub `main`, one local branch, one remote head, and one registered worktree.

- [ ] **Step 1: Push only `main` and verify the remote commit**

Run:

```powershell
git push origin main
$localMain = git rev-parse main
$remoteMainLine = git ls-remote --heads origin refs/heads/main
$remoteMain = ($remoteMainLine -split "`t")[0]
if ($localMain -ne $remoteMain) {
    throw "Local main $localMain does not match origin/main $remoteMain."
}
Write-Output "main=$localMain"
```

Expected: push exits `0` and one identical commit hash is printed for local and remote `main`.

- [ ] **Step 2: Delete the merged feature branch and prune stale worktree metadata**

Run:

```powershell
git merge-base --is-ancestor codex/remaining-product-work main
if ($LASTEXITCODE -ne 0) {
    throw 'Refusing to delete codex/remaining-product-work because it is not contained in main.'
}
git branch -d codex/remaining-product-work
git worktree prune
```

Expected: ancestry check exits `0`, the branch is deleted without force, and worktree pruning exits `0`. No filesystem worktree removal is needed because the inventory contains only the main repository worktree.

- [ ] **Step 3: Prove that only `main` remains locally and on GitHub**

Run:

```powershell
git fetch --all --prune
git status --porcelain=v2 --branch --untracked-files=all
git worktree list --porcelain
git branch -a -vv --no-abbrev
git ls-remote --heads origin
$localBranches = @(git for-each-ref --format='%(refname:short)' refs/heads)
$remoteHeads = @(git ls-remote --heads origin)
if ($localBranches.Count -ne 1 -or $localBranches[0] -ne 'main') {
    throw "Unexpected local branches: $($localBranches -join ', ')"
}
if ($remoteHeads.Count -ne 1 -or $remoteHeads[0] -notmatch 'refs/heads/main$') {
    throw "Unexpected remote heads: $($remoteHeads -join ', ')"
}
```

Expected: status is clean on `main`; one worktree is registered at `C:/Users/Evaks/Desktop/dobby`; local `main`, `origin/main`, and `origin/HEAD -> origin/main` point to the same hash; the validation script exits without throwing; and no other local or GitHub branch remains.
