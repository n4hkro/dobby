using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Core;
using Dobby.PluginFramework.Manifest;

namespace Dobby.PluginFramework.Packaging;

public static class PluginPackageReader
{
    public const string ManifestFileName = "dobby-plugin.manifest.json";

    public static Result<PluginPackageArchive> Read(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || (!File.Exists(packagePath) && !Directory.Exists(packagePath)))
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        try
        {
            if (Directory.Exists(packagePath))
            {
                return ReadDirectory(packagePath);
            }

            return ReadArchive(packagePath);
        }
        catch (InvalidDataException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }
        catch (JsonException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.SerializationFailed);
        }
        catch (IOException)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.IoFailed);
        }
    }

    private static Result<PluginPackageArchive> ReadArchive(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        return ReadEntries(packagePath, archive.Entries.Select(ReadEntry).ToArray());
    }

    private static Result<PluginPackageArchive> ReadDirectory(string packagePath)
    {
        var entries = Directory
            .EnumerateFiles(packagePath, "*", SearchOption.AllDirectories)
            .Select(path => ReadEntry(path, Path.GetRelativePath(packagePath, path).Replace('\\', '/')))
            .ToArray();

        return ReadEntries(packagePath, entries);
    }

    private static Result<PluginPackageArchive> ReadEntries(
        string packagePath,
        IReadOnlyList<PluginPackageEntryRecord> entries)
    {
        var manifestEntries = entries
            .Where(entry => entry.NormalizedPath.Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (manifestEntries.Length != 1)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var manifestJson = Encoding.UTF8.GetString(manifestEntries[0].Content).TrimStart('\uFEFF');
        var manifest = ParseManifest(manifestJson);
        if (!manifest.IsSuccess)
        {
            return Result.Failure<PluginPackageArchive>(ErrorCode.InvalidInput);
        }

        var archive = new PluginPackageArchive(
            packagePath,
            ManifestFileName,
            manifest.Manifest!,
            entries.Select(entry => new PluginPackageEntry(
                    entry.NormalizedPath,
                    entry.Content,
                    entry.Sha256,
                    entry.IsSymlink))
                .ToArray(),
            manifestJson);

        return Result.Success(archive);
    }

    private static PluginPackageEntryRecord ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();

        return new PluginPackageEntryRecord(
            NormalizePath(entry.FullName),
            bytes,
            Sha256(bytes),
            IsSymlink(entry));
    }

    private static PluginPackageEntryRecord ReadEntry(string path, string relativePath)
    {
        var bytes = File.ReadAllBytes(path);
        return new PluginPackageEntryRecord(
            NormalizePath(relativePath),
            bytes,
            Sha256(bytes),
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
    }

    private static PluginManifestValidationOutcome ParseManifest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return PluginManifestValidationOutcome.Failure([new PluginManifestValidationReason("invalid-manifest", "Manifest root must be an object.")]);
        }

        var schemaReasons = PluginManifestSchemaValidator.Validate(root);
        if (schemaReasons.Count != 0)
        {
            return PluginManifestValidationOutcome.Failure(schemaReasons);
        }

        var allowedRootProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "pluginId",
            "version",
            "vendorId",
            "minHostVersion",
            "entryAssembly",
            "displayName",
            "description",
            "icon",
            "dependsOn",
            "capabilities",
            "operationTargets",
            "extensionPoints",
            "files",
            "signature"
        };

        foreach (var property in root.EnumerateObject())
        {
            if (!allowedRootProperties.Contains(property.Name))
            {
                return PluginManifestValidationOutcome.Failure([
                    new PluginManifestValidationReason("invalid-manifest", $"Unexpected manifest property '{property.Name}'.", property.Name)
                ]);
            }
        }

        try
        {
            var pluginId = GetString(root, "pluginId");
            var version = GetString(root, "version");
            var vendorId = GetString(root, "vendorId");
            var minHostVersion = GetString(root, "minHostVersion");
            var entryAssembly = GetString(root, "entryAssembly");
            var displayName = GetString(root, "displayName");
            var description = GetString(root, "description");
            var icon = TryGetProperty(root, "icon", out var iconElement)
                ? ParseIcon(iconElement)
                : null;
            var dependsOn = GetArray(root, "dependsOn", ParseDependency);
            var capabilities = GetArray(root, "capabilities", ParseCapability);
            var operationTargets = GetArray(root, "operationTargets", ParseOperationTarget);
            var extensionPoints = GetArray(root, "extensionPoints", ParseExtensionPoint);
            var files = GetArray(root, "files", ParseFile);
            var signature = ParseSignature(GetObject(root, "signature"));

            return PluginManifestValidationOutcome.Success(new PluginManifest(
                pluginId,
                vendorId,
                version,
                minHostVersion,
                entryAssembly,
                displayName,
                description,
                icon,
                dependsOn,
                capabilities,
                operationTargets,
                extensionPoints,
                files,
                signature));
        }
        catch (InvalidOperationException exception)
        {
            return PluginManifestValidationOutcome.Failure([
                new PluginManifestValidationReason("invalid-manifest", exception.Message)
            ]);
        }
    }

    private static PluginManifestDependency ParseDependency(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "pluginId", "minVersion");
        return new PluginManifestDependency(
            GetString(objectElement, "pluginId"),
            GetString(objectElement, "minVersion"));
    }

    private static PluginCapability ParseCapability(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "name", "scope");
        return new PluginCapability(
            GetString(objectElement, "name"),
            GetString(objectElement, "scope"));
    }

    private static PluginOperationTarget ParseOperationTarget(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "id", "verbs", "labelKey");
        return new PluginOperationTarget(
            GetString(objectElement, "id"),
            GetStringArray(objectElement, "verbs"),
            GetString(objectElement, "labelKey"));
    }

    private static PluginExtensionPoint ParseExtensionPoint(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "id", "target", "routeKey", "labelKey", "parentId", "group", "weight");
        return new PluginExtensionPoint(
            GetString(objectElement, "id"),
            GetString(objectElement, "target"),
            GetString(objectElement, "routeKey"),
            TryGetOptionalString(objectElement, "labelKey"),
            TryGetOptionalString(objectElement, "parentId"),
            TryGetOptionalString(objectElement, "group"),
            TryGetOptionalInt(objectElement, "weight"));
    }

    private static PluginPackageFile ParseFile(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "path", "sha256");
        return new PluginPackageFile(
            GetString(objectElement, "path"),
            GetString(objectElement, "sha256"));
    }

    private static PluginIcon ParseIcon(JsonElement element)
    {
        var objectElement = GetObject(element);
        EnsureOnlyProperties(objectElement, "path");
        return new PluginIcon(GetString(objectElement, "path"));
    }

    private static PluginPackageSignature ParseSignature(JsonElement element)
    {
        EnsureOnlyProperties(element, "algorithm", "keyVersion", "value");
        return new PluginPackageSignature(
            GetString(element, "algorithm"),
            GetInt(element, "keyVersion"),
            GetString(element, "value"));
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static JsonElement GetObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return GetObject(element);
    }

    private static JsonElement GetObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Manifest object value is malformed.");
        }

        return element;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.GetString() ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
    }

    private static string? TryGetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Manifest property '{propertyName}' must be a string when present.");
        }

        return value.GetString();
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.GetInt32();
    }

    private static int? TryGetOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException($"Manifest property '{propertyName}' must be an integer when present.");
        }

        return value.GetInt32();
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.EnumerateArray()
            .Select(item => item.GetString() ?? throw new InvalidOperationException($"Manifest property '{propertyName}' is required."))
            .ToArray();
    }

    private static IReadOnlyList<T> GetArray<T>(JsonElement element, string propertyName, Func<JsonElement, T> factory)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Manifest property '{propertyName}' is required.");
        }

        return value.EnumerateArray().Select(factory).ToArray();
    }

    private static void EnsureOnlyProperties(JsonElement element, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedSet.Contains(property.Name))
            {
                throw new InvalidOperationException($"Manifest contains unexpected property '{property.Name}'.");
            }
        }
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/').Normalize(NormalizationForm.FormC);
    }

    private static bool IsSymlink(ZipArchiveEntry entry)
    {
        var externalAttributes = entry.ExternalAttributes >> 16;
        var fileType = externalAttributes & 0xF000;
        return fileType == 0xA000;
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record PluginPackageEntryRecord(
        string NormalizedPath,
        byte[] Content,
        string Sha256,
        bool IsSymlink);
}
