using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;

namespace Dobby.UnitTests.PluginFramework.Packaging;

internal static class PluginPackageTestData
{
    public const string PluginId = "com.dobby.sample";
    public const string VendorId = "dobby.vendor";
    public const string Version = "1.0.0";
    public const string MinHostVersion = "1.0.0";
    public const string EntryAssembly = "bin/plugins/com.dobby.sample.dll";
    public const string DisplayName = "Sample Plugin";
    public const string Description = "Sample plugin description";
    public const string FilePath = "content/hello.txt";
    public const string AlternateFilePath = "Content/hello.txt";

    public static readonly byte[] EntryAssemblyContent = Encoding.UTF8.GetBytes("sample plugin assembly");
    public static readonly byte[] FileContent = Encoding.UTF8.GetBytes("hello plugin");

    public static string EntryAssemblyHash => HashHex(EntryAssemblyContent);

    public static string FileHash => HashHex(FileContent);

    public static PluginManifest CreateManifest(
        IReadOnlyList<PluginManifestDependency>? dependsOn = null,
        IReadOnlyList<PluginCapability>? capabilities = null,
        IReadOnlyList<PluginOperationTarget>? operationTargets = null,
        IReadOnlyList<PluginExtensionPoint>? extensionPoints = null,
        IReadOnlyList<PluginPackageFile>? files = null,
        PluginPackageSignature? signature = null,
        PluginIcon? icon = null,
        string? pluginId = null,
        string? vendorId = null,
        string? version = null,
        string? minHostVersion = null,
        string? entryAssembly = null,
        string? displayName = null,
        string? description = null)
    {
        return new PluginManifest(
            pluginId ?? PluginId,
            vendorId ?? VendorId,
            version ?? Version,
            minHostVersion ?? MinHostVersion,
            entryAssembly ?? EntryAssembly,
            displayName ?? DisplayName,
            description ?? Description,
            icon,
            dependsOn ?? [],
            capabilities ?? [new PluginCapability("plugin-page", "plugin-page")],
            operationTargets ?? [new PluginOperationTarget($"{PluginId}.greet", ["execute"], "sample.greet.label")],
            extensionPoints ?? [new PluginExtensionPoint("sample.home", "plugin-page-region", "sample.home", "sample.home.label", null, "default", 100)],
            files ?? DefaultFiles(),
            signature ?? new PluginPackageSignature("ECDSA-P256-SHA256", 1, "AQ"));
    }

    public static string CreateManifestJson(
        bool includeUnexpectedRootProperty = false,
        IReadOnlyList<PluginPackageFile>? files = null,
        IReadOnlyList<PluginManifestDependency>? dependsOn = null,
        IReadOnlyList<PluginCapability>? capabilities = null,
        IReadOnlyList<PluginOperationTarget>? operationTargets = null,
        IReadOnlyList<PluginExtensionPoint>? extensionPoints = null,
        PluginPackageSignature? signature = null,
        PluginIcon? icon = null,
        string? pluginId = null,
        string? vendorId = null,
        string? version = null,
        string? minHostVersion = null,
        string? entryAssembly = null,
        string? displayName = null,
        string? description = null)
    {
        var root = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["pluginId"] = pluginId ?? PluginId,
            ["version"] = version ?? Version,
            ["vendorId"] = vendorId ?? VendorId,
            ["minHostVersion"] = minHostVersion ?? MinHostVersion,
            ["entryAssembly"] = entryAssembly ?? EntryAssembly,
            ["displayName"] = displayName ?? DisplayName,
            ["description"] = description ?? Description,
            ["dependsOn"] = (dependsOn ?? []).Select(dependency => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pluginId"] = dependency.PluginId,
                ["minVersion"] = dependency.MinVersion
            }).ToArray(),
            ["capabilities"] = (capabilities ?? [new PluginCapability("plugin-page", "plugin-page")]).Select(capability => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = capability.Name,
                ["scope"] = capability.Scope
            }).ToArray(),
            ["operationTargets"] = (operationTargets ?? [new PluginOperationTarget($"{PluginId}.greet", ["execute"], "sample.greet.label")]).Select(target => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = target.Id,
                ["verbs"] = target.Verbs.ToArray(),
                ["labelKey"] = target.LabelKey
            }).ToArray(),
            ["extensionPoints"] = (extensionPoints ?? [new PluginExtensionPoint("sample.home", "plugin-page-region", "sample.home", "sample.home.label", null, "default", 100)])
                .Select(ExtensionPointJson)
                .ToArray(),
            ["files"] = (files ?? DefaultFiles()).Select(file => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = file.Path,
                ["sha256"] = file.Sha256
            }).ToArray(),
            ["signature"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["algorithm"] = signature?.Algorithm ?? "ECDSA-P256-SHA256",
                ["keyVersion"] = signature?.KeyVersion ?? 1,
                ["value"] = signature?.Value ?? "AQ"
            }
        };

        if (includeUnexpectedRootProperty)
        {
            root["schemaVersion"] = 1;
        }

        return JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static Dictionary<string, object?> ExtensionPointJson(PluginExtensionPoint extensionPoint)
    {
        var json = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = extensionPoint.Id,
            ["target"] = extensionPoint.Target,
            ["routeKey"] = extensionPoint.RouteKey
        };

        if (extensionPoint.LabelKey is not null)
        {
            json["labelKey"] = extensionPoint.LabelKey;
        }

        if (extensionPoint.ParentId is not null)
        {
            json["parentId"] = extensionPoint.ParentId;
        }

        if (extensionPoint.Group is not null)
        {
            json["group"] = extensionPoint.Group;
        }

        if (extensionPoint.Weight is not null)
        {
            json["weight"] = extensionPoint.Weight;
        }

        return json;
    }

    public static PluginPackageArchive CreateArchive(
        PluginManifest manifest,
        IReadOnlyList<PluginPackageEntry>? entries = null,
        string? packagePath = null,
        string? manifestJson = null)
    {
        return new PluginPackageArchive(
            packagePath ?? "test.dobbyplugin",
            PluginPackageReader.ManifestFileName,
            manifest,
            entries ?? [new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson ?? CreateManifestJson()), HashHex(Encoding.UTF8.GetBytes(manifestJson ?? CreateManifestJson())), false),
                new PluginPackageEntry(EntryAssembly, EntryAssemblyContent, EntryAssemblyHash, false),
                new PluginPackageEntry(FilePath, FileContent, FileHash, false)],
            manifestJson ?? CreateManifestJson());
    }

    public static IReadOnlyList<PluginPackageFile> DefaultFiles()
    {
        return
        [
            new PluginPackageFile(EntryAssembly, EntryAssemblyHash),
            new PluginPackageFile(FilePath, FileHash)
        ];
    }

    public static string HashHex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string HashHex(string text)
    {
        return HashHex(Encoding.UTF8.GetBytes(text));
    }
}
