# Persistence, Atomicity and Diagnostics Execution Plan

**Dependencies:** `ADR-0061`; consumed by identity, plugin and licensing plans  
**Completion blockers addressed:** F-006, F-010, F-015, F-016, F-018, F-025

## PA-01 — Make traceability executable and current

**Priority:** P0  
**Depends on:** none

### Files to inspect/modify/create

- `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`
- `docs/implementation-plan/01_TRACEABILITY_AND_FINDINGS.md`
- `tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/ReleaseEvidenceTests.cs`
- new `tools/verify-traceability/` script and machine-readable status input

### Steps

1. Replace prose/source-presence inference with a small machine-readable register
   containing stable row/finding ID, classification, owning test(s), deferred
   decision and last verified commit.
2. Generate or validate the human-readable legacy table from that register. Do
   not maintain contradictory counts by hand.
3. Require an executable test identifier/evidence path for `Implemented`; allow
   architecture tests for dependency shape only. `Evidence-only` and
   `Deferred-by-design` must remain distinct.
4. Fail CI for duplicate IDs, missing owners, invalid classifications, broken
   file/test references and summary-count drift.

### Acceptance

- Rows 39–56 cannot remain stale when their production handlers/tests exist.
- A feature cannot become `Implemented` solely because a source token exists.

## PA-02 — Introduce a provider-owned mutation unit of work and outbox

**Priority:** P0-High  
**Depends on:** PA-01

### Files to inspect/modify/create

- `src/Dobby.Infrastructure/Persistence/IHostPersistenceTransactionScope.cs`
- `src/Dobby.Infrastructure/Persistence/HostPersistenceTransactionScope.cs`
- `src/Dobby.Infrastructure/Persistence/IHostPersistenceCommandExecutor.cs`
- `src/Dobby.Infrastructure/Persistence/HostPersistenceCommandExecutor.cs`
- `src/Dobby.Audit/IHostTransactionAuditTrail.cs`
- `src/Dobby.Audit/Persistence/DurableAuditTrail.cs`
- migrations after `0006_plugin_storage_values.sql`
- new application-facing mutation transaction/outbox contracts
- `tests/Dobby.IntegrationTests/Persistence/HostPersistenceTests.cs`
- new atomicity/fault-injection integration tests

### Steps

1. Map every workflow that currently performs more than one durable write:
   user/session/password, license import/attestation/audit, plugin install state/
   metadata/audit and recovery consumption/reset/audit.
2. Define an application-facing unit of work that exposes repositories/audit/
   outbox bound to one physical provider transaction without leaking SQLite types
   outside Infrastructure.
3. Add an append-only outbox table with event ID, purpose/type, bounded canonical
   payload or safe fields, created/attempt/completed timestamps and retry state.
   Enforce unique idempotency keys.
4. Make audit append participate in the caller transaction. A workflow that
   requires audit cannot commit state if audit insertion fails.
5. Deliver non-transactional effects only after commit through a bounded,
   cancellable, retryable dispatcher. Duplicate delivery must be harmless.
6. Build deterministic failure injection at command number, audit append, outbox
   append and commit. Verify rollback at each point and recovery after process
   restart.
7. Keep transactions short: no file hashing, archive extraction, PBKDF, network,
   UI or plugin callbacks while a database transaction is open.

### Acceptance

- Every multi-write security workflow uses one unit of work or documents why its
  file phase is governed by a separate crash journal plus atomic state commit.
- Fault injection proves no externally reported partial success.
- Outbox retry survives restart and does not duplicate the effective action.

## PA-03 — Safe errors, audit and diagnostics

**Priority:** P0-High  
**Depends on:** PA-02

### Files to inspect/modify

- `src/Dobby.Infrastructure/Persistence/PersistenceError.cs`
- `src/Dobby.Infrastructure/Persistence/EncryptedSqliteConnectionFactory.cs`
- `src/Dobby.Audit/AuditDetailsSanitizer.cs`
- `src/Dobby.Audit/AuditEvent.cs`
- `src/Dobby.Diagnostics/DiagnosticRedactor.cs`
- `src/Dobby.Diagnostics/DiagnosticsExportPackageBuilder.cs`
- `src/Dobby.Host/Plugins/PluginRuntimeDiagnosticsFileSink.cs`
- `src/Dobby.Host/ShellDiagnosticsExportCoordinator.cs`
- audit/diagnostic/persistence tests

