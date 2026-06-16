using Dobby.Core;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Packaging;

public sealed class PluginPackageInstallerTests
{
    [Fact]
    public void Install_writes_authoritative_state_and_metadata_after_promote()
    {
        var package = CreateDescriptor();
        var staging = new RecordingStagingArea();
        var stateStore = new RecordingStateStore();
        var metadataCache = new RecordingMetadataCache();
        var installer = new PluginPackageInstaller(staging, stateStore, metadataCache);

        var result = installer.Install(package);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PluginId.ShouldBe(package.Manifest.PluginId);
        stateStore.Upserted.ShouldNotBeNull();
        stateStore.Upserted.PackageFingerprint.ShouldBe(package.Fingerprint);
        metadataCache.Upserted.ShouldNotBeNull();
        metadataCache.Upserted.DisplayName.ShouldBe(package.Manifest.DisplayName);
        staging.CleanedPaths.ShouldBe(["stage"]);
    }

    [Fact]
    public void Install_cleans_stage_and_promoted_path_when_state_store_write_fails()
    {
        var package = CreateDescriptor();
        var staging = new RecordingStagingArea();
        var stateStore = new RecordingStateStore { UpsertResult = Result.Failure(ErrorCode.DatabaseUnavailable) };
        var metadataCache = new RecordingMetadataCache();
        var installer = new PluginPackageInstaller(staging, stateStore, metadataCache);

        var result = installer.Install(package);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        metadataCache.Upserted.ShouldBeNull();
        staging.CleanedPaths.ShouldBe(["stage", "runtime"]);
    }

    [Fact]
    public void Install_removes_authoritative_state_and_cleans_paths_when_metadata_cache_write_fails()
    {
        var package = CreateDescriptor();
        var staging = new RecordingStagingArea();
        var stateStore = new RecordingStateStore();
        var metadataCache = new RecordingMetadataCache { UpsertResult = Result.Failure(ErrorCode.DatabaseUnavailable) };
        var installer = new PluginPackageInstaller(staging, stateStore, metadataCache);

        var result = installer.Install(package);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        stateStore.RemovedPluginIds.ShouldBe([package.Manifest.PluginId]);
        staging.CleanedPaths.ShouldBe(["stage", "runtime"]);
    }

    private static PluginPackageDescriptor CreateDescriptor()
    {
        var manifest = PluginPackageTestData.CreateManifest();
        var fingerprint = PluginPackageFingerprint.Compute(manifest);
        return new PluginPackageDescriptor(
            "source.dobbyplugin",
            fingerprint.Fingerprint,
            fingerprint.CanonicalPayload,
            manifest,
            [],
            PluginPackageReader.ManifestFileName,
            PluginPackageTestData.CreateManifestJson());
    }

    private sealed class RecordingStagingArea : IPluginInstallStagingArea
    {
        public Result<string> StageResult { get; init; } = Result.Success("stage");

        public Result<string> PromoteResult { get; init; } = Result.Success("runtime");

        public List<string> CleanedPaths { get; } = [];

        public Result<string> Stage(PluginPackageDescriptor package) => StageResult;

        public Result<string> Promote(string stagedPath, PluginPackageDescriptor package) => PromoteResult;

        public Result Cleanup(string stagedPath)
        {
            CleanedPaths.Add(stagedPath);
            return Result.Success();
        }

        public Result<int> Recover()
        {
            return Result.Success(0);
        }

        public Result<int> CleanupRuntimeOrphans(IReadOnlyCollection<string> authoritativeRuntimePaths)
        {
            return Result.Success(0);
        }
    }

    private sealed class RecordingStateStore : IInstalledPluginStateStore
    {
        public Result UpsertResult { get; init; } = Result.Success();

        public InstalledPluginState? Upserted { get; private set; }

        public List<string> RemovedPluginIds { get; } = [];

        public Result Upsert(InstalledPluginState state)
        {
            Upserted = state;
            return UpsertResult;
        }

        public Result<InstalledPluginState?> Get(string pluginId)
        {
            return Result.Success<InstalledPluginState?>(Upserted);
        }

        public Result<IReadOnlyList<InstalledPluginState>> Snapshot()
        {
            return Result.Success<IReadOnlyList<InstalledPluginState>>(Upserted is null ? [] : [Upserted]);
        }

        public Result SetEnabled(string pluginId, bool isEnabled, string statusSummary)
        {
            return Result.Success();
        }

        public Result Remove(string pluginId)
        {
            RemovedPluginIds.Add(pluginId);
            return Result.Success();
        }
    }

    private sealed class RecordingMetadataCache : IInstalledPluginMetadataCache
    {
        public Result UpsertResult { get; init; } = Result.Success();

        public InstalledPluginMetadata? Upserted { get; private set; }

        public Result Upsert(InstalledPluginMetadata metadata)
        {
            Upserted = metadata;
            return UpsertResult;
        }

        public Result<InstalledPluginMetadata?> Get(string pluginId)
        {
            return Result.Success<InstalledPluginMetadata?>(Upserted);
        }

        public Result<IReadOnlyList<InstalledPluginMetadata>> Snapshot()
        {
            return Result.Success<IReadOnlyList<InstalledPluginMetadata>>(Upserted is null ? [] : [Upserted]);
        }

        public Result RebuildFrom(IReadOnlyList<InstalledPluginState> installedStates)
        {
            return Result.Success();
        }

        public Result Remove(string pluginId)
        {
            Upserted = null;
            return Result.Success();
        }
    }
}
