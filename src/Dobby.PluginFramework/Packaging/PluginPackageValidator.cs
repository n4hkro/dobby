using Dobby.Core;
using Dobby.Core.Versioning;
using Dobby.PluginFramework.Manifest;
using Dobby.Security.Trust;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Dobby.PluginFramework.Packaging;

public static class PluginPackageValidator
{
    private static readonly HashSet<string> AllowedSharedDobbyAssemblies = new(StringComparer.Ordinal)
    {
        "Dobby.Contracts",
        "Dobby.Core",
        "Dobby.SDK"
    };

    private static readonly string[] ForbiddenAssemblyReferencePrefixes =
    [
        "System.Reflection",
        "System.Runtime.Loader"
    ];

    private static readonly string[] ForbiddenAssemblyMarkers =
    [
        "Dobby.Host",
        "Dobby.Infrastructure",
        "Dobby.Audit",
        "Dobby.Configuration",
        "Dobby.Diagnostics",
        "Dobby.Licensing",
        "Dobby.Messaging",
        "Dobby.Security",
        "Assembly.Load",
        "LoadFrom(",
        "LoadFile(",
        "AssemblyLoadContext",
        "AssemblyDependencyResolver"
    ];

    public static PluginPackageValidationOutcome Validate(
        PluginPackageArchive archive,
        HostVersion hostVersion,
        IPluginTrustVerifier trustVerifier)
    {
        if (archive is null)
        {
            throw new ArgumentNullException(nameof(archive));
        }

        ArgumentNullException.ThrowIfNull(trustVerifier);

        var reasons = new List<PluginPackageValidationReason>();

        var manifestValidation = PluginManifestValidator.ValidateDetailed(archive.Manifest, hostVersion);
        if (!manifestValidation.IsSuccess)
        {
            reasons.AddRange(manifestValidation.Reasons.Select(reason =>
                new PluginPackageValidationReason(reason.Code, reason.Message, reason.Path)));
            return PluginPackageValidationOutcome.Failure(reasons);
        }

        var manifestFiles = archive.Manifest.Files
            .Select(file => new PluginPackageEntry(
                NormalizePath(file.Path),
                Array.Empty<byte>(),
                file.Sha256,
                false))
            .ToArray();

        var actualFiles = archive.Entries
            .Where(entry => !entry.Path.Equals(archive.ManifestPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ValidateActualEntries(actualFiles, reasons);
        ValidateForbiddenContent(manifestFiles, actualFiles, reasons);
        ValidateAssemblyBoundaries(actualFiles, reasons);
        ValidateCompleteness(manifestFiles, actualFiles, reasons);

        if (reasons.Count > 0)
        {
            return PluginPackageValidationOutcome.Failure(reasons);
        }

        var fingerprint = PluginPackageFingerprint.Compute(archive.Manifest);
        var descriptor = new PluginPackageDescriptor(
            archive.PackagePath,
            fingerprint.Fingerprint,
            fingerprint.CanonicalPayload,
            archive.Manifest,
            archive.Entries,
            archive.ManifestPath,
            archive.ManifestJson);

        var trust = trustVerifier.Verify(new PluginTrustPayload(
            archive.Manifest.VendorId,
            archive.Manifest.Signature.KeyVersion,
            archive.Manifest.Signature.Algorithm,
            fingerprint.CanonicalPayload,
            archive.Manifest.Signature.Value));

        if (!trust.IsSuccess)
        {
            reasons.Add(new PluginPackageValidationReason(trust.Code, trust.Message, "signature"));
            return PluginPackageValidationOutcome.Failure(reasons);
        }

        return PluginPackageValidationOutcome.Success(descriptor);
    }

    private static void ValidateActualEntries(
        IReadOnlyList<PluginPackageEntry> entries,
        ICollection<PluginPackageValidationReason> reasons)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (entry.IsSymlink)
            {
                reasons.Add(new PluginPackageValidationReason("forbidden-content", "Symlink package entries are forbidden.", entry.Path));
            }

            if (!IsCanonicalPackageRelativePath(entry.Path))
            {
                reasons.Add(new PluginPackageValidationReason("forbidden-content", "Package entry path is forbidden.", entry.Path));
            }

            if (!seen.Add(entry.Path))
            {
                reasons.Add(new PluginPackageValidationReason("forbidden-content", "Duplicate package entry path detected.", entry.Path));
            }
        }
    }

    private static void ValidateCompleteness(
        IReadOnlyList<PluginPackageEntry> manifestFiles,
        IReadOnlyList<PluginPackageEntry> actualFiles,
        ICollection<PluginPackageValidationReason> reasons)
    {
        var manifestLookup = new Dictionary<string, PluginPackageEntry>(StringComparer.Ordinal);
        var manifestCaseLookup = new Dictionary<string, PluginPackageEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifestFiles)
        {
            manifestLookup.TryAdd(file.Path, file);
            manifestCaseLookup.TryAdd(file.Path, file);
        }

