using Dobby.Contracts.Licensing;
using Dobby.Core;

namespace Dobby.Licensing;

public sealed class PluginCapabilityLicensing : ICapabilityLicensing
{
    private readonly Func<Result<PluginRuntimeMode>> currentModeProvider;

    public PluginCapabilityLicensing(Func<Result<PluginRuntimeMode>> currentModeProvider)
    {
        this.currentModeProvider = currentModeProvider ?? throw new ArgumentNullException(nameof(currentModeProvider));
    }

    public PluginCapabilityLicensing(
        string pluginId,
        string packageFingerprint,
        ILicenseRuntimeModeProvider runtimeModeProvider)
        : this(() => runtimeModeProvider.GetCurrentMode(pluginId, packageFingerprint))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageFingerprint);
        ArgumentNullException.ThrowIfNull(runtimeModeProvider);
    }

    public Result<PluginRuntimeMode> GetCurrentMode()
    {
        return currentModeProvider();
    }
}
