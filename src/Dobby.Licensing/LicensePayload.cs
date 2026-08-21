namespace Dobby.Licensing;

public enum LicenseGrantIntent
{
    Granted,
    Revoked
}

public sealed record LicensePayload(
    int SchemaVersion,
    string Purpose,
    string KeyId,
    string LicenseId,
    string RevisionSeriesId,
    int LicenseRevision,
    string PluginId,
    string CustomerId,
    string DeploymentId,
    string DeviceKeyHash,
    string PackageFingerprint,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    int GracePeriodDays,
    LicenseGrantIntent Intent);
