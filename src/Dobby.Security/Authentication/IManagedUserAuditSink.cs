using Dobby.Core;

namespace Dobby.Security.Authentication;

public interface IManagedUserAuditSink
{
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
