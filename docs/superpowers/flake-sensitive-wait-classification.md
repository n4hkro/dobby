# Flake-Sensitive Wait Classification

Source command:

```powershell
rg -n "SpinWait|Thread\.Sleep|Task\.Delay|TimeSpan\.FromSeconds|DispatcherPriority\.ApplicationIdle" tests
```

This table records the current wait/pump classification before any further flake-related behavior changes. It is evidence for triage, not proof that a flake can never recur.

| Location | Hit | Classification | Current action |
| --- | --- | --- | --- |
| `tests/Dobby.SmokeTests/ShellKeyboardSmokeTests.cs:177` | `DispatcherPriority.ApplicationIdle` | render/dispatcher pump acceptable | No change. This pumps the WPF dispatcher before layout/render assertions. |
| `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs:554` | `DispatcherPriority.ApplicationIdle` | render/dispatcher pump acceptable | No change. This is the shared WPF pump helper. |
| `tests/Dobby.IntegrationTests/Host/WorkSessionShellIntegrationTests.cs:564` | `Thread.Sleep(10)` | explicit async completion already present | No change without reproduction. Completion is gated by `TaskCompletionSource`; the sleep only paces the pump loop. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs:33-35` | `SpinWait.SpinUntil`, `TimeSpan.FromSeconds(5)` | timing-risk requiring a reproduced failure before behavior changes | No change in this patch. This is outside the C4/F1 WPF lifecycle path. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs:54-56` | `SpinWait.SpinUntil`, `TimeSpan.FromSeconds(5)` | timing-risk requiring a reproduced failure before behavior changes | No change in this patch. This waits for background audit retention reporting. |
| `tests/Dobby.IntegrationTests/Host/HostLoginRuntimeFactoryTests.cs:160-167` | `SpinWait.SpinUntil`, `TimeSpan.FromSeconds(5)` | timing-risk requiring a reproduced failure before behavior changes | No change in this patch. This is cleanup synchronization for audit retention completion. |
| `tests/Dobby.IntegrationTests/Startup/SingleInstanceCoordinatorTests.cs:23` | `WaitOne(TimeSpan.FromSeconds(1))` | explicit async completion already present | No change. This waits on a named kernel event signaled by the handoff path. |
| `tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs:177` | `DispatcherPriority.ApplicationIdle` | render/dispatcher pump acceptable | No change. This pumps WPF before rendering the snapshot. |
| `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapTransactionTests.cs:85` | `WaitAsync(TimeSpan.FromSeconds(10))` | explicit async completion already present | No change. This is bounded waiting on `TaskCompletionSource`. |
| `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapTransactionTests.cs:92` | `Task.Delay(100)` | timing-risk requiring a reproduced failure before behavior changes | No change in this patch. If this test flakes, replace with an explicit second-task entry signal. |
| `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapTransactionTests.cs:161` | `ManualResetEventSlim.Wait(TimeSpan.FromSeconds(30))` | explicit async completion already present | No change. This is a bounded fail-safe for a manually released transaction latch. |
| `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapCoordinatorTests.cs:57` | `WaitAsync(TimeSpan.FromSeconds(10))` | explicit async completion already present | No change. This is bounded waiting on `TaskCompletionSource`. |
| `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapCoordinatorTests.cs:66` | `Task.Delay(TimeSpan.FromMilliseconds(500))` | timing-risk requiring a reproduced failure before behavior changes | No change in this patch. If it flakes, replace the timeout probe with an explicit entry signal. |
| `tests/Dobby.IntegrationTests/Auth/OwnerBootstrapCoordinatorTests.cs:119` | `ManualResetEventSlim.Wait(TimeSpan.FromSeconds(30))` | explicit async completion already present | No change. This is a bounded fail-safe for a manually released transaction latch. |
| `tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs:38` | `FakeTimeProvider.Advance(TimeSpan.FromSeconds(10))` | deterministic fake-clock/domain-time advance | No change. No wall-clock wait occurs. |
| `tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs:74` | `FakeTimeProvider.Advance(TimeSpan.FromSeconds(10))` | deterministic fake-clock/domain-time advance | No change. No wall-clock wait occurs. |
| `tests/Dobby.UnitTests/Security/LoginThrottleServiceTests.cs:20-22` | `FakeTimeProvider.Advance(TimeSpan.FromSeconds(...))` | deterministic fake-clock/domain-time advance | No change. No wall-clock wait occurs. |
| `tests/Dobby.UnitTests/Host/LoginViewModelThrottleStateTests.cs:28` | `FakeTimeProvider.Advance(TimeSpan.FromSeconds(10))` | deterministic fake-clock/domain-time advance | No change. No wall-clock wait occurs. |

## Triage Rule

Do not write behavioral fixes for these waits unless the current failure reproduces and points to the specific wait or harness state. If a C4/F1 WPF lifecycle failure returns, first inspect WPF/STA/Dispatcher cleanup, pending action reset, shared/static state, test ordering, and explicit completion signals before blaming production lifecycle logic.
