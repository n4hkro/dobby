# Licensing, Provisioning and Runtime Gates Execution Plan

**Dependencies:** `ADR-0060`, `PL-03`, `PA-02`  
**Completion blockers addressed:** F-014 through F-017; legacy rows 38–48

## LC-01 — Freeze license v3 and setup-authorization contracts

**Priority:** P0-Critical  
**Depends on:** PL-03 format/purpose decision

### Files to inspect/modify/create

- `docs/schemas/dobby-plugin-license.schema.json` (retain as v2)
- new `docs/schemas/dobby-plugin-license-v3.schema.json`
- new `docs/schemas/dobby-branch-setup-authorization-v1.schema.json`
- `src/Dobby.Licensing/LicenseArtifact.cs`
- `src/Dobby.Licensing/LicensePayload.cs`
- `src/Dobby.Licensing/LicenseLifecycleState.cs`
- `src/Dobby.Licensing/LicenseCanonicalJsonWriter.cs`
- `src/Dobby.Admin/Commands/AdminCommandHost.cs`
- license fixture builders/schema/canonicalization tests

### Steps

1. Freeze v2 as a legacy input. Create v3 with explicit schema version, purpose,
   key ID, license/revision/plugin/customer/deployment/device/package bindings,
   issued/not-before/expires/grace policy and signed intent limited to `Granted`
   or `Revoked`.
2. Put explicit maxima on artifact bytes, identifiers, revision, grace days and
   all strings/collections. Require UTC canonical timestamps and checked ordering.
3. Remove `Pending/Active/Grace/ReadOnlyExpired` from signed authority. Publish a
   deterministic lifecycle truth table over intent, binding, trusted-time health,
   `notBefore`, `expiresAt` and grace end. Invalid/revoked produces Denied;
   valid-before-window produces Pending; valid-in-window Active; valid-after-
   expiry through grace Grace; then ReadOnlyExpired.
4. Define v2 migration explicitly. Recommended: accept a valid v2 artifact only
   to preserve an existing installed LKG during a bounded migration window; do
   not allow a new v2 import after the v3 activation gate. Map status without ever
   turning v2 `pending/denied/readonly` into a new grant.
5. Create a separate BranchSetup authorization schema with purpose, operation
   scope, branch/deployment identity, nonce, issue/expiry and key ID. It is design
   input for the future track and receives no Standalone runtime handler.
6. Update Admin commands/usage so license generation cannot generate setup proof.

### Acceptance

- Schemas and canonicalization tests cover property ordering, Unicode, number/
  date boundaries and signature purpose.
- No signed field can ambiguously override the derived lifecycle.
- A plugin license is rejected by setup-authorization verification and vice versa.

## LC-02 — Implement bounded parsing and deterministic derived lifecycle

**Priority:** P0-Critical  
**Depends on:** LC-01, PL-03

### Files to inspect/modify

- `src/Dobby.Licensing/LicenseArtifactParser.cs`
- `src/Dobby.Licensing/LicensePayloadValidator.cs`
- `src/Dobby.Licensing/LicenseSignatureVerifier.cs`
- `src/Dobby.Licensing/LicenseRuntimeModeService.cs`
- `src/Dobby.Licensing/LicenseRuntimeModeMapper.cs`
- `src/Dobby.Licensing/LicenseRevisionPolicy.cs`
- `src/Dobby.Licensing/LicenseStateRecord.cs`
- configuration and all license unit tests

### Steps

1. Reject files over the byte limit before JSON parse. Configure bounded JSON
   depth/token/string handling; reject duplicates, unknown properties and
   non-canonical/invalid base64url before cryptographic work.
2. Replace `Enum.Parse` and unchecked persisted-value conversions with explicit
   `TryParse` maps returning typed fail-closed corruption results.
3. Use checked date/grace arithmetic and validate all timestamps/ranges before
   calculating lifecycle. Inject trusted clock/time-integrity state.
4. Implement the LC-01 truth table as a pure function. Persist signed grant input
   and time attestation, then derive current runtime mode on every evaluation.
5. Keep binding and anti-rollback/revision checks independent and ordered so a
   safe rejection reason is stable and does not leak unnecessary identifiers.
6. Add exhaustive table/transition tests, boundary ±1 tick/day tests, malformed
   persisted-state tests, clock rollback/forward tests and property/fuzz tests.

### Acceptance

- A valid future-`notBefore` grant is Pending, never Active.
- Date overflow, unknown enum/schema/key/purpose and oversized fields return safe
  Denied/rejection results without exception escape or LKG mutation.
- Repeated evaluation with the same inputs is deterministic.

## LC-03 — Make import and LKG update atomic

