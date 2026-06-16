using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.PluginFramework.Manifest;

namespace Dobby.PluginFramework.Packaging;

public static class PluginPackageFingerprint
{
    public static PluginPackageFingerprintResult Compute(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonicalPayload(writer, manifest);
        }

        var canonicalPayload = stream.ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(canonicalPayload)).ToLowerInvariant();
        return new PluginPackageFingerprintResult(canonicalPayload, fingerprint);
    }

    private static void WriteCanonicalPayload(Utf8JsonWriter writer, PluginManifest manifest)
    {
        writer.WriteStartObject();
        WriteString(writer, "pluginId", manifest.PluginId);
        WriteString(writer, "vendorId", manifest.VendorId);
        WriteString(writer, "version", manifest.Version);
        WriteString(writer, "minHostVersion", manifest.MinHostVersion);
        WriteString(writer, "entryAssembly", Normalize(manifest.EntryAssembly));
        WriteString(writer, "displayName", Normalize(manifest.DisplayName));
        WriteString(writer, "description", Normalize(manifest.Description));

        if (manifest.Icon is not null)
        {
            writer.WritePropertyName("icon");
            writer.WriteStartObject();
            WriteString(writer, "path", Normalize(manifest.Icon.Path));
            writer.WriteEndObject();
        }

        WriteDependencies(writer, manifest.DependsOn);
        WriteCapabilities(writer, manifest.Capabilities);
        WriteOperationTargets(writer, manifest.OperationTargets);
        WriteExtensionPoints(writer, manifest.ExtensionPoints);
        WriteFiles(writer, manifest.Files);
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void WriteDependencies(Utf8JsonWriter writer, IReadOnlyList<PluginManifestDependency> dependencies)
    {
        writer.WritePropertyName("dependsOn");
        writer.WriteStartArray();
        foreach (var dependency in dependencies)
        {
            writer.WriteStartObject();
            WriteString(writer, "pluginId", dependency.PluginId);
            WriteString(writer, "minVersion", dependency.MinVersion);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteCapabilities(Utf8JsonWriter writer, IReadOnlyList<PluginCapability> capabilities)
    {
        writer.WritePropertyName("capabilities");
        writer.WriteStartArray();
        foreach (var capability in capabilities)
        {
            writer.WriteStartObject();
            WriteString(writer, "name", capability.Name);
            WriteString(writer, "scope", capability.Scope);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteOperationTargets(Utf8JsonWriter writer, IReadOnlyList<PluginOperationTarget> targets)
    {
        writer.WritePropertyName("operationTargets");
        writer.WriteStartArray();
        foreach (var target in targets)
        {
            writer.WriteStartObject();
            WriteString(writer, "id", target.Id);
            writer.WritePropertyName("verbs");
            writer.WriteStartArray();
            foreach (var verb in target.Verbs)
            {
                writer.WriteStringValue(verb);
            }

            writer.WriteEndArray();
            WriteString(writer, "labelKey", target.LabelKey);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteExtensionPoints(Utf8JsonWriter writer, IReadOnlyList<PluginExtensionPoint> extensionPoints)
    {
        writer.WritePropertyName("extensionPoints");
        writer.WriteStartArray();
        foreach (var extensionPoint in extensionPoints)
        {
            writer.WriteStartObject();
            WriteString(writer, "id", extensionPoint.Id);
            WriteString(writer, "target", extensionPoint.Target);
            WriteString(writer, "routeKey", extensionPoint.RouteKey);

            if (extensionPoint.LabelKey is not null)
            {
                WriteString(writer, "labelKey", extensionPoint.LabelKey);
            }

            if (extensionPoint.ParentId is not null)
            {
                WriteString(writer, "parentId", extensionPoint.ParentId);
            }

            if (extensionPoint.Group is not null)
            {
                WriteString(writer, "group", extensionPoint.Group);
            }

            if (extensionPoint.Weight is not null)
            {
                writer.WriteNumber("weight", extensionPoint.Weight.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteFiles(Utf8JsonWriter writer, IReadOnlyList<PluginPackageFile> files)
    {
        writer.WritePropertyName("files");
        writer.WriteStartArray();
        foreach (var file in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            WriteString(writer, "path", Normalize(file.Path));
            WriteString(writer, "sha256", file.Sha256);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteString(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteString(name, Normalize(value));
    }

    private static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormC);
    }
}

public sealed record PluginPackageFingerprintResult(
    byte[] CanonicalPayload,
    string Fingerprint);
