using Dobby.Core;

namespace Dobby.Contracts.Licensing;

public enum PluginRuntimeMode
{
    FullAccess,
    ReadOnlyExpired,
    Denied
}

public interface ICapabilityLicensing
{
    Result<PluginRuntimeMode> GetCurrentMode();
}
