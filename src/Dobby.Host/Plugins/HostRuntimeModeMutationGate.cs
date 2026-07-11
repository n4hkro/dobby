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
        var runtimeMode = ResolveRuntimeMode(pluginId);
        if (!runtimeMode.IsSuccess)
        {
            return MapProviderFailure(runtimeMode.ErrorCode);
        }

        return runtimeMode.Value switch
        {
            PluginRuntimeMode.FullAccess => Result.Success(),
            PluginRuntimeMode.ReadOnlyExpired => Result.Failure(ErrorCode.LicenseReadOnly),
            _ => Result.Failure(ErrorCode.LicenseDenied)
        };
    }

    public Result EnsurePackageInstallAllowed(string pluginId)
    {
        var runtimeMode = ResolveRuntimeMode(pluginId);
        if (!runtimeMode.IsSuccess)
        {
            return MapProviderFailure(runtimeMode.ErrorCode);
        }

        // A new package can have no license yet and become Denied after install.
        return runtimeMode.Value == PluginRuntimeMode.ReadOnlyExpired
            ? Result.Failure(ErrorCode.LicenseReadOnly)
            : Result.Success();
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
