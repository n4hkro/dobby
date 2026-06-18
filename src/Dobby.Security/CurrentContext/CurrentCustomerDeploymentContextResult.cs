namespace Dobby.Security.CurrentContext;

public sealed record CurrentCustomerDeploymentContextResult(
    bool IsSuccess,
    CurrentCustomerDeploymentContext? Context,
    string ReasonCode,
    string SupportCode)
{
    public static CurrentCustomerDeploymentContextResult Success(CurrentCustomerDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new CurrentCustomerDeploymentContextResult(true, context, string.Empty, string.Empty);
    }

    public static CurrentCustomerDeploymentContextResult Failure(string supportCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supportCode);
        return new CurrentCustomerDeploymentContextResult(false, null, "binding-mismatch", supportCode);
    }
}
