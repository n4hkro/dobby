using Dobby.Core;

namespace Dobby.Contracts.Policies;

public interface ICapabilityPolicy<in TCapability>
{
    Result<PolicyDecision> Evaluate(TCapability capability, PolicyContext context);
}

public sealed record PolicyContext(
    string? UserId,
    string? PluginId,
    CorrelationId CorrelationId);

public readonly record struct PolicyDecision(bool Allowed, string? DenyReason)
{
    public static PolicyDecision Allow() => new(true, null);

    public static PolicyDecision Deny(string reason) => new(false, reason);
}

public readonly record struct OperationPermissionCapability(string OperationKey);
