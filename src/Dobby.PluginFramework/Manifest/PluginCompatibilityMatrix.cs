using Dobby.Core.Versioning;

namespace Dobby.PluginFramework.Manifest;

public sealed record PluginCompatibilityDecision(bool IsCompatible, string ReasonCode)
{
    public static PluginCompatibilityDecision Compatible { get; } = new(true, string.Empty);

    public static PluginCompatibilityDecision Reject(string reasonCode) => new(false, reasonCode);
}

public static class PluginCompatibilityMatrix
{
    public const int CurrentPackageFormatVersion = 2;
    public const int SupportedHostApiMajor = 2;
    public const int SupportedHostApiMinor = 0;

    public static PluginCompatibilityDecision Evaluate(
        PluginManifest manifest,
        HostVersion installedHostVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion != PluginManifestContract.CurrentSchemaVersion)
        {
            return PluginCompatibilityDecision.Reject("unsupported-manifest-version");
        }

        if (PackageFormatVersionForSchema(manifest.SchemaVersion) != CurrentPackageFormatVersion)
        {
            return PluginCompatibilityDecision.Reject("unsupported-package-format-version");
        }

        if (!TryParseApiVersion(manifest.HostApiVersion, out var apiMajor, out var apiMinor))
        {
            return PluginCompatibilityDecision.Reject("invalid-host-api-version");
        }

        if (apiMajor != SupportedHostApiMajor)
        {
            return PluginCompatibilityDecision.Reject("unsupported-host-api-major");
        }

        if (apiMinor > SupportedHostApiMinor)
        {
            return PluginCompatibilityDecision.Reject("unsupported-host-api-minor");
        }

        if (!HostVersion.TryParse(manifest.MinHostVersion, out var minimumHostVersion))
        {
            return PluginCompatibilityDecision.Reject("invalid-minimum-host-version");
        }

        return installedHostVersion.CompareTo(minimumHostVersion) < 0
            ? PluginCompatibilityDecision.Reject("host-version-below-minimum")
            : PluginCompatibilityDecision.Compatible;
    }

    public static int PackageFormatVersionForSchema(int manifestSchemaVersion) =>
        manifestSchemaVersion switch
        {
            PluginManifestContract.CurrentSchemaVersion => CurrentPackageFormatVersion,
            _ => 0
        };

    private static bool TryParseApiVersion(string value, out int major, out int minor)
    {
        major = 0;
        minor = 0;
        var parts = value.Split('.');
        return parts.Length == 2 &&
            int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out major) &&
            int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out minor);
    }
}
