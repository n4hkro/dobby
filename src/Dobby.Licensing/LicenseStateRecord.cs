namespace Dobby.Licensing;

public sealed record LicenseStateRecord(
    RevisionSeriesKey RevisionSeriesKey,
    string PluginId,
    string CustomerId,
    string DeploymentId,
    string DeviceFingerprint,
    string PackageFingerprint,
    int LicenseRevision,
    LicenseLifecycleState LifecycleState,
    DateTimeOffset ImportedAtUtc);
