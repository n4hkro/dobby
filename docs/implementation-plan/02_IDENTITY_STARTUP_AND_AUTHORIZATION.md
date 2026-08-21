# Identity, Startup and Authorization Execution Plan

**Dependencies:** `ADR-0058`, `ADR-0061`; `PA-02` before atomic mutation tasks  
**Completion blockers addressed:** F-001 through F-006, F-024

## ID-01 — Freeze identity contracts and migration policy

**Priority:** P0  
**Depends on:** none

### Files to inspect/modify

- `src/Dobby.Configuration/AuthOptions.cs`
- `src/Dobby.Configuration/DobbyOptionsValidator.cs`
- `src/Dobby.Security/Authentication/PasswordPolicyValidator.cs`
- `src/Dobby.Security/Authentication/KdfPolicyResolver.cs`
- `src/Dobby.Security/Authentication/DisplayNameValidator.cs`
- `src/Dobby.Security/Authentication/UsernameCanonicalValidator.cs`
- `src/Dobby.Host/appsettings.json`
- matching configuration/security tests

### Steps

1. Add tests that enumerate every accepted configuration value and prove that an
   accepted KDF has an implementation. Resolve the current Argon2 contradiction
   by either implementing the approved dependency/parameters or removing it from
   accepted runtime configuration; do not silently fall back.
2. Freeze versioned credential metadata: algorithm, parameters, salt, hash and
   created/updated timestamps. Verification upgrades older parameters only after
   successful authentication and within the atomic password-write boundary.
3. Define username/display-name normalization, maximum lengths and control/bidi
   character behavior. Keep authentication identifiers stable and culture
   invariant; display names remain user-facing values.
4. Keep password policy compatible with password managers and paste. Enforce
   length and maximum work/input bounds; do not introduce arbitrary character
   composition rules. If an offline common-password list is added, version it,
   bound lookup cost and define update provenance.
5. Add table-driven positive/negative and Unicode boundary tests.

### Acceptance

- No configuration value can select an unimplemented KDF.
- Credential records are versioned and old valid records have explicit migration
  behavior.
- Password/username/display-name limits are deterministic and localized UI maps
  stable error codes without echoing input.

## ID-02 — Remove persisted/replayable passwords

**Priority:** P0-Critical  
**Depends on:** ID-01

### Files to inspect/modify/delete

- `src/Dobby.Security/Authentication/RememberMeSlotService.cs`
- `src/Dobby.Security/Authentication/IRememberMeStartupSnapshotSink.cs`
- `src/Dobby.Host/StartupRememberMeSnapshotStore.cs`
- `src/Dobby.Host/StartupRememberMeSnapshotSink.cs`
- `src/Dobby.Host/Login/LoginStartupSnapshot.cs`
- `src/Dobby.Host/Login/LoginStartupSnapshotLoader.cs`
- `src/Dobby.Host/Login/LoginViewModel.cs`
- `src/Dobby.Host/Login/LoginRuntimeWorkflow.cs`
- `src/Dobby.Host/App.xaml.cs`
- remember-me/snapshot/login tests in UnitTests and IntegrationTests

### Steps

1. First add restart tests proving that only the selected normalized username is
   restored and the password box is empty. Add a negative test that the masked
   placeholder cannot cause authentication.
2. Replace the remember-me record with a versioned username-only preference in
   the canonical protected/config persistence owner. It need not be encrypted,
   but it must be bounded and validated.
3. Add a one-time idempotent legacy migration that decrypts no value for reuse:
   retain a safe normalized username if available, then securely remove the
   legacy password slots and `startup-remember-me.json`. Treat corrupt/unreadable
   legacy data as delete-and-continue with safe diagnostics.
4. Remove password fields from startup snapshots, sinks, view-model state and all
   constructors/contracts. Do not keep obsolete overloads that accept a password.
5. Add a test-specific residue scanner over canonical and known legacy roots for
   plaintext password fixtures, serialized protected password records and the
   old snapshot filename after migration.
6. Update localization: “remember me” must mean remember account/username, not
   automatic login.

### Acceptance

- A new process cannot authenticate without new user input or a separately
  approved authenticator ceremony.
- No production type or serialized contract for remembered login carries a
  password/password equivalent.
- Legacy migration is idempotent, preserves only a valid username and removes all
  known duplicate secret files.

