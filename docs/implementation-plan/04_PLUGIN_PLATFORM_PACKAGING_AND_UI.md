# Plugin Platform, Packaging and UI Execution Plan

**Dependencies:** `ADR-0059`, `PA-02`; key profile also serves identity/licensing  
**Completion blockers addressed:** F-007 through F-013, F-019, F-021

## PL-01 — Freeze the supported trust model and manifest v2

**Priority:** P0-Critical  
**Depends on:** none

### Files to inspect/modify/create

- `docs/schemas/dobby-plugin-manifest.schema.json` (retain as v1)
- new `docs/schemas/dobby-plugin-manifest-v2.schema.json`
- `src/Dobby.PluginFramework/Manifest/PluginManifest.cs`
- `src/Dobby.PluginFramework/Manifest/PluginManifestValidator.cs`
- `src/Dobby.Security/Trust/*`
- `src/Dobby.PluginFramework/Activation/PluginLoadContext.cs`
- `src/Dobby.Host/Trust/README.md`
- schema/manifest/trust/architecture tests

### Steps

1. Mark the current schema as v1 in its description and freeze it. Author v2
   with explicit `schemaVersion`, `hostApiVersion`, `minHostVersion`, bounded
   identifiers/arrays/strings/files and signature purpose/key ID.
2. Define the supported trust tier as vendor/organization-reviewed, signed,
   fully trusted in-process code. Update user/admin surfaces and docs so they do
   not claim sandboxing of hostile code.
3. Rename binary/API marker scanning as compatibility/policy preflight. It can
   reject known policy violations but must not be used as the authorization or
   isolation boundary.
4. Keep `AssemblyLoadContext` only for dependency/load-lifetime isolation. Decide
   collectible versus process-lifetime loading based on unload tests; do not load
   unmanaged binaries unless the trusted manifest explicitly declares and signs
   them under a reviewed policy.
5. Define v1 migration: existing installed v1 packages may be inventoried but
   must be reverified and upgraded/repackaged before the GA gate. New installs
   after the migration gate require v2.
6. Add architecture tests prohibiting documentation/UI strings that describe
   in-process plugins as sandboxed/untrusted-safe.

### Acceptance

- Schema v2 is strict and validates committed positive fixtures only.
- Product trust language is honest and consistent across UI, docs and tools.
- No code path treats marker scanning or `AssemblyLoadContext` as proof that
  hostile code is safe.

## PL-02 — Stream and bound package validation/extraction

**Priority:** P0-Critical  
**Depends on:** PL-01

### Files to inspect/modify/create

- `src/Dobby.PluginFramework/Packaging/PluginPackageReader.cs`
- `src/Dobby.PluginFramework/Packaging/PluginPackageValidator.cs`
- `src/Dobby.PluginFramework/Packaging/PluginPackageInstaller.cs`
- `src/Dobby.PluginFramework/Packaging/PluginPackageValidationReason.cs`
- `src/Dobby.Host/Plugins/PluginInstallStagingArea.cs`
- `src/Dobby.Configuration/PluginsOptions.cs`
- package reader/validator/installer tests and malicious fixture generator

### Steps

1. Add a package-limit value object validated at startup. Initial defaults:
   64 MiB compressed package, 256 KiB manifest, 256 entries, 64 MiB per entry,
   256 MiB aggregate uncompressed data and 50:1 maximum per-entry/aggregate
   expansion ratio. Keep a code-level hard ceiling so configuration cannot make
   processing unbounded.
2. Reject oversized package files before opening. Inspect central-directory
   metadata with checked arithmetic before allocating, then enforce limits again
   while streaming because metadata is untrusted.
3. Parse only the bounded manifest stream. Do not read every entry into a byte
   array. Hash/copy each file through a bounded buffer directly into a newly
   created staging file.
4. Implement one canonical path validator shared by schema/application/install:
   normalize separators to `/`; reject rooted, drive, UNC, backslash, colon/NUL/
   controls, empty, `.` or `..` segments, trailing dot/space, Windows reserved
   basenames (`CON`, `PRN`, `AUX`, `NUL`, `COM1`…`LPT9`), alternate data streams,
   case-insensitive duplicates and file/directory prefix collisions.
5. Resolve the final destination with `Path.GetFullPath` under the exact staging
   root and compare using Windows-appropriate case-insensitive boundary plus a
   trailing separator. Create new files only; never overwrite. Reject reparse
   points/symlinks in any existing path segment.
6. Require exact manifest/file-set equality: one manifest, no duplicate ZIP
   names, every payload file declared once, every declared file present once,
   entry assembly declared and all hashes verified before signature/trust.
7. Clean staging through a validated canonical owner on cancellation/failure.
   Preserve only a bounded safe diagnostic reason and correlation ID.
8. Add a generated malicious corpus: zip bomb metadata/stream, count/size/ratio
   boundary ±1, traversal, absolute/drive/UNC, mixed slash, empty segments,
   reserved names/extensions, trailing dot/space, ADS, Unicode/case collisions,
   duplicate entries, prefix collisions, undeclared/missing files and hash races.

