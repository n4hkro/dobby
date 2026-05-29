using Dobby.Contracts.Policies;
using Dobby.Core;

namespace Dobby.Security.Authorization;

public sealed class OperationAuthorizationPolicy(
    OperationTargetRegistry registry,
    IPermissionGrantReader permissionGrants) : ICapabilityPolicy<OperationPermissionCapability>
{
    public const string NonDisclosingDenyReason = "permission-denied";

    public Result<PolicyDecision> Evaluate(OperationPermissionCapability capability, PolicyContext context)
    {
        if (string.IsNullOrWhiteSpace(context.UserId))
        {
            return Deny();
        }

        var parsed = OperationKey.Parse(capability.OperationKey);
        if (!parsed.IsSuccess || !registry.Supports(parsed.Value))
        {
            return Deny();
        }

        var userState = permissionGrants.LoadUserState(context.UserId);
        if (!userState.IsSuccess || !userState.Value.IsActive)
        {
            return Deny();
        }

        if (userState.Value.IsOwner)
        {
            return Result.Success(PolicyDecision.Allow());
        }

        var hasGrant = permissionGrants.HasGrant(context.UserId, parsed.Value);
        return hasGrant.IsSuccess && hasGrant.Value
            ? Result.Success(PolicyDecision.Allow())
            : Deny();
    }

    private static Result<PolicyDecision> Deny()
    {
        return Result.Success(PolicyDecision.Deny(NonDisclosingDenyReason));
    }
}
