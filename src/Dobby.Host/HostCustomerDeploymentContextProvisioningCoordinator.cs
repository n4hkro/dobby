using System.Security.Cryptography;
using System.Text;
using Dobby.Audit;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Security.Authorization;
using Dobby.Security.CurrentContext;

namespace Dobby.Host;

public sealed class HostCustomerDeploymentContextProvisioningCoordinator(
    CurrentCustomerDeploymentContextService contextService,
    OperationAuthorizationPolicy authorizationPolicy,
    IAuditTrail auditTrail,
    TimeProvider timeProvider)
{
    public const string OperationKey = "customer-deployment.context.execute";

    public Result<CurrentCustomerDeploymentContextResult> Provision(
        string actorId,
        string customerId,
        string deploymentId,
        CorrelationId correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);

        var authorized = authorizationPolicy.Evaluate(
            new OperationPermissionCapability(OperationKey),
            new PolicyContext(actorId, PluginId: null, correlationId));
        if (!authorized.IsSuccess || !authorized.Value.Allowed)
        {
            auditTrail.Append(new AuditEvent(
                AuditEventName.PermissionDenied,
                actorId,
                OperationKey,
                AuditResult.Denied,
                correlationId,
                timeProvider.GetUtcNow()));
            return Result.Failure<CurrentCustomerDeploymentContextResult>(ErrorCode.Forbidden);
        }

        CurrentCustomerDeploymentContextResult result;
        try
        {
            result = contextService.ProvisionContext(customerId, deploymentId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            auditTrail.Append(new AuditEvent(
                AuditEventName.CustomerDeploymentContextProvision,
                actorId,
                "current-context",
                AuditResult.Failed,
                correlationId,
                timeProvider.GetUtcNow(),
                SafeDetails(customerId, deploymentId, "context-provisioning-failed")));
            return Result.Failure<CurrentCustomerDeploymentContextResult>(ErrorCode.DatabaseUnavailable);
        }

        if (!result.IsSuccess)
        {
            auditTrail.Append(new AuditEvent(
                AuditEventName.CustomerDeploymentContextProvision,
                actorId,
                "current-context",
                AuditResult.Failed,
                correlationId,
                timeProvider.GetUtcNow(),
                SafeDetails(customerId, deploymentId, result.SupportCode)));
            return Result.Failure<CurrentCustomerDeploymentContextResult>(ErrorCode.ValidationFailed);
        }

        auditTrail.Append(new AuditEvent(
            AuditEventName.CustomerDeploymentContextProvision,
            actorId,
            "current-context",
            AuditResult.Success,
            correlationId,
            timeProvider.GetUtcNow(),
            SafeDetails(customerId, deploymentId, supportCode: null)));
        return Result.Success(result);
    }

    private static IReadOnlyDictionary<string, string> SafeDetails(
        string customerId,
        string deploymentId,
        string? supportCode)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["customerIdHash"] = HashPrefix(customerId),
            ["deploymentIdHash"] = HashPrefix(deploymentId)
        };
        if (!string.IsNullOrWhiteSpace(supportCode))
        {
            details["supportCode"] = supportCode;
        }

        return details;
    }

    private static string HashPrefix(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant()[..12];
    }
}
