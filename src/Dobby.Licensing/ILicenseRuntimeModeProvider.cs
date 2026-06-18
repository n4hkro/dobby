using Dobby.Contracts.Licensing;
using Dobby.Core;

namespace Dobby.Licensing;

public interface ILicenseRuntimeModeProvider
{
    Result<PluginRuntimeMode> GetCurrentMode(string pluginId, string packageFingerprint);
}
