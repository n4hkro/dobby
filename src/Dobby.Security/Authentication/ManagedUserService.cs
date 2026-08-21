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
    IManagedUserAuditSink auditSink)
{
    private readonly IManagedUserAuditSink audit = auditSink ?? throw new ArgumentNullException(nameof(auditSink));

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

        var normalizedDisplayName = DisplayNameValidator.Normalize(displayName);
        if (!normalizedDisplayName.IsSuccess)
        {
            return Result.Failure<string>(normalizedDisplayName.ErrorCode!.Value);
        }

        var userId = Guid.CreateVersion7().ToString("D");
        var now = timeProvider.GetUtcNow();
        var timestamp = now.ToString("O");
        var preparedCredential = credentialStore.PrepareCreateCredential(userId, password, now);
        if (!preparedCredential.IsSuccess)
        {
            return Result.Failure<string>(preparedCredential.ErrorCode!.Value);
        }

        try
        {
            return audit.ExecuteUserMutation(
                actorUserId,
                userId,
                ManagedUserAuditAction.Create,
                correlationId,
                commands =>
                {
                    commands.Execute(
                        "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES (@UserId, @UsernameNormalized, @DisplayName, 0, 1, @CreatedAtUtc, @UpdatedAtUtc);",
                        new
                        {
                            UserId = userId,
                            UsernameNormalized = normalizedUsername.Value,
                            DisplayName = normalizedDisplayName.Value,
                            CreatedAtUtc = timestamp,
                            UpdatedAtUtc = timestamp
                        });
                    var credential = credentialStore.ApplyPreparedCredential(commands, preparedCredential.Value);
                    return credential.IsSuccess
                        ? Result.Success(userId)
                        : Result.Failure<string>(credential.ErrorCode!.Value);
                });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Result.Failure<string>(ErrorCode.AlreadyExists);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure<string>(ErrorCode.DatabaseUnavailable);
        }
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

        var normalizedDisplayName = DisplayNameValidator.Normalize(displayName);
        if (!normalizedDisplayName.IsSuccess)
        {
            return Result.Failure(normalizedDisplayName.ErrorCode!.Value);
        }

        try
        {
            return audit.ExecuteUserMutation(
                actorUserId,
                targetUserId,
                ManagedUserAuditAction.Update,
                correlationId,
                commands =>
                {
                    var affected = commands.Execute(
                        "UPDATE Users SET DisplayName = @DisplayName, UpdatedAtUtc = @UpdatedAtUtc WHERE UserId = @UserId AND IsOwner = 0;",
                        new
                        {
                            UserId = targetUserId,
                            DisplayName = normalizedDisplayName.Value,
                            UpdatedAtUtc = timeProvider.GetUtcNow().ToString("O")
                        });
                    return affected == 0 ? Result.Failure(ErrorCode.NotFound) : Result.Success();
                });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
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

        var now = timeProvider.GetUtcNow();
        var preparedCredential = credentialStore.PreparePasswordRebind(targetUserId, newPassword, now);
        if (!preparedCredential.IsSuccess)
        {
            return Result.Failure(preparedCredential.ErrorCode!.Value);
        }

        try
        {
            return audit.ExecuteUserMutation(
                actorUserId,
                targetUserId,
                ManagedUserAuditAction.PasswordReset,
                correlationId,
                commands =>
                {
                    var reset = credentialStore.ApplyPreparedCredential(commands, preparedCredential.Value);
                    if (!reset.IsSuccess)
                    {
                        return reset;
                    }

                    InvalidateSessions(commands, targetUserId, "password-reset", now);
                    return Result.Success();
                });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
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

        try
        {
            return audit.ExecutePermissionMutation(
                actorUserId,
                targetUserId,
                parsed.Value.ToString(),
                grant: true,
                correlationId,
                commands => permissionGrants.Grant(commands, targetUserId, parsed.Value));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
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

        try
        {
            return audit.ExecutePermissionMutation(
                actorUserId,
                targetUserId,
                parsed.Value.ToString(),
                grant: false,
                correlationId,
                commands => permissionGrants.Revoke(commands, targetUserId, parsed.Value));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
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

        try
        {
            var now = timeProvider.GetUtcNow();
            return audit.ExecuteUserMutation(
                actorUserId,
                targetUserId,
                auditAction,
                correlationId,
                commands =>
                {
                    var affected = commands.Execute(
                        "UPDATE Users SET IsActive = @IsActive, UpdatedAtUtc = @UpdatedAtUtc WHERE UserId = @UserId AND IsOwner = 0;",
                        new
                        {
                            UserId = targetUserId,
                            IsActive = isActive ? 1 : 0,
                            UpdatedAtUtc = now.ToString("O")
                        });
                    if (affected == 0)
                    {
                        return Result.Failure(ErrorCode.NotFound);
                    }

                    if (!isActive)
                    {
                        InvalidateSessions(commands, targetUserId, reason, now);
                    }

                    return Result.Success();
                });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
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

    private static void InvalidateSessions(
        IHostPersistenceCommandExecutor commands,
        string userId,
        string reason,
        DateTimeOffset invalidatedAtUtc)
    {
        commands.Execute(
            "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = @Reason WHERE UserId = @UserId AND InvalidatedAtUtc IS NULL;",
            new
            {
                UserId = userId,
                Reason = reason,
                InvalidatedAtUtc = invalidatedAtUtc.ToString("O")
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
