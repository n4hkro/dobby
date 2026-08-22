using Dobby.Configuration;
using Dobby.Core;

namespace Dobby.Security.Authentication;

public enum SessionCheckpointKind
{
    Active,
    Locked,
    Expired,
    Invalidated
}

public enum SessionMutationAuditKind
{
    Logout,
    Expired,
    Invalidated
}

public interface ISessionMutationCoordinator
{
    Result Invalidate(
        string userId,
        string sessionId,
        string reason,
        SessionMutationAuditKind auditKind,
        CorrelationId correlationId);
}

public sealed record SessionCheckpoint(
    SessionCheckpointKind Kind,
    string? Reason = null,
    bool JustTransitioned = false,
    ErrorCode? ErrorCode = null);

public sealed class SessionLifecycleService(
    ISessionStore store,
    ActiveSessionAccessor activeSessionAccessor,
    IUserCredentialVerifier credentialVerifier,
    TimeProvider timeProvider,
    SessionOptions sessionOptions,
    ISessionMutationCoordinator? mutationCoordinator = null)
{
    private readonly object sessionGate = new();

    public bool CommitsMutationAudit => mutationCoordinator is not null;

    public bool IsLocked { get; private set; }

    public Result<SessionRecord> Create(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        lock (sessionGate)
        {
            var prepared = PrepareSession(userId);
            if (!prepared.IsSuccess)
            {
                return prepared;
            }

            store.Create(prepared.Value);
            var active = activeSessionAccessor.SetActive(prepared.Value);
            return active.IsSuccess
                ? prepared
                : Result.Failure<SessionRecord>(active.ErrorCode!.Value);
        }
    }

    internal Result<SessionRecord> CreateDurable(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        lock (sessionGate)
        {
            var prepared = PrepareSession(userId);
            if (!prepared.IsSuccess)
            {
                return prepared;
            }

            store.Create(prepared.Value);
            return prepared;
        }
    }

    public Result ActivateExisting(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (sessionGate)
        {
            if (activeSessionAccessor.Current?.SessionId == session.SessionId)
            {
                return Result.Success();
            }

            return activeSessionAccessor.SetActive(session);
        }
    }

    public Result Lock()
    {
        lock (sessionGate)
        {
            if (activeSessionAccessor.Current is null)
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            IsLocked = true;
            return Result.Success();
        }
    }

    public Result Unlock(string password)
    {
        lock (sessionGate)
        {
            var current = activeSessionAccessor.Current;
            if (current is null)
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            var verified = credentialVerifier.VerifyPassword(current.UserId, password);
            if (!verified.IsSuccess)
            {
                return Result.Failure(verified.ErrorCode ?? ErrorCode.DatabaseUnavailable);
            }

            if (!verified.Value)
            {
                return Result.Failure(ErrorCode.Unauthorized);
            }

            var now = timeProvider.GetUtcNow();
            store.UpdateLastActivity(current.SessionId, now);
            activeSessionAccessor.Replace(current with { LastActivityAtUtc = now });
            IsLocked = false;
            return Result.Success();
        }
    }

    public Result RecordActivity()
    {
        lock (sessionGate)
        {
            var current = activeSessionAccessor.Current;
            if (current is null)
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            var now = timeProvider.GetUtcNow();
            store.UpdateLastActivity(current.SessionId, now);
            activeSessionAccessor.Replace(current with { LastActivityAtUtc = now });
            return Result.Success();
        }
    }

    public Result Logout(string reason, CorrelationId? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        lock (sessionGate)
        {
            var current = activeSessionAccessor.Current;
            if (current is null)
            {
                return Result.Failure(ErrorCode.NotFound);
            }

            var persisted = PersistInvalidation(
                current,
                reason,
                SessionMutationAuditKind.Logout,
                correlationId);
            if (!persisted.IsSuccess)
            {
                return persisted;
            }

            activeSessionAccessor.Clear();
            IsLocked = false;
            return Result.Success();
        }
    }

    public SessionCheckpoint Checkpoint(CorrelationId? correlationId = null)
    {
        lock (sessionGate)
        {
            var current = activeSessionAccessor.Current;
            if (current is null)
            {
                return new SessionCheckpoint(SessionCheckpointKind.Expired, "missing");
            }

            var now = timeProvider.GetUtcNow();
            var invalidationReason = store.GetInvalidationReason(current.SessionId);
            if (!string.IsNullOrWhiteSpace(invalidationReason))
            {
                activeSessionAccessor.Clear();
                IsLocked = false;
                return new SessionCheckpoint(SessionCheckpointKind.Invalidated, invalidationReason);
            }

            if (!store.IsUserActive(current.UserId))
            {
                return Invalidate(current, "deactivated", now, correlationId);
            }

            if (now >= current.ExpiresAtUtc)
            {
                return Expire(current, "absolute", now, correlationId);
            }

            if (now - current.LastActivityAtUtc >= TimeSpan.FromMinutes(sessionOptions.IdleTimeoutMinutes))
            {
                if (sessionOptions.AutoLockEnabledByDefault)
                {
                    var justTransitioned = !IsLocked;
                    IsLocked = true;
                    return new SessionCheckpoint(SessionCheckpointKind.Locked, JustTransitioned: justTransitioned);
                }

                return Expire(current, "idle", now, correlationId);
            }

            return new SessionCheckpoint(IsLocked ? SessionCheckpointKind.Locked : SessionCheckpointKind.Active);
        }
    }

    private Result<SessionRecord> PrepareSession(string userId)
    {
        if (activeSessionAccessor.Current is not null)
        {
            return Result.Failure<SessionRecord>(ErrorCode.AlreadyExists);
        }

        var now = timeProvider.GetUtcNow();
        return Result.Success(new SessionRecord(
            Guid.CreateVersion7().ToString("D"),
            userId,
            now,
            now,
            now.AddMinutes(sessionOptions.AbsoluteTimeoutMinutes),
            null,
            1));
    }

    private SessionCheckpoint Expire(
        SessionRecord current,
        string reason,
        DateTimeOffset now,
        CorrelationId? correlationId)
    {
        var result = PersistInvalidation(
            current,
            reason,
            SessionMutationAuditKind.Expired,
            correlationId,
            now);
        if (!result.IsSuccess)
        {
            return new SessionCheckpoint(
                SessionCheckpointKind.Active,
                "persistence-unavailable",
                ErrorCode: result.ErrorCode);
        }

        activeSessionAccessor.Clear();
        IsLocked = false;
        return new SessionCheckpoint(SessionCheckpointKind.Expired, reason);
    }

    private SessionCheckpoint Invalidate(
        SessionRecord current,
        string reason,
        DateTimeOffset now,
        CorrelationId? correlationId)
    {
        var result = PersistInvalidation(
            current,
            reason,
            SessionMutationAuditKind.Invalidated,
            correlationId,
            now);
        if (!result.IsSuccess)
        {
            return new SessionCheckpoint(
                SessionCheckpointKind.Active,
                "persistence-unavailable",
                ErrorCode: result.ErrorCode);
        }

        activeSessionAccessor.Clear();
        IsLocked = false;
        return new SessionCheckpoint(SessionCheckpointKind.Invalidated, reason);
    }

    private Result PersistInvalidation(
        SessionRecord current,
        string reason,
        SessionMutationAuditKind auditKind,
        CorrelationId? correlationId,
        DateTimeOffset? invalidatedAt = null)
    {
        if (mutationCoordinator is not null && correlationId.HasValue)
        {
            return mutationCoordinator.Invalidate(
                current.UserId,
                current.SessionId,
                reason,
                auditKind,
                correlationId.Value);
        }

        store.Invalidate(current.SessionId, reason, invalidatedAt ?? timeProvider.GetUtcNow());
        return Result.Success();
    }
}
