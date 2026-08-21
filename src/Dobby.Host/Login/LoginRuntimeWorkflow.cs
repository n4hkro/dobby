using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

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

public sealed class LoginRuntimeWorkflow(
    LoginService loginService,
    SessionLifecycleService sessionLifecycleService,
    RememberMeSlotService rememberMeSlotService,
    LoginAuditCoordinator auditCoordinator,
    IHostPersistenceTransactionScope? transactionScope = null) : ILoginRuntimeWorkflow
{
    private readonly object submissionGate = new();

    public LoginRuntimeWorkflowResult Submit(LoginSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        lock (submissionGate)
        {
            if (transactionScope is null)
            {
                return CompleteSuccessfulThrottleReset(
                    SubmitCore(submission, durableOnly: false, out _),
                    submission.Username);
            }

            LoginRuntimeWorkflowResult? result = null;
            try
            {
                transactionScope.ExecuteInTransaction(_ =>
                {
                    result = SubmitCore(submission, durableOnly: true, out var credentialsSucceeded);
                    if (credentialsSucceeded && !result.IsAuthenticated)
                    {
                        throw new PostAuthenticationMutationException(
                            result.ErrorCode ?? ErrorCode.DatabaseUnavailable);
                    }
                });
            }
            catch (PostAuthenticationMutationException exception)
            {
                return LoginRuntimeWorkflowResult.InfrastructureFailure(exception.ErrorCode);
            }

            var committed = result ?? LoginRuntimeWorkflowResult.InfrastructureFailure(ErrorCode.DatabaseUnavailable);
            if (committed.IsAuthenticated && committed.Session is not null)
            {
                var activated = sessionLifecycleService.ActivateExisting(committed.Session);
                if (!activated.IsSuccess)
                {
                    return LoginRuntimeWorkflowResult.InfrastructureFailure(activated.ErrorCode!.Value);
                }
            }

            return CompleteSuccessfulThrottleReset(committed, submission.Username);
        }
    }

    private LoginRuntimeWorkflowResult CompleteSuccessfulThrottleReset(
        LoginRuntimeWorkflowResult result,
        string submittedUsername)
    {
        if (result.IsAuthenticated && result.Session is not null)
        {
            var normalized = UsernameCanonicalValidator.Normalize(submittedUsername);
            if (normalized.IsSuccess)
            {
                _ = loginService.RecordSuccessfulSession(result.Session.UserId, normalized.Value);
            }
        }

        return result;
    }

    private LoginRuntimeWorkflowResult SubmitCore(
        LoginSubmission submission,
        bool durableOnly,
        out bool credentialsSucceeded)
    {
        credentialsSucceeded = false;
        var attempt = loginService.Login(
            submission.Username,
            submission.Password,
            submission.DisplayName,
            submission.CorrelationId);

        if (attempt.Kind != LoginAttemptKind.Success)
        {
            auditCoordinator.Append(attempt, submission.CorrelationId);
            return LoginRuntimeWorkflowResult.Unauthenticated(attempt);
        }

        credentialsSucceeded = true;

        var normalized = UsernameCanonicalValidator.Normalize(submission.Username);
        if (!normalized.IsSuccess)
        {
            return LoginRuntimeWorkflowResult.InfrastructureFailure(normalized.ErrorCode!.Value);
        }

        var session = durableOnly
            ? sessionLifecycleService.CreateDurable(attempt.UserId!)
            : sessionLifecycleService.Create(attempt.UserId!);
        if (!session.IsSuccess)
        {
            return LoginRuntimeWorkflowResult.InfrastructureFailure(session.ErrorCode!.Value);
        }

        var remember = rememberMeSlotService.ApplySuccessfulLogin(
            attempt.UserId!,
            normalized.Value,
            submission.RememberMe);
        if (!remember.IsSuccess)
        {
            return LoginRuntimeWorkflowResult.InfrastructureFailure(remember.ErrorCode!.Value);
        }

        auditCoordinator.Append(attempt, submission.CorrelationId);
        return LoginRuntimeWorkflowResult.Authenticated(session.Value);
    }

    private sealed class PostAuthenticationMutationException(ErrorCode errorCode) : Exception
    {
        public ErrorCode ErrorCode { get; } = errorCode;
    }

}
