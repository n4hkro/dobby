using Dobby.Contracts.Licensing;
using Dobby.Core;

namespace Dobby.Host.Plugins;

public sealed class HostRuntimeModeMutationGate
{
    private readonly Func<string, Result<PluginRuntimeMode>> runtimeModeProvider;

    public HostRuntimeModeMutationGate(Func<string, Result<PluginRuntimeMode>> runtimeModeProvider)
    {
        this.runtimeModeProvider = runtimeModeProvider ?? throw new ArgumentNullException(nameof(runtimeModeProvider));
    }

    public Result EnsurePluginMutationAllowed(string pluginId)
    {
        return EnsureAllowed(pluginId, PluginRuntimeAction.PluginAction);
    }

    public Result EnsurePluginViewAllowed(string pluginId)
    {
        return EnsureAllowed(pluginId, PluginRuntimeAction.View);
    }

    public Result EnsurePluginStorageReadAllowed(string pluginId)
    {
        return EnsureAllowed(pluginId, PluginRuntimeAction.StorageRead);
    }

    public Result EnsurePluginBackgroundStartAllowed(string pluginId)
    {
        return EnsureAllowed(pluginId, PluginRuntimeAction.BackgroundStart);
    }

    public Result EnsurePackageInstallAllowed(string pluginId, bool isUpdate = false)
    {
        return EnsureAllowed(
            pluginId,
            isUpdate ? PluginRuntimeAction.PackageUpdate : PluginRuntimeAction.InitialPackageInstall);
    }

    public Result EnsurePluginLifecycleMutationAllowed(string pluginId)
    {
        return EnsureAllowed(pluginId, PluginRuntimeAction.EnableDisableUninstall);
    }

    public Result EnsureDomainExportAllowed(string pluginId)
    {
        return EnsureAllowed(pluginId, PluginRuntimeAction.DomainExport);
    }

    private Result EnsureAllowed(string pluginId, PluginRuntimeAction action)
    {
        var runtimeMode = ResolveRuntimeMode(pluginId);
        if (!runtimeMode.IsSuccess)
        {
            return MapProviderFailure(runtimeMode.ErrorCode);
        }

        return PluginRuntimeActionPolicy.Evaluate(runtimeMode.Value, action) switch
        {
            PluginRuntimeActionDecision.Allowed => Result.Success(),
            _ when runtimeMode.Value == PluginRuntimeMode.ReadOnlyExpired => Result.Failure(ErrorCode.LicenseReadOnly),
            _ => Result.Failure(ErrorCode.LicenseDenied)
        };
    }

    private Result<PluginRuntimeMode> ResolveRuntimeMode(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return runtimeModeProvider(pluginId);
    }

    private static Result MapProviderFailure(ErrorCode? errorCode)
    {
        return errorCode == ErrorCode.LicenseBindingMismatch
            ? Result.Failure(ErrorCode.LicenseBindingMismatch)
            : Result.Failure(ErrorCode.LicenseDenied);
    }
}
