# Sprint 1 Architecture Baseline

## Sources

- `SPRINT_1_EXECUTION_PLAN.md`
- `SPRINT_IMPLEMENTATION_PLAN.md`
- `docs/02_ARCHITECTURE_MAP.md`
- `docs/04_RESULT_VS_EXCEPTION_POLICY.md`
- `docs/08_CODING_STANDARDS.md`
- `docs/09_BUILD_RUN_AND_RELEASE.md`
- `docs/adr/ADR-0040-internal-host-adapters-boundary.md`
- `docs/adr/ADR-0053-capability-surface-runtime-mode.md`
- `Dobby.sln`
- `src/**/*.csproj`
- `tests/Dobby.IntegrationTests/Architecture/*.cs`

## Documentation Baseline Phase Classification

- P1-core: initial skeleton ownership and guardrail scope from docs `01`, `02`, `03`, `05`, `06`, `08`, and `09`, covering Core, Contracts, SDK, PluginFramework, Security, Licensing, Configuration, Localization, Navigation, Messaging, Application, Diagnostics, Audit, UI, Shell, Infrastructure, Host, Admin, sample-plugin shape, build conventions, package governance, encoding, and architecture boundaries.
- P1-if-time: optional or stretch surfaces such as fuller Admin composition, richer UI page-state/dialog surfaces, and later tool wrappers are recorded as non-blocking for Sprint 1 unless an accepted ADR or sprint plan explicitly promotes them.
- P2/future: update/rollback, backup/restore, retention, shared-data tooling, external deep links, advanced plugin update/repair flows, and other future runtime behavior stay outside Sprint 1 implementation.
- Branch Shared/Tier-B: Branch Shared and Tier-B materials are documentation/schema-only for Sprint 1; no Branch Setup runtime, PostgreSQL provider, Npgsql package, branch admin/migration tool, or shared-data runtime is authorized by this baseline.
- Initial-skeleton exclusions: `Dobby.Update`, `Dobby.BranchSetup`, `Dobby.Infrastructure.Postgres`, PostgreSQL deployment code, storage migrations/repositories/provider packages, plugin manager UI/runtime, license import UI/runtime, auth/session features, installer packaging, and release packaging are not Sprint 1 implementation work.

## Current Project Map

- `Dobby.Admin` -> `Dobby.Application`, `Dobby.Core`, `Dobby.Security`
- `Dobby.Application` -> `Dobby.Contracts`, `Dobby.Core`
- `Dobby.Audit` -> `Dobby.Core`, `Dobby.Infrastructure`
- `Dobby.Configuration` -> `Dobby.Core`
- `Dobby.Contracts` -> `Dobby.Core`
- `Dobby.Core` -> none
- `Dobby.Diagnostics` -> `Dobby.Core`
- `Dobby.Host` -> `Dobby.Application`, `Dobby.Audit`, `Dobby.Configuration`, `Dobby.Contracts`, `Dobby.Core`, `Dobby.Diagnostics`, `Dobby.Infrastructure`, `Dobby.Licensing`, `Dobby.Localization`, `Dobby.Messaging`, `Dobby.Navigation`, `Dobby.PluginFramework`, `Dobby.Security`, `Dobby.Shell`, `Dobby.UI`
- `Dobby.Infrastructure` -> `Dobby.Application`, `Dobby.Core`
- `Dobby.Licensing` -> `Dobby.Contracts`, `Dobby.Core`
- `Dobby.Localization` -> `Dobby.Core`
- `Dobby.Messaging` -> `Dobby.Contracts`, `Dobby.Core`
- `Dobby.Navigation` -> `Dobby.Contracts`, `Dobby.Core`
- `Dobby.PluginFramework` -> `Dobby.Contracts`, `Dobby.Core`, `Dobby.Security`
- `Dobby.SDK` -> `Dobby.Contracts`, `Dobby.Core`
- `Dobby.Security` -> `Dobby.Configuration`, `Dobby.Contracts`, `Dobby.Core`, `Dobby.Infrastructure`
- `Dobby.Shell` -> `Dobby.Application`, `Dobby.Contracts`, `Dobby.Core`, `Dobby.Localization`, `Dobby.Navigation`, `Dobby.UI`
- `Dobby.UI` -> `Dobby.Core`, `Dobby.Localization`

## Static Guardrail Status

- Baseline before Sprint 1 guardrail changes: `dotnet restore Dobby.sln`, `dotnet build Dobby.sln --no-restore`, and `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` completed successfully.
- Sprint 1 added focused static guardrails for architecture dependency boundaries, forbidden initial projects, `ErrorCode` baseline registry equality, Dobby-owned namespace dumping-ground segments, package governance, encoding, plugin/SDK/Contracts boundaries, and release evidence presence.
- Focused integration test red check before evidence and encoding remediation failed only for the expected missing evidence files, the `Dobby.sln` BOM, and the temporary print-boundary blocker evidence.
- Final verification on 2026-05-11: `dotnet restore Dobby.sln` passed; `dotnet build Dobby.sln --no-restore` passed with 0 warnings and 0 errors; `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` passed with 50 total tests, 0 failures; `dotnet format Dobby.sln --verify-no-changes` passed.
- Sprint 2 final verification on 2026-05-12: `dotnet restore Dobby.sln` passed; `dotnet build Dobby.sln --no-restore` passed with 0 warnings and 0 errors; `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` passed with 111 total tests, 0 failures; `dotnet format Dobby.sln --verify-no-changes` passed. The ADR-0001 type-level Contracts/Core surface guardrail and plugin-facing no-WPF Contracts guardrail are now active in `ContractsCoreSurfaceTests`.

## Known Blockers

