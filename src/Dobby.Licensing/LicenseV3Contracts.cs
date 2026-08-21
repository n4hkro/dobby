namespace Dobby.Licensing;

public static class LicenseV3Contract
{
    public const int SchemaVersion = 3;
    public const string Purpose = "dobby.plugin-license";
    public const string SignatureAlgorithm = "ECDSA-P256-SHA256";
    public const string SignatureFormat = "IeeeP1363FixedFieldConcatenation";
    public const int MaximumArtifactBytes = 256 * 1024;
    public const int MaximumIdentifierLength = 128;
    public const int MaximumGracePeriodDays = 365;
}

public enum LicenseGrantIntentV3
{
    Granted,
    Revoked
}

public sealed record LicensePayloadV3(
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
    LicenseGrantIntentV3 Intent);

public sealed record LicenseSignatureBlockV3(
    string Purpose,
    string Algorithm,
    string Format,
    string KeyId,
    string Value);

public sealed record LicenseArtifactV3(
    LicensePayloadV3 Payload,
    LicenseSignatureBlockV3 Signature);
