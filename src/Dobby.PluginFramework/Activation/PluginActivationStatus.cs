using Dobby.Core.Versioning;
using Dobby.PluginFramework.Manifest;
using NuGet.Versioning;

namespace Dobby.PluginFramework.Activation;

public enum PluginActivationStatus
{
    Active,
    Skipped,
    Rejected,
    Faulted
}

public sealed record PluginActivationOutcome(
    string PluginId,
    PluginActivationStatus Status,
    string Reason);

public sealed record PluginCompatibilityOutcome(
    bool IsCompatible,
    PluginActivationStatus Status,
    string Reason);

public static class PluginCompatibilityEvaluator
{
    public static PluginCompatibilityOutcome Evaluate(PluginManifest manifest, HostVersion installedHostVersion)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (!NuGetVersion.TryParse(manifest.Version, out var pluginVersion) ||
            pluginVersion.IsPrerelease ||
            pluginVersion.HasMetadata)
        {
            return new PluginCompatibilityOutcome(false, PluginActivationStatus.Rejected, "invalid-manifest");
        }

        if (!HostVersion.TryParse(manifest.MinHostVersion, out var minimumHostVersion))
        {
            return new PluginCompatibilityOutcome(false, PluginActivationStatus.Rejected, "invalid-manifest");
        }

        if (installedHostVersion.CompareTo(minimumHostVersion) < 0)
        {
            return new PluginCompatibilityOutcome(
                false,
                PluginActivationStatus.Rejected,
                $"host-version-below-min: required={minimumHostVersion} installed={installedHostVersion}");
        }

        return new PluginCompatibilityOutcome(true, PluginActivationStatus.Active, string.Empty);
    }
}