- `SPRINT1-INFRA-APP-PRINT-BOUNDARY`: path (a) is renewed for Sprint 2 only by the human-authorized 2026-05-12 decision recorded in `## Sprint 2 Reconfirmation`. The current `Dobby.Infrastructure` to `Dobby.Application` reference remains an evidence-bound unresolved blocker, not architectural approval of the edge. The hard sunset gate is Sprint 9 per `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 9 "Sprint 1 Print Boundary Sunset"; Sprint 9 must remove the `Dobby.Infrastructure` to `Dobby.Application` `ProjectReference`, complete `IPrintTargetClassifier` ADR-0053 host-enforcement/DI consumption, and remove the `IsTemporaryPrintBoundaryEdge` / `PrintBoundaryBlockerIsRecorded` architecture-test allowance.
- Plugin manifest schema/code/sample drift, including the schema-required `capabilities[].name` field, is deferred to the plugin/package implementation sprint.
- Operation verb taxonomy drift is deferred to the authorization/plugin-manifest implementation sprint.

## Dobby.sln Encoding Evidence

- Implementation-time pre-fix leading bytes: `EF BB BF 0D 0A 4D 69 63`.
- Pre-fix result: UTF-8 BOM was present.
- Action: removed the BOM as the default Sprint 1 encoding baseline fix under the repository UTF-8/no-BOM rule.
- Post-fix leading bytes: `0D 0A 4D 69 63 72 6F 73`.
- Post-fix result: UTF-8 BOM is absent.

## Deferred Drift

- Branch Shared runtime projects remain absent from the initial skeleton.
- `Dobby.Infrastructure` currently lacks the allowed `Dobby.Contracts` reference from the architecture dependency table; this is recorded only as observed current graph evidence, not as a mismatch or required Sprint 1 fix.
- `Dobby.SDK` currently lacks the allowed `Dobby.Localization` reference from ADR-0021 and the architecture dependency table; this is recorded only as observed current graph evidence, not as SDK completeness proof or a required Sprint 1 fix.
- Sprint 1 does not implement storage providers, plugin manager UI/runtime behavior, license import/enforcement, auth/session behavior, shell UI, installer packaging, Branch Shared runtime, or `Dobby.Update`.

## Sprint 2 Reconfirmation

- At Sprint 2 start, the architecture-baseline pass must either (a) renew `SPRINT1-INFRA-APP-PRINT-BOUNDARY` by human confirmation, (b) remove the named architecture-test exception and let the `Dobby.Infrastructure` to `Dobby.Application` edge fail, or (c) record the human-authorized ADR-0040 or architecture-map amendment that changes the boundary.
- Human-authorized reconciled decision recorded on 2026-05-12 (Asia/Baku): path (a) is selected. `SPRINT1-INFRA-APP-PRINT-BOUNDARY` is renewed for Sprint 2 only; this is not architectural approval of the `Dobby.Infrastructure` to `Dobby.Application` edge. The hard sunset gate is Sprint 9 per `SPRINT_IMPLEMENTATION_PLAN.md` Sprint 9 "Sprint 1 Print Boundary Sunset"; Sprint 9 must remove the `Dobby.Infrastructure` to `Dobby.Application` `ProjectReference`, complete `IPrintTargetClassifier` ADR-0053 host-enforcement/DI consumption, and remove the `IsTemporaryPrintBoundaryEdge` / `PrintBoundaryBlockerIsRecorded` architecture-test allowance.
- Sprint 2 final evidence on 2026-05-12 consumed the path (a) renewal with all final gates passing: `dotnet restore Dobby.sln`, `dotnet build Dobby.sln --no-restore` with 0 warnings and 0 errors, `dotnet test Dobby.sln --no-build --logger "console;verbosity=minimal"` with 111 total tests and 0 failures, and `dotnet format Dobby.sln --verify-no-changes`. The renewal remains Sprint 2-only, is not architectural approval of the `Dobby.Infrastructure` to `Dobby.Application` edge, and the Sprint 9 sunset gate remains unchanged.

## Sprint 4 Phase B Decision Notes

- 2026-05-14 (Asia/Baku): D-01 project-graph isolation strategy APPROVED by `n4hkro`. The selected mitigation is `PrivateAssets="all"` on the temporary `Dobby.Infrastructure -> Dobby.Application` print-boundary `ProjectReference`. This is the human-approved mitigation referenced in `SPRINT_4_EXECUTION_PLAN.md` §13.9; it does not amend the architecture dependency table, does not approve the temporary print edge as permanent architecture, and does not replace the Sprint 9 print-boundary sunset.
- Sprint 4 verifies D-01 with two complementary checks: a metadata-aware transitive dependency check in `ArchitectureDependencyBoundaryTests` that treats `PrivateAssets="all"` project references as non-propagating, and a build-output smoke test that asserts `Dobby.Application.dll` is absent from `Dobby.Audit` and `Dobby.Security` `bin/` outputs.
- Cross-reference: `decision-log.md` anchor `phase-b-decisions-2026-05-14`.
- This note records the strategy decision only. Sprint 4 implementation evidence (test count, new edges present, deferred drift updates, and reconfirmation path) is appended in Step 9.14 as a separate Sprint 4 Reconfirmation entry.

## Sprint 4 Reconfirmation

Date: 2026-05-15 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED.

Verification summary:

- Pre-implementation baseline on 2026-05-15 passed with 175 total tests, 0 failures.
- Sprint 4 implementation verification passed: `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror` completed with 0 warnings and 0 errors.
- Sprint 4 implementation verification passed: `dotnet test .\Dobby.sln -c Debug --no-build --logger "console;verbosity=minimal"` completed with 198 total tests, 0 failures.
- Source-level test attribute count after Sprint 4 implementation is 143 `[Fact]`/`[Theory]` declarations under `tests/`.

Architecture graph evidence:

- `Dobby.Audit -> Dobby.Infrastructure` is now present for durable audit persistence and is covered by architecture tests.
- `Dobby.Security -> Dobby.Infrastructure` was not added because Sprint 4 did not introduce a first-use security consumer for the protected-storage wrappers.
- `Dobby.Infrastructure -> Dobby.Contracts` remains an allowed-but-absent edge from the architecture map and is not required by the Sprint 4 implementation.
- `Dobby.Infrastructure -> Dobby.Application` remains the temporary print-boundary edge only; this reconfirmation does not approve it as permanent architecture and does not change the Sprint 9 sunset gate.
- The temporary print-boundary edge is constrained with `PrivateAssets="all"` and `Private=false`; architecture tests verify metadata-aware non-propagation and verify that `Dobby.Application.dll` is not copied to `Dobby.Audit` or `Dobby.Security` build outputs.

Boundary evidence:

- Plugin/sample projects do not reference `Dobby.Infrastructure`.
- `Dobby.Contracts` and `Dobby.Core` public surface guardrails remain unchanged.
- No Branch Shared runtime project was added; Branch Shared remains docs-only for Sprint 4.
- No Sprint 5 KDF runtime, Sprint 6 permission runtime, Sprint 8 plugin install schema, Sprint 9 license runtime, Sprint 10 sample-plugin behavior, or Sprint 11 publish/installer smoke work was pulled into Sprint 4.

## Sprint 5 Reconfirmation

Date: 2026-05-19 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED.

Verification summary:

- Final verification on 2026-05-19 (Asia/Baku): `dotnet restore .\Dobby.sln -v minimal` completed with `DOBBY_RESTORE_EXIT=0`.
- Final verification on 2026-05-19 (Asia/Baku): `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror -v minimal` completed with `DOBBY_BUILD_EXIT=0`.
- Final verification on 2026-05-19 (Asia/Baku): `dotnet test .\Dobby.sln -c Debug --no-build --logger "console;verbosity=minimal"` completed with 333 total tests, 0 failures, and `DOBBY_TEST_EXIT=0`.
- Final verification on 2026-05-19 (Asia/Baku): `dotnet format .\Dobby.sln --verify-no-changes --no-restore` completed with exit code 0.
- Follow-up verification on 2026-05-20 (Asia/Baku): `dotnet restore .\Dobby.sln -v minimal`, `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror -v minimal`, `dotnet test .\Dobby.sln -c Debug --no-build --logger "console;verbosity=minimal"`, and `dotnet format .\Dobby.sln --verify-no-changes --no-restore` completed successfully with 344 total tests, 0 failures, and 0 skipped tests.
- Post-review fix verification on 2026-05-20 (Asia/Baku): `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror -v minimal`, `dotnet test .\Dobby.sln --no-build --logger "console;verbosity=minimal"`, `dotnet format .\Dobby.sln --verify-no-changes --verbosity minimal`, and `dotnet build .\Dobby.sln -c Release --no-restore -warnaserror -v minimal` completed successfully with 346 total tests, 0 failures, 0 skipped tests, and 0 warnings.
- Execution follow-up verification on 2026-05-20 (Asia/Baku): `dotnet format .\Dobby.sln --verify-no-changes --verbosity minimal`, `dotnet test .\Dobby.sln -c Debug`, and `dotnet build .\Dobby.sln -c Release --no-restore -warnaserror -v minimal` completed successfully with 352 total tests, 0 failures, 0 skipped tests, and 0 warnings.
- Recovery rollback evidence follow-up on 2026-05-20 (Asia/Baku): `RecoveryResponseImportConfirmTests.Confirm_rolls_back_credential_nonce_session_and_audit_when_transaction_audit_fails` was added and targeted verification completed with 5 recovery confirm tests, 0 failures, and 0 skipped tests. This closes the Sprint 5 mid-transaction failure rollback evidence gap for Owner recovery confirm.
- Current closure verification on 2026-05-20 (Asia/Baku): `dotnet restore .\Dobby.sln -v minimal`, `dotnet build .\Dobby.sln -c Debug --no-restore -warnaserror -v minimal`, `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal`, `dotnet build .\Dobby.sln -c Release --no-restore -warnaserror -v minimal`, and `dotnet test .\Dobby.sln -c Debug --no-build --logger "console;verbosity=minimal"` completed successfully with 365 total tests, 0 failures, 0 skipped tests, and 0 warnings.

Consensus gap closure evidence:

- Host WPF runtime surface, login-first startup flow, concrete platform seams, Host-mediated login/session audit bridges, password-verifying unlock behavior, recovery request schema, DoD-named tests, and JCS recovery fixture refresh are implemented and covered by the current test suite.
- Sprint 5 closure follow-up on 2026-05-19 wires `App.xaml.cs` through `HostLoginRuntimeFactory`, passes `LoginRuntimeWorkflow` into `LoginWindow`, requires successful `LoginService` + `SessionLifecycleService.Create` before the login dialog is accepted, creates/clears the ADR-0055 remember-me slot from the same Host login workflow, routes `LockWindow` unlock/logout through `LockRuntimeWorkflow` instead of accepting the dialog directly, passes `PostLoginSessionRuntime` plus the authenticated `SessionRecord` into `MainWindow`, reopens the login dialog when post-login checkpointing requests login return, and binds the recovery export/import view buttons to the Host-owned `RecoveryViewModel` adapter/coordinator surface.
- Sprint 5 closure follow-up on 2026-05-19 completes the remaining host-side gaps: `.dobbyrecovery` import now verifies the selected file through `RecoveryResponseImportCoordinator.VerifyFile`, prompts for the new Owner password through a Host-owned `IRecoveryPasswordResetPrompt`, confirms reset or cancels without nonce burn; self-service password change now checks a matching active, non-expired session before credential rebind; the login surface exposes explicit `Saved login-i unut` clearing and wires password edits to clear only the masked form prefill; post-login checkpointing routes auto-lock to `LockWindow` and records session audit through `SessionLifecycleAuditCoordinator`.
- Sprint 5 closure follow-up on 2026-05-19 also closes the review-found auth/session gaps: `SessionLifecycleService` now invalidates active sessions when the backing user is deactivated and `SessionLifecycleAuditCoordinator` emits `session.invalidated`; `CredentialStore` maps protected credential unwrap failure to a typed fail-closed result; credential and remember-me protection entropy is derived from runtime `ApplicationEntropy` plus user/slot scope and wired through `HostLoginRuntimeFactory`, Owner bootstrap, self-service password change, and recovery password reset.
- Sprint 5 follow-up on 2026-05-20 closes the D-18 recovery response contract gap: `.dobbyrecovery` import now rejects unsupported response `type`, unsupported `payload.keyVersion`, and non-canonical response property casing before accepting a verified challenge.
- Sprint 5 follow-up on 2026-05-20 closes the remaining local review gaps: every recovery import attempt now emits a Host-mediated `recovery.import` audit row with `requestId`, deterministic `outcome`, and hashed nonce; host input refreshes `Sessions.LastActivityAtUtc` so the 30-minute idle timeout is activity-based; remember-me slot mutation now occurs only after session creation succeeds.
- Sprint 5 follow-up on 2026-05-20 closes the R-06 Owner bootstrap race coverage gap: `OwnerBootstrapCoordinator` serializes first-Owner bootstrap transaction entry in-process, and `OwnerBootstrapTransactionTests.Concurrent_bootstrap_attempts_are_serialized_and_create_only_one_owner` verifies two parallel attempts produce one Owner, one credential row, one `user.create` audit row, and zero Owner `Permissions` rows.
- Sprint 5 execution follow-up on 2026-05-20 closes the remaining AC-029 and recovery-confirm review gaps: the post-login placeholder now exposes a current-user account password-change action plus manual lock entry point, manual lock routes through the same `LockWindow` runtime as auto-lock, login throttle countdown refreshes until the button is re-enabled, successful unlock refreshes `Sessions.LastActivityAtUtc`, and recovery password-reset confirm revalidates `expiresAt` before rebinding credentials or burning the nonce.
- Sprint 5 execution follow-up on 2026-05-20 hardens the D-18/AC-007 recovery response schema gate: `RecoveryResponseImporter.ReadEnvelope` rejects unsupported envelope, payload, and device-identity members, including managed-user target fields such as `targetUserId`, before signature verification can accept the response.
- Sprint 5 recovery rollback follow-up on 2026-05-20 proves an injected transaction-scoped audit failure after credential rebind/nonce burn/session invalidation attempts rolls the whole recovery confirm transaction back: old password remains valid, new password fails, `ConsumedRecoveryNonces` stays empty, the active session remains uninvalidated, and no audit row is committed.
- `Dobby.Security -> Dobby.Audit` remains absent; audit emission remains Host-mediated.

### Sprint 5 AC Matrix

- AC-001: `PreLoginAllowedWorkloadTests`, `StartupBootstrapWiringTests`, and `HostRuntimeSurfaceTests` verify the closed pre-login workload, milestone order, and login-first host surface.
- AC-002: `LoginServiceTests`, `OwnerBootstrapTransactionTests`, and `OwnerBootstrapCoordinatorTests` verify clean-install first Owner bootstrap, optional `DisplayName`, transactional `Users` + `Credentials` + `user.create`, in-process serialization of concurrent first-Owner bootstrap attempts, and zero Owner `Permissions` rows.
- AC-007 (host-side): `RecoveryRequestExportTests`, `RecoveryResponseImportFixtureTests`, `RecoveryResponseImportConfirmTests`, `RecoveryResponseImporterUnitTests`, `RecoveryCanonicalJsonWriterTests`, `RecoveryViewModelFileInteractionTests`, and `LoginWindowRuntimeWiringTests` verify request export, TEST-ONLY signed fixture import, D-18 response `type`/`payload.keyVersion`/canonical property casing/unsupported-member enforcement, managed-user target-field rejection, P1363/base64/DER rejection, cancel/confirm behavior, active Owner session invalidation, `session.invalidated` audit, and Host-owned file/shell/clipboard/password-prompt adapters.
- AC-008 contribution: `LoginServiceTests` and `LoginRuntimeWorkflowTests` verify offline local auth/session creation without network or plugin activation; Sprint 11 still owns consolidated release-smoke evidence.
- AC-025: `RememberMeSlotTests`, `LoginViewModelRememberMeTests`, `LoginWindowRuntimeWiringTests`, and `LoginRuntimeWorkflowTests` verify masked saved-login prefill, no auto-login, no reveal control, explicit `Saved login-i unut`, password-edit prefill clearing, single-slot replace, and OFF clear.
- AC-026: `LoginThrottleServiceTests`, `LoginViewModelThrottleStateTests`, and `LoginServiceTests` verify `[0,0,10,30,60]` application-wide throttle, countdown surface, username-change non-reset, and success reset.
- AC-028: `ActiveSessionAccessorTests`, `SessionLifecycleServiceTests`, `SessionLogoutAndLockTests`, `LoginRuntimeWorkflowTests`, `PostLoginSessionRuntimeTests`, and `LoginWindowRuntimeWiringTests` verify one active session, logout invalidation, lock preservation, remember-me persistence boundaries, and post-login session checkpoint login-return behavior.
- AC-029: `SelfServicePasswordChangeTests`, `SessionLogoutAndLockTests`, `SessionLifecycleServiceTests`, `SessionLifecycleAuditCoordinatorTests`, `PostLoginSessionRuntimeTests`, and `LoginWindowRuntimeWiringTests` verify no-current-password self-service password change with active-session guard, old/new password behavior, session-version/remember-me rebind, manual lock/unlock/logout, auto-lock to the same lock surface, deactivation invalidation audit, and session audit.
- AC-044: `SingleInstanceCoordinatorTests`, `StartupBootstrapWiringTests`, and `HostRuntimeSurfaceTests` verify the coordinator is acquired before `LoginVisible` and second instance handoff exits before writable login persistence.
- AC-045: `UsernameCanonicalValidatorTests`, `PasswordPolicyValidatorTests`, `DisplayNameValidatorTests`, `CredentialStoreRoundTripTests`, and `OwnerBootstrapTransactionTests` verify username/password validation, storage round-trip, normalization conflict rejection, and display-name separation with Azerbaijani/local Unicode.
- AC-050: `SessionTimeoutIntegrationTests`, `SessionLifecycleServiceTests`, `PostLoginSessionRuntimeTests`, and `LoginWindowRuntimeWiringTests` verify 8h absolute expiry, 30m idle expiry with auto-lock disabled, deactivated-user session invalidation, authenticated context cleanup, and login-return signal plus app login-loop wiring on expiry.
- AC-051 (skeleton-time): `KdfPolicyResolverTests`, `CredentialStoreRoundTripTests`, `SecurityTests`, `AuthOptionsTests`, `ConfigurationTests`, and `NoPlaintextSecretContentScanTests` verify PBKDF2-HMAC-SHA256 600000 / 16-byte salt / 32-byte hash, floor enforcement, unknown/Argon2-without-amendment fail-closed behavior, tampered protected credential fail-closed behavior, and ADR-0055 machine-bound credential wrapping with runtime application entropy.

Architecture graph evidence:

- `Dobby.Security -> Dobby.Infrastructure` is now present for protected credential material, remember-me slot storage, and hardware identity abstraction consumption.
- `Dobby.Security -> Dobby.Configuration` is now present for `AuthOptions` / `SessionOptions` policy resolution.
- `Dobby.Security -> Dobby.Audit` remains absent; audit emission remains Host-mediated.
- `System.Management` is pinned centrally at `10.0.8` and referenced only from `Dobby.Infrastructure` under the `net10.0-windows` hardware identity adapter slice.
- Plugin-visible assemblies remain unable to reference Sprint 5 auth, recovery, protected-storage, or hardware identity types.
- Sprint 5 session material status: no separate protected session secret material was introduced; `Sessions` remains metadata-only as recorded in Sprint 4 storage materialization. Remember-me material uses `DpapiCurrentUserProtector` through `RememberMeSlotService` with runtime `ApplicationEntropy` plus per-user/per-slot entropy, and credential material uses `DpapiLocalMachineProtector` through `CredentialStore` with runtime `ApplicationEntropy` plus per-user credential entropy.
- B-01/B-02/B-03/B-04/B-05/B-06/B-07/B-08 and ADR-0055 closure gaps are recorded as closed for Sprint 5. Remaining deferred items are planned Sprint 10/11 production vendor key/Admin-generated `.dobbyrecovery` evidence and Sprint 11 release-candidate latency/smoke handover only.

Recovery fixture evidence status: COMPLETE.

- TEST-ONLY recovery public key path: `tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/vendor-recovery-public-key.TEST-ONLY.txt`.
- TEST-ONLY recovery public key SHA-256: `1a30c878be9f0d69adcfbad0a7244e19738a3c81fb9ca7eb2581a8b1c2455a19`.
- Fixture inventory and custody details are recorded in `artifacts/release-evidence/skeleton-local/test-fixture-authority.md`.
- Production vendor private key infrastructure, Admin-generated `.dobbyrecovery` evidence, release-candidate recovery smoke evidence, and production public key handover remain deferred to Sprint 10/11 as planned.

## Sprint 6 Implementation Evidence

Date: 2026-05-25 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED.

Implementation summary:

- Operation authorization now uses the Sprint 6 canonical verb set, including `configure` and `grant`, with operation keys parsed by last dot and unknown/malformed keys failing closed.
- Built-in operation targets are registered for `plugins.license`, `plugins.management`, and `users.management`; `users.management` uses the D-20 approved verbs `view`, `create`, `update`, `delete`, `execute`, and `grant`.
- Managed users remain default-deny under durable grant-only `Permissions` rows. Owner all-grants continue to come from `Users.IsOwner=1`, with no Owner permission rows required.
- User Management service behavior covers list/detail under `view`, create/update/deactivate/reactivate/reset-password/grant/revoke, Owner protection, self-grant protection, cannot-grant-absent-permission, delete-as-lifecycle behavior, session invalidation, and no physical hard-delete path.
- Settings/About source behavior is implemented under D-21: `Dobby.Host.Settings` owns authenticated source orchestration only; durable Settings preference ownership remains in `Dobby.Configuration`; concrete `HostConfiguration` persistence is Host-wired through the existing table.
- Account Settings source behavior exposes password-change and saved-login shortcuts backed by the existing post-login password-change coordinator and remember-me slot clearer, without adding manual lock/logout duplication to Settings.
- About source behavior exposes support-safe version/device/support/legal fields, does not expose license import/status, and uses the neutral `About.OwnerStatus.Registered` key for managed users.

Sprint 6 AC matrix:

- AC-022: `OperationVerbParserTests`, `OperationTargetRegistryTests`, `OperationAuthorizationPolicyTests`, `UserManagementAuthorizationGuardTests`, `OperationAuthorizationIntegrationTests`, and `PluginFrameworkTests` cover canonical verbs, built-in targets, plugin metadata target registration, grant-only/default-deny, unknown target/verb fail-closed behavior, non-disclosing deny, Host-mediated permission-denied audit, Owner all-grants, self-grant guard, Owner protection, and cannot-grant-absent-permission.
- AC-030: `UserManagementServiceTests` covers Owner-managed user CRUD, service validation, duplicate username handling, reset, deactivate/reactivate, grant/revoke toggles, matching-grant managed-user mutations, `view`-gated list/detail, managed-user denial without permission, no credential material in list/detail results, delete-as-lifecycle behavior, and no hard-delete path.
- AC-039: `UserManagementServiceTests`, `SessionTimeoutIntegrationTests`, and the existing `LoginServiceTests` cover active-session invalidation on reset/deactivation, next-checkpoint invalidation for deactivated active users, session invalidation audit, and future-login denial for deactivated managed users until reactivation.
- AC-035: `SettingsAndAboutViewModelTests`, `SettingsPreferenceSurfaceTests`, `SettingsPreferencePersistenceTests`, `AboutInfoProviderTests`, `SettingsLocalizationStringCatalogTests`, and `HostLoginRuntimeFactoryTests` cover the minimum Settings category source tree, Appearance shared theme preference, Host-wired `HostConfiguration` persistence, Account shortcuts backed by existing password-change and saved-login clear behavior without manual lock/logout duplication, passive auto-lock source state, User Management entry gating, About privacy fields, no license import/status, and neutral Owner identity key.

Verification summary:

- Pre-implementation baseline on 2026-05-25: `dotnet test Dobby.sln --configuration Release --no-restore` completed with 422 total tests, 0 failures, and 0 skipped tests.
- Sprint 6 RED verification on 2026-05-25: `dotnet test Dobby.sln --configuration Release --no-restore` failed on missing Sprint 6 types/API surface as expected before implementation.
- Sprint 6 implementation verification before evidence append on 2026-05-25: `dotnet test Dobby.sln --configuration Release --no-restore` completed all non-evidence Sprint 6 tests successfully: 330 unit tests passed, 4 smoke tests passed, and 152 integration tests passed; the only remaining failure was `Sprint6ReleaseEvidenceTests` because this evidence block was not yet present.
- Final Sprint 6 verification on 2026-05-25: `dotnet build Dobby.sln --configuration Release --no-restore -warnaserror` completed with 0 warnings and 0 errors.
- Final Sprint 6 verification on 2026-05-25: `dotnet test Dobby.sln --configuration Release --no-build --logger "console;verbosity=minimal"` completed with 487 total tests, 0 failures, and 0 skipped tests.
- Final Sprint 6 verification on 2026-05-25: `dotnet format Dobby.sln --verify-no-changes --no-restore --verbosity minimal` completed with exit code 0.
- Sprint 6 audit follow-up on 2026-05-26: targeted TDD checks added Account Settings shortcut backing plus User Management validation, duplicate, revoke, matching-grant mutation, deactivation-session, and Host-mediated authorization-denial audit coverage. Focused verification completed with `SettingsAndAboutViewModelTests` 5/5 passed, `UserManagementServiceTests` 12/12 passed, `OperationAuthorizationIntegrationTests` 4/4 passed, and `HostLoginRuntimeFactoryTests` 3/3 passed.

Architecture and boundary evidence:

- Dobby.Security -> Dobby.Audit edge: absent. Security remains audit-neutral; User Management audit coordination is Host-mediated through `Dobby.Host.Settings.UserManagementAuditCoordinator` and existing `AuditEventName` constants.
- `Dobby.Configuration -> Dobby.Infrastructure` edge: absent. `Dobby.Configuration` defines typed Settings preference behavior and abstractions only; Host wires the concrete `HostConfiguration` table adapter.
- No new Core `ErrorCode` values were added.
- No new audit event names were added.
- No new package pins, package sources, native assets, or inline package versions were added. No new package pins.
- No schema widening was added; existing `Users`, `Credentials`, `Sessions`, `Permissions`, `AuditLog`, and `HostConfiguration` tables carry Sprint 6 behavior.

Decision closure:

- D-20 remains the exact authority for the built-in User Management target id `users.management`.
- D-21 remains the exact authority for `Dobby.Host.Settings` source orchestration and `Dobby.Configuration` durable Settings preference ownership.

Deferred work preserved:

- Sprint 7 still owns rendered Settings tree, theme interaction polish, snapshots, and manual visual evidence.
- Sprint 8 still owns plugin package activation and seeding plugin `operationTargets` from validated signed packages.
- Sprint 9/10 still own license import/runtime and plugin management lifecycle UI beyond the Sprint 6 built-in registry targets.

## Sprint 7 Implementation Evidence

Date: 2026-05-28 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED.

Implementation summary:

- Authenticated shell composition is implemented around the existing `Dobby.Host.MainWindow` WPF container and the `Dobby.Shell` view-model/state surface. The shell exposes `TopToolbar`, `Sidebar`, `ThirdBar`, `MainContent`, `BottomStatus`, and `MinimizedWorkStrip` state through `ShellLayoutState`.
- Navigation descriptor composition is owned by `Dobby.Navigation` through `NavigationContribution` and `NavigationComposer`; `Dobby.Shell` renders/consumes the composed model and does not own descriptor normalization.
- Built-in Home, Plugins, and Settings route identities remain unchanged: `Dobby.Shell.Navigation.Home`, `Dobby.Shell.Navigation.Plugins`, and `Dobby.Shell.Navigation.Settings`.
- Theme preference switching is Host-mediated by `ShellThemeCoordinator` and writes the same `AppearancePreference` consumed by Settings Appearance.
- Shell view preferences are typed in `Dobby.Configuration` through `ShellViewPreference` and persisted through authenticated-user-scoped `HostConfiguration` keys derived by `SettingsPreferenceScope`. The persisted key namespace uses a stable hash and does not contain raw user identifiers.
- Host localization now has Sprint 7 visible-shell keys, `Host.az-AZ.resx` parity, a Dobby-owned `IStringLocalizer<T>` adapter path with fallback behavior, and production missing-key diagnostics recording through `ShellLocalizationDiagnosticsRecorder`.
- Work sessions remain in-memory and process-lifetime only. `WorkSessionRegistry` and `WorkSessionCloseCoordinator` cover explicit minimize/restore, progress/result/error/input/screen state retention, close/cancel/minimize warning choices, lock preservation, logout warning/close handling, plugin-disable pending close requests, and no restart restore.
- Authenticated diagnostics export is exposed through `ShellDiagnosticsExportCoordinator`, uses the existing local `DiagnosticsExportPackageBuilder`, keeps `Uploaded=false`, redacts/excludes sensitive entries, and records the existing `diagnostics.export.created` audit event.
- `WpfUiDispatcher` implements the existing `Dobby.Messaging.UiDispatch.IUiDispatcher` abstraction in `Dobby.Shell.UiDispatch` per the Q8 dispatcher placement constraint, and production background status updates are wired through `ShellBackgroundOperationCoordinator`.
- Rendered WPF nested navigation evidence now includes top, child, and leaf route buttons. `ShellSnapshotTests.Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation` asserts rendered WPF child route tag `plugin.sample.analytics.daily`, rendered WPF leaf route tag `plugin.sample.analytics.daily.filters`, localized rendered notification labels for the Toast/Banner/Modal taxonomy, and localized rendered feedback labels for Loading/Empty/Success/Error/Confirmation states.
- Shell preference evidence covers Host adaptation between durable `ShellViewPreference` and shell runtime `ShellLayoutPreference`. ShellViewPreference fields are adapted through Host into ShellLayoutPreference runtime state. `ShellPreferenceMapperTests.Shell_runtime_preference_adapter_round_trips_all_committed_sprint7_fields` verifies `SidebarOpenNodeIds`, `SidebarRouteOrder`, `ThirdBarRouteOrder`, `TopToolbarPluginOrder`, `HiddenTopToolbarPluginIds`, window state, and `LastActiveModuleRouteKey`; `SettingsPreferencePersistenceTests.Preferences_persist_through_host_configuration_table_without_new_schema` keeps durable storage coverage; `MainWindow.xaml.cs` and `App.xaml.cs` call the production adapter at the runtime/durable boundary.
- Work-session navigation/logout warning evidence covers `PendingWorkSessionCloseRequest`: `WorkSessionShellIntegrationTests.Route_button_click_shows_warning_and_stay_minimize_close_resolve_navigation` clicks real WPF route buttons, shows the warning panel, proves Stay keeps the current route unchanged, and proves Minimize and Close continue requested navigation after resolving the pending work-session request. `WorkSessionShellIntegrationTests.Logout_button_uses_work_session_warning_and_cancel_or_close_before_returning_to_login` proves logout first surfaces the same warning and honors Cancel/Close before returning to login. `WorkSessionShellIntegrationTests.Plugin_disable_request_uses_same_close_cancel_minimize_lifecycle_without_navigation` proves plugin-disable uses the same cancel/minimize/close lifecycle without route activation.
- Resolution/DPI matrix evidence is covered by `ShellSnapshotTests.Shell_rendered_window_covers_resolution_and_dpi_matrix`: 1366x768 automated pass, 1920x1080 125% DPI automated pass, 2560x1440 automated pass, 3840x2160 automated pass, and 1920x1080 150% DPI automated pass. The matrix renders the same WPF shell path offscreen, verifies required surfaces remain visible, verifies localized notification/feedback presentation remains present, and asserts nonblank sampled color variance.
- Profile menu opening evidence is covered by `ShellKeyboardSmokeTests.Rendered_shell_supports_keyboard_traversal_focusable_actions_and_accessible_labels`: `ProfileMenuButton` opens `ProfileMenuPanel`, then the rendered menu exposes profile/account info, password change, manual lock, logout, and saved-login clear actions.
- Minimum contrast evidence is covered by `ShellUiStaticScanTests.Theme_contrast_tokens_meet_minimum_accessibility_ratio`, which checks light and dark theme background/text and surface/text ratios at 4.5:1 or higher and focus/background at 3.0:1 or higher. Runtime high-contrast and reduced-motion preference handling is covered by `ShellThemePreferenceTests.Accessibility_preferences_can_override_high_contrast_and_reduced_motion_resources` and `ShellUiStaticScanTests.Theme_coordinator_uses_runtime_accessibility_preferences`.
- Sprint 7 local automation substitution for manual UI evidence: the local verification environment uses rendered WPF automation as the accepted substitute for the manual/out-of-band resolution, DPI, theme-switch, accessibility, shell-state, and Settings/About rows. This is limited to Sprint 7 local evidence closure and does not replace a human release-gate smoke pass.

Sprint 7 AC matrix:

- AC-003: `ShellLayoutStateTests`, `ShellSnapshotTests`, `ShellKeyboardSmokeTests`, and `ShellUiStaticScanTests` cover required shell surfaces, rendered WPF snapshot coverage plus descriptor snapshot coverage for 1024x680 and 1920x1080 light/dark, notification/feedback presentation, focus traversal, and token/localization usage.
- AC-016: `ShellThemePreferenceTests`, `ShellThemeAndSettingsIntegrationTests`, `SettingsPreferenceSurfaceTests`, `SettingsPreferencePersistenceTests`, and `ShellPreferenceUserIsolationTests` cover shared TopToolbar/Settings Appearance preference behavior and authenticated-user-scoped shell preference persistence.
- AC-017: `ShellLocalizationCoverageTests`, `ShellLayoutStateTests.View_model_localizes_notification_and_feedback_presentations`, `ShellLocalizationDiagnosticsRecorderTests`, `ShellUiStaticScanTests`, and `EncodingBoundaryTests` cover centralized shell keys, `Host.az-AZ.resx parity`, adapter lookup/fallback behavior, production missing-key diagnostics recorder wiring, localized notification/feedback presentation labels, and UTF-8/no-BOM boundaries.
- AC-019: `WorkSessionLifecycleTests` and `WorkSessionShellIntegrationTests` cover minimize/restore, warning choices, close/cancel/minimize, lock/logout/plugin-disable behavior, no restart restore, and progress/result/error/input/screen state retention.
- AC-023: `NavigationComposerTests`, `NavigationTests`, and `ShellSnapshotTests` cover Home-first/Settings-last ordering, hierarchy, reset override behavior, route fallback, and nested `TopToolbar`/`Sidebar`/`ThirdBar` descriptor plus rendered WPF route-button rendering without plugin activation.
- AC-027: `ShellKeyboardSmokeTests` covers nested sample route activation, Settings route activation, Sidebar collapse, theme switch, profile menu inventory and opening through `ProfileMenuButton` / `ProfileMenuPanel`, real rendered WPF focusable actions, keyboard traversal, and focus traversal entries.
- AC-035: rendered Settings/About access is covered by `ShellSnapshotTests.Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation` plus the existing `SettingsAndAboutViewModelTests` / `AboutInfoProviderTests`; About remains app version, build number/date, host edition/channel, redacted device code, plugin empty-state boundary, support contact, legal line, neutral Owner identity, and no license import/status.
- AC-040: `ShellKeyboardSmokeTests` records focus traversal entries for shell controls, checks rendered WPF focusable actions and accessible button labels, and keeps route/control state covered through the keyboard-smoke surface; `ShellUiStaticScanTests.Theme_contrast_tokens_meet_minimum_accessibility_ratio` covers minimum contrast for light/dark shell tokens.
- AC-048: `AzerbaijaniDiacriticsRoundTripTests`, `ShellLocalizationCoverageTests`, and the existing encoding tests cover Azerbaijani diacritics across UI-facing localization, HostConfiguration storage, and audit-supported payloads.
- AC-033 contribution: `ShellDiagnosticsExportActionTests` and existing `DiagnosticsExportBoundaryTests` cover local diagnostics export, redaction/exclusion, `Uploaded=false`, no vendor upload, and `diagnostics.export.created` audit.

Verification summary:

- Pre-implementation baseline on 2026-05-27: `dotnet test Dobby.sln -c Release` completed with 497 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 RED verification on 2026-05-27: `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj -c Release --no-restore` failed on missing Sprint 7 shell/layout/localization/composition types as expected before implementation.
- Sprint 7 unit verification on 2026-05-27: `dotnet test tests/Dobby.UnitTests/Dobby.UnitTests.csproj -c Release --no-restore` completed with 344 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 integration verification before evidence append on 2026-05-27: `dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName!~Sprint7ReleaseEvidenceTests"` completed with 170 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 smoke verification on 2026-05-27: `dotnet test tests/Dobby.SmokeTests/Dobby.SmokeTests.csproj -c Release --no-restore` completed with 5 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 package-governance verification on 2026-05-27: `dotnet test tests/Dobby.IntegrationTests/Dobby.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~PackageGovernanceTests"` completed with 12 total tests, 0 failures, and 0 skipped tests. This includes `dobby.sprint7.toolchainPins.v1` Markdown/JSON parity and unchanged `xunit.runner.visualstudio` / `coverlet.collector` private test-infrastructure scope.
- Final Sprint 7 verification on 2026-05-27: `dotnet restore Dobby.sln -v minimal` completed with exit code 0.
- Final Sprint 7 verification on 2026-05-27: `dotnet build Dobby.sln -c Release --no-restore -warnaserror -v minimal` completed with 0 warnings and 0 errors.
- Final Sprint 7 verification on 2026-05-27: `dotnet test Dobby.sln -c Release --no-build --logger "console;verbosity=minimal"` completed with 529 total tests, 0 failures, and 0 skipped tests.
- Final Sprint 7 verification on 2026-05-27: `dotnet format Dobby.sln --verify-no-changes --no-restore --verbosity minimal` completed with exit code 0.
- Sprint 7 remediation verification on 2026-05-28: `dotnet build .\Dobby.sln -c Release --no-restore -warnaserror -v minimal` completed with 0 warnings and 0 errors.
- Sprint 7 remediation verification on 2026-05-28: `dotnet test .\Dobby.sln -c Release --no-build --logger "console;verbosity=minimal"` completed with 542 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 remediation verification on 2026-05-28: `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal` completed with exit code 0.
- Sprint 7 agreed-gap closure verification on 2026-05-28: `dotnet build .\Dobby.sln --configuration Release --no-restore -warnaserror -v minimal` completed with 0 warnings and 0 errors.
- Sprint 7 agreed-gap closure verification on 2026-05-28: `dotnet test .\Dobby.sln --configuration Release --no-build --logger "console;verbosity=minimal"` completed with 553 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 agreed-gap closure verification on 2026-05-28: `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal` completed with exit code 0.
- Sprint 7 execution-gap closure verification on 2026-05-28: `dotnet build .\Dobby.sln --configuration Release --no-restore -warnaserror -v minimal` completed with 0 warnings and 0 errors.
- Sprint 7 execution-gap closure verification on 2026-05-28: `dotnet test .\Dobby.sln --configuration Release --no-restore --no-build --logger "console;verbosity=minimal"` completed with 558 total tests, 0 failures, and 0 skipped tests.
- Sprint 7 execution-gap closure verification on 2026-05-28: `dotnet format .\Dobby.sln --verify-no-changes --no-restore --verbosity minimal` completed with exit code 0.

Shell snapshot/manual evidence rows:

- 1024x680 light descriptor evidence: `ShellSnapshotTests.Shell_snapshot_descriptor_covers_required_surfaces_and_nested_navigation_without_plugin_activation(1024, 680, Light)` covers viewport marker, theme marker, `TopToolbar`, `Sidebar`, `ThirdBar`, and nested `sample.analytics.daily.filters`.
- 1920x1080 dark descriptor evidence: `ShellSnapshotTests.Shell_snapshot_descriptor_covers_required_surfaces_and_nested_navigation_without_plugin_activation(1920, 1080, Dark)` covers viewport marker, theme marker, `TopToolbar`, `Sidebar`, `ThirdBar`, and nested `sample.analytics.daily.filters`.
- 1024x680 light rendered WPF evidence: `ShellSnapshotTests.Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation(1024, 680, Light)` renders `Dobby.Host.MainWindow` offscreen, verifies visible `TopToolbar`, `Sidebar`, `ThirdBar`, `MainContent`, `BottomStatus`, and `MinimizedWorkStrip`, verifies route-button tags for Home/Plugins/Settings plus `plugin.sample.analytics`, verifies Settings/About text, and asserts the rendered bitmap is nonblank through sampled color variance.
- 1920x1080 dark rendered WPF evidence: `ShellSnapshotTests.Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation(1920, 1080, Dark)` renders the same WPF shell path with the dark theme resource dictionary and the same route/surface/Settings/About/nonblank assertions.
- 1366x768 automated pass: `ShellSnapshotTests.Shell_rendered_window_covers_resolution_and_dpi_matrix(1366, 768, Light, 96, 96)` verifies required surfaces, rendered notification/feedback presentation, and nonblank sampled color variance.
- 1920x1080 125% DPI automated pass: `ShellSnapshotTests.Shell_rendered_window_covers_resolution_and_dpi_matrix(1920, 1080, Dark, 120, 120)` verifies required surfaces, rendered notification/feedback presentation, and nonblank sampled color variance.
- 2560x1440 automated pass: `ShellSnapshotTests.Shell_rendered_window_covers_resolution_and_dpi_matrix(2560, 1440, Light, 96, 96)` verifies required surfaces, rendered notification/feedback presentation, and nonblank sampled color variance.
- 3840x2160 automated pass: `ShellSnapshotTests.Shell_rendered_window_covers_resolution_and_dpi_matrix(3840, 2160, Dark, 96, 96)` verifies required surfaces, rendered notification/feedback presentation, and nonblank sampled color variance.
- 150% DPI automated pass: `ShellSnapshotTests.Shell_rendered_window_covers_resolution_and_dpi_matrix(1920, 1080, Light, 144, 144)` verifies required surfaces, rendered notification/feedback presentation, and nonblank sampled color variance.
- Keyboard/accessibility smoke evidence: `ShellKeyboardSmokeTests.Rendered_shell_supports_keyboard_traversal_focusable_actions_and_accessible_labels` checks rendered WPF `NotificationPresentationList` and `FeedbackStatePresentationList`, opens `ProfileMenuButton` into `ProfileMenuPanel`, verifies visible profile actions, checks rendered WPF focusable shell buttons, real keyboard traversal through `MoveFocus`, and accessible button labels for Logout and Manual Lock. `ShellKeyboardSmokeTests.Shell_view_model_supports_keyboard_route_theme_sidebar_and_profile_inventory_smoke` records focus traversal entries for `TopToolbar.Home` and `ProfileMenu.Logout`, route activation for nested sample navigation plus Settings, Sidebar collapse, and theme switch. Profile menu inventory is `profile-account-info`, `change-password`, `manual-lock`, `logout`, and `clear-saved-login`.
- Notification taxonomy evidence: `ShellLayoutStateTests` records `Toast`, `Banner`, and `Modal`; `ShellLayoutStateTests.View_model_localizes_notification_and_feedback_presentations`, `ShellSnapshotTests`, and `ShellKeyboardSmokeTests` verify Toast/Banner/Modal rendered presentation through localized labels.
- Feedback-state evidence: `ShellLayoutStateTests` records `Loading`, `Empty`, `Success`, `Error`, and `Confirmation`; `ShellLayoutStateTests.View_model_localizes_notification_and_feedback_presentations`, `ShellSnapshotTests`, and `ShellKeyboardSmokeTests` verify Loading/Empty/Success/Error/Confirmation rendered presentation through localized labels.
- Rendered Settings/About evidence: `ShellSnapshotTests.Shell_rendered_window_covers_required_surfaces_settings_about_and_nested_navigation_without_plugin_activation` renders Settings categories and the About field set through the WPF shell; source tests preserve the Sprint 6 About field policy and no license import/status boundary. Sprint 11 remains owner of consolidated release screenshots and installer smoke.

Architecture and boundary evidence:

- `HD-S7-001` / `Q1` is closed by `toolchain-pin-gate.md` under `dobby.sprint7.toolchainPins.v1`; no `Directory.Packages.props` drift, new package source, or native asset was introduced for Sprint 7.
- No Dobby.Shell -> Dobby.Configuration reference. `Dobby.Shell` remains configuration-neutral; Host coordinates persisted settings and theme resources.
- `Dobby.Shell -> Dobby.Messaging` is present only for the Q8-approved `WpfUiDispatcher` implementation of the existing UI dispatcher abstraction; Host production status wiring uses `ShellBackgroundOperationCoordinator` plus `WpfUiDispatcher`.
- `ShellThemeCoordinator.cs` and `ShellDiagnosticsExportCoordinator.cs` remain in the flat `Dobby.Host` namespace; no `Dobby.Host.Shell` or `Dobby.Host.Diagnostics` namespace-placement drift was introduced.
- No schema widening was added; Sprint 7 shell preferences use existing `HostConfiguration` key/value rows.
- No new Core ErrorCode.
- No new audit event name.
- No Contracts/SDK WPF widening or plugin-facing shell API widening.
- No runtime language switching.
- No plugin activation/package validation.
- No license import/enforcement behavior.
- No work-session restart restore.
- No telemetry/vendor upload.
- No Admin tools, sample plugin, installer, or release-gate consolidation work was pulled into Sprint 7.

Decision and assumption closure:

- D-20 and D-21 remain consumed predecessor decisions for User Management target identity and Settings/About source ownership.
- Q8 dispatcher placement is closed by `src/Dobby.Shell/UiDispatch/WpfUiDispatcher.cs`.
- Q9 Host namespace placement is closed by flat `Dobby.Host` coordinator files.
- Implementation assumptions A1-A7 from `SPRINT_7_EXECUTION_PLAN.md` are closed without human-decision escalation.

## Sprint 8 Implementation Evidence

Date: 2026-06-10 (Asia/Baku)

Status: IMPLEMENTED AND VERIFIED.

- SPRINT8-PACKAGE-GOVERNANCE: plugin package reader, validator, fingerprint, installer rollback, runtime activation scheduling, and trust-package coverage are in place; source installs are gated to local `.dobbyplugin` files while installed runtime directories remain an internal activation/recovery input.
- SPRINT8-SOURCE-PACKAGE-GATE: `HostPluginPackageInstallCoordinator` rejects directory and non-`.dobbyplugin` source paths before staging, records `unsupported-package-source`, and continues to validate trusted `.dobbyplugin` archives before install.
- SPRINT8-TRUST-RESOURCE-PACKAGING: bootstrap P-256 public trust anchors were generated with explicit owner approval and committed only at the canonical public PEM paths. Production PEM SHA-256 is `00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f`; dev/QA PEM SHA-256 is `cb0d35dafea42031e18c778ff208d7b142b280d73ba68983d5f3afccb6a08afc`. Private counterparts remain outside the workspace under the owner-only `C:\Users\Evaks\.dobby-signing-keys\sprint8-bootstrap` directory and must be rotated before production release. `PluginTrustResourcePackagingTests` passed, and explicit Production/DevQa builds proved each `Dobby.Security` assembly embeds only its selected logical resource.
- SPRINT8-ACTIVATION-REVALIDATION: `HostPluginRuntimeCoordinator` re-runs package validation and trust verification against installed runtime packages before compatibility checks, operation-target seeding, or assembly loading; activation rejects trust failures, stale package fingerprints with `package-fingerprint-mismatch`, and plugin id mismatches.
- SPRINT8-COMPATIBILITY-DEPENDENCY: manifest validation now rejects prerelease versions, malformed package-relative paths, and self-dependencies before activation work; `PluginOperationTargetContributionBuilder` emits Security-ready operation-target contributions only after manifest validation succeeds.
- SPRINT8-SESSION-CANCELLATION: host post-login runtime cancels in-flight plugin activation on lock, logout, expiry, invalidation, and disposal, and the host coordinator resets activation state after post-login migration recovery.
- SPRINT8-ALC-ISOLATION: each installed plugin is activated through a non-collectible `PluginLoadContext` with host-pinned shared assemblies and plugin-private dependency resolution from the runtime path.
- SPRINT8-EVENT-BUS-HOSTING: plugin activation now receives host-owned `PluginHostServices`; the production factory wires `InMemoryEventBus` into plugin service construction and `IPluginActivationContext` without exposing host root DI.
- SPRINT8-PAGE-RENDER-HOSTING: `PluginPageRenderRegistry.CreateDefault()` allowlists `PluginTextPageModel` into host-rendered WPF content, rejects unknown plugin models, and `MainWindow` uses that approved default registry for the `PluginPageHost` surface.
- SPRINT8-PLUGIN-PROGRESS-HOSTING: plugin extension points can receive `IBackgroundOperationReporter` and `IBackgroundOperationCancellation` as official host contract services; the sample plugin reports plugin-owned progress through the host reporter while shell progress remains the only UI sink.
- SPRINT8-BACKGROUND-OPERATION: the host activation path publishes official background-operation snapshots through `BackgroundOperationReporter` and `ShellBackgroundOperationCoordinator`; shell progress remains the only UI sink.
- SPRINT8-ACTIVATION-SCHEDULING: successful login now schedules post-login plugin activation after the shell window is rendered, using the authenticated runtime and activation cancellation token.
- SPRINT8-BOUNDARY-VERIFICATION: `dotnet build .\Dobby.sln -c Release --no-restore -warnaserror -v minimal` passed on 2026-06-10 with 0 warnings and 0 errors; `dotnet test .\Dobby.sln -c Release --no-restore --no-build --logger "console;verbosity=minimal"` passed on 2026-06-10 with 0 failures: Unit 447, Integration 197, Smoke 6.
