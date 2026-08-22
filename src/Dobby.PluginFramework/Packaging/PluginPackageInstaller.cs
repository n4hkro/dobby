using Dobby.Core;

namespace Dobby.PluginFramework.Packaging;

public interface IPluginInstallStagingArea
{
    Result<string> Stage(PluginPackageDescriptor package);

    Result<string> Promote(string stagedPath, PluginPackageDescriptor package);

    Result Cleanup(string stagedPath);

    Result RemoveRuntime(string runtimePath);

    Result MarkStateCommitted(string runtimePath);

    Result Complete(string runtimePath);

    Result<int> Recover();

    Result<int> CleanupRuntimeOrphans(IReadOnlyCollection<string> authoritativeRuntimePaths);
}

public sealed class PluginPackageInstaller(
    IPluginInstallStagingArea stagingArea,
    IInstalledPluginStateStore stateStore,
    IInstalledPluginMetadataCache metadataCache,
    IPluginInstallStateCommitter? stateCommitter = null)
{
    public Result<InstalledPluginState> Install(
        PluginPackageDescriptor package,
        PluginInstallCommitContext? commitContext = null,
        bool restartRequired = false)
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
            StatusSummary: restartRequired ? "Restart required" : "Enabled",
            InstalledAtUtc: now,
            UpdatedAtUtc: now,
            RestartRequired: restartRequired);

        var metadata = new InstalledPluginMetadata(
            package.Manifest.PluginId,
            package.Manifest.DisplayName,
            package.Manifest.Version,
            package.Manifest.Description,
            package.Fingerprint,
            IsEnabled: true,
            StatusSummary: restartRequired ? "Restart required" : "Enabled",
            InstalledAtUtc: now,
            UpdatedAtUtc: now,
            RestartRequired: restartRequired);

        var committed = stateCommitter is null
            ? CommitLegacy(installed, metadata, promoted.Value, stage.Value)
            : commitContext is null
                ? Result.Failure(ErrorCode.InvalidInput)
                : stateCommitter.Commit(package, installed, metadata, commitContext);
        if (!committed.IsSuccess)
        {
            stagingArea.Cleanup(stage.Value);
            stagingArea.Cleanup(promoted.Value);
            return Result.Failure<InstalledPluginState>(committed.ErrorCode ?? ErrorCode.DatabaseUnavailable);
        }

        // Completion is deliberately best-effort after the authoritative commit.
        // Startup recovery deterministically finishes a committed operation if the
        // process stops or the final journal update is temporarily unavailable.
        stagingArea.Complete(promoted.Value);
        return Result.Success(installed);
    }

    private Result CommitLegacy(
        InstalledPluginState installed,
        InstalledPluginMetadata metadata,
        string runtimePath,
        string stagedPath)
    {
        if (!stateStore.Upsert(installed).IsSuccess)
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }

        if (!metadataCache.Upsert(metadata).IsSuccess)
        {
            stateStore.Remove(installed.PluginId);
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }

        var marked = stagingArea.MarkStateCommitted(runtimePath);
        if (marked is null || marked.IsSuccess)
        {
            return Result.Success();
        }

        metadataCache.Remove(installed.PluginId);
        stateStore.Remove(installed.PluginId);
        stagingArea.Cleanup(stagedPath);
        return marked;
    }
}
