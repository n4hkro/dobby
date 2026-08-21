using Dobby.Core;
using Dobby.Host.Sessions;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed record LockRuntimeWorkflowActionResult(
    bool IsSuccess,
    ErrorCode? ErrorCode = null,
    int RetryAfterSeconds = 0)
{
    public static LockRuntimeWorkflowActionResult Success()
    {
        return new LockRuntimeWorkflowActionResult(IsSuccess: true);
    }

    public static LockRuntimeWorkflowActionResult Failure(ErrorCode errorCode, int retryAfterSeconds = 0)
    {
        return new LockRuntimeWorkflowActionResult(IsSuccess: false, errorCode, retryAfterSeconds);
    }

    public Result ToResult()
    {
        return IsSuccess
            ? Result.Success()
            : Result.Failure(ErrorCode ?? Dobby.Core.ErrorCode.UnsupportedOperation);
    }
}

public sealed class LockRuntimeWorkflow(
    SessionLifecycleService sessionLifecycleService,
    SessionLifecycleAuditCoordinator auditCoordinator,
    LoginThrottleService? unlockThrottleService = null)
{
    public Result Unlock(string userId, string password, CorrelationId correlationId)
    {
        return UnlockForLockScreen(userId, password, correlationId).ToResult();
    }

    public LockRuntimeWorkflowActionResult UnlockForLockScreen(
        string userId,
        string password,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var throttle = unlockThrottleService?.Check(userId);
        if (throttle is { IsAvailable: false })
        {
            var busy = Result.Failure(ErrorCode.ResourceBusy);
            auditCoordinator.AppendUnlock(userId, busy, correlationId);
            return LockRuntimeWorkflowActionResult.Failure(ErrorCode.ResourceBusy);
        }

        if (throttle is { IsThrottled: true })
        {
            var blocked = Result.Failure(ErrorCode.Unauthorized);
            auditCoordinator.AppendUnlock(userId, blocked, correlationId);
            return LockRuntimeWorkflowActionResult.Failure(
                ErrorCode.Unauthorized,
                throttle.RetryAfterSeconds);
        }

        var result = sessionLifecycleService.Unlock(password);
        auditCoordinator.AppendUnlock(userId, result, correlationId);
        if (result.IsSuccess)
        {
            unlockThrottleService?.RecordSuccess(userId);
            return LockRuntimeWorkflowActionResult.Success();
        }

        var errorCode = result.ErrorCode ?? ErrorCode.DatabaseUnavailable;
        if (errorCode == ErrorCode.Unauthorized)
        {
            var failed = unlockThrottleService?.RecordFailure(userId);
            return LockRuntimeWorkflowActionResult.Failure(
                errorCode,
                failed?.RetryAfterSeconds ?? 0);
        }

        return LockRuntimeWorkflowActionResult.Failure(errorCode);
    }

    public Result Logout(string userId, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return sessionLifecycleService.Logout("user-logout", correlationId);
    }
}
