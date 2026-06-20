using Dobby.Core;

namespace Dobby.PluginFramework.Packaging;

public sealed record InstalledPluginState(
    string PluginId,
    string Version,
    string PackageFingerprint,
    string DisplayName,
    string Description,
    bool IsEnabled,
    string ValidationSummary,
    string RuntimePath,
    string StatusSummary,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsPendingUninstall = false,
    string DataRetentionChoice = "keep",
    bool RestartRequired = false,
    DateTimeOffset? LastActivatedAtUtc = null);

public interface IInstalledPluginStateStore
{
    Result Upsert(InstalledPluginState state);

    Result<InstalledPluginState?> Get(string pluginId);

    Result<IReadOnlyList<InstalledPluginState>> Snapshot();

    Result SetEnabled(string pluginId, bool isEnabled, string statusSummary);

    Result Remove(string pluginId);
}
