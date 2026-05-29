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

public sealed record SessionCheckpoint(SessionCheckpointKind Kind, string? Reason = null, bool JustTransitioned = false);

public sealed class SessionLifecycleService(
    ISessionStore store,
    ActiveSessionAccessor activeSessionAccessor,
    IUserCredentialVerifier credentialVerifier,
    TimeProvider timeProvider,
    SessionOptions sessionOptions)
{
    public bool IsLocked { get; private set; }

    public Result<SessionRecord> Create(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var now = timeProvider.GetUtcNow();
        var session = new SessionRecord(
            Guid.CreateVersion7().ToString("D"),
            userId,
            now,
            now,
            now.AddMinutes(sessionOptions.AbsoluteTimeoutMinutes),
            null,
            1);

        var active = activeSessionAccessor.SetActive(session);
        if (!active.IsSuccess)
        {
            return Result.Failure<SessionRecord>(active.ErrorCode!.Value);
        }

        store.Create(session);
        return Result.Success(session);
    }

    public Result Lock()
    {
        if (activeSessionAccessor.Current is null)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        IsLocked = true;
        return Result.Success();
    }

    public Result Unlock(string password)
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
        activeSessionAccessor.Replace(current with { LastActivityAtUtc = now });
        store.UpdateLastActivity(current.SessionId, now);
        IsLocked = false;
        return Result.Success();
    }

    public Result RecordActivity()
    {
        var current = activeSessionAccessor.Current;
        if (current is null)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        var now = timeProvider.GetUtcNow();
        activeSessionAccessor.Replace(current with { LastActivityAtUtc = now });
        store.UpdateLastActivity(current.SessionId, now);
        return Result.Success();
    }

    public Result Logout(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var current = activeSessionAccessor.Current;
        if (current is null)
        {
            return Result.Failure(ErrorCode.NotFound);
        }

        store.Invalidate(current.SessionId, reason, timeProvider.GetUtcNow());
        activeSessionAccessor.Clear();
        IsLocked = false;
        return Result.Success();
    }

    public SessionCheckpoint Checkpoint()
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
            Invalidate(current, "deactivated", now);
            return new SessionCheckpoint(SessionCheckpointKind.Invalidated, "deactivated");
        }

        if (now >= current.ExpiresAtUtc)
        {
            Expire(current, "absolute", now);
            return new SessionCheckpoint(SessionCheckpointKind.Expired, "absolute");
        }

        if (now - current.LastActivityAtUtc >= TimeSpan.FromMinutes(sessionOptions.IdleTimeoutMinutes))
        {
            if (sessionOptions.AutoLockEnabledByDefault)
            {
                var justTransitioned = !IsLocked;
                IsLocked = true;
                return new SessionCheckpoint(SessionCheckpointKind.Locked, JustTransitioned: justTransitioned);
            }

            Expire(current, "idle", now);
            return new SessionCheckpoint(SessionCheckpointKind.Expired, "idle");
        }

        return new SessionCheckpoint(IsLocked ? SessionCheckpointKind.Locked : SessionCheckpointKind.Active);
    }

    private void Expire(SessionRecord current, string reason, DateTimeOffset now)
    {
        store.Invalidate(current.SessionId, reason, now);
        activeSessionAccessor.Clear();
        IsLocked = false;
    }

    private void Invalidate(SessionRecord current, string reason, DateTimeOffset now)
    {
        store.Invalidate(current.SessionId, reason, now);
        activeSessionAccessor.Clear();
        IsLocked = false;
    }
}
