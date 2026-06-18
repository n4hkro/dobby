namespace Dobby.Security.CurrentContext;

public sealed record CurrentCustomerDeploymentContext(
    string CustomerId,
    string DeploymentId);
