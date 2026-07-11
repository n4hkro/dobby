namespace Dobby.Security.CurrentContext;

public sealed class CurrentCustomerDeploymentContextService
{
    public const string DefaultCustomerId = "default";
    public const string DefaultDeploymentId = "default";

    private static readonly CurrentCustomerDeploymentContext DefaultContext = new(
        DefaultCustomerId,
        DefaultDeploymentId);

    private readonly ICurrentCustomerDeploymentContextStore store;

    public CurrentCustomerDeploymentContextService(ICurrentCustomerDeploymentContextStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CurrentCustomerDeploymentContextResult EnsureDefaultContext()
    {
        var contexts = store.ReadActiveContexts();
        if (contexts.Count == 0)
        {
            store.SeedDefaultContext(DefaultContext);
            return CurrentCustomerDeploymentContextResult.Success(DefaultContext);
        }

        return EvaluateCurrentContext(contexts);
    }

    public CurrentCustomerDeploymentContextResult GetCurrentContext()
    {
        return EvaluateCurrentContext(store.ReadActiveContexts());
    }

    internal CurrentCustomerDeploymentContextResult ProvisionContext(string customerId, string deploymentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);

        var context = new CurrentCustomerDeploymentContext(customerId, deploymentId);
        store.ProvisionContext(context);
        return EvaluateCurrentContext(store.ReadActiveContexts());
    }

    public CurrentCustomerDeploymentContextResult ValidateMatches(string customerId, string deploymentId)
    {
        var current = GetCurrentContext();
        if (!current.IsSuccess)
        {
            return current;
        }

        return current.Context!.CustomerId.Equals(customerId, StringComparison.Ordinal) &&
            current.Context.DeploymentId.Equals(deploymentId, StringComparison.Ordinal)
            ? current
            : CurrentCustomerDeploymentContextResult.Failure("context-binding-mismatch");
    }

    private static CurrentCustomerDeploymentContextResult EvaluateCurrentContext(
        IReadOnlyList<CurrentCustomerDeploymentContext> contexts)
    {
        if (contexts.Count == 0)
        {
            return CurrentCustomerDeploymentContextResult.Failure("missing-current-context");
        }

        if (contexts.Count != 1)
        {
            return CurrentCustomerDeploymentContextResult.Failure("duplicate-current-context");
        }

        var context = contexts[0];
        if (string.IsNullOrWhiteSpace(context.CustomerId) ||
            string.IsNullOrWhiteSpace(context.DeploymentId))
        {
            return CurrentCustomerDeploymentContextResult.Failure("invalid-current-context");
        }

        return CurrentCustomerDeploymentContextResult.Success(context);
    }
}
