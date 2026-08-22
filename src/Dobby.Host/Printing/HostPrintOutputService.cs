using Dobby.Application.Printing;
using Dobby.Core;
using Dobby.Licensing;
using Dobby.Contracts.Licensing;

namespace Dobby.Host.Printing;

[PluginRuntimeActionOwner(PluginRuntimeAction.PhysicalPrint, PluginRuntimeAction.VirtualOrFileOutput)]
public sealed class HostPrintOutputService(
    ILicenseRuntimeModeProvider runtimeModeProvider,
    HostPrintOutputCoordinator printOutputCoordinator)
{
    public async ValueTask<Result> ExecuteAsync(
        string pluginId,
        string packageFingerprint,
        PrintTargetDescriptor target,
        bool isPrintToFile,
        Func<CancellationToken, ValueTask<Result>> execute,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFingerprint);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(execute);

        var runtimeMode = runtimeModeProvider.GetCurrentMode(pluginId, packageFingerprint);
        if (!runtimeMode.IsSuccess)
        {
            return Result.Failure(runtimeMode.ErrorCode ?? ErrorCode.LicenseDenied);
        }

        return await printOutputCoordinator.ExecuteAsync(
            runtimeMode.Value,
            target,
            isPrintToFile,
            execute,
            cancellationToken).ConfigureAwait(false);
    }
}
