using Dobby.Configuration;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Host.Settings;
using Dobby.Host.Plugins;
using Dobby.Host.Sessions;
using Dobby.Security.Authentication;
using Dobby.Security.Authorization;

namespace Dobby.Host.Login;

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
    HostPluginRuntimeCoordinator PluginRuntimeCoordinator,
    IBackgroundOperationReporter BackgroundOperations,
    string DiagnosticsSourceDirectory,
    string DiagnosticsOutputZipPath) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        PostLoginSessionRuntime.Dispose();
        await PluginRuntimeCoordinator.DisposeAsync().ConfigureAwait(false);
    }
}
