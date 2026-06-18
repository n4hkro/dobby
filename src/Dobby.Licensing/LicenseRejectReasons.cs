namespace Dobby.Licensing;

public static class LicenseRejectReasons
{
    public const string InvalidLicenseFile = "invalid-license-file";
    public const string InvalidSignature = "invalid-signature";
    public const string UntrustedVendor = "untrusted-vendor";
    public const string BindingMismatch = "binding-mismatch";
    public const string PackageMismatch = "package-mismatch";
    public const string OlderRevision = "older-revision";
    public const string PermissionDenied = "permission-denied";
    public const string Cancelled = "cancelled";
}
