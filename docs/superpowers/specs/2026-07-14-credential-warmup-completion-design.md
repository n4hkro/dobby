# Deterministic Credential Warmup Completion Design

## Goal

Remove the credential-warmup integration test's scheduling-dependent wait while preserving the login UI's existing fire-and-forget behavior and the production warmup cache semantics.

## Failure Evidence

The Release flake-sensitive profile reproduced one failure in integration iteration 5:

- `LoginRuntimeWorkflowTests.Submit_reuses_completed_credential_warmup_without_repeating_password_verification`
- The test actively spun for five seconds waiting for `CountingCredentialVerifier.VerifyCount` to become `1`.
- The warmup itself had been queued with `Task.Run`, so the waiting test and the queued work competed for thread-pool capacity during the parallel 350-test integration run.
- The four preceding full integration iterations passed, and the failing test subsequently passed 20 of 20 isolated repetitions.

The fixture creates a unique temporary data root for every test and contains no mutable static state involved in warmup completion. The evidence therefore points to test-harness scheduling rather than cross-test database, window, dispatcher, or static-state leakage.

## Considered Approaches

### 1. Return an observable completion task (chosen)

Change the warmup workflow contract to return the `Task` that represents the complete warmup operation. The UI will explicitly discard that task to retain its current non-blocking behavior, while integration tests can await the exact completion signal.

This removes active spinning, reflection over private cache state, and a guessed completion deadline from the test.

### 2. Use asynchronous polling only in the test

Convert the test to `async`, signal verifier entry with a `TaskCompletionSource`, and poll the private warmed-credential field with yielding delays. This is a smaller production diff but still depends on polling, a timeout, and private reflection, so it does not provide an exact completion boundary.

### 3. Reduce test parallelism or increase timeouts

Disable xUnit collection parallelism, raise the five-second limit, or increase thread-pool capacity. These options make the suite slower or environment-dependent and mask the missing synchronization contract instead of removing it.

## Approved Design

### Warmup contract

Rename the asynchronous contract member to follow .NET conventions:

```csharp
public interface ILoginCredentialWarmupWorkflow
{
    Task WarmCredentialsAsync(LoginCredentialWarmup warmup);
}
```

`LoginRuntimeWorkflow.WarmCredentialsAsync` will:

1. Validate the argument synchronously.
2. Increment `credentialWarmupVersion` exactly once for the invocation.
3. Return the existing `Task.Run` operation that executes `WarmCredentialsCore`.

The returned task completes after the invocation has validated credentials and either published or cleared its version-matched cache entry. A later warmup may supersede that entry under the existing version rules; awaiting one invocation does not override those rules.

### Deferred workflow

`DeferredLoginRuntimeWorkflow.WarmCredentialsAsync` will return its existing background operation. That operation will asynchronously obtain the prepared runtime and await the inner `ILoginCredentialWarmupWorkflow.WarmCredentialsAsync` call when available.

The deferred workflow will preserve its current handling of expected infrastructure exceptions: non-fatal exceptions are contained and the operation completes without surfacing a login-window failure. `OutOfMemoryException` and `StackOverflowException` remain outside the catch filter.

### Login window

`LoginWindow.QueueCredentialWarmup` will explicitly start and discard the returned task:

```csharp
_ = warmupWorkflow.WarmCredentialsAsync(new LoginCredentialWarmup(
    username,
    credential,
    CorrelationId.New()));
```

The dispatcher timer, debounce behavior, UI responsiveness, and submit path remain unchanged. No UI event handler waits for credential warmup.

### Credential lifetime and cache behavior

The workflow will not store the returned task in a field. The task closure retains the submitted credential only for the same lifetime as the current queued delegate. No new credential persistence, logging, cancellation, retry, or cache lifetime is introduced.

## Test Design

The existing integration test will become an async test and await `WarmCredentialsAsync` directly before submitting. It will retain the behavioral assertions that:

- the warmup verifies the password once;
- submit authenticates successfully;
- submit does not repeat password verification;
- session and login-audit records are created.

The reflection-based `WaitForCompletedCredentialWarmup` helper and both `SpinWait.SpinUntil` calls in this test will be removed.

Static wiring coverage will be updated to require the task-returning async contract and the login window's explicit fire-and-forget call. Deferred-workflow coverage will verify that its task does not complete before the inner warmup completes and does complete afterward.

## Error Handling

Credential validation failures and ordinary warmup exceptions keep the current behavior: the cache is cleared only when the invocation version is still current, and the warmup does not turn into a user-visible login error. Argument validation remains synchronous at the public boundary.

No timeout is added to production. Test timeouts may guard against a genuinely stuck operation, but they will surround an explicit task completion signal rather than poll mutable state.

## Scope Boundaries

In scope:

- `ILoginCredentialWarmupWorkflow` and its two implementations.
- The login-window fire-and-forget call site.
- Integration and static wiring tests directly affected by the contract.
- Deterministic flake verification and portable publish after the fix.

Out of scope:

- Changing credential validation, fingerprinting, or cache version semantics.
- Adding warmup cancellation, retries, or production timeouts.
- Changing global xUnit parallelism or thread-pool settings.
- Refactoring unrelated login, WPF, or persistence behavior.

## Acceptance Criteria

- The warmup workflow exposes exact task completion without storing the task or credentials beyond the operation lifetime.
- Login UI warmup remains non-blocking.
- The reuse test contains no `SpinWait` and no reflection over `warmedCredential`.
- Password verification still occurs exactly once across warmup and submit.
- Focused repeated verification and the full Release flake-sensitive profile pass with no failures.
- `dotnet format` verification and warning-as-error builds pass without restore.
- A refreshed portable build is published with `tools/publish-portable/publish-portable.ps1 -NoRestore` and its executable path is reported.
