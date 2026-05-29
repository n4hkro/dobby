using Dobby.Core;
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
    LoginAuditCoordinator auditCoordinator) : ILoginRuntimeWorkflow
{
    public LoginRuntimeWorkflowResult Submit(LoginSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

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
            submission.RememberMe);
        if (!remember.IsSuccess)
        {
            sessionLifecycleService.Logout("login.remember-me-failed");
            return LoginRuntimeWorkflowResult.InfrastructureFailure(remember.ErrorCode!.Value);
        }

        auditCoordinator.Append(attempt, submission.CorrelationId);
        return LoginRuntimeWorkflowResult.Authenticated(session.Value);
    }
}
