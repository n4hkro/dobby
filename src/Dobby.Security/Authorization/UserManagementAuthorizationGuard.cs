using Dobby.Contracts.Policies;
using Dobby.Core;

namespace Dobby.Security.Authorization;

public sealed class UserManagementAuthorizationGuard(
    OperationAuthorizationPolicy authorizationPolicy,
    IPermissionGrantReader permissionGrants,
    OperationTargetRegistry registry)
{
    public Result EnsureCanViewUsers(string actorUserId, CorrelationId correlationId)
    {
        return EnsureUserManagementOperation(actorUserId, OperationVerb.View, correlationId);
    }

    public Result EnsureCanCreateUser(string actorUserId, CorrelationId correlationId)
    {
        return EnsureUserManagementOperation(actorUserId, OperationVerb.Create, correlationId);
    }

    public Result EnsureCanManageUser(
        string actorUserId,
        string targetUserId,
        OperationVerb verb,
        CorrelationId correlationId)
    {
        var allowed = EnsureUserManagementOperation(actorUserId, verb, correlationId);
        if (!allowed.IsSuccess)
        {
            return allowed;
        }

        var target = permissionGrants.LoadUserState(targetUserId);
        if (!target.IsSuccess)
        {
            return Result.Failure(target.ErrorCode!.Value);
        }

        return target.Value.IsOwner
            ? Result.Failure(ErrorCode.Forbidden)
            : Result.Success();
    }

    public Result EnsureCanGrant(
        string actorUserId,
        string targetUserId,
        OperationKey permission,
        CorrelationId correlationId)
    {
        if (!registry.Supports(permission))
        {
            return Result.Failure(ErrorCode.Forbidden);
        }

        var allowed = EnsureUserManagementOperation(actorUserId, OperationVerb.Grant, correlationId);
        if (!allowed.IsSuccess)
        {
            return allowed;
        }

        if (StringComparer.Ordinal.Equals(actorUserId, targetUserId))
        {
            return Result.Failure(ErrorCode.Forbidden);
        }

        var target = permissionGrants.LoadUserState(targetUserId);
        if (!target.IsSuccess)
        {
            return Result.Failure(target.ErrorCode!.Value);
        }

        if (target.Value.IsOwner)
        {
            return Result.Failure(ErrorCode.Forbidden);
        }

        var actor = permissionGrants.LoadUserState(actorUserId);
        if (!actor.IsSuccess)
        {
            return Result.Failure(actor.ErrorCode!.Value);
        }

        if (actor.Value.IsOwner)
        {
            return Result.Success();
        }

        var actorHasRequestedPermission = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(permission.ToString()),
            new PolicyContext(actorUserId, null, correlationId));
        return actorHasRequestedPermission.IsSuccess && actorHasRequestedPermission.Value.Allowed
            ? Result.Success()
            : Result.Failure(ErrorCode.Forbidden);
    }

    public Result EnsureCanRevoke(
        string actorUserId,
        string targetUserId,
        OperationKey permission,
        CorrelationId correlationId)
    {
        return EnsureCanGrant(actorUserId, targetUserId, permission, correlationId);
    }

    private Result EnsureUserManagementOperation(string actorUserId, OperationVerb verb, CorrelationId correlationId)
    {
        var operationKey = new OperationKey(BuiltInOperationTargets.UserManagementTargetId, verb);
        var decision = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(operationKey.ToString()),
            new PolicyContext(actorUserId, null, correlationId));

        return decision.IsSuccess && decision.Value.Allowed
            ? Result.Success()
            : Result.Failure(ErrorCode.Forbidden);
    }
}
