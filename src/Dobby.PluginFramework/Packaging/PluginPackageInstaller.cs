using Dobby.Core;

namespace Dobby.PluginFramework.Packaging;

public interface IPluginInstallStagingArea
{
    Result<string> Stage(PluginPackageDescriptor package);

    Result<string> Promote(string stagedPath, PluginPackageDescriptor package);

    Result Cleanup(string stagedPath);

    Result<int> Recover();

    Result<int> CleanupRuntimeOrphans(IReadOnlyCollection<string> authoritativeRuntimePaths);
}

public sealed class PluginPackageInstaller(
    IPluginInstallStagingArea stagingArea,
    IInstalledPluginStateStore stateStore,
    IInstalledPluginMetadataCache metadataCache)
{
    public Result<InstalledPluginState> Install(PluginPackageDescriptor package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var stage = stagingArea.Stage(package);
        if (!stage.IsSuccess)
        {
            return Result.Failure<InstalledPluginState>(stage.ErrorCode ?? ErrorCode.IoFailed);
        }

        var promoted = stagingArea.Promote(stage.Value, package);
        if (!promoted.IsSuccess)
        {
            stagingArea.Cleanup(stage.Value);
            return Result.Failure<InstalledPluginState>(promoted.ErrorCode ?? ErrorCode.IoFailed);
        }

        var now = DateTimeOffset.UtcNow;
        var installed = new InstalledPluginState(
            package.Manifest.PluginId,
            package.Manifest.Version,
            package.Fingerprint,
            package.Manifest.DisplayName,
            package.Manifest.Description,
            IsEnabled: true,
            ValidationSummary: "verified",
            RuntimePath: promoted.Value,
            StatusSummary: "Enabled",
            InstalledAtUtc: now,
            UpdatedAtUtc: now);

        if (!stateStore.Upsert(installed).IsSuccess)
        {
            stagingArea.Cleanup(stage.Value);
            stagingArea.Cleanup(promoted.Value);
            return Result.Failure<InstalledPluginState>(ErrorCode.DatabaseUnavailable);
        }

        var metadata = new InstalledPluginMetadata(
            package.Manifest.PluginId,
            package.Manifest.DisplayName,
            package.Manifest.Version,
            package.Manifest.Description,
            package.Fingerprint,
            IsEnabled: true,
            StatusSummary: "Enabled",
            InstalledAtUtc: now,
            UpdatedAtUtc: now);

        if (!metadataCache.Upsert(metadata).IsSuccess)
        {
            stateStore.Remove(package.Manifest.PluginId);
            stagingArea.Cleanup(stage.Value);
            stagingArea.Cleanup(promoted.Value);
            return Result.Failure<InstalledPluginState>(ErrorCode.DatabaseUnavailable);
        }

        stagingArea.Cleanup(stage.Value);
        return Result.Success(installed);
    }
}
