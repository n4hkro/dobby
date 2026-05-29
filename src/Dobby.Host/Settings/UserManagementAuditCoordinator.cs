using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Settings;

public sealed class UserManagementAuditCoordinator(
    IHostTransactionAuditTrail auditTrail,
    IHostPersistenceCommandExecutor executor,
    TimeProvider timeProvider) : IManagedUserAuditSink
{
    public void AppendUserMutation(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        bool succeeded,
        CorrelationId correlationId)
    {
        Append(new AuditEvent(
            ToEventName(action),
            actorId,
            targetUserId,
            succeeded ? AuditResult.Success : AuditResult.Failed,
            correlationId,
            timeProvider.GetUtcNow()));
    }

    public void AppendPermissionMutation(
        string actorId,
        string targetUserId,
        string permissionKey,
        bool grant,
        bool succeeded,
        CorrelationId correlationId)
    {
        Append(new AuditEvent(
            grant ? AuditEventName.PermissionGrant : AuditEventName.PermissionRevoke,
            actorId,
            targetUserId,
            succeeded ? AuditResult.Success : AuditResult.Failed,
            correlationId,
            timeProvider.GetUtcNow(),
            new Dictionary<string, string> { ["permissionKey"] = permissionKey }));
    }

    public void AppendPermissionDenied(string actorId, string operationKey, CorrelationId correlationId)
    {
        Append(new AuditEvent(
            AuditEventName.PermissionDenied,
            actorId,
            operationKey,
            AuditResult.Denied,
            correlationId,
            timeProvider.GetUtcNow()));
    }

    public void AppendOwnerProtectionDenied(string actorId, string targetUserId, CorrelationId correlationId)
    {
        Append(new AuditEvent(
            AuditEventName.OwnerProtectionDenied,
            actorId,
            targetUserId,
            AuditResult.Denied,
            correlationId,
            timeProvider.GetUtcNow()));
    }

    private void Append(AuditEvent auditEvent)
    {
        auditTrail.AppendInScope(executor, auditEvent);
    }

    private static string ToEventName(ManagedUserAuditAction action)
    {
        return action switch
        {
            ManagedUserAuditAction.Create => AuditEventName.UserCreate,
            ManagedUserAuditAction.Update => AuditEventName.UserUpdate,
            ManagedUserAuditAction.PasswordReset => AuditEventName.UserPasswordReset,
            ManagedUserAuditAction.Activate => AuditEventName.UserActivate,
            ManagedUserAuditAction.Deactivate => AuditEventName.UserDeactivate,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported User Management audit action.")
        };
    }
}
