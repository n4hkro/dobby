using Dobby.PluginFramework.Manifest;

namespace Dobby.PluginFramework.Packaging;

public sealed record PluginPackageArchive(
    string PackagePath,
    string ManifestPath,
    PluginManifest Manifest,
    IReadOnlyList<PluginPackageEntry> Entries,
    string ManifestJson);

public sealed record PluginPackageEntry(
    string Path,
    byte[] Content,
    string Sha256,
    bool IsSymlink);

public sealed record PluginPackageDescriptor(
    string PackagePath,
    string Fingerprint,
    byte[] CanonicalPayload,
    PluginManifest Manifest,
    IReadOnlyList<PluginPackageEntry> Entries,
    string ManifestPath,
    string ManifestJson);

public sealed record PluginPackageValidationOutcome(
    bool IsSuccess,
    PluginPackageDescriptor? Descriptor,
    IReadOnlyList<PluginPackageValidationReason> Reasons)
{
    public static PluginPackageValidationOutcome Success(PluginPackageDescriptor descriptor)
    {
        return new PluginPackageValidationOutcome(true, descriptor, []);
    }

    public static PluginPackageValidationOutcome Failure(IReadOnlyList<PluginPackageValidationReason> reasons)
    {
        return new PluginPackageValidationOutcome(false, null, reasons);
    }
}
