using Dobby.Core;
using Dobby.Host.Sessions;
using Dobby.Security.Authentication;

namespace Dobby.Host.Login;

public sealed class LockRuntimeWorkflow(
    SessionLifecycleService sessionLifecycleService,
    SessionLifecycleAuditCoordinator auditCoordinator)
{
    public Result Unlock(string userId, string password, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var result = sessionLifecycleService.Unlock(password);
        auditCoordinator.AppendUnlock(userId, result, correlationId);
        return result;
    }

    public Result Logout(string userId, CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var result = sessionLifecycleService.Logout("user-logout");
        auditCoordinator.AppendLogout(userId, result, correlationId);
        return result;
    }
}
