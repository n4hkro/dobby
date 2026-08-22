using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Sessions;

public sealed class SessionLifecycleAuditCoordinator(
    IAuditTrail auditTrail,
    TimeProvider timeProvider,
    IHostMutationUnitOfWork? mutationUnitOfWork = null) : ISessionMutationCoordinator
{
    public Result Invalidate(
        string userId,
        string sessionId,
        string reason,
        SessionMutationAuditKind auditKind,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (mutationUnitOfWork is null)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }

        try
        {
            return mutationUnitOfWork.ExecuteResult(context =>
            {
                var now = timeProvider.GetUtcNow();
                var affected = context.Commands.Execute(
                    "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = @InvalidationReason WHERE SessionId = @SessionId AND UserId = @UserId AND InvalidatedAtUtc IS NULL;",
                    new
                    {
                        SessionId = sessionId,
                        UserId = userId,
                        InvalidatedAtUtc = now.ToUniversalTime().ToString("O"),
                        InvalidationReason = reason
                    });
                if (affected != 1)
                {
                    return Result.Failure(ErrorCode.NotFound);
                }

                context.AppendAudit(Create(
                    ToEventName(auditKind),
                    userId,
                    AuditResult.Success,
                    correlationId,
                    auditKind == SessionMutationAuditKind.Logout
                        ? null
                        : new Dictionary<string, string> { ["reason"] = reason }));
                context.EnqueueOutbox(new HostOutboxMessage(
                    $"session-mutation:{correlationId}",
                    "security.session",
                    ToMessageType(auditKind),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["reason"] = reason,
                        ["sessionId"] = sessionId,
                        ["userId"] = userId
                    },
                    correlationId.ToString()));
                return Result.Success();
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    public void AppendCheckpoint(string userId, SessionCheckpoint checkpoint, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (checkpoint.Kind == SessionCheckpointKind.Locked && checkpoint.JustTransitioned)
        {
            auditTrail.Append(Create(AuditEventName.SessionLock, userId, AuditResult.Success, correlationId));
            return;
        }

        if (checkpoint.Kind == SessionCheckpointKind.Expired && checkpoint.Reason is "idle" or "absolute")
        {
            auditTrail.Append(Create(
                AuditEventName.SessionExpired,
                userId,
                AuditResult.Success,
                correlationId,
                new Dictionary<string, string> { ["reason"] = checkpoint.Reason }));
            return;
        }

        if (checkpoint.Kind == SessionCheckpointKind.Invalidated)
        {
            auditTrail.Append(Create(
                AuditEventName.SessionInvalidated,
                userId,
                AuditResult.Success,
                correlationId,
                new Dictionary<string, string> { ["reason"] = checkpoint.Reason ?? "invalidated" }));
        }
    }

    public void AppendUnlock(string userId, Result result, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(result);

        auditTrail.Append(result.IsSuccess
            ? Create(AuditEventName.SessionUnlock, userId, AuditResult.Success, correlationId)
            : Create(
                AuditEventName.SessionUnlock,
                userId,
                AuditResult.Failed,
                correlationId,
                new Dictionary<string, string> { ["reason"] = ToUnlockFailureReason(result.ErrorCode) }));
    }

    public void AppendLogout(string userId, Result result, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            auditTrail.Append(Create(AuditEventName.UserLogout, userId, AuditResult.Success, correlationId));
        }
    }

    public void AppendLock(string userId, Result result, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            auditTrail.Append(Create(AuditEventName.SessionLock, userId, AuditResult.Success, correlationId));
        }
    }

    private AuditEvent Create(
        string eventName,
        string userId,
        AuditResult result,
        CorrelationId correlationId,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return new AuditEvent(
            eventName,
            userId,
            "host-session",
            result,
            correlationId,
            timeProvider.GetUtcNow(),
            details);
    }

    private static string ToUnlockFailureReason(ErrorCode? errorCode)
    {
        if (errorCode == ErrorCode.Unauthorized)
        {
            return "wrong-password";
        }

        if (errorCode == ErrorCode.NotFound)
        {
            return "no-session";
        }

        return "credential-unavailable";
    }

    private static string ToEventName(SessionMutationAuditKind auditKind)
    {
        return auditKind switch
        {
            SessionMutationAuditKind.Logout => AuditEventName.UserLogout,
            SessionMutationAuditKind.Expired => AuditEventName.SessionExpired,
            SessionMutationAuditKind.Invalidated => AuditEventName.SessionInvalidated,
            _ => throw new ArgumentOutOfRangeException(nameof(auditKind), auditKind, "Unsupported session mutation audit kind.")
        };
    }

    private static string ToMessageType(SessionMutationAuditKind auditKind)
    {
        return auditKind switch
        {
            SessionMutationAuditKind.Logout => "session.logged-out",
            SessionMutationAuditKind.Expired => "session.expired",
            SessionMutationAuditKind.Invalidated => "session.invalidated",
            _ => throw new ArgumentOutOfRangeException(nameof(auditKind), auditKind, "Unsupported session mutation outbox kind.")
        };
    }
}