        var actualLookup = new Dictionary<string, PluginPackageEntry>(StringComparer.Ordinal);
        var actualCaseLookup = new Dictionary<string, PluginPackageEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in actualFiles)
        {
            actualLookup.TryAdd(file.Path, file);
            actualCaseLookup.TryAdd(file.Path, file);
        }

        foreach (var file in actualFiles)
        {
            if (manifestLookup.TryGetValue(file.Path, out var manifest))
            {
                if (!file.Sha256.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add(new PluginPackageValidationReason("hash-mismatch", "Package entry hash does not match the manifest.", file.Path));
                }

                continue;
            }

            if (manifestCaseLookup.TryGetValue(file.Path, out _))
            {
                reasons.Add(new PluginPackageValidationReason("forbidden-content", "Package entry path collides by case with the manifest.", file.Path));
                continue;
            }

            reasons.Add(new PluginPackageValidationReason("forbidden-content", "Package entry is not declared in the manifest files section.", file.Path));
        }

        foreach (var file in manifestFiles)
        {
            if (actualLookup.ContainsKey(file.Path))
            {
                continue;
            }

            if (actualCaseLookup.TryGetValue(file.Path, out _))
            {
                reasons.Add(new PluginPackageValidationReason("forbidden-content", "Manifest file path collides by case with the package entry.", file.Path));
                continue;
            }

            reasons.Add(new PluginPackageValidationReason("forbidden-content", "Manifest file entry is missing from the package.", file.Path));
        }
    }

    private static void ValidateForbiddenContent(
        IReadOnlyList<PluginPackageEntry> manifestFiles,
        IReadOnlyList<PluginPackageEntry> actualFiles,
        ICollection<PluginPackageValidationReason> reasons)
    {
        foreach (var file in manifestFiles.Concat(actualFiles))
        {
            if (IsForbiddenPackageContentPath(file.Path))
            {
                reasons.Add(new PluginPackageValidationReason(
                    "forbidden-content",
                    "Package contains content that is forbidden for plugin packages.",
                    file.Path));
            }
        }
    }

    private static void ValidateAssemblyBoundaries(
        IReadOnlyList<PluginPackageEntry> actualFiles,
        ICollection<PluginPackageValidationReason> reasons)
    {
        foreach (var entry in actualFiles.Where(entry => entry.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            ValidateAssemblyMarkers(entry, reasons);
            ValidateAssemblyReferences(entry, reasons);
        }
    }

    private static void ValidateAssemblyMarkers(
        PluginPackageEntry entry,
        ICollection<PluginPackageValidationReason> reasons)
    {
        var text = Encoding.UTF8.GetString(entry.Content);
        foreach (var marker in ForbiddenAssemblyMarkers)
        {
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                reasons.Add(new PluginPackageValidationReason(
                    "forbidden-content",
                    "Plugin assembly contains forbidden host-boundary or dynamic-load references.",
                    entry.Path));
                return;
            }
        }
    }

    private static void ValidateAssemblyReferences(
        PluginPackageEntry entry,
        ICollection<PluginPackageValidationReason> reasons)
    {
        try
        {
            using var stream = new MemoryStream(entry.Content);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return;
            }

            var metadata = peReader.GetMetadataReader();
            foreach (var handle in metadata.AssemblyReferences)
            {
                var reference = metadata.GetAssemblyReference(handle);
                var assemblyName = metadata.GetString(reference.Name);
                if (IsForbiddenAssemblyReference(assemblyName))
                {
                    reasons.Add(new PluginPackageValidationReason(
                        "forbidden-content",
                        "Plugin assembly references a forbidden host implementation, reflection, or other-plugin assembly.",
                        entry.Path));
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or InvalidOperationException)
        {
            // Non-PE payloads are still checked by hash/path/content marker validation.
        }
    }

    private static bool IsForbiddenAssemblyReference(string assemblyName)
    {
        if (assemblyName.StartsWith("Dobby.", StringComparison.Ordinal) &&
            !AllowedSharedDobbyAssemblies.Contains(assemblyName))
        {
            return true;
        }

        return ForbiddenAssemblyReferencePrefixes.Any(prefix =>
            assemblyName.Equals(prefix, StringComparison.Ordinal) ||
            assemblyName.StartsWith(prefix + ".", StringComparison.Ordinal));
    }

    private static bool IsForbiddenPackageContentPath(string path)
    {
        var normalized = NormalizePath(path);
        var fileName = Path.GetFileName(normalized);
        if (fileName.EndsWith(".dobbylicense", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var lower = normalized.ToLowerInvariant();
        if (lower.Contains("customer-data", StringComparison.Ordinal) ||
            lower.Contains("customer-specific-license", StringComparison.Ordinal) ||
            lower.Contains("device-fingerprint", StringComparison.Ordinal) ||
            lower.Contains("vendor-private-key", StringComparison.Ordinal) ||
            lower.Contains("private-key", StringComparison.Ordinal) ||
            lower.Contains("host-internal-config", StringComparison.Ordinal) ||
            lower.Contains("host.internal.config", StringComparison.Ordinal) ||
            lower.Contains("host-config", StringComparison.Ordinal))
        {
            return true;
        }

        return lower.EndsWith(".sql", StringComparison.Ordinal) &&
            (lower.Contains("/migrations/", StringComparison.Ordinal) ||
             lower.StartsWith("migrations/", StringComparison.Ordinal) ||
             lower.Contains("/persistence/", StringComparison.Ordinal));
    }

    private static string NormalizePath(string value)
    {
        return value.Replace('\\', '/').Normalize(NormalizationForm.FormC);
    }

    private static bool IsCanonicalPackageRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains(':', StringComparison.Ordinal) ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("\\", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && !segments.Any(IsReservedWindowsName);
    }

    private static bool IsReservedWindowsName(string segment)
    {
        return segment.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (segment.Length == 4 && segment.StartsWith("COM", StringComparison.OrdinalIgnoreCase) && char.IsDigit(segment[3]) && segment[3] is >= '1' and <= '9') ||
            (segment.Length == 4 && segment.StartsWith("LPT", StringComparison.OrdinalIgnoreCase) && char.IsDigit(segment[3]) && segment[3] is >= '1' and <= '9');
    }
}
