namespace Dobby.Licensing;

public sealed record LicenseImportResult(
    bool IsSuccess,
    string ReasonCode,
    string SupportCode,
    LicenseLifecycleState LifecycleState,
    string PluginId = "")
{
    public static LicenseImportResult Success(LicenseLifecycleState lifecycleState, string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return new LicenseImportResult(true, string.Empty, string.Empty, lifecycleState, pluginId);
    }

    public static LicenseImportResult Failure(string reasonCode, string supportCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(supportCode);
        return new LicenseImportResult(false, reasonCode, supportCode, LicenseLifecycleState.Denied);
    }
}