## ID-03 — Durable throttle and bounded credential verification

**Priority:** P0-High  
**Depends on:** ID-01, PA-02

### Files to inspect/modify/create

- `src/Dobby.Security/Authentication/LoginThrottleService.cs`
- `src/Dobby.Security/Authentication/LoginService.cs`
- `src/Dobby.Security/Authentication/IUserCredentialVerifier.cs`
- `src/Dobby.Security/Authentication/CredentialStore.cs`
- `src/Dobby.Host/Login/LoginRuntimeWorkflow.cs`
- `src/Dobby.Configuration/AuthOptions.cs`
- persistence migration after `0006_...sql`
- new durable throttle repository/service and deterministic clock abstractions
- throttle/login/runtime workflow tests

### Steps

1. Write deterministic tests with an injected clock for account isolation,
   normalized-account keys, device/global buckets, restart persistence, expiry,
   success reset, failed commit and concurrent attempts.
2. Define the policy in configuration with bounded counts/windows/backoff and a
   maximum concurrent password-verification count. Avoid user-enumerating result
   differences.
3. Persist throttle buckets in the encrypted host database. Update/check them in
   a transaction using compare/update semantics that cannot lose concurrent
   failures.
4. Acquire a bounded verifier lease before PBKDF work. Charge all real credential
   verification, including any warm-up, to the same attempt. Return a safe busy
   or throttled result without starting work when capacity/policy denies it.
5. Prefer deleting input-driven warm-up. If retained, give it cancellation linked
   to input/session/window lifetime, concurrency one per login view, a short TTL,
   one-shot consumption and explicit zeroization/release of retained buffers.
6. Record safe audit reason codes without usernames/passwords in details and
   without leaking whether an account exists.

### Acceptance

- Restart cannot bypass a lockout/backoff.
- Failures for account A do not lock account B except through the explicitly
  tested device/global abuse bucket.
- Rapid input cannot create unbounded queued or running PBKDF work.
- Successful login clears only documented buckets after the session transaction
  commits.

## ID-04 — Enforce a true login-first runtime

**Priority:** P0-Critical  
**Depends on:** ID-03

### Files to inspect/modify/create

- `src/Dobby.Host/App.xaml.cs`
- `src/Dobby.Host/Login/LoginRuntimePreparer.cs`
- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- `src/Dobby.Host/Login/HostLoginRuntime.cs`
- `src/Dobby.Host/Login/PostLoginSessionRuntime.cs`
- `src/Dobby.Host/Login/DeferredLoginRuntimeWorkflow.cs`
- `src/Dobby.Host/Login/AuditRetentionMaintenance.cs`
- `src/Dobby.Host/Startup/HostBootstrapper.cs`
- `src/Dobby.Host/PostLoginShellPresenter.cs`
- startup/login runtime tests

### Steps

1. Add behavior probes/fakes whose constructors and `Start` methods record when
   shell, plugin catalog/activation, licensing/maintenance, retention and business
   repositories are created. Assert zero pre-auth events, including failed login,
   recovery view and window cancellation paths.
2. Split composition into an explicit minimal `PreAuthRuntime` and a disposable
   session-owned `AuthenticatedRuntime`. A lazy/task that starts full composition
   at login-window creation is still a failure.
3. Keep only the closed pre-auth services from ADR-0061. Defer audit retention,
   plugin/license initialization, post-login migrations and shell model creation
   until session creation succeeds.
4. On successful login, commit session state first, then create the authenticated
   scope with linked cancellation. Show bounded progress only after the login
   outcome is established.
5. On logout/expiry/window exit, cancel and await authenticated operations,
   dispose plugin/load/runtime state and return to a newly clean pre-auth scope.
6. Localize the hard-coded login-runtime-unavailable message and all new states.
7. Preserve and measure first-paint, login-interactive and authenticated-ready as
   distinct milestones.

### Acceptance

- Executable tests prove no forbidden runtime is constructed or started before
  authentication, not merely that call-site text is absent.
- Failed login/recovery does not start or leak an authenticated scope.
- Logout disposes the scope and a second login creates a clean scope.

## ID-05 — Correct password, user and session mutations

**Priority:** P0-High  
**Depends on:** PA-02, ID-03, ID-04

### Files to inspect/modify

