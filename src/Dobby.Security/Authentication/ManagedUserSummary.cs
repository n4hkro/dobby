namespace Dobby.Security.Authentication;

public sealed record ManagedUserSummary(
    string UserId,
    string UsernameNormalized,
    string? DisplayName,
    bool IsOwner,
    bool IsActive);
