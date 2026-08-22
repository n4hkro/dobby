using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class SelfServicePasswordChangeCoordinator(
    IHostPersistenceCommandExecutor executor,
    CredentialStore credentialStore,
    RememberMeSlotService rememberMeSlotService,
    LoginThrottleService reauthenticationThrottle,
    IHostMutationUnitOfWork mutationUnitOfWork,
    IAuditTrail auditTrail,
    TimeProvider timeProvider)
{
    public Result ChangePassword(
        string userId,
        string sessionId,
        string currentPassword,
        string newPassword,
        string newPasswordConfirmation,
        bool rememberMe,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!StringComparer.Ordinal.Equals(newPassword, newPasswordConfirmation))
        {
            return Result.Failure(ErrorCode.ValidationFailed);
        }

        try
        {
            var now = timeProvider.GetUtcNow();
            if (!HasActiveSession(executor, userId, sessionId, now))
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            var throttle = reauthenticationThrottle.Check(userId);
            if (!throttle.IsAvailable)
            {
                return Result.Failure(ErrorCode.ResourceBusy);
            }

            if (throttle.IsThrottled)
            {
                return Result.Failure(ErrorCode.Unauthorized);
            }

            var verified = credentialStore.VerifyPassword(userId, currentPassword);
            if (!verified.IsSuccess)
            {
                return Result.Failure(verified.ErrorCode ?? ErrorCode.ResourceBusy);
            }

            if (!verified.Value)
            {
                var failed = reauthenticationThrottle.RecordFailure(userId);
                AppendVerificationFailure(userId, correlationId, now);
                return Result.Failure(failed.IsAvailable ? ErrorCode.Unauthorized : ErrorCode.ResourceBusy);
            }

            var prepared = credentialStore.PreparePasswordRebind(userId, newPassword, now);
            if (!prepared.IsSuccess)
            {
                return Result.Failure(prepared.ErrorCode!.Value);
            }

            var result = mutationUnitOfWork.ExecuteResult(context =>
                ApplyPasswordChange(
                    context,
                    prepared.Value,
                    userId,
                    sessionId,
                    rememberMe,
                    correlationId,
                    now));
            if (result.IsSuccess)
            {
                _ = reauthenticationThrottle.RecordSuccess(userId);
            }

            return result;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    private Result ApplyPasswordChange(
        IHostMutationContext context,
        PreparedCredentialMutation prepared,
        string userId,
        string sessionId,
        bool rememberMe,
        CorrelationId correlationId,
        DateTimeOffset now)
    {
        if (!HasActiveSession(context.Commands, userId, sessionId, now))
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var username = context.Commands.Query<string>(
            "SELECT UsernameNormalized FROM Users WHERE UserId = @UserId AND IsActive = 1;",
            new { UserId = userId }).SingleOrDefault();
        if (string.IsNullOrWhiteSpace(username))
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var rebound = credentialStore.ApplyPreparedCredential(context.Commands, prepared);
        if (!rebound.IsSuccess)
        {
            return rebound;
        }

        context.Commands.Execute(
            "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = 'password-change' WHERE UserId = @UserId AND SessionId <> @SessionId AND InvalidatedAtUtc IS NULL;",
            new
            {
                UserId = userId,
                SessionId = sessionId,
                InvalidatedAtUtc = now.ToUniversalTime().ToString("O")
            });

        var affected = context.Commands.Execute(
            "UPDATE Sessions SET SessionVersion = SessionVersion + 1, LastActivityAtUtc = @UpdatedAtUtc WHERE SessionId = @SessionId AND UserId = @UserId AND InvalidatedAtUtc IS NULL;",
            new
            {
                SessionId = sessionId,
                UserId = userId,
                UpdatedAtUtc = now.ToUniversalTime().ToString("O")
            });
        if (affected != 1)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var remember = rememberMeSlotService.ApplySuccessfulLogin(
            context.Commands,
            userId,
            username,
            rememberMe);
        if (!remember.IsSuccess)
        {
            return remember;
        }

        context.AppendAudit(new AuditEvent(
            AuditEventName.UserPasswordChange,
            userId,
            userId,
            AuditResult.Success,
            correlationId,
            now,
            new Dictionary<string, string>
            {
                ["state"] = "current-preserved-others-revoked"
            }));
        context.EnqueueOutbox(new HostOutboxMessage(
            $"credential-change:{correlationId}",
            "security.session",
            "credential.changed",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["currentSessionId"] = sessionId,
                ["sessionPolicy"] = "preserve-current-revoke-others",
                ["userId"] = userId
            },
            correlationId.ToString()));

        return Result.Success();
    }

    private static bool HasActiveSession(
        IHostPersistenceCommandExecutor commands,
        string userId,
        string sessionId,
        DateTimeOffset now)
    {
        return commands.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE SessionId = @SessionId AND UserId = @UserId AND InvalidatedAtUtc IS NULL AND ExpiresAtUtc > @Now;",
            new
            {
                SessionId = sessionId,
                UserId = userId,
                Now = now.ToUniversalTime().ToString("O")
            }) == 1;
    }

    private void AppendVerificationFailure(
        string userId,
        CorrelationId correlationId,
        DateTimeOffset occurredAt)
    {
        auditTrail.Append(new AuditEvent(
            AuditEventName.SecurityVerificationFailed,
            userId,
            "self-service-password-change",
            AuditResult.Failed,
            correlationId,
            occurredAt,
            new Dictionary<string, string> { ["reason"] = "reauthentication-failed" }));
    }
}
