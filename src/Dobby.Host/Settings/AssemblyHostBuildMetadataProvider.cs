using System.Globalization;
using System.IO;
using System.Reflection;

namespace Dobby.Host.Settings;

public sealed class AssemblyHostBuildMetadataProvider(Assembly assembly) : IHostBuildMetadataProvider
{
    public HostBuildMetadata Current { get; } = Resolve(assembly);

    private static HostBuildMetadata Resolve(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var buildNumber =
            MetadataValue(assembly, "BuildNumber") ??
            CanonicalInformationalVersion(assembly) ??
            assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
            assembly.GetName().Version?.ToString(3) ??
            "metadata-unavailable";

        var buildDate = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key.Equals("BuildDate", StringComparison.Ordinal))
            ?.Value;
        if (string.IsNullOrWhiteSpace(buildDate) && !string.IsNullOrWhiteSpace(assembly.Location))
        {
            buildDate = File.GetLastWriteTimeUtc(assembly.Location).ToString("O", CultureInfo.InvariantCulture);
        }

        return new HostBuildMetadata(buildNumber, buildDate ?? "metadata-unavailable");
    }

    private static string? MetadataValue(Assembly assembly, string key)
    {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key.Equals(key, StringComparison.Ordinal))
            ?.Value;
    }

    private static string? CanonicalInformationalVersion(Assembly assembly)
    {
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion)
            ? null
            : informationalVersion.Split(['-', '+'], 2)[0];
    }
}
