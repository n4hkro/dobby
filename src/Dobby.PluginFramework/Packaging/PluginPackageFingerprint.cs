using System.Security.Cryptography;
using System.Text;
using Dobby.PluginFramework.Manifest;
using Dobby.Security.Trust;

namespace Dobby.PluginFramework.Packaging;

public static class PluginPackageFingerprint
{
    public static PluginPackageFingerprintResult Compute(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var unsignedManifest = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = manifest.SchemaVersion,
            ["purpose"] = Normalize(manifest.Purpose),
            ["pluginId"] = Normalize(manifest.PluginId),
            ["version"] = manifest.Version,
            ["vendorId"] = Normalize(manifest.VendorId),
            ["hostApiVersion"] = manifest.HostApiVersion,
            ["minHostVersion"] = manifest.MinHostVersion,
            ["entryAssembly"] = Normalize(manifest.EntryAssembly),
            ["displayName"] = Normalize(manifest.DisplayName),
            ["description"] = Normalize(manifest.Description),
            ["nativeLibraries"] = manifest.NativeLibraries.Select(Normalize).ToArray(),
            ["dependsOn"] = manifest.DependsOn.Select(dependency => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pluginId"] = Normalize(dependency.PluginId),
                ["minVersion"] = dependency.MinVersion
            }).ToArray(),
            ["capabilities"] = manifest.Capabilities.Select(capability => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = Normalize(capability.Name),
                ["scope"] = capability.Scope
            }).ToArray(),
            ["operationTargets"] = manifest.OperationTargets.Select(target => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = Normalize(target.Id),
                ["verbs"] = target.Verbs.ToArray(),
                ["labelKey"] = Normalize(target.LabelKey)
            }).ToArray(),
            ["extensionPoints"] = manifest.ExtensionPoints.Select(ExtensionPoint).ToArray(),
            ["files"] = manifest.Files
                .OrderBy(file => file.Path, StringComparer.Ordinal)
                .Select(file => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = Normalize(file.Path),
                    ["sha256"] = file.Sha256
                }).ToArray()
        };

        if (manifest.Icon is not null)
        {
            unsignedManifest["icon"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = Normalize(manifest.Icon.Path)
            };
        }

        var canonicalJson = ArtifactCanonicalJson.Write(unsignedManifest);
        var canonicalPayload = ArtifactCanonicalPayload.Compose(
            ArtifactSignaturePurposes.PluginPackage,
            PluginManifestContract.CurrentSchemaVersion,
            canonicalJson);
        var fingerprint = Convert.ToHexString(SHA256.HashData(canonicalPayload)).ToLowerInvariant();
        return new PluginPackageFingerprintResult(canonicalPayload, fingerprint);
    }

    private static Dictionary<string, object?> ExtensionPoint(PluginExtensionPoint extensionPoint)
    {
        var value = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Normalize(extensionPoint.Id),
            ["target"] = extensionPoint.Target,
            ["routeKey"] = Normalize(extensionPoint.RouteKey)
        };

        if (extensionPoint.LabelKey is not null)
        {
            value["labelKey"] = Normalize(extensionPoint.LabelKey);
        }

        if (extensionPoint.ParentId is not null)
        {
            value["parentId"] = Normalize(extensionPoint.ParentId);
        }

        if (extensionPoint.Group is not null)
        {
            value["group"] = Normalize(extensionPoint.Group);
        }

        if (extensionPoint.Weight is not null)
        {
            value["weight"] = extensionPoint.Weight.Value;
        }

        return value;
    }

    private static string Normalize(string value) => value.Normalize(NormalizationForm.FormC);
}

public sealed record PluginPackageFingerprintResult(
    byte[] CanonicalPayload,
    string Fingerprint);
