using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Security.Authentication;

public interface IManagedUserAuditSink
{
    Result<T> ExecuteUserMutation<T>(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result<T>> mutation);

    Result ExecuteUserMutation(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result> mutation);

    Result ExecutePermissionMutation(
        string actorId,
        string targetUserId,
        string permissionKey,
        bool grant,
        CorrelationId correlationId,
        Func<IHostPersistenceCommandExecutor, Result> mutation);

    void AppendUserMutation(
        string actorId,
        string targetUserId,
        ManagedUserAuditAction action,
        bool succeeded,
        CorrelationId correlationId);

    void AppendPermissionMutation(
        string actorId,
        string targetUserId,
        string permissionKey,
        bool grant,
        bool succeeded,
        CorrelationId correlationId);

    void AppendPermissionDenied(string actorId, string operationKey, CorrelationId correlationId);

    void AppendOwnerProtectionDenied(string actorId, string targetUserId, CorrelationId correlationId);
}
