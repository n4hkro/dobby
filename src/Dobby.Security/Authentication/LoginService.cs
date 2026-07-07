using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public sealed class LoginService(
    IHostPersistenceCommandExecutor executor,
    IUserCredentialVerifier credentialVerifier,
    IOwnerBootstrapCoordinator ownerBootstrapCoordinator,
    LoginThrottleService throttleService)
{
    public LoginAttemptResult Login(string username, string password, string? displayName, CorrelationId correlationId)
    {
        var normalized = UsernameCanonicalValidator.Normalize(username);
        if (!normalized.IsSuccess || !PasswordPolicyValidator.Validate(password).IsSuccess)
        {
            return new LoginAttemptResult(LoginAttemptKind.ValidationFailed);
        }

        var throttle = throttleService.Check();
        if (throttle.IsThrottled)
        {
            return new LoginAttemptResult(LoginAttemptKind.Throttled, RetryAfterSeconds: throttle.RetryAfterSeconds);
        }

        if (executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0)
        {
            var bootstrap = ownerBootstrapCoordinator.BootstrapFirstOwner(username, displayName, password, correlationId);
            if (!bootstrap.IsSuccess)
            {
                return new LoginAttemptResult(LoginAttemptKind.ValidationFailed);
            }

            var bootstrapped = LoadUser(normalized.Value);
            if (bootstrapped is null)
            {
                return new LoginAttemptResult(LoginAttemptKind.ValidationFailed);
            }

            throttleService.RecordSuccess();
            return LoginAttemptResult.Success(bootstrapped.UserId);
        }

        var user = LoadUser(normalized.Value);
        if (user is null)
        {
            var failed = throttleService.RecordFailure(normalized.Value);
            return new LoginAttemptResult(LoginAttemptKind.InvalidCredentials, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        if (user.IsActive == 0)
        {
            var failed = throttleService.RecordFailure(normalized.Value);
            return new LoginAttemptResult(LoginAttemptKind.Deactivated, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        var verification = credentialVerifier.VerifyPassword(user.UserId, password);
        if (!verification.IsSuccess)
        {
            return new LoginAttemptResult(LoginAttemptKind.ValidationFailed);
        }

        if (!verification.Value)
        {
            var failed = throttleService.RecordFailure(normalized.Value);
            return new LoginAttemptResult(LoginAttemptKind.InvalidCredentials, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        throttleService.RecordSuccess();
        return LoginAttemptResult.Success(user.UserId);
    }

    public LoginCredentialValidationResult ValidateExistingCredentials(string username, string password)
    {
        var normalized = UsernameCanonicalValidator.Normalize(username);
        if (!normalized.IsSuccess || !PasswordPolicyValidator.Validate(password).IsSuccess)
        {
            return LoginCredentialValidationResult.Unauthenticated();
        }

        var throttle = throttleService.Check();
        if (throttle.IsThrottled)
        {
            return LoginCredentialValidationResult.Unauthenticated();
        }

        if (executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0)
        {
            return LoginCredentialValidationResult.Unauthenticated();
        }

        var user = LoadUser(normalized.Value);
        if (user is null || user.IsActive == 0)
        {
            return LoginCredentialValidationResult.Unauthenticated();
        }

        var verification = credentialVerifier.VerifyPassword(user.UserId, password);
        if (!verification.IsSuccess || !verification.Value)
        {
            return LoginCredentialValidationResult.Unauthenticated();
        }

        return LoginCredentialValidationResult.Authenticated(user.UserId, normalized.Value);
    }

    public LoginAttemptResult LoginWithValidatedCredential(string username, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var normalized = UsernameCanonicalValidator.Normalize(username);
        if (!normalized.IsSuccess)
        {
            return new LoginAttemptResult(LoginAttemptKind.ValidationFailed);
        }

        var throttle = throttleService.Check();
        if (throttle.IsThrottled)
        {
            return new LoginAttemptResult(LoginAttemptKind.Throttled, RetryAfterSeconds: throttle.RetryAfterSeconds);
        }

        var user = LoadUser(normalized.Value);
        if (user is null || !StringComparer.Ordinal.Equals(user.UserId, userId))
        {
            var failed = throttleService.RecordFailure(normalized.Value);
            return new LoginAttemptResult(LoginAttemptKind.InvalidCredentials, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        if (user.IsActive == 0)
        {
            var failed = throttleService.RecordFailure(normalized.Value);
            return new LoginAttemptResult(LoginAttemptKind.Deactivated, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        throttleService.RecordSuccess();
        return LoginAttemptResult.Success(user.UserId);
    }

    private UserRow? LoadUser(string usernameNormalized)
    {
        return executor.Query<UserRow>(
            "SELECT UserId, IsActive FROM Users WHERE UsernameNormalized = @UsernameNormalized;",
            new { UsernameNormalized = usernameNormalized }).SingleOrDefault();
    }

    private sealed class UserRow
    {
        public string UserId { get; init; } = string.Empty;

        public int IsActive { get; init; }
    }
}

public sealed record LoginCredentialValidationResult(
    bool IsAuthenticated,
    string? UserId,
    string? UsernameNormalized)
{
    public static LoginCredentialValidationResult Authenticated(string userId, string usernameNormalized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameNormalized);

        return new LoginCredentialValidationResult(true, userId, usernameNormalized);
    }

    public static LoginCredentialValidationResult Unauthenticated()
    {
        return new LoginCredentialValidationResult(false, null, null);
    }
}
