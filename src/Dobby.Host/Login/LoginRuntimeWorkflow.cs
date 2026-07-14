using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.Host.Login;

public sealed record LoginSubmission(
    string Username,
    string Password,
    string? DisplayName,
    bool RememberMe,
    CorrelationId CorrelationId);

public sealed record LoginRuntimeWorkflowResult(
    bool IsAuthenticated,
    LoginAttemptKind AttemptKind,
    SessionRecord? Session,
    int RetryAfterSeconds,
    ErrorCode? ErrorCode = null)
{
    public static LoginRuntimeWorkflowResult Unauthenticated(LoginAttemptResult attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        return new LoginRuntimeWorkflowResult(false, attempt.Kind, null, attempt.RetryAfterSeconds);
    }

    public static LoginRuntimeWorkflowResult InfrastructureFailure(ErrorCode errorCode)
    {
        return new LoginRuntimeWorkflowResult(false, LoginAttemptKind.ValidationFailed, null, 0, errorCode);
    }

    public static LoginRuntimeWorkflowResult Authenticated(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new LoginRuntimeWorkflowResult(true, LoginAttemptKind.Success, session, 0);
    }
}

public interface ILoginRuntimeWorkflow
{
    LoginRuntimeWorkflowResult Submit(LoginSubmission submission);
}

public sealed record LoginCredentialWarmup(
    string Username,
    string Password,
    CorrelationId CorrelationId);

public interface ILoginCredentialWarmupWorkflow
{
    Task WarmCredentialsAsync(LoginCredentialWarmup warmup);
}

public sealed class LoginRuntimeWorkflow(
    LoginService loginService,
    SessionLifecycleService sessionLifecycleService,
    RememberMeSlotService rememberMeSlotService,
    LoginAuditCoordinator auditCoordinator,
    IHostPersistenceTransactionScope? transactionScope = null) : ILoginRuntimeWorkflow, ILoginCredentialWarmupWorkflow
{
    private readonly byte[] credentialWarmupSecret = RandomNumberGenerator.GetBytes(32);
    private int credentialWarmupVersion;
    private WarmedCredential? warmedCredential;

    public LoginRuntimeWorkflowResult Submit(LoginSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (transactionScope is null)
        {
            return SubmitCore(submission);
        }

        LoginRuntimeWorkflowResult? result = null;
        transactionScope.ExecuteInTransaction(_ =>
        {
            result = SubmitCore(submission);
        });

        return result ?? LoginRuntimeWorkflowResult.InfrastructureFailure(ErrorCode.DatabaseUnavailable);
    }

    private LoginRuntimeWorkflowResult SubmitCore(LoginSubmission submission)
    {
        var attempt = TryUseWarmedCredential(submission, out var warmedUserId)
            ? loginService.LoginWithValidatedCredential(submission.Username, warmedUserId)
            : loginService.Login(
                submission.Username,
                submission.Password,
                submission.DisplayName,
                submission.CorrelationId);

        if (attempt.Kind != LoginAttemptKind.Success)
        {
            auditCoordinator.Append(attempt, submission.CorrelationId);
            return LoginRuntimeWorkflowResult.Unauthenticated(attempt);
        }

        var normalized = UsernameCanonicalValidator.Normalize(submission.Username);
        if (!normalized.IsSuccess)
        {
            return LoginRuntimeWorkflowResult.InfrastructureFailure(normalized.ErrorCode!.Value);
        }

        var session = sessionLifecycleService.Create(attempt.UserId!);
        if (!session.IsSuccess)
        {
            return LoginRuntimeWorkflowResult.InfrastructureFailure(session.ErrorCode!.Value);
        }

        var remember = rememberMeSlotService.ApplySuccessfulLogin(
            attempt.UserId!,
            normalized.Value,
            submission.Password,
            submission.RememberMe);
        if (!remember.IsSuccess)
        {
            sessionLifecycleService.Logout("login.remember-me-failed");
            return LoginRuntimeWorkflowResult.InfrastructureFailure(remember.ErrorCode!.Value);
        }

        auditCoordinator.Append(attempt, submission.CorrelationId);
        return LoginRuntimeWorkflowResult.Authenticated(session.Value);
    }

    public Task WarmCredentialsAsync(LoginCredentialWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);

        var version = Interlocked.Increment(ref credentialWarmupVersion);
        return Task.Run(() => WarmCredentialsCore(warmup, version));
    }

    private void WarmCredentialsCore(LoginCredentialWarmup warmup, int version)
    {
        try
        {
            var validation = loginService.ValidateExistingCredentials(warmup.Username, warmup.Password);
            if (!validation.IsAuthenticated ||
                string.IsNullOrWhiteSpace(validation.UserId) ||
                string.IsNullOrWhiteSpace(validation.UsernameNormalized))
            {
                ClearWarmedCredential(version);
                return;
            }

            var fingerprint = ComputeCredentialFingerprint(validation.UsernameNormalized, warmup.Password);
            if (version == Volatile.Read(ref credentialWarmupVersion))
            {
                Volatile.Write(
                    ref warmedCredential,
                    new WarmedCredential(validation.UserId, validation.UsernameNormalized, fingerprint));
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            ClearWarmedCredential(version);
        }
    }

    private void ClearWarmedCredential(int version)
    {
        if (version == Volatile.Read(ref credentialWarmupVersion))
        {
            Volatile.Write(ref warmedCredential, null);
        }
    }

    private bool TryUseWarmedCredential(LoginSubmission submission, out string userId)
    {
        userId = string.Empty;
        var normalized = UsernameCanonicalValidator.Normalize(submission.Username);
        if (!normalized.IsSuccess)
        {
            return false;
        }

        var cached = Volatile.Read(ref warmedCredential);
        if (cached is null ||
            !StringComparer.Ordinal.Equals(cached.UsernameNormalized, normalized.Value))
        {
            return false;
        }

        var fingerprint = ComputeCredentialFingerprint(normalized.Value, submission.Password);
        if (!CryptographicOperations.FixedTimeEquals(fingerprint, cached.PasswordFingerprint))
        {
            return false;
        }

        userId = cached.UserId;
        return true;
    }

    private byte[] ComputeCredentialFingerprint(string usernameNormalized, string password)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(credentialWarmupSecret);
        hasher.AppendData(Encoding.UTF8.GetBytes(usernameNormalized));
        hasher.AppendData([0]);
        hasher.AppendData(Encoding.UTF8.GetBytes(password));
        return hasher.GetHashAndReset();
    }

    private sealed record WarmedCredential(
        string UserId,
        string UsernameNormalized,
        byte[] PasswordFingerprint);
}
