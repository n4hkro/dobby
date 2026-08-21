namespace Dobby.Licensing;

public sealed record LicenseImportResult(
    bool IsSuccess,
    string ReasonCode,
    string SupportCode,
    LicenseLifecycleState LifecycleState,
    string PluginId = "",
    bool AuditCommitted = false)
{
    public static LicenseImportResult Success(
        LicenseLifecycleState lifecycleState,
        string pluginId,
        bool auditCommitted = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return new LicenseImportResult(true, string.Empty, string.Empty, lifecycleState, pluginId, auditCommitted);
    }

    public static LicenseImportResult Failure(string reasonCode, string supportCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(supportCode);
        return new LicenseImportResult(false, reasonCode, supportCode, LicenseLifecycleState.Denied);
    }
}