- `src/Dobby.Host/Login/SelfServicePasswordChangeCoordinator.cs`
- `src/Dobby.Security/Authentication/ManagedUserService.cs`
- `src/Dobby.Security/Authentication/SessionLifecycleService.cs`
- `src/Dobby.Security/Authentication/SessionStore.cs`
- `src/Dobby.Security/Authentication/ActiveSessionAccessor.cs`
- `src/Dobby.Host/Settings/UserManagementAuditCoordinator.cs`
- `src/Dobby.Host/Sessions/SessionLifecycleAuditCoordinator.cs`
- relevant host/security/integration tests

### Steps

1. Add current-password and new-confirmation inputs to self-service change. Verify
   current credentials through the same durable throttle/reauth boundary; use a
   stable non-disclosing failure.
2. Move change/reset/create/deactivate/session-invalidate plus mandatory audit and
   outbox state into application transaction commands from `PA-02`.
3. Ensure credential update revokes other sessions according to the documented
   rule, while the current session behavior is explicit and tested.
4. Create durable session state before publishing it through
   `ActiveSessionAccessor`. If commit fails, memory and durable state remain at
   the prior value.
5. Add injected failures before/after each repository write and commit; assert no
   half-created user, changed password without audit, orphaned session or stale
   active accessor.
6. Add concurrent owner/user operations and WPF coordinator tests without timing
   sleeps.

### Acceptance

- Old password fails and new password succeeds only after one committed change.
- A failed mutation changes neither effective state nor required audit evidence.
- No successful UI result precedes durable commit.

## ID-06 — Replace fuzzy recovery and unsafe support identity

**Priority:** P0-Critical  
**Depends on:** PA-02, PL-03 key-profile foundation

### Files to inspect/modify/create

- `src/Dobby.Security/Authentication/DeviceFingerprint*.cs`
- `src/Dobby.Security/Authentication/Recovery*.cs`
- `src/Dobby.Security/Authentication/IVendorRecoveryPublicKey.cs`
- `src/Dobby.Infrastructure/HardwareIdentity/*`
- `src/Dobby.Host/Login/Recovery*`
- `src/Dobby.Host/Settings/FixedSupportDeviceCodeProvider.cs`
- `src/Dobby.Admin/Commands/AdminCommandHost.cs`
- `tools/create-recovery-response/*`
- versioned recovery request/response schemas and tests

### Steps

1. Freeze recovery v2 schemas with strict bounds and canonical bytes. Request
   fields include schema/purpose, request ID, random nonce, customer/deployment/
   device identity, recovery public-key hash, issue/expiry and vendor key ID.
   Response repeats/binds every authority field and desired authorized action.
2. Generate a device recovery key pair during protected bootstrap. Protect the
   private key with the correct DPAPI/ACL owner; expose only the public-key hash.
   Define key-loss as a fail-closed support/provisioning event.
3. Sign request possession proof with the device key. Verify vendor response with
   the purpose-separated recovery key and verify request match, possession,
   expiry and single-use replay record before allowing reset.
4. Atomically consume the response, reset Owner credentials/revoke sessions and
   write audit/outbox evidence. Confirmation/cancellation consumes nothing.
5. Retain hardware values only as bounded diagnostics/risk signals. They cannot
   contribute threshold points to authorization.
6. Replace raw MachineGuid-derived support code with a versioned keyed digest over
   stable non-secret device identity. Format it for human transcription with a
   checksum; never log the raw input/key.
7. Upgrade admin tool and fixtures; keep v1 only behind an explicit migration
   decision, never as silent fallback.

### Acceptance

- Wrong nonce/request/device/public key/purpose/key version, expired response and
  replay all fail closed.
- Changing MAC/disk/board observations neither authorizes nor blocks a valid key-
  bound recovery.
- No user-visible or exported value exposes raw MachineGuid material.

## Workstream Verification

Run focused tests after each task and, at the workstream gate:

```powershell
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Security|FullyQualifiedName~Recovery|FullyQualifiedName~Login|FullyQualifiedName~Session"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Auth|FullyQualifiedName~Recovery|FullyQualifiedName~Startup|FullyQualifiedName~Sessions"
dotnet test .\Dobby.sln -c Release --no-restore
```

Record deterministic test names, counts and any migration fixture hashes in the
current release-evidence directory.
