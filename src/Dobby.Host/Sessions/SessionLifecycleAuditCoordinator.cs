using Dobby.Audit;
using Dobby.Core;
using Dobby.Security.Authentication;

namespace Dobby.Host.Sessions;

public sealed class SessionLifecycleAuditCoordinator(IAuditTrail auditTrail, TimeProvider timeProvider)
{
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
}
