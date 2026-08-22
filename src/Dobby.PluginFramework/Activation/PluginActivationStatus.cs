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
    string Reason,
    string? Warning = null);

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

        var compatibility = PluginCompatibilityMatrix.Evaluate(manifest, installedHostVersion);
        if (!compatibility.IsCompatible)
        {
            return new PluginCompatibilityOutcome(
                false,
                PluginActivationStatus.Rejected,
                compatibility.ReasonCode);
        }

        return new PluginCompatibilityOutcome(true, PluginActivationStatus.Active, string.Empty);
    }
}
