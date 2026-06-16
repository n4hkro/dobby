using Dobby.Core;
using Dobby.Host.Sessions;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public interface ILockWindowPresenter
{
    bool ShowLockWindow(LockRuntimeWorkflow workflow, SessionRecord session);
}

public interface IPostLoginPluginActivationCancellation
{
    void CancelPostLoginActivation();
}

public sealed class PostLoginSessionRuntime(
    SessionLifecycleService sessionLifecycleService,
    SessionLifecycleAuditCoordinator auditCoordinator,
    LockRuntimeWorkflow lockRuntimeWorkflow,
    ILockWindowPresenter lockWindowPresenter,
    IPostLoginPluginActivationCancellation? pluginActivationCancellation = null) : IDisposable
{
    public Result RecordActivity()
    {
        return sessionLifecycleService.RecordActivity();
    }

    public Result LockNow(SessionRecord session, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var lockResult = sessionLifecycleService.Lock();
        auditCoordinator.AppendLock(session.UserId, lockResult, correlationId);
        if (!lockResult.IsSuccess)
        {
            return lockResult;
        }

        CancelPluginActivation();
        return lockWindowPresenter.ShowLockWindow(lockRuntimeWorkflow, session)
            ? Result.Success()
            : Result.Failure(ErrorCode.Cancelled);
    }

    public Result Logout(SessionRecord session, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var result = sessionLifecycleService.Logout("user.logout");
        auditCoordinator.AppendLogout(session.UserId, result, correlationId);
        if (result.IsSuccess)
        {
            CancelPluginActivation();
        }

        return result;
    }

    public Result Checkpoint(SessionRecord session, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var checkpoint = sessionLifecycleService.Checkpoint();
        auditCoordinator.AppendCheckpoint(session.UserId, checkpoint, correlationId);

        if (checkpoint.Kind == SessionCheckpointKind.Locked && checkpoint.JustTransitioned)
        {
            CancelPluginActivation();
            return lockWindowPresenter.ShowLockWindow(lockRuntimeWorkflow, session)
                ? Result.Success()
                : Result.Failure(ErrorCode.Cancelled);
        }

        if (checkpoint.Kind is SessionCheckpointKind.Expired or SessionCheckpointKind.Invalidated)
        {
            CancelPluginActivation();
            return Result.Failure(ErrorCode.Unauthorized);
        }

        return Result.Success();
    }

    public void Dispose()
    {
        CancelPluginActivation();
    }

    private void CancelPluginActivation()
    {
        pluginActivationCancellation?.CancelPostLoginActivation();
    }
}
