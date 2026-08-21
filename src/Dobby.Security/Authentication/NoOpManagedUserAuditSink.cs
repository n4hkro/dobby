using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public sealed class NoOpManagedUserAuditSink(IHostPersistenceCommandExecutor executor) : IManagedUserAuditSink
{
    public Result<T> ExecuteUserMutation<T>(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result<T>> mutation)
    {
        return mutation(executor);
    }

    public Result ExecuteUserMutation(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result> mutation)
    {
        return mutation(executor);
    }

    public Result ExecutePermissionMutation(
        string actorId,
        string targetUserId,
        string permissionKey,
        bool grant,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result> mutation)
    {
        return mutation(executor);
    }

    public void AppendUserMutation(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        bool succeeded,
        CorrelationId correlationId)
    {
    }

    public void AppendPermissionMutation(
        string actorId,
        string targetUserId,
        string permissionKey,
        bool grant,
        bool succeeded,
        CorrelationId correlationId)
    {
    }

    public void AppendPermissionDenied(string actorId, string operationKey, CorrelationId correlationId)
    {
    }

    public void AppendOwnerProtectionDenied(string actorId, string targetUserId, CorrelationId correlationId)
    {
    }
}
