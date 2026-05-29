using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authorization;
using Microsoft.Data.Sqlite;

namespace Dobby.Security.Authentication;

public sealed class ManagedUserService(
    IHostPersistenceCommandExecutor executor,
    CredentialStore credentialStore,
    PermissionGrantStore permissionGrants,
    UserManagementAuthorizationGuard guard,
    TimeProvider timeProvider,
    IManagedUserAuditSink? auditSink = null)
{
    private readonly IManagedUserAuditSink audit = auditSink ?? NoOpManagedUserAuditSink.Instance;

    public Result<IReadOnlyList<ManagedUserSummary>> ListUsers(string actorUserId, CorrelationId correlationId)
    {
        var allowed = guard.EnsureCanViewUsers(actorUserId, correlationId);
        if (!allowed.IsSuccess)
        {
            audit.AppendPermissionDenied(actorUserId, UserManagementOperation(OperationVerb.View), correlationId);
            return Result.Failure<IReadOnlyList<ManagedUserSummary>>(allowed.ErrorCode!.Value);
        }

        var rows = executor.Query<UserRow>(
            "SELECT UserId, UsernameNormalized, DisplayName, IsOwner, IsActive FROM Users ORDER BY IsOwner DESC, UsernameNormalized;");
        return Result.Success<IReadOnlyList<ManagedUserSummary>>(rows.Select(ToSummary).ToArray());
    }

    public Result<ManagedUserDetail> GetUser(string actorUserId, string targetUserId, CorrelationId correlationId)
    {
        var allowed = guard.EnsureCanViewUsers(actorUserId, correlationId);
        if (!allowed.IsSuccess)
        {
            audit.AppendPermissionDenied(actorUserId, UserManagementOperation(OperationVerb.View), correlationId);
            return Result.Failure<ManagedUserDetail>(allowed.ErrorCode!.Value);
        }

        var row = LoadUser(targetUserId);
        if (row is null)
        {
            return Result.Failure<ManagedUserDetail>(ErrorCode.NotFound);
        }

        var grants = permissionGrants.ListGrants(targetUserId);
        if (!grants.IsSuccess)
        {
            return Result.Failure<ManagedUserDetail>(grants.ErrorCode!.Value);
        }

        return Result.Success(new ManagedUserDetail(
            row.UserId,
            row.UsernameNormalized,
            row.DisplayName,
            row.IsOwner == 1,
            row.IsActive == 1,
            grants.Value.Select(key => key.ToString()).ToArray()));
    }

    public Result<string> CreateManagedUser(
        string actorUserId,
        string username,
        string? displayName,
        string password,
        CorrelationId correlationId)
    {
        var allowed = guard.EnsureCanCreateUser(actorUserId, correlationId);
        if (!allowed.IsSuccess)
        {
            audit.AppendPermissionDenied(actorUserId, UserManagementOperation(OperationVerb.Create), correlationId);
            return Result.Failure<string>(allowed.ErrorCode!.Value);
        }

        var normalizedUsername = UsernameCanonicalValidator.Normalize(username);
        if (!normalizedUsername.IsSuccess)
        {
            return Result.Failure<string>(normalizedUsername.ErrorCode!.Value);
        }

        var passwordValidation = PasswordPolicyValidator.Validate(password);
        if (!passwordValidation.IsSuccess)
        {
            return Result.Failure<string>(passwordValidation.ErrorCode!.Value);
        }

        var userId = Guid.CreateVersion7().ToString("D");
        var timestamp = timeProvider.GetUtcNow().ToString("O");
        try
        {
            executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, @UsernameNormalized, @DisplayName, 0, 1, @CreatedAtUtc, @UpdatedAtUtc);",
                new
                {
                    UserId = userId,
                    UsernameNormalized = normalizedUsername.Value,
                    DisplayName = DisplayNameValidator.Normalize(displayName),
                    CreatedAtUtc = timestamp,
                    UpdatedAtUtc = timestamp
                });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Result.Failure<string>(ErrorCode.AlreadyExists);
        }

        var credential = credentialStore.CreateCredential(userId, password, timeProvider.GetUtcNow());
        if (!credential.IsSuccess)
        {
            return Result.Failure<string>(credential.ErrorCode!.Value);
        }

        audit.AppendUserMutation(actorUserId, userId, ManagedUserAuditAction.Create, succeeded: true, correlationId);
        return Result.Success(userId);
    }

    public Result UpdateManagedUser(
        string actorUserId,
        string targetUserId,
        string? displayName,
        CorrelationId correlationId)
    {
        var allowed = guard.EnsureCanManageUser(actorUserId, targetUserId, OperationVerb.Update, correlationId);
        if (!allowed.IsSuccess)
        {
            AuditDeniedForTarget(actorUserId, targetUserId, OperationVerb.Update, correlationId);
            return allowed;
        }

        var affected = executor.Execute(
            "UPDATE Users SET DisplayName = @DisplayName, UpdatedAtUtc = @UpdatedAtUtc WHERE UserId = @UserId AND IsOwner = 0;",
            new
            {
                UserId = targetUserId,
                DisplayName = DisplayNameValidator.Normalize(displayName),
                UpdatedAtUtc = timeProvider.GetUtcNow().ToString("O")
            });
        if (affected == 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        audit.AppendUserMutation(actorUserId, targetUserId, ManagedUserAuditAction.Update, succeeded: true, correlationId);
        return Result.Success();
    }

    public Result DeactivateUser(string actorUserId, string targetUserId, CorrelationId correlationId)
    {
        return SetActiveState(actorUserId, targetUserId, isActive: false, "deactivated", ManagedUserAuditAction.Deactivate, correlationId);
    }

    public Result ReactivateUser(string actorUserId, string targetUserId, CorrelationId correlationId)
    {
        return SetActiveState(actorUserId, targetUserId, isActive: true, "reactivated", ManagedUserAuditAction.Activate, correlationId);
    }

    public Result ResetPassword(
        string actorUserId,
        string targetUserId,
        string newPassword,
        CorrelationId correlationId)
    {
        var allowed = guard.EnsureCanManageUser(actorUserId, targetUserId, OperationVerb.Execute, correlationId);
        if (!allowed.IsSuccess)
        {
            AuditDeniedForTarget(actorUserId, targetUserId, OperationVerb.Execute, correlationId);
            return allowed;
        }

        var reset = credentialStore.RebindPassword(targetUserId, newPassword, timeProvider.GetUtcNow());
        if (!reset.IsSuccess)
        {
            return reset;
        }

        InvalidateSessions(targetUserId, "password-reset");
        audit.AppendUserMutation(actorUserId, targetUserId, ManagedUserAuditAction.PasswordReset, succeeded: true, correlationId);
        return Result.Success();
    }

    public Result GrantPermission(
        string actorUserId,
        string targetUserId,
        string operationKey,
        CorrelationId correlationId)
    {
        var parsed = OperationKey.Parse(operationKey);
        if (!parsed.IsSuccess)
        {
            audit.AppendPermissionDenied(actorUserId, operationKey, correlationId);
            return Result.Failure(parsed.ErrorCode!.Value);
        }

        var allowed = guard.EnsureCanGrant(actorUserId, targetUserId, parsed.Value, correlationId);
        if (!allowed.IsSuccess)
        {
            AuditDeniedForPermission(actorUserId, targetUserId, parsed.Value.ToString(), correlationId);
            return allowed;
        }

        var grant = permissionGrants.Grant(targetUserId, parsed.Value);
        if (grant.IsSuccess)
        {
            audit.AppendPermissionMutation(actorUserId, targetUserId, parsed.Value.ToString(), grant: true, succeeded: true, correlationId);
        }

        return grant;
    }

    public Result RevokePermission(
        string actorUserId,
        string targetUserId,
        string operationKey,
        CorrelationId correlationId)
    {
        var parsed = OperationKey.Parse(operationKey);
        if (!parsed.IsSuccess)
        {
            audit.AppendPermissionDenied(actorUserId, operationKey, correlationId);
            return Result.Failure(parsed.ErrorCode!.Value);
        }

        var allowed = guard.EnsureCanRevoke(actorUserId, targetUserId, parsed.Value, correlationId);
        if (!allowed.IsSuccess)
        {
            AuditDeniedForPermission(actorUserId, targetUserId, parsed.Value.ToString(), correlationId);
            return allowed;
        }

        var revoke = permissionGrants.Revoke(targetUserId, parsed.Value);
        if (revoke.IsSuccess)
        {
            audit.AppendPermissionMutation(actorUserId, targetUserId, parsed.Value.ToString(), grant: false, succeeded: true, correlationId);
        }

        return revoke;
    }

    private Result SetActiveState(
        string actorUserId,
        string targetUserId,
        bool isActive,
        string reason,
        ManagedUserAuditAction auditAction,
        CorrelationId correlationId)
    {
        var allowed = guard.EnsureCanManageUser(actorUserId, targetUserId, OperationVerb.Delete, correlationId);
        if (!allowed.IsSuccess)
        {
            AuditDeniedForTarget(actorUserId, targetUserId, OperationVerb.Delete, correlationId);
            return allowed;
        }

        var affected = executor.Execute(
            "UPDATE Users SET IsActive = @IsActive, UpdatedAtUtc = @UpdatedAtUtc WHERE UserId = @UserId AND IsOwner = 0;",
            new
            {
                UserId = targetUserId,
                IsActive = isActive ? 1 : 0,
                UpdatedAtUtc = timeProvider.GetUtcNow().ToString("O")
            });
        if (affected == 0)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        if (!isActive)
        {
            InvalidateSessions(targetUserId, reason);
        }

        audit.AppendUserMutation(actorUserId, targetUserId, auditAction, succeeded: true, correlationId);
        return Result.Success();
    }

    private void AuditDeniedForTarget(string actorUserId, string targetUserId, OperationVerb verb, CorrelationId correlationId)
    {
        if (IsOwner(targetUserId))
        {
            audit.AppendOwnerProtectionDenied(actorUserId, targetUserId, correlationId);
            return;
        }

        audit.AppendPermissionDenied(actorUserId, UserManagementOperation(verb), correlationId);
    }

    private void AuditDeniedForPermission(string actorUserId, string targetUserId, string permissionKey, CorrelationId correlationId)
    {
        if (IsOwner(targetUserId))
        {
            audit.AppendOwnerProtectionDenied(actorUserId, targetUserId, correlationId);
            return;
        }

        audit.AppendPermissionDenied(actorUserId, permissionKey, correlationId);
    }

    private bool IsOwner(string userId)
    {
        return executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE UserId = @UserId AND IsOwner = 1;",
            new { UserId = userId }) == 1;
    }

    private static string UserManagementOperation(OperationVerb verb)
    {
        return new OperationKey(BuiltInOperationTargets.UserManagementTargetId, verb).ToString();
    }

    private void InvalidateSessions(string userId, string reason)
    {
        executor.Execute(
            "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = @Reason WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;",
            new
            {
                UserId = userId,
                Reason = reason,
                InvalidatedAtUtc = timeProvider.GetUtcNow().ToString("O")
            });
    }

    private UserRow? LoadUser(string userId)
    {
        return executor.Query<UserRow>(
            "SELECT UserId, UsernameNormalized, DisplayName, IsOwner, IsActive FROM Users WHERE UserId = @UserId;",
            new { UserId = userId }).SingleOrDefault();
    }

    private static ManagedUserSummary ToSummary(UserRow row)
    {
        return new ManagedUserSummary(
            row.UserId,
            row.UsernameNormalized,
            row.DisplayName,
            row.IsOwner == 1,
            row.IsActive == 1);
    }

    private sealed class UserRow
    {
        public string UserId { get; init; } = string.Empty;

        public string UsernameNormalized { get; init; } = string.Empty;

        public string? DisplayName { get; init; }

        public int IsOwner { get; init; }

        public int IsActive { get; init; }
    }
}
