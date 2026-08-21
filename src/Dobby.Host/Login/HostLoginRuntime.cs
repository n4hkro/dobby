using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Application.Printing;
using Dobby.Host.Settings;
using Dobby.Host.Plugins;
using Dobby.Host.Printing;
using Dobby.Host.Sessions;
using Dobby.Licensing;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;
using Dobby.Security.CurrentContext;

namespace Dobby.Host.Login;

public static class LoginRuntimeCompositionEvents
{
    public const string PreAuthRuntimeConstructed = "pre-auth-runtime-constructed";
    public const string AuthenticatedRuntimeConstructionStarted = "authenticated-runtime-construction-started";
    public const string BusinessRepositoriesConstructed = "business-repositories-constructed";
    public const string LicensingConstructed = "licensing-constructed";
    public const string AuditRetentionStarted = "audit-retention-started";
    public const string PluginRuntimeConstructed = "plugin-runtime-constructed";
    public const string AuthenticatedRuntimeConstructed = "authenticated-runtime-constructed";

    public static IReadOnlySet<string> ForbiddenBeforeAuthentication { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthenticatedRuntimeConstructionStarted,
        BusinessRepositoriesConstructed,
        LicensingConstructed,
        AuditRetentionStarted,
        PluginRuntimeConstructed,
        AuthenticatedRuntimeConstructed
    };
}

public interface ILoginRuntimeCompositionProbe
{
    void Record(string eventName);
}

public sealed record HostPreLoginRuntime(
    ILoginRuntimeWorkflow LoginWorkflow,
    LoginViewModel LoginViewModel,
    RecoveryViewModel RecoveryViewModel);

public sealed record HostLoginRuntime(
    ILoginRuntimeWorkflow LoginWorkflow,
    LoginViewModel LoginViewModel,
    RecoveryViewModel RecoveryViewModel,
    SessionLifecycleService SessionLifecycleService,
    PostLoginSessionRuntime PostLoginSessionRuntime,
    RecoveryRequestExportCoordinator RecoveryRequestExportCoordinator,
    RecoveryResponseImportCoordinator RecoveryResponseImportCoordinator,
    SelfServicePasswordChangeCoordinator SelfServicePasswordChangeCoordinator,
    OperationAuthorizationPolicy OperationAuthorizationPolicy,
    ManagedUserService ManagedUserService,
    Func<string, Result<UserPermissionState>> UserPermissionStateReader,
    ISettingsPreferenceSource SettingsPreferenceSource,
    Func<string, ISettingsPreferenceSource> SettingsPreferenceSourceFactory,
    AccountSettingsModel AccountSettingsModel,
    SettingsNavigationModel SettingsNavigationModel,
    AboutInfoProvider AboutInfoProvider,
    ShellDiagnosticsExportCoordinator DiagnosticsExportCoordinator,
    HostPluginPackageInstallCoordinator PluginPackageInstallCoordinator,
    HostPluginLifecycleCoordinator PluginLifecycleCoordinator,
    HostRuntimeModeMutationGate RuntimeModeMutationGate,
    LicenseImportCoordinator LicenseImportCoordinator,
    HostPluginRuntimeCoordinator PluginRuntimeCoordinator,
    CurrentCustomerDeploymentContextService CurrentCustomerDeploymentContextService,
    HostCustomerDeploymentContextProvisioningCoordinator CustomerDeploymentContextProvisioningCoordinator,
    ILicenseStateStore LicenseStateStore,
    ILicenseRuntimeModeProvider LicenseRuntimeModeProvider,
    IPrintTargetClassifier PrintTargetClassifier,
    HostPrintOutputCoordinator PrintOutputCoordinator,
    HostPrintOutputService PrintOutputService,
    IBackgroundOperationReporter BackgroundOperations,
    string DiagnosticsSourceDirectory,
    string DiagnosticsOutputZipPath) : IAsyncDisposable
{
    internal CancellationTokenSource? AuthenticatedLifetime { get; init; }

    internal Task? AuditRetentionTask { get; init; }

    public Result<CurrentCustomerDeploymentContextResult> ProvisionCustomerDeploymentContext(
        string actorId,
        string customerId,
        string deploymentId,
        CorrelationId correlationId)
    {
        return CustomerDeploymentContextProvisioningCoordinator.Provision(
            actorId,
            customerId,
            deploymentId,
            correlationId);
    }

    public ValueTask<Result> ExecutePluginPrintAsync(
        string pluginId,
        string packageFingerprint,
        PrintTargetDescriptor target,
        bool isPrintToFile,
        Func<CancellationToken, ValueTask<Result>> execute,
        CancellationToken cancellationToken = default)
    {
        return PrintOutputService.ExecuteAsync(
            pluginId,
            packageFingerprint,
            target,
            isPrintToFile,
            execute,
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        AuthenticatedLifetime?.Cancel();
        PostLoginSessionRuntime.Dispose();
        if (AuditRetentionTask is not null)
        {
            try
            {
                await AuditRetentionTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await PluginRuntimeCoordinator.DisposeAsync().ConfigureAwait(false);
        AuthenticatedLifetime?.Dispose();
    }
}
