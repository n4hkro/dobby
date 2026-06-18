namespace Dobby.Licensing;

public sealed record LicenseImportRequest(
    string FileName,
    byte[] ArtifactBytes,
    Func<string> CurrentDeviceFingerprintProvider,
    Func<string, string> InstalledPackageFingerprintResolver,
    bool PermissionGranted,
    string CorrelationId,
    DateTimeOffset ImportedAtUtc);
