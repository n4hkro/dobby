namespace Dobby.Security.Authentication;

public sealed record SessionRecord(
    string SessionId,
    string UserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? InvalidationReason,
    int SessionVersion);
