# Task 07 Execution Note: License Context And ReadOnlyExpired

Created immediately before Task 7 implementation from the current dirty working tree.

## Authority Preflight

The master plan and canonical ADR conflicted on one requirement:

- `docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md:661` requires a real non-default customer/deployment provisioning path.
- `docs/adr/ADR-0052-tenant-customer-context-v2.md:87-89` explicitly excludes non-default provisioning from Phase 1 and requires a future explicit admin/operator workflow or an ADR amendment.
- The master plan global constraint requires Sprint/ADR behavior authority to win over an audited row unless the authority is explicitly amended.

The user resolved the conflict on 2026-07-10 by choosing option 2: authorize a narrow ADR-0052 amendment and implement a real host-internal admin/operator provisioning path. The amendment changes only the default-only/non-default-provisioning exclusion. The remaining Phase 1 boundaries stay binding: exactly one active context, no customer-switching UI, no plugin visibility of raw customer/deployment identifiers, and license import remains lookup-only and must never create or mutate context opportunistically.

## Current Git Status

Relevant dirty files at task start:

```text
 M src/Dobby.Host/MainWindow.xaml
 M src/Dobby.Host/MainWindow.xaml.cs
 M src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs
 M src/Dobby.Shell/ShellViewModel.cs
 M tests/Dobby.IntegrationTests/Shell/ShellSnapshotTests.cs
?? src/Dobby.Host/Plugins/HostPluginStorage.cs
?? src/Dobby.Host/Plugins/HostPluginStorageCompartment.cs
```

Ownership notes:

- `MainWindow.xaml`, `MainWindow.xaml.cs`, `ShellViewModel`, and `ShellSnapshotTests` contain accepted Task 4-6 shared UI work. Task 7 is the next authorized shared-UI lane owner and must preserve those changes.
- `HostPluginRuntimeCoordinator` contains accepted Task 6 page/fault/atomic activation-status behavior. Task 7 may extend license-mode consumption without weakening that transaction or rollback logic.
- `HostPluginStorage` and `HostPluginStorageCompartment` are untracked Task 2 implementations. Task 7 may add mode-aware write gates while preserving encrypted persistence, key validation, and authenticated user scoping.

## Current Production Seams

- `Dobby.Security.CurrentContext` owns single-context lookup/default materialization and currently rejects non-default values.
- License import and activation/runtime-mode lookup already consume the same Security-owned context and do not provision it.
- `ShellViewModel` and `PostLoginShellPresenter` already expose license permission, mode, status/list/detail, feedback, and busy state.
- `MainWindow` renders license status text but has no import button, picker invocation, or import click handler.
- `HostPluginPackageInstallCoordinator` already denies package install in `ReadOnlyExpired`.
- Concrete plugin storage writes exist and currently have no runtime-mode gate.
- No concrete plugin background starter, domain import/export, report writer, or generic file-output owner exists; these require explicit absent-surface proof unless a real owner is introduced.
- Print classification/policy exists, but no production print execution/confirmation seam calls it.

## Intended Touches

### Will Touch

- Security current-context service/store and tests only after authority resolution.
- License shell production wiring in `MainWindow.xaml`, `MainWindow.xaml.cs`, `ShellViewModel`, presenter/runtime composition, picker/coordinator tests, and snapshots/static guards.
- A single host-owned runtime-mode enforcement boundary and concrete plugin-storage/package-install call sites.
- Print policy plus a host-owned confirmation/execution boundary and deterministic tests.
- ReadOnlyExpired matrix integration tests, using absent-surface proof for operations with no concrete owner.
- Existing license runtime/import types only where a failing test proves a real gap.

### Will Not Touch

- Applied migration SQL or license wire schema.
- Contracts/SDK raw license or customer/deployment visibility.
- Package pins, trust material, signing fixtures, or private keys.
- Task 6 plugin page routing, event lease, diagnostics redaction, or atomic activation-status persistence except required constructor/call-site preservation.
- Unrelated auth/session/settings/theme/release files or line-ending normalization.

## RED Tests

Minimum deterministic RED methods:

- `CustomerDeploymentContextIntegrationTests.Provisioned_context_is_single_security_owned_context`
- `LicenseImportShellSurfaceTests.Plugins_license_surface_invokes_file_picker_and_coordinator`
- `ReadOnlyExpiredEnforcementIntegrationTests.ReadOnlyExpired_blocks_all_mutation_surfaces`
- `PrintOutputPolicyTests.ReadOnlyExpired_physical_print_requires_confirmation`

Expected initial failures:

- No approved non-default provisioning API exists and current context policy rejects non-default values.
- Production `MainWindow` has no license import button/handler and does not invoke `WindowsLicenseFilePicker` through `LicenseImportCoordinator`.
- Plugin storage writes bypass runtime-mode enforcement; other absent surfaces are not yet proven by the required consolidated matrix test.
- Physical print succeeds immediately in `ReadOnlyExpired` and no confirmation seam exists.

## Production Seams

- Security-owned context policy: `CurrentCustomerDeploymentContextService` plus `ICurrentCustomerDeploymentContextStore`/`HostCustomerDeploymentContextStore`.
- License shell: `PostLoginShellPresenter`, `ShellViewModel`, `MainWindow`, `ILicenseFilePicker`, `WindowsLicenseFilePicker`, and `LicenseImportCoordinator`.
- Runtime enforcement: one host-owned typed gate consumed by concrete mutation adapters; `OperationAuthorizationPolicy` remains permission-owned.
- Print: application classifier/policy, infrastructure classifier adapter, and a host-owned confirmation/execution coordinator.

## Focused Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~LicenseImportCoordinator|FullyQualifiedName~HostPluginPackageInstallCoordinator|FullyQualifiedName~PrintOutputPolicy"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~CustomerDeploymentContextIntegrationTests|FullyQualifiedName~LicenseImportIntegrationTests|FullyQualifiedName~LicenseRuntimeModeIntegrationTests|FullyQualifiedName~ReadOnlyExpiredEnforcementIntegrationTests|FullyQualifiedName~LicenseImportShellSurfaceTests|FullyQualifiedName~ShellSnapshotTests"
git diff --check
```
