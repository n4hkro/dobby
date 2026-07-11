# Task 01 Execution Note: Authority Reconciliation And Red Baseline

## Current Status

Branch: `codex/remaining-product-work`

Baseline verification before task start:

```powershell
dotnet test .\Dobby.sln --no-restore
```

Result: Passed. Unit tests 664/664, smoke tests 20/20, integration tests 295/295.

`git status --short` before task start:

```text
 M src/Dobby.UI/ThemeTokens.cs
 M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
?? PROGRAM_DEVELOPMENT_WORK_ITEMS.md
?? PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md
?? docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md
```

The two modified tracked files currently have no visible `git diff` content. Treat them as pre-existing local status and do not edit them for Task 1.

## Dirty Files Relevant To Task

Will touch:

- `tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs`

Will not touch:

- `src/Dobby.UI/ThemeTokens.cs`
- `tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs`
- `PROGRAM_DEVELOPMENT_WORK_ITEMS.md`
- `PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md`
- `docs/superpowers/plans/2026-07-08-remaining-product-work-implementation.md`

## Post-Review Scope Adjustment

The first task review found that the new KDF guardrail could not prove the sprint-authority `Login.KdfUnsupported` path because `LoginAttemptKind` only exposed a generic `ValidationFailed` result. The scope was expanded after that red finding to the minimum auth surface needed to preserve the Sprint 5 authority:

Will touch after review:

- `src/Dobby.Security/Authentication/LoginAttemptResult.cs`
- `src/Dobby.Security/Authentication/LoginService.cs`
- `src/Dobby.Host/Login/HostLoginRuntimeFactory.cs`
- `src/Dobby.Host/Login/LoginAuditCoordinator.cs`
- `tests/Dobby.IntegrationTests/Auth/LoginServiceTests.cs`
- `tests/Dobby.IntegrationTests/Auth/UserManagementServiceTests.cs`
- `tests/Dobby.IntegrationTests/Host/LoginRuntimeWorkflowTests.cs`
- `tests/Dobby.UnitTests/Host/LoginAuditCoordinatorTests.cs`
- `tests/Dobby.IntegrationTests/Architecture/RemainingProductWorkAuthorityTests.cs`

The change remains prohibited from adding Argon2 implementation, packages, package pins, or stricter password/display-name behavior.

## Red Test Metadata

Red test classes and methods:

- `RemainingProductWorkAuthorityTests.Canonical_runtime_storage_auth_and_audit_authority_is_preserved`
- `LoginServiceTests.Unsupported_kdf_policy_fails_closed`
- `LoginServiceTests.Unauthorized_verifier_failure_returns_validation_failed`
- `LoginAuditCoordinatorTests.Failed_login_attempts_append_user_login_failed_with_safe_reason`

Expected initial failure:

- Before the guardrail test exists, the focused filter does not prove the remaining-product-work authority contract.
- After adding the guardrail and auth regressions, the red surface should fail only where current code violates the approved sprint/spec authority facts listed in Task 1 or misclassifies non-KDF `ErrorCode.Unauthorized` failures as `LoginAttemptKind.KdfUnsupported`.

Production seam:

- Existing validators/options/runtime layout/audit contracts only.
- No product behavior expansion beyond preserving Sprint 5's typed unsupported-KDF login result.
- No applied migration edits.

Focused no-restore verification:

```powershell
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~LoginServiceTests"
dotnet test .\tests\Dobby.IntegrationTests\Dobby.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~LoginServiceTests|FullyQualifiedName~RemainingProductWorkAuthorityTests|FullyQualifiedName~LoginRuntimeWorkflowTests|FullyQualifiedName~UserManagementServiceTests"
dotnet test .\tests\Dobby.UnitTests\Dobby.UnitTests.csproj --no-restore --filter "FullyQualifiedName~LoginAuditCoordinatorTests"
```

Green surface to keep after implementation:

- `LoginServiceTests.Unsupported_kdf_policy_fails_closed`
- `LoginServiceTests.Unauthorized_verifier_failure_returns_validation_failed`
- `LoginAuditCoordinatorTests`
- `RemainingProductWorkAuthorityTests`
