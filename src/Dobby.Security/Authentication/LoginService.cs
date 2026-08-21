using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public sealed class LoginService(
    IHostPersistenceCommandExecutor executor,
    KdfPolicyResolver kdfPolicyResolver,
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

        if (!kdfPolicyResolver.Resolve().IsSuccess)
        {
            return new LoginAttemptResult(LoginAttemptKind.KdfUnsupported);
        }

        if (executor.QueryScalar<int>("SELECT COUNT(*) FROM Users;") == 0)
        {
            var bootstrapThrottleKey = "username:" + normalized.Value;
            var throttle = throttleService.Check(bootstrapThrottleKey);
            if (!throttle.IsAvailable)
            {
                return new LoginAttemptResult(LoginAttemptKind.Busy);
            }

            if (throttle.IsThrottled)
            {
                return new LoginAttemptResult(LoginAttemptKind.Throttled, RetryAfterSeconds: throttle.RetryAfterSeconds);
            }

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

            return LoginAttemptResult.Success(bootstrapped.UserId);
        }

        var user = LoadUser(normalized.Value);
        var throttleKey = user?.UserId ?? "username:" + normalized.Value;
        var currentThrottle = throttleService.Check(throttleKey);
        if (!currentThrottle.IsAvailable)
        {
            return new LoginAttemptResult(LoginAttemptKind.Busy);
        }

        if (currentThrottle.IsThrottled)
        {
            return new LoginAttemptResult(
                LoginAttemptKind.Throttled,
                RetryAfterSeconds: currentThrottle.RetryAfterSeconds);
        }

        if (user is null)
        {
            var failed = throttleService.RecordFailure(throttleKey);
            if (!failed.IsAvailable)
            {
                return new LoginAttemptResult(LoginAttemptKind.Busy);
            }

            return new LoginAttemptResult(LoginAttemptKind.InvalidCredentials, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        if (user.IsActive == 0)
        {
            var failed = throttleService.RecordFailure(throttleKey);
            if (!failed.IsAvailable)
            {
                return new LoginAttemptResult(LoginAttemptKind.Busy);
            }

            return new LoginAttemptResult(LoginAttemptKind.InvalidCredentials, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        var verification = credentialVerifier.VerifyPassword(user.UserId, password);
        if (!verification.IsSuccess)
        {
            return new LoginAttemptResult(
                verification.ErrorCode == ErrorCode.ResourceBusy
                    ? LoginAttemptKind.Busy
                    : LoginAttemptKind.ValidationFailed);
        }

        if (!verification.Value)
        {
            var failed = throttleService.RecordFailure(throttleKey);
            if (!failed.IsAvailable)
            {
                return new LoginAttemptResult(LoginAttemptKind.Busy);
            }

            return new LoginAttemptResult(LoginAttemptKind.InvalidCredentials, RetryAfterSeconds: failed.RetryAfterSeconds);
        }

        return LoginAttemptResult.Success(user.UserId);
    }

    public LoginThrottleState RecordSuccessfulSession(string userId, string usernameNormalized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameNormalized);

        var userResult = throttleService.RecordSuccess(userId);
        var provisionalResult = throttleService.RecordSuccess("username:" + usernameNormalized);
        return userResult.IsAvailable && provisionalResult.IsAvailable
            ? provisionalResult
            : new LoginThrottleState(IsThrottled: true, RetryAfterSeconds: 1, IsAvailable: false);
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