**Priority:** P0-High  
**Depends on:** LC-02, PA-02

### Files to inspect/modify

- `src/Dobby.Licensing/LicenseImportService.cs`
- `src/Dobby.Licensing/ILicenseStateStore.cs`
- `src/Dobby.Licensing/LicenseImportAttestation.cs`
- `src/Dobby.Host/HostLicenseStateStore.cs`
- `src/Dobby.Host/Plugins/LicenseImportCoordinator.cs`
- `src/Dobby.Host/Plugins/LicenseImportShellAction.cs`
- `src/Dobby.Host/Plugins/HostPluginActivationStatusPersistence.cs`
- migrations and license import/integration tests

### Steps

1. Make validation pure/read-only. Invalid, mismatched, stale, revoked or malformed
   candidates never open a state-changing transaction and always preserve LKG.
2. For a valid candidate, begin the `PA-02` unit of work and atomically store the
   signed grant input, revision series/LKG pointer, trusted-time attestation,
   mandatory audit and post-commit activation-refresh outbox event.
3. Publish new in-memory runtime mode and refresh activation only after commit.
   If delivery fails, retry from outbox; do not roll back committed license state
   or report that the import itself failed.
4. Serialize imports by revision-series key and make equal-revision idempotency
   explicit. Concurrent older/newer imports must have deterministic outcomes.
5. Inject failure at every store/audit/outbox/commit/publish step and restart from
   each resulting durable state.
6. Preserve safe actionable rejection code in UI while keeping artifact contents,
   device/customer identifiers and exception detail out of user/audit output.

### Acceptance

- Invalid import cannot modify LKG, attestation, activation or audit success state.
- Valid import either commits all authoritative state/audit/outbox or none.
- Concurrent/retried import cannot roll back to an older revision or duplicate
  the effective activation refresh.

## LC-04 — Complete existing-surface runtime enforcement

**Priority:** P1-High  
**Depends on:** LC-02, LC-03, PA-03

### Files to inspect/modify

- `src/Dobby.Host/Plugins/HostRuntimeModeMutationGate.cs`
- `src/Dobby.Licensing/PluginCapabilityLicensing.cs`
- `src/Dobby.Security/Authorization/OperationAuthorizationPolicy.cs`
- `src/Dobby.Host/Plugins/HostPluginStorage.cs`
- `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`
- `src/Dobby.Host/Plugins/HostPluginLifecycleCoordinator.cs`
- `src/Dobby.Host/Printing/HostPrintOutputCoordinator.cs`
- `src/Dobby.Host/ShellDiagnosticsExportCoordinator.cs`
- ReadOnlyExpired and shell/action integration tests

### Steps

1. Build a central action matrix for every currently implemented surface across
   Pending, Active, Grace, ReadOnlyExpired and Denied. Include view, plugin
   action, storage write, background start, install/update, enable/disable/
   uninstall, domain export when it exists, print and diagnostics.
2. Apply policy at the application command/storage/start boundary, not only in
   button visibility. UI availability mirrors the same result but is not the
   authority.
3. Preserve read/view behavior explicitly. In Grace, apply the accepted policy;
   in ReadOnlyExpired block mutations/new output while allowing approved physical
   print only through the documented confirmation path; Denied fails closed.
4. Exempt the minimal host support diagnostics bundle from plugin license mode.
   Keep plugin/domain data export and plugin-contributed diagnostic data behind
   capability/license/redaction policy.
5. Register a required policy owner whenever a future domain background/import/
   export/report/file-output command is introduced. Add an architecture test that
   application command handlers declare the applicable mutation/output policy;
   do not treat current absence as permanent proof.
6. Replace regex/source-presence acceptance with parameterized behavior tests
   calling each real command boundary and WPF availability mapping.

### Acceptance

- The central matrix and executable tests agree for every existing surface and
  every lifecycle state.
- Direct backend calls cannot bypass a disabled UI control.
- Host diagnostics always works and contains no gated plugin/domain payload.

## Standalone Provisioning Boundary

The current Standalone `default/default` customer/deployment context is accepted
only as a documented single-install baseline. Do not invent real customer,
Branch Shared or setup behavior in this workstream. The dedicated setup schema
created in LC-01 is future contract preparation only; runtime execution belongs
to `08_BRANCH_SHARED_FUTURE_TRACK.md` after owner approval.

## Workstream Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Licens"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Licens|FullyQualifiedName~ReadOnlyExpired"
dotnet test .\Dobby.sln -c Release --no-restore
```

Persist the lifecycle truth-table result, malformed corpus summary and atomicity
fault matrix in the release evidence for the candidate commit.
