using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.Security.Authentication;

namespace Dobby.Host.Settings;

public sealed class UserManagementAuditCoordinator(
    IHostTransactionAuditTrail auditTrail,
    IHostPersistenceCommandExecutor executor,
    TimeProvider timeProvider,
    IHostMutationUnitOfWork? mutationUnitOfWork = null) : IManagedUserAuditSink
{
    public Result<T> ExecuteUserMutation<T>(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result<T>> mutation)
    {
        return RequireMutationUnitOfWork().ExecuteResult(context =>
        {
            var result = mutation(context.Commands);
            if (!result.IsSuccess)
            {
                return result;
            }

            context.AppendAudit(CreateUserMutationEvent(actorId, targetUserId, action, correlationId));
            context.EnqueueOutbox(CreateUserMutationMessage(actorId, targetUserId, action, correlationId));
            return result;
        });
    }

    public Result ExecuteUserMutation(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result> mutation)
    {
        return RequireMutationUnitOfWork().ExecuteResult(context =>
        {
            var result = mutation(context.Commands);
            if (!result.IsSuccess)
            {
                return result;
            }

            context.AppendAudit(CreateUserMutationEvent(actorId, targetUserId, action, correlationId));
            context.EnqueueOutbox(CreateUserMutationMessage(actorId, targetUserId, action, correlationId));
            return result;
        });
    }

    public Result ExecutePermissionMutation(
        string actorId,
        string targetUserId,
        string permissionKey,
        bool grant,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result> mutation)
    {
        return RequireMutationUnitOfWork().ExecuteResult(context =>
        {
            var result = mutation(context.Commands);
            if (!result.IsSuccess)
            {
                return result;
            }

            context.AppendAudit(new AuditEvent(
                grant ? AuditEventName.PermissionGrant : AuditEventName.PermissionRevoke,
                actorId,
                targetUserId,
                AuditResult.Success,
                correlationId,
                timeProvider.GetUtcNow(),
                new Dictionary<string, string> { ["permissionKey"] = permissionKey }));
            context.EnqueueOutbox(new HostOutboxMessage(
                $"permission-mutation:{correlationId}",
                "security.authorization",
                grant ? "permission.granted" : "permission.revoked",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["actorId"] = actorId,
                    ["permissionKey"] = permissionKey,
                    ["targetUserId"] = targetUserId
                },
                correlationId.ToString()));
            return result;
        });
    }

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

    private IHostMutationUnitOfWork RequireMutationUnitOfWork()
    {
        return mutationUnitOfWork ?? throw new InvalidOperationException(
            "Atomic User Management mutation support was not configured.");
    }

    private AuditEvent CreateUserMutationEvent(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId)
    {
        return new AuditEvent(
            ToEventName(action),
            actorId,
            targetUserId,
            AuditResult.Success,
            correlationId,
            timeProvider.GetUtcNow());
    }

    private static HostOutboxMessage CreateUserMutationMessage(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId)
    {
        return new HostOutboxMessage(
            $"user-mutation:{correlationId}",
            "security.user",
            action switch
            {
                ManagedUserAuditAction.Create => "user.created",
                ManagedUserAuditAction.Update => "user.updated",
                ManagedUserAuditAction.PasswordReset => "credential.reset",
                ManagedUserAuditAction.Activate => "user.activated",
                ManagedUserAuditAction.Deactivate => "user.deactivated",
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported User Management outbox action.")
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["actorId"] = actorId,
                ["targetUserId"] = targetUserId
            },
            correlationId.ToString());
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