### Acceptance

- Peak memory is bounded independently of total uncompressed package size.
- Every corpus case fails before promotion and writes nothing outside the newly
  created staging root.
- A maximum-size valid package verifies with cancellation and real progress.

## PL-03 — Purpose-separated signatures and rotating key rings

**Priority:** P0-High  
**Depends on:** PL-01

### Files to inspect/modify/create

- `src/Dobby.Security/Trust/PluginTrustContracts.cs`
- `src/Dobby.Security/Trust/PluginTrustPublicKeyProvider.cs`
- `src/Dobby.Security/Trust/PluginTrustVerifier.cs`
- `src/Dobby.Licensing/LicenseSignatureVerifier.cs`
- `src/Dobby.Security/Authentication/VendorRecoveryPublicKey.cs`
- `src/Dobby.Security/Authentication/RecoveryCanonicalJsonWriter.cs`
- `src/Dobby.Admin/Commands/AdminCommandHost.cs`
- all signing tools and crypto fixture builders/tests

### Steps

1. Introduce a shared cryptographic profile library that defines only format:
   algorithm `ECDSA-P256-SHA256`, signature format
   `IeeeP1363FixedFieldConcatenation`, unpadded base64url, canonical UTF-8 JSON,
   schema version, domain/purpose and key ID. Do not centralize authority policy.
2. Define non-interchangeable purpose identifiers for plugin package, license,
   recovery response and BranchSetup authorization. Canonical bytes must include
   the purpose/domain so the same payload/signature cannot cross protocols.
3. Replace single-key/`const 1` providers with bounded versioned public-key rings:
   active signing key, accepted verification keys, not-before/retired metadata
   and deterministic unknown/retired-key failure.
4. Keep private keys external to the repository and process arguments/logs. Test
   keys are visibly TEST-ONLY and production configuration rejects them.
5. Add cross-protocol substitution tests, DER-versus-P1363 tests, base64 padding/
   malleability/canonicalization tests, key rotation overlap and retirement tests.
6. Make tools and runtime use the same canonicalization library and verify tool
   output independently before publishing it.

### Acceptance

- Every artifact identifies its purpose and key ID and uses the exact byte/
  signature encoding in code and tests.
- A valid signature for one purpose is invalid for every other purpose.
- Rotation accepts only explicitly configured overlap and production rejects all
  TEST-ONLY keys/profiles.

## PL-04 — Crash-consistent install, promotion and recovery

**Priority:** P1-High  
**Depends on:** PA-02, PL-02, PL-03

### Files to inspect/modify

- `src/Dobby.PluginFramework/Packaging/PluginPackageInstaller.cs`
- `src/Dobby.PluginFramework/Packaging/InstalledPluginStateStore.cs`
- `src/Dobby.PluginFramework/Packaging/InstalledPluginMetadataCache.cs`
- host adapters under `src/Dobby.Host/Plugins/HostInstalledPlugin*`
- `src/Dobby.Host/Plugins/HostPluginPackageInstallCoordinator.cs`
- `src/Dobby.Host/Plugins/PluginInstallStagingArea.cs`
- persistence migrations and install-state tests

### Steps

1. Define an install transaction state machine with unique operation ID and
   states such as Staged, Verified, ReadyToPromote, Promoted, StateCommitted and
   Completed. Persist authority before each non-atomic file transition.
2. Never overwrite the active package in place. Promote a versioned immutable
   directory by same-volume atomic rename; update the authoritative active/LKG
   pointer in the database transaction with audit/outbox.
3. Treat metadata cache as a projection. Rebuild it from authoritative installed
   state and verified manifest on startup or detected divergence.
4. Implement idempotent recovery for every state and injected crash point. An
   uncommitted promoted directory is quarantined/removed; a committed active
   pointer is completed/reprojected, never silently rolled back.
5. Serialize conflicting operations by plugin ID while allowing bounded parallel
   verification for different plugins. Handle uninstall/enable/disable races.
6. Preserve previous LKG until new activation succeeds according to policy;
   failure restores the pointer/state without running unverified code.

### Acceptance

- Restart at every transition yields exactly one authoritative active version,
  correct LKG and a reconstructible cache.
- No partial package becomes discoverable/activatable.
- Concurrent operations have deterministic typed outcomes.

## PL-05 — Host/API compatibility and asynchronous lifecycle v2

**Priority:** P1-High  
**Depends on:** PL-01, PL-04

### Files to inspect/modify

- `src/Dobby.Contracts/PluginFramework/PluginContracts.cs`
- `src/Dobby.Core/Versioning/HostVersion.cs`
- `src/Dobby.PluginFramework/Manifest/*`
- `src/Dobby.PluginFramework/Activation/*`
- `src/Dobby.Host/Plugins/HostPluginLifecycleCoordinator.cs`
- `src/Dobby.Host/Plugins/HostPluginRuntimeCoordinator.cs`
- contract/manifest/activation/integration tests

