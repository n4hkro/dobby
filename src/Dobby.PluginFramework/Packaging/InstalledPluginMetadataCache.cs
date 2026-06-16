using Dobby.Core;

namespace Dobby.PluginFramework.Packaging;

public sealed record InstalledPluginMetadata(
    string PluginId,
    string DisplayName,
    string Version,
    string Description,
    string PackageFingerprint,
    bool IsEnabled,
    string StatusSummary,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc);

public interface IInstalledPluginMetadataCache
{
    Result Upsert(InstalledPluginMetadata metadata);

    Result<InstalledPluginMetadata?> Get(string pluginId);

    Result<IReadOnlyList<InstalledPluginMetadata>> Snapshot();

    Result RebuildFrom(IReadOnlyList<InstalledPluginState> installedStates);

    Result Remove(string pluginId);
}
