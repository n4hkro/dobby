using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.Security.Authorization;
using NuGet.Versioning;

namespace Dobby.PluginFramework.Manifest;

public static class PluginManifestValidator
{
    private static readonly HashSet<string> AllowedCapabilityScopes = new(StringComparer.Ordinal)
    {
        "shell",
        "plugin-page",
        "background-task"
    };

    private static readonly HashSet<string> AllowedExtensionTargets = new(StringComparer.Ordinal)
    {
        "top-toolbar",
        "sidebar",
        "third-bar",
        "plugin-page-region"
    };

    public static Result<PluginManifest> Validate(PluginManifest manifest, HostVersion hostVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(manifest.PluginId) ||
            !IsStableSemVer(manifest.Version) ||
            !HostVersion.TryParse(manifest.MinHostVersion, out var minHostVersion) ||
            minHostVersion.CompareTo(hostVersion) > 0 ||
            manifest.Capabilities.Any(capability => !AllowedCapabilityScopes.Contains(capability.Scope)) ||
            manifest.ExtensionPoints.Any(extension => !AllowedExtensionTargets.Contains(extension.Target)) ||
            HasInvalidOperationTargets(manifest.OperationTargets))
        {
            return Result.Failure<PluginManifest>(ErrorCode.ValidationFailed);
        }

        return Result.Success(manifest);
    }

    private static bool IsStableSemVer(string value)
    {
        return NuGetVersion.TryParse(value, out var version) &&
            !version.IsPrerelease &&
            !version.HasMetadata &&
            value.Count(character => character == '.') == 2;
    }

    private static bool HasInvalidOperationTargets(IReadOnlyList<PluginOperationTarget> targets)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Id) ||
                string.IsNullOrWhiteSpace(target.LabelKey) ||
                target.Verbs.Count == 0 ||
                !seen.Add(target.Id) ||
                target.Verbs.Any(verb => !OperationVerbParser.TryParse(verb, out _)))
            {
                return true;
            }
        }

        return false;
    }
}
