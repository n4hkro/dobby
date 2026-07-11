namespace Dobby.Security.Authorization;

public static class BuiltInOperationTargets
{
    public const string PluginLicenseTargetId = "plugins.license";
    public const string PluginManagementTargetId = "plugins.management";
    public const string CustomerDeploymentContextTargetId = "customer-deployment.context";
    public const string UserManagementTargetId = "users.management";

    public static IReadOnlyList<OperationTarget> All { get; } =
    [
        new OperationTarget(
            PluginLicenseTargetId,
            [OperationVerb.View, OperationVerb.Import],
            "Dobby.Plugins.License",
            OperationTargetOrigin.BuiltIn),
        new OperationTarget(
            PluginManagementTargetId,
            [OperationVerb.View, OperationVerb.Import, OperationVerb.Configure, OperationVerb.Delete],
            "Dobby.Plugins.Management",
            OperationTargetOrigin.BuiltIn),
        new OperationTarget(
            CustomerDeploymentContextTargetId,
            [OperationVerb.Execute],
            "Dobby.CustomerDeployment.Context",
            OperationTargetOrigin.BuiltIn),
        new OperationTarget(
            UserManagementTargetId,
            [OperationVerb.View, OperationVerb.Create, OperationVerb.Update, OperationVerb.Delete, OperationVerb.Execute, OperationVerb.Grant],
            "Dobby.Settings.UserManagement",
            OperationTargetOrigin.BuiltIn)
    ];
}
