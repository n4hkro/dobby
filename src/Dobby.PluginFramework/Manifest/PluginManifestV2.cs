namespace Dobby.PluginFramework.Manifest;

public static class PluginManifestContract
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;
    public const string PackagePurpose = "dobby.plugin-package";
    public const string SignatureAlgorithm = "ECDSA-P256-SHA256";
    public const string SignatureFormat = "IeeeP1363FixedFieldConcatenation";
    public const int MaximumFiles = 256;
}

public sealed record PluginManifestV2(
    int SchemaVersion,
    string Purpose,
    string PluginId,
    string Version,
    string VendorId,
    string HostApiVersion,
    string MinHostVersion,
    string EntryAssembly,
    string DisplayName,
    string Description,
    PluginIcon? Icon,
    IReadOnlyList<string> NativeLibraries,
    IReadOnlyList<PluginManifestDependency> DependsOn,
    IReadOnlyList<PluginCapability> Capabilities,
    IReadOnlyList<PluginOperationTarget> OperationTargets,
    IReadOnlyList<PluginExtensionPoint> ExtensionPoints,
    IReadOnlyList<PluginPackageFile> Files,
    PluginPackageSignatureV2 Signature);

public sealed record PluginPackageSignatureV2(
    string Purpose,
    string Algorithm,
    string Format,
    string KeyId,
    string Value);
