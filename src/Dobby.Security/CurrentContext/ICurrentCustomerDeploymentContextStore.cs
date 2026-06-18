namespace Dobby.Security.CurrentContext;

public interface ICurrentCustomerDeploymentContextStore
{
    IReadOnlyList<CurrentCustomerDeploymentContext> ReadActiveContexts();

    void SeedDefaultContext(CurrentCustomerDeploymentContext context);
}
