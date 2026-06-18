namespace Dobby.Licensing;

public sealed record LicensePayload(
    int SchemaVersion,
    string LicenseId,
    string PluginId,
    string CustomerId,
    string DeploymentId,
    string DeviceFingerprintAlgorithm,
    string DeviceFingerprint,
    string PackageFingerprint,
    int LicenseRevision,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset ExpiresAt,
    int GracePeriodDays,
    LicenseArtifactStatus Status,
    int KeyVersion);
