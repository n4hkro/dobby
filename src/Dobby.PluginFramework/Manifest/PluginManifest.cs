namespace Dobby.PluginFramework.Manifest;

public sealed record PluginManifest(
    int SchemaVersion,
    string PluginId,
    string Version,
    string MinHostVersion,
    IReadOnlyList<PluginCapability> Capabilities,
    IReadOnlyList<PluginOperationTarget> OperationTargets,
    IReadOnlyList<PluginExtensionPoint> ExtensionPoints,
    IReadOnlyList<PluginManifestDependency> DependsOn);

public sealed record PluginCapability(string Scope);

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
