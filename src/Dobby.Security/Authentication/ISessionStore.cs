namespace Dobby.Security.Authentication;

public interface ISessionStore
{
    void Create(SessionRecord session);

    void UpdateLastActivity(string sessionId, DateTimeOffset lastActivityAtUtc);

    void Invalidate(string sessionId, string reason, DateTimeOffset invalidatedAtUtc);

    string? GetInvalidationReason(string sessionId);

    bool IsUserActive(string userId);
}
