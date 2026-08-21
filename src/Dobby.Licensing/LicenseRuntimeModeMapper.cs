using Dobby.Contracts.Licensing;

namespace Dobby.Licensing;

public enum LicenseArtifactStatus
{
    Active,
    Pending,
    ReadOnly,
    Denied
}

public static class LicenseRuntimeModeMapper
{
    public static PluginRuntimeMode Map(LicenseLifecycleState state)
    {
        return state switch
        {
            LicenseLifecycleState.Active => PluginRuntimeMode.FullAccess,
            LicenseLifecycleState.Grace => PluginRuntimeMode.FullAccess,
            LicenseLifecycleState.ReadOnlyExpired => PluginRuntimeMode.ReadOnlyExpired,
            LicenseLifecycleState.NoLicense => PluginRuntimeMode.Denied,
            LicenseLifecycleState.Pending => PluginRuntimeMode.Denied,
            LicenseLifecycleState.Denied => PluginRuntimeMode.Denied,
            _ => PluginRuntimeMode.Denied
        };
    }

    public static PluginRuntimeMode Map(LicenseArtifactStatus status)
    {
        return status switch
        {
            LicenseArtifactStatus.Active => PluginRuntimeMode.FullAccess,
            LicenseArtifactStatus.Pending => PluginRuntimeMode.Denied,
            LicenseArtifactStatus.ReadOnly => PluginRuntimeMode.ReadOnlyExpired,
            LicenseArtifactStatus.Denied => PluginRuntimeMode.Denied,
            _ => PluginRuntimeMode.Denied
        };
    }
}