### Steps

1. Freeze a compatibility matrix across manifest schema, plugin package version,
   host product version and SDK/host API version. Reject unknown major API;
   define compatible minor behavior explicitly.
2. Introduce async lifecycle v2 methods with `CancellationToken` and host-owned
   timeout: initialize/activate/deactivate/unload and page action execution.
3. Link cancellation to authenticated scope, user operation, plugin disable/
   uninstall and app shutdown. Distinguish timeout, cancellation, expected result
   and plugin exception.
4. Provide a deliberate v1 adapter only for the migration window. It runs trusted
   code under a bounded host operation and produces an upgrade warning; remove it
   at the documented gate.
5. Make load context collectible only if reliable unload tests demonstrate no
   references/threads/handlers survive; otherwise document process-lifetime load
   and require restart after update/uninstall.

### Acceptance

- Compatibility decisions are table-driven and identical in tool/runtime.
- No lifecycle/action path uses `CancellationToken.None` as the effective token.
- Timeouts/cancellation clean registrations, operations and work sessions.

## PL-06 — Declarative, localizable plugin component UI

**Priority:** P1-High  
**Depends on:** PL-05

### Files to inspect/modify/create

- `src/Dobby.Contracts/PluginPages/PluginPageContracts.cs`
- `src/Dobby.Contracts/PluginPages/PluginNavigationContracts.cs`
- `src/Dobby.Shell/PluginPages/PluginPageRenderRegistry.cs`
- `src/Dobby.Host/Plugins/PluginPageRouteCoordinator.cs`
- localization contracts/resources and sample plugin
- render/route/localization/WPF behavior tests

### Steps

1. Define immutable bounded component models: localized text/heading, field
   groups, text/number/date/choice/toggle inputs, validation summaries,
   table/list with bounded columns/rows/page requests and command descriptors.
2. Replace action delegates in models with opaque command IDs. Host invokes them
   through a plugin command dispatcher that applies session, operation target,
   capability, license, cancellation, timeout, progress and exception boundaries.
3. Add plugin localization bundles keyed by culture with default/fallback rules.
   Render resolved values, never raw resource keys. Validate bundle size/key
   parity and sanitize formatted arguments.
4. Render all components through host-owned WPF templates/styles and accessible
   names, focus order, keyboard invocation, validation announcements and high-
   contrast/theme resources. No WPF type crosses Contracts/SDK.
5. Define bounded async paging/edit/submit state; plugin never receives a host
   service provider or direct UI thread access.
6. Upgrade the sample plugin and add snapshots plus behavioral keyboard/command/
   cancellation/localization tests for all three cultures.

### Acceptance

- A realistic form-and-table workflow can be implemented without custom WPF or
  arbitrary delegates.
- Every visible plugin string resolves through plugin/host fallback correctly.
- Host policies execute before every command and command cancellation is proven.

## PL-07 — Correct fault attribution and runtime lifetime

**Priority:** P1-Medium  
**Depends on:** PL-05, PA-03

### Files to inspect/modify

- `src/Dobby.PluginFramework/Activation/PluginExceptionBoundary.cs`
- `src/Dobby.Host/Plugins/PluginRuntimeAuditCoordinator.cs`
- `src/Dobby.Host/Plugins/PluginRuntimeDiagnosticsCoordinator.cs`
- `src/Dobby.Host/Plugins/PluginCurrentProcessActivationTracker.cs`
- `src/Dobby.Host/Plugins/PluginRuntimeDiagnosticsFileSink.cs`
- runtime/fault/quarantine tests

### Steps

1. Create an outcome taxonomy: success, expected rejection/validation,
   authorization/license denial, cancellation, timeout, plugin-declared failure,
   plugin exception/contract breach and host/infrastructure failure.
2. Increment runtime-fault/quarantine policy only for the explicitly documented
   plugin-defect categories. Expected package/license/capability/input failures
   remain safe results.
3. Attribute host failures to host diagnostics, not the plugin. Preserve stable
   correlation and safe user messages.
4. Make activation tracking change only after atomic status commit; unwind event
   subscriptions, services, operations and work sessions on every failed phase.
5. Add repeated expected-rejection tests proving the fault count remains zero,
   and repeated actual exception tests proving threshold/quarantine behavior.

### Acceptance

- Fault counters reflect plugin defects rather than normal user/policy outcomes.
- Cleanup completes deterministically and a failed plugin cannot leave active
  routes, handlers, operations or work sessions.

## Workstream Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Plugin"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Plugin"
.\tools\verify-plugin-package\verify-plugin-package.ps1 -Package <valid-signed-sample>
dotnet test .\Dobby.sln -c Release --no-restore
```

Capture peak-memory/cancellation results for the maximum valid package and all
malicious corpus outcomes. Do not commit oversized generated corpus archives;
commit deterministic generators and small structural fixtures.
