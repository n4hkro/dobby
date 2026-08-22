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

public interface IPostLoginLicenseImportCancellation
{
    void CancelLicenseImport();
}

public sealed class PostLoginSessionRuntime(
    SessionLifecycleService sessionLifecycleService,
    SessionLifecycleAuditCoordinator auditCoordinator,
    LockRuntimeWorkflow lockRuntimeWorkflow,
    ILockWindowPresenter lockWindowPresenter,
    IPostLoginPluginActivationCancellation? pluginActivationCancellation = null,
    IPostLoginLicenseImportCancellation? licenseImportCancellation = null) : IDisposable
{
    private readonly object sessionGate = new();

    public Result RecordActivity()
    {
        lock (sessionGate)
        {
            return sessionLifecycleService.RecordActivity();
        }
    }

    public Result LockNow(SessionRecord session, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(session);

        Result lockResult;
        lock (sessionGate)
        {
            lockResult = sessionLifecycleService.Lock();
            auditCoordinator.AppendLock(session.UserId, lockResult, correlationId);
        }

        if (!lockResult.IsSuccess)
        {
            return lockResult;
        }

        CancelInFlightPostLoginWork();
        return lockWindowPresenter.ShowLockWindow(lockRuntimeWorkflow, session)
            ? Result.Success()
            : Result.Failure(ErrorCode.Cancelled);
    }

    public Result Logout(SessionRecord session, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(session);

        Result result;
        lock (sessionGate)
        {
            result = sessionLifecycleService.Logout("user.logout", correlationId);
        }

        if (result.IsSuccess)
        {
            if (!sessionLifecycleService.CommitsMutationAudit)
            {
                auditCoordinator.AppendLogout(session.UserId, result, correlationId);
            }

            CancelInFlightPostLoginWork();
        }

        return result;
    }

    public Result Checkpoint(SessionRecord session, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(session);

        SessionCheckpoint checkpoint;
        lock (sessionGate)
        {
            checkpoint = sessionLifecycleService.Checkpoint(correlationId);
            if (checkpoint.Kind == SessionCheckpointKind.Locked)
            {
                auditCoordinator.AppendCheckpoint(session.UserId, checkpoint, correlationId);
            }
        }

        if (checkpoint.ErrorCode.HasValue)
        {
            return Result.Failure(checkpoint.ErrorCode.Value);
        }

        if (checkpoint.Kind == SessionCheckpointKind.Locked && checkpoint.JustTransitioned)
        {
            CancelInFlightPostLoginWork();
            return lockWindowPresenter.ShowLockWindow(lockRuntimeWorkflow, session)
                ? Result.Success()
                : Result.Failure(ErrorCode.Cancelled);
        }

        if (checkpoint.Kind is SessionCheckpointKind.Expired or SessionCheckpointKind.Invalidated)
        {
            CancelInFlightPostLoginWork();
            return Result.Failure(ErrorCode.Unauthorized);
        }

        return Result.Success();
    }

    public void Dispose()
    {
        CancelInFlightPostLoginWork();
    }

    private void CancelInFlightPostLoginWork()
    {
        pluginActivationCancellation?.CancelPostLoginActivation();
        licenseImportCancellation?.CancelLicenseImport();
    }
}
