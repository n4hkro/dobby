# Sprint 11 Release Smoke

Status: LOCAL SMOKE BASELINE PASSED; release-ready smoke remains blocked by missing installer evidence and final scripted local-gate evidence.

## Artifact Binding

- Publish artifact: `src/Dobby.Host/bin/Release/net10.0-windows/publish/`
- Host executable SHA-256: `0cbf66dc862b7e69ab5402789d546b2060b951bca6e6718260a06c2c525e1ce3`
- Installer artifact: HD-S11-003 MSIX-primary RC mechanics path accepted only; installer artifact evidence is still required.
- Local gate: HD-S11-005 approved as `repository CI ownership is absent; equivalent local release gate human-approved`; scripted local build/test/static-scan/smoke/hash evidence is still required.

## AC-008

Covered by startup and smoke tests:

- `PreLoginAllowedWorkloadTests.cs`
- `StartupBootstrapWiringTests.cs`
- `ShellSmokeTests.cs`

The current smoke baseline passed 11 tests with 0 failures.

## AC-046

Background-operation and heartbeat coverage:

- `ShellBackgroundOperationCoordinatorTests.Background_operation_updates_are_posted_through_shell_ui_dispatcher`
- `ShellBackgroundOperationCoordinatorTests.Plugin_owned_background_operation_reaches_shell_through_official_reporter`
- `ShellUiStaticScanTests.Plugins_manager_long_running_lifecycle_actions_report_background_operations`
- `ShellUiStaticScanTests.License_import_long_running_action_reports_background_operations`
- `PluginIntegrationSuiteTests` verifies plugin activation background-operation Running and Completed snapshots.
- `HostLoginRuntimeFactoryTests` verifies post-login runtime background-operation reporting through host-owned services.

Sprint 11 changed `MainWindow.xaml.cs` so the license-import handler now reports Running and terminal background-operation snapshots while the import task is active.

## Smoke Workflow Disposition

| Workflow | Evidence |
| --- | --- |
| login-first | Startup integration tests and smoke baseline. |
| Owner bootstrap | Existing startup/security integration coverage; no new owner-bootstrap policy added. |
| user management | Existing authorization and shell coverage; no new user-management feature added. |
| shell first paint/navigation | `ShellSmokeTests.cs`, `ShellKeyboardSmokeTests.cs`, `ShellSnapshotTests.cs`. |
| plugin install/activation | Existing plugin manager smoke/static coverage and `PluginIntegrationSuiteTests`. |
| license import/mode | License integration tests and Sprint 11 license-import background-operation guard. |
| work session logout/lock | `WorkSessionShellIntegrationTests.cs`; smoke baseline. |
| diagnostics export | `ShellDiagnosticsExportActionTests.cs`; no telemetry/upload client added. |
| sample plugin actions | Smoke baseline; sample plugin remains excluded from production publish output. |
| crash recovery | `CrashRecoverySmokeTests.cs`. |

## Blockers

Release smoke cannot be called release-ready until the approved installer artifact exists and scripted local-gate evidence passes.

Authority decisions approve the path; P1/release-ready is not closed until evidence passes.
