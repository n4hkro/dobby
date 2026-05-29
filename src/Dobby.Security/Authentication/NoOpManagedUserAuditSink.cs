using Dobby.Core;

namespace Dobby.Security.Authentication;

public sealed class NoOpManagedUserAuditSink : IManagedUserAuditSink
{
    public static NoOpManagedUserAuditSink Instance { get; } = new();

    private NoOpManagedUserAuditSink()
    {
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