### Steps

1. Inventory every place that stores or displays `Exception.Message`, stack trace,
   SQL, filesystem path, artifact JSON, connection material, username or device
   hardware fields.
2. Split safe public/audit error from local diagnostic detail. Safe error contains
   stable code, category, retryability and correlation ID. Diagnostic detail is
   structured, redacted and written only to the diagnostics owner.
3. Replace free-form audit details with typed/allow-listed fields per event. Add
   length/count bounds before persistence. Forbid raw exception/payload fields at
   the `IAuditTrail` boundary, not only in callers.
4. Strengthen diagnostics redaction for structured JSON and known secret types
   before regex defense-in-depth. Normalize paths to safe tokens and include only
   explicitly approved relative/log metadata.
5. Remove plugin-license lifecycle checks from host support diagnostics export.
   Keep authorization/session and local path/size rules; keep plugin/domain data
   export behind its own policy.
6. Add adversarial tests with passwords, tokens, PEM/private material, connection
   strings, Windows/UNC paths, SQL, multiline stack traces, Unicode separators
   and nested JSON. Assert absence in audit, user output and exported bundles.

### Acceptance

- UI/audit never receives raw infrastructure exception detail.
- Correlation IDs allow safe linkage to a redacted local diagnostic record.
- Host diagnostics export succeeds for Pending, Active, Grace,
  ReadOnlyExpired and Denied plugin-license states.

## PA-04 — Crash recovery, retention and backup correctness

**Priority:** P1  
**Depends on:** PA-02, plugin task PL-04

### Files to inspect/modify

- `src/Dobby.Host/RuntimeFolderLayout.cs`
- `src/Dobby.Host/RuntimeFolderPreparer.cs`
- `src/Dobby.Infrastructure/Persistence/HostPersistenceMaintenanceService.cs`
- `src/Dobby.Infrastructure/Persistence/HostPersistenceMigrator.cs`
- `src/Dobby.Audit/Persistence/AuditRetentionSweeper.cs`
- `src/Dobby.Host/Login/AuditRetentionMaintenance.cs`
- `src/Dobby.Host/Startup/CrashRecoverySmokeHook.cs`
- runtime/persistence/retention/smoke tests

### Steps

1. Make maintenance an authenticated/background owner; it must not start during
   pre-auth composition.
2. Define SQLite online-backup/checkpoint semantics, destination permissions,
   retention, integrity verification, cancellation and low-disk behavior. A
   folder named `backup` is not an implementation.
3. Define restore as an explicit support/admin operation with current-store
   quarantine, schema/integrity checks and rollback. Never overwrite a live store
   before the candidate is verified.
4. Reconcile audit retention with atomic/outbox requirements: do not remove
   undelivered mandatory evidence, preserve configured floor and make concurrent
   sweep/write safe.
5. Centralize startup recovery ordering for database journal, plugin install
   transaction and temporary outputs. Each recovery handler is idempotent and
   operates only under validated canonical roots.
6. Add abrupt-process/crash fixtures (prebuilt on-disk states, not timing kills)
   for every state transition and verify deterministic next startup.

### Acceptance

- Backup is an integrity-checked database snapshot that can be restored in a
  test and preserves encrypted-at-rest requirements.
- Retention does not begin before login and cannot delete pending outbox evidence.
- Startup recovery completes/rolls back each enumerated partial state exactly
  once and leaves no out-of-root files.

## Workstream Verification

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Audit|FullyQualifiedName~Diagnostics"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Persistence|FullyQualifiedName~Audit|FullyQualifiedName~Diagnostics|FullyQualifiedName~Runtime"
dotnet test .\Dobby.sln -c Release --no-restore
```

Archive the fault-injection matrix and the tested on-disk state fixture hashes as
release evidence; do not retain real user data in fixtures.
