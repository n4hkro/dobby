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
using Dobby.Infrastructure.Persistence;

namespace Dobby.Host.Login;

public static class LoginRuntimeCompositionEvents
{
    public const string PreAuthRuntimeConstructed = "pre-auth-runtime-constructed";
    public const string AuthenticatedRuntimeConstructionStarted = "authenticated-runtime-construction-started";
    public const string BusinessRepositoriesConstructed = "business-repositories-constructed";
    public const string LicensingConstructed = "licensing-constructed";
    public const string AuditRetentionStarted = "audit-retention-started";
    public const string PluginRuntimeConstructed = "plugin-runtime-constructed";
    public const string PersistenceMaintenanceConstructed = "persistence-maintenance-constructed";
    public const string AuthenticatedRuntimeConstructed = "authenticated-runtime-constructed";

    public static IReadOnlySet<string> ForbiddenBeforeAuthentication { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthenticatedRuntimeConstructionStarted,
        BusinessRepositoriesConstructed,
        LicensingConstructed,
        AuditRetentionStarted,
        PluginRuntimeConstructed,
        PersistenceMaintenanceConstructed,
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
    AuthenticatedPersistenceMaintenanceCoordinator PersistenceMaintenanceCoordinator,
    IBackgroundOperationReporter BackgroundOperations,
    string DiagnosticsSourceDirectory,
    string DiagnosticsOutputZipPath) : IAsyncDisposable
{
    internal CancellationTokenSource? AuthenticatedLifetime { get; init; }

    internal Task? AuditRetentionTask { get; init; }

    internal LicenseActivationRefreshOutboxHandler? LicenseActivationRefreshHandler { get; init; }

    internal HostOutboxDispatcher? OutboxDispatcher { get; init; }

    private Task? outboxDispatchTask;
    private int outboxPumpStarted;

    public void StartLicenseActivationRefreshDispatch(Func<string, ValueTask> refreshHandler)
    {
        ArgumentNullException.ThrowIfNull(refreshHandler);
        if (LicenseActivationRefreshHandler is null || OutboxDispatcher is null || AuthenticatedLifetime is null)
        {
            return;
        }

        LicenseActivationRefreshHandler.Register(refreshHandler);
        if (Interlocked.CompareExchange(ref outboxPumpStarted, 1, 0) == 0)
        {
            outboxDispatchTask = RunOutboxPumpAsync(OutboxDispatcher, AuthenticatedLifetime.Token);
        }
    }

    public async ValueTask DispatchPendingPostCommitEffectsAsync(CancellationToken cancellationToken = default)
    {
        if (OutboxDispatcher is not null)
        {
            await OutboxDispatcher.DispatchBatchAsync(cancellationToken).ConfigureAwait(false);
        }
    }

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

        if (outboxDispatchTask is not null)
        {
            try
            {
                await outboxDispatchTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await PluginRuntimeCoordinator.DisposeAsync().ConfigureAwait(false);
        AuthenticatedLifetime?.Dispose();
    }

    private static async Task RunOutboxPumpAsync(
        HostOutboxDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await dispatcher.DispatchBatchAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                System.Diagnostics.Trace.TraceWarning($"host.outbox.dispatch.failed:{exception.GetType().Name}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }
}
