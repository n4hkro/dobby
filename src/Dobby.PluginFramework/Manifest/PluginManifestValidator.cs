using System.Text.RegularExpressions;
using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.PluginFramework.Packaging;
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

    private static readonly Regex ResourceKeyPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LocalIdentifierPattern = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RouteKeyPattern = new("^[A-Za-z0-9][A-Za-z0-9._/-]*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PluginIdPattern = new("^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex OperationTargetIdPattern = new("^[A-Za-z0-9][A-Za-z0-9_-]*(?:\\.[A-Za-z0-9][A-Za-z0-9_-]*)+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static Result<PluginManifest> Validate(PluginManifest manifest, HostVersion hostVersion)
    {
        var detailed = ValidateDetailed(manifest, hostVersion);
        return detailed.IsSuccess
            ? Result.Success(manifest)
            : Result.Failure<PluginManifest>(ErrorCode.ValidationFailed);
    }

    public static PluginManifestValidationOutcome ValidateDetailed(PluginManifest manifest, HostVersion hostVersion)
    {
        if (manifest is null)
        {
            throw new ArgumentNullException(nameof(manifest));
        }

        var reasons = new List<PluginManifestValidationReason>();

        if (!IsPluginId(manifest.PluginId))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Plugin id is missing or malformed.", "pluginId"));
        }

        if (!IsNonEmptyIdentifier(manifest.VendorId))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Vendor id is missing.", "vendorId"));
        }

        if (!IsStableSemVer(manifest.Version))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Plugin version must be stable SemVer.", "version"));
        }

        if (!HostVersion.TryParse(manifest.MinHostVersion, out _))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Minimum host version is malformed.", "minHostVersion"));
        }

        if (!IsCanonicalPackageRelativePath(manifest.EntryAssembly))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Entry assembly must be a canonical package-relative path.", "entryAssembly"));
        }

        if (!IsNonEmptyIdentifier(manifest.DisplayName))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Display name is required.", "displayName"));
        }

        if (!IsNonEmptyIdentifier(manifest.Description))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Description is required.", "description"));
        }

        if (manifest.Icon is not null && !IsCanonicalPackageRelativePath(manifest.Icon.Path))
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Icon path must be canonical when present.", "icon.path"));
        }

        ValidateCapabilities(manifest.Capabilities, reasons);
        ValidateOperationTargets(manifest.PluginId, manifest.OperationTargets, reasons);
        ValidateExtensionPoints(manifest.ExtensionPoints, reasons);
        ValidateDependsOn(manifest.PluginId, manifest.DependsOn, reasons);
        ValidateFiles(manifest.Files, reasons);
        ValidateSignature(manifest.Signature, reasons);

        return reasons.Count == 0
            ? PluginManifestValidationOutcome.Success(manifest)
            : PluginManifestValidationOutcome.Failure(reasons);
    }

    private static void ValidateCapabilities(IReadOnlyList<PluginCapability> capabilities, ICollection<PluginManifestValidationReason> reasons)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < capabilities.Count; index++)
        {
            var capability = capabilities[index];
            if (!IsNonEmptyIdentifier(capability.Name))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Capability name is required.", $"capabilities[{index}].name"));
            }

            if (!AllowedCapabilityScopes.Contains(capability.Scope))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Capability scope is not allowed.", $"capabilities[{index}].scope"));
            }

            if (!seen.Add(capability.Name))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Capability names must be unique.", $"capabilities[{index}].name"));
            }
        }
    }

    private static void ValidateOperationTargets(
        string pluginId,
        IReadOnlyList<PluginOperationTarget> targets,
        ICollection<PluginManifestValidationReason> reasons)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            var path = $"operationTargets[{index}]";

            if (!OperationTargetIdPattern.IsMatch(target.Id))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target id is malformed.", $"{path}.id"));
            }
            else if (!target.Id.StartsWith(pluginId + ".", StringComparison.Ordinal))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target id must live under the plugin namespace.", $"{path}.id"));
            }

            if (string.IsNullOrWhiteSpace(target.LabelKey) || !ResourceKeyPattern.IsMatch(target.LabelKey))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target labelKey is required.", $"{path}.labelKey"));
            }

            if (target.Verbs.Count == 0)
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target verbs must not be empty.", $"{path}.verbs"));
            }
            else if (target.Verbs.Count != target.Verbs.Distinct(StringComparer.Ordinal).Count())
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target verbs must be unique.", $"{path}.verbs"));
            }

            if (!seen.Add(target.Id))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target ids must be unique.", $"{path}.id"));
            }

            for (var verbIndex = 0; verbIndex < target.Verbs.Count; verbIndex++)
            {
                var verb = target.Verbs[verbIndex];
                if (!OperationVerbParser.TryParse(verb, out _))
                {
                    reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Operation target verb is not canonical.", $"{path}.verbs[{verbIndex}]"));
                }
            }
        }
    }

    private static void ValidateExtensionPoints(
        IReadOnlyList<PluginExtensionPoint> extensionPoints,
        ICollection<PluginManifestValidationReason> reasons)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var byId = new Dictionary<string, PluginExtensionPoint>(StringComparer.Ordinal);

        for (var index = 0; index < extensionPoints.Count; index++)
        {
            var extension = extensionPoints[index];
            var path = $"extensionPoints[{index}]";

            if (!IsLocalIdentifier(extension.Id))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension id is missing or malformed.", $"{path}.id"));
            }
            else if (!seen.Add(extension.Id))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension ids must be unique.", $"{path}.id"));
            }
            else
            {
                byId[extension.Id] = extension;
            }

            if (!AllowedExtensionTargets.Contains(extension.Target))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension target is not allowed.", $"{path}.target"));
            }

            if (string.IsNullOrWhiteSpace(extension.RouteKey) || !IsCanonicalRouteKey(extension.RouteKey))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension routeKey is malformed.", $"{path}.routeKey"));
            }

            if (string.IsNullOrWhiteSpace(extension.LabelKey))
            {
                if (!CanUseDisplayNameLabelFallback(extension))
                {
                    reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension labelKey is required.", $"{path}.labelKey"));
                }
            }
            else if (!ResourceKeyPattern.IsMatch(extension.LabelKey))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension labelKey is malformed.", $"{path}.labelKey"));
            }

            if (extension.Group is not null && !extension.Group.Equals("default", StringComparison.Ordinal))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension group must be default when present.", $"{path}.group"));
            }

            if (extension.Weight is { } weight && (weight < -1_000_000 || weight > 1_000_000))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Extension weight is outside the allowed range.", $"{path}.weight"));
            }
        }

        foreach (var extension in extensionPoints)
        {
            if (extension.ParentId is null)
            {
                continue;
            }

            if (!IsLocalIdentifier(extension.ParentId))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Parent extension id is malformed.", $"extensionPoints[{extension.Id}].parentId"));
                continue;
            }

            if (!byId.ContainsKey(extension.ParentId))
            {
                reasons.Add(new PluginManifestValidationReason("missing-parent", "Parent extension id does not exist.", $"extensionPoints[{extension.Id}].parentId"));
                continue;
            }

            if (CreatesCycle(extension.Id, byId, new HashSet<string>(StringComparer.Ordinal)))
            {
                reasons.Add(new PluginManifestValidationReason("parent-cycle", "Extension parent chain contains a cycle.", $"extensionPoints[{extension.Id}].parentId"));
            }
        }
    }

    private static bool CreatesCycle(
        string startId,
        IReadOnlyDictionary<string, PluginExtensionPoint> byId,
        ISet<string> visiting)
    {
        if (!byId.TryGetValue(startId, out var current) || current.ParentId is null)
        {
            return false;
        }

        if (!visiting.Add(startId))
        {
            return true;
        }

        if (byId.TryGetValue(current.ParentId, out var parent))
        {
            if (visiting.Contains(parent.Id))
            {
                return true;
            }

            return CreatesCycle(parent.Id, byId, visiting);
        }

        return false;
    }

    private static void ValidateDependsOn(
        string pluginId,
        IReadOnlyList<PluginManifestDependency> dependencies,
        ICollection<PluginManifestValidationReason> reasons)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < dependencies.Count; index++)
        {
            var dependency = dependencies[index];
            var path = $"dependsOn[{index}]";

            if (!IsPluginId(dependency.PluginId))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Dependency plugin id is malformed.", $"{path}.pluginId"));
            }

            if (dependency.PluginId.Equals(pluginId, StringComparison.Ordinal))
            {
                reasons.Add(new PluginManifestValidationReason("self-dependency", "Plugin cannot depend on itself.", $"{path}.pluginId"));
            }

            if (!IsStableSemVer(dependency.MinVersion))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Dependency minimum version must be stable SemVer.", $"{path}.minVersion"));
            }

            if (!seen.Add(dependency.PluginId))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Dependency plugin ids must be unique.", $"{path}.pluginId"));
            }
        }
    }

    private static void ValidateFiles(IReadOnlyList<PluginPackageFile> files, ICollection<PluginManifestValidationReason> reasons)
    {
        var declaredPaths = new List<string>(files.Count);

        if (files.Count == 0)
        {
            reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Package files must not be empty.", "files"));
        }

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var path = $"files[{index}]";

            if (!IsCanonicalPackageRelativePath(file.Path))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Package file path is malformed.", $"{path}.path"));
            }
            else
            {
                declaredPaths.Add(file.Path);
            }

            if (!IsSha256(file.Sha256))
            {
                reasons.Add(new PluginManifestValidationReason("invalid-manifest", "Package file sha256 must be lowercase hex.", $"{path}.sha256"));
            }

        }

        if (PluginPackagePathPolicy.HasCaseOrUnicodeCollision(declaredPaths))
        {
            reasons.Add(new PluginManifestValidationReason(
                "invalid-manifest",
                "Package file paths must be unique under case-insensitive Unicode-normalized comparison.",
                "files"));
        }

        if (PluginPackagePathPolicy.HasFileDirectoryPrefixCollision(declaredPaths))
        {
            reasons.Add(new PluginManifestValidationReason(
                "invalid-manifest",
                "A package file path cannot also be the directory prefix of another file.",
                "files"));
        }
    }

    private static void ValidateSignature(PluginPackageSignature signature, ICollection<PluginManifestValidationReason> reasons)
    {
        if (!signature.Algorithm.Equals("ECDSA-P256-SHA256", StringComparison.Ordinal))
        {
            reasons.Add(new PluginManifestValidationReason("signature-failed", "Signature algorithm is not approved.", "signature.algorithm"));
        }

        if (signature.KeyVersion != 1)
        {
            reasons.Add(new PluginManifestValidationReason("signature-failed", "Signature key version is not approved.", "signature.keyVersion"));
        }

        if (!IsBase64Url(signature.Value))
        {
            reasons.Add(new PluginManifestValidationReason("signature-failed", "Signature value is malformed.", "signature.value"));
        }
    }

    private static bool IsCanonicalRouteKey(string value)
    {
        return RouteKeyPattern.IsMatch(value);
    }

    private static bool CanUseDisplayNameLabelFallback(PluginExtensionPoint extension)
    {
        return extension.Target.Equals("top-toolbar", StringComparison.Ordinal) &&
            extension.ParentId is null;
    }

    private static bool IsCanonicalPackageRelativePath(string value)
    {
        return PluginPackagePathPolicy.TryNormalize(value, out var normalized) &&
            normalized.Equals(value, StringComparison.Ordinal);
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(character => Uri.IsHexDigit(character) && !char.IsUpper(character));
    }

    private static bool IsBase64Url(string value)
    {
        return value.Length > 0 && value.All(character =>
            char.IsLetterOrDigit(character) ||
            character is '-' or '_');
    }

    private static bool IsStableSemVer(string value)
    {
        return NuGetVersion.TryParse(value, out var version) &&
            !version.IsPrerelease &&
            !version.HasMetadata &&
            value.Count(character => character == '.') == 2;
    }

    private static bool IsNonEmptyIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsLocalIdentifier(string value)
    {
        return LocalIdentifierPattern.IsMatch(value);
    }

    private static bool IsPluginId(string value)
    {
        return PluginIdPattern.IsMatch(value);
    }
}

public sealed record PluginManifestValidationReason(
    string Code,
    string Message,
    string? Path = null);

public sealed record PluginManifestValidationOutcome(
    bool IsSuccess,
    PluginManifest? Manifest,
    IReadOnlyList<PluginManifestValidationReason> Reasons)
{
    public static PluginManifestValidationOutcome Success(PluginManifest manifest)
    {
        return new PluginManifestValidationOutcome(true, manifest, []);
    }

    public static PluginManifestValidationOutcome Failure(IReadOnlyList<PluginManifestValidationReason> reasons)
    {
        return new PluginManifestValidationOutcome(false, null, reasons);
    }
}
