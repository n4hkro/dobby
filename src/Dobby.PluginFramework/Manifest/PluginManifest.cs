namespace Dobby.PluginFramework.Manifest;

public sealed record PluginManifest(
    string PluginId,
    string VendorId,
    string Version,
    string MinHostVersion,
    string EntryAssembly,
    string DisplayName,
    string Description,
    PluginIcon? Icon,
    IReadOnlyList<PluginManifestDependency> DependsOn,
    IReadOnlyList<PluginCapability> Capabilities,
    IReadOnlyList<PluginOperationTarget> OperationTargets,
    IReadOnlyList<PluginExtensionPoint> ExtensionPoints,
    IReadOnlyList<PluginPackageFile> Files,
    PluginPackageSignature Signature)
{
    public int SchemaVersion { get; init; } = PluginManifestContract.CurrentSchemaVersion;

    public string Purpose { get; init; } = PluginManifestContract.PackagePurpose;

    public string HostApiVersion { get; init; } = "2.0";

    public IReadOnlyList<string> NativeLibraries { get; init; } = [];
}

public sealed record PluginCapability(string Name, string Scope);

public sealed record PluginIcon(string Path);

public sealed record PluginOperationTarget(
    string Id,
    IReadOnlyList<string> Verbs,
    string LabelKey);

public sealed record PluginExtensionPoint(
    string Id,
    string Target,
    string RouteKey,
    string? LabelKey,
    string? ParentId,
    string? Group,
    int? Weight);

public sealed record PluginManifestDependency(string PluginId, string MinVersion);

public sealed record PluginPackageFile(string Path, string Sha256);

public sealed record PluginPackageSignature(
    string Purpose,
    string Algorithm,
    string Format,
    string KeyId,
    string Value);
