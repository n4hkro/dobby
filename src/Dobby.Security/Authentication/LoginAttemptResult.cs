namespace Dobby.Security.Authentication;

public enum LoginAttemptKind
{
    Success,
    InvalidCredentials,
    Throttled,
    Deactivated,
    KdfUnsupported,
    Busy,
    ValidationFailed
}

public sealed record LoginAttemptResult(LoginAttemptKind Kind, string? UserId = null, int RetryAfterSeconds = 0)
{
    public static LoginAttemptResult Success(string userId) => new(LoginAttemptKind.Success, userId);
}
