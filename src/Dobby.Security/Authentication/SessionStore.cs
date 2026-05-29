using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public sealed class SessionStore(IHostPersistenceCommandExecutor executor) : ISessionStore
{
    public void Create(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);

        executor.Execute(
            "INSERT INTO Sessions(SessionId, UserId, CreatedAtUtc, LastActivityAtUtc, ExpiresAtUtc, InvalidatedAtUtc, InvalidationReason, SessionVersion) VALUES (@SessionId, @UserId, @CreatedAtUtc, @LastActivityAtUtc, @ExpiresAtUtc, NULL, NULL, @SessionVersion);",
            new
            {
                session.SessionId,
                session.UserId,
                CreatedAtUtc = session.CreatedAtUtc.ToUniversalTime().ToString("O"),
                LastActivityAtUtc = session.LastActivityAtUtc.ToUniversalTime().ToString("O"),
                ExpiresAtUtc = session.ExpiresAtUtc.ToUniversalTime().ToString("O"),
                session.SessionVersion
            });
    }

    public void UpdateLastActivity(string sessionId, DateTimeOffset lastActivityAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        executor.Execute(
            "UPDATE Sessions SET LastActivityAtUtc = @LastActivityAtUtc WHERE SessionId = @SessionId AND InvalidatedAtUtc IS NULL;",
            new
            {
                SessionId = sessionId,
                LastActivityAtUtc = lastActivityAtUtc.ToUniversalTime().ToString("O")
            });
    }

    public void Invalidate(string sessionId, string reason, DateTimeOffset invalidatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        executor.Execute(
            "UPDATE Sessions SET InvalidatedAtUtc = @InvalidatedAtUtc, InvalidationReason = @InvalidationReason WHERE SessionId = @SessionId;",
            new
            {
                SessionId = sessionId,
                InvalidatedAtUtc = invalidatedAtUtc.ToUniversalTime().ToString("O"),
                InvalidationReason = reason
            });
    }

    public string? GetInvalidationReason(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return executor.Query<string>(
            "SELECT InvalidationReason FROM Sessions WHERE SessionId = @SessionId AND InvalidatedAtUtc IS NOT NULL;",
            new { SessionId = sessionId }).SingleOrDefault();
    }

    public bool IsUserActive(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE UserId = @UserId AND IsActive = 1;",
            new { UserId = userId }) == 1;
    }
}
