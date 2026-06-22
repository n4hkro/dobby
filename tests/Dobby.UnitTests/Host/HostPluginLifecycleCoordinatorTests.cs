using Dobby.Audit;
using Dobby.Contracts.Policies;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class HostPluginLifecycleCoordinatorTests
{
    [Fact]
    public void Enable_plugin_requires_configure_permission_before_state_mutation()
    {
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            policy: CreatePolicy(new UserPermissionState("managed", IsOwner: false, IsActive: true)));

        var result = coordinator.EnablePlugin("managed", "com.dobby.sample", CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.Forbidden);
        stateStore.DidNotReceive().SetEnabled(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
    }

    [Fact]
    public void Disable_plugin_updates_state_cache_and_audits_existing_taxonomy()
    {
        var audit = new InMemoryAuditTrail();
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        var state = CreateState(isEnabled: true);
        stateStore.Get(state.PluginId).Returns(Result.Success<InstalledPluginState?>(state));
        stateStore.SetEnabled(state.PluginId, false, "Disabled").Returns(Result.Success());
        metadataCache.Upsert(Arg.Any<InstalledPluginMetadata>()).Returns(Result.Success());
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            metadataCache: metadataCache,
            auditTrail: audit);

        var result = coordinator.DisablePlugin("owner", state.PluginId, CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        stateStore.Received(1).SetEnabled(state.PluginId, false, "Disabled");
        metadataCache.Received(1).Upsert(Arg.Is<InstalledPluginMetadata>(metadata =>
            metadata.PluginId == state.PluginId &&
            !metadata.IsEnabled &&
            metadata.StatusSummary == "Disabled"));
        var auditEvent = audit.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.PluginDisable);
        auditEvent.Target.ShouldBe($"plugin:{state.PluginId}");
    }

    [Fact]
    public void Uninstall_enabled_but_not_current_process_activated_plugin_removes_runtime_state_cache_and_audits_data_choice()
    {
        var audit = new InMemoryAuditTrail();
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        var staging = Substitute.For<IPluginInstallStagingArea>();
        var dataRetentionStore = Substitute.For<IPluginDataRetentionStore>();
        var activationTracker = Substitute.For<IPluginCurrentProcessActivationTracker>();
        var state = CreateState(isEnabled: true);
        stateStore.Get(state.PluginId).Returns(Result.Success<InstalledPluginState?>(state));
        activationTracker.IsActivatedInCurrentProcess(state.PluginId).Returns(false);
        staging.Cleanup(state.RuntimePath).Returns(Result.Success());
        dataRetentionStore.DeletePluginData(state.PluginId).Returns(Result.Success());
        stateStore.Remove(state.PluginId).Returns(Result.Success());
        metadataCache.Remove(state.PluginId).Returns(Result.Success());
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            metadataCache: metadataCache,
            staging: staging,
            dataRetentionStore: dataRetentionStore,
            activationTracker: activationTracker,
            auditTrail: audit);

        var result = coordinator.UninstallPlugin(
            "owner",
            state.PluginId,
            PluginUninstallDataChoice.DeleteData,
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        staging.Received(1).Cleanup(state.RuntimePath);
        dataRetentionStore.Received(1).DeletePluginData(state.PluginId);
        stateStore.Received(1).Remove(state.PluginId);
        metadataCache.Received(1).Remove(state.PluginId);
        var auditEvent = audit.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.PluginUninstall);
        auditEvent.Details!["dataChoice"].ShouldBe("delete");
        auditEvent.Details!["restartRequired"].ShouldBe("false");
    }

    [Fact]
    public void Uninstall_activated_plugin_schedules_pending_uninstall_without_runtime_deletion()
    {
        var audit = new InMemoryAuditTrail();
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        var staging = Substitute.For<IPluginInstallStagingArea>();
        var activationTracker = Substitute.For<IPluginCurrentProcessActivationTracker>();
        var state = CreateState(isEnabled: true);
        stateStore.Get(state.PluginId).Returns(Result.Success<InstalledPluginState?>(state));
        activationTracker.IsActivatedInCurrentProcess(state.PluginId).Returns(true);
        stateStore.Upsert(Arg.Any<InstalledPluginState>()).Returns(Result.Success());
        metadataCache.Upsert(Arg.Any<InstalledPluginMetadata>()).Returns(Result.Success());
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            metadataCache: metadataCache,
            staging: staging,
            activationTracker: activationTracker,
            auditTrail: audit);

        var result = coordinator.UninstallPlugin(
            "owner",
            state.PluginId,
            PluginUninstallDataChoice.KeepData,
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        staging.DidNotReceive().Cleanup(Arg.Any<string>());
        stateStore.Received(1).Upsert(Arg.Is<InstalledPluginState>(updated =>
            updated.PluginId == state.PluginId &&
            !updated.IsEnabled &&
            updated.IsPendingUninstall &&
            updated.RestartRequired &&
            updated.DataRetentionChoice == "keep" &&
            updated.StatusSummary == "Pending uninstall"));
        metadataCache.Received(1).Upsert(Arg.Is<InstalledPluginMetadata>(metadata =>
            metadata.PluginId == state.PluginId &&
            !metadata.IsEnabled &&
            metadata.IsPendingUninstall &&
            metadata.RestartRequired &&
            metadata.StatusSummary == "Pending uninstall"));
        var auditEvent = audit.Snapshot().Single();
        auditEvent.EventName.ShouldBe(AuditEventName.PluginUninstall);
        auditEvent.Details!["restartRequired"].ShouldBe("true");
    }

    [Fact]
    public void Enable_plugin_rejects_pending_uninstall_state_before_state_mutation()
    {
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        var state = CreateState(isEnabled: false) with
        {
            IsPendingUninstall = true,
            RestartRequired = true,
            StatusSummary = "Pending uninstall"
        };
        stateStore.Get(state.PluginId).Returns(Result.Success<InstalledPluginState?>(state));
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            metadataCache: metadataCache);

        var result = coordinator.EnablePlugin("owner", state.PluginId, CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        stateStore.DidNotReceive().SetEnabled(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
        metadataCache.DidNotReceive().Upsert(Arg.Any<InstalledPluginMetadata>());
    }

    [Fact]
    public void Uninstall_plugin_rejects_pending_uninstall_state_before_cleanup()
    {
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var staging = Substitute.For<IPluginInstallStagingArea>();
        var dataRetentionStore = Substitute.For<IPluginDataRetentionStore>();
        var state = CreateState(isEnabled: false) with
        {
            IsPendingUninstall = true,
            RestartRequired = true,
            StatusSummary = "Pending uninstall"
        };
        stateStore.Get(state.PluginId).Returns(Result.Success<InstalledPluginState?>(state));
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            staging: staging,
            dataRetentionStore: dataRetentionStore);

        var result = coordinator.UninstallPlugin(
            "owner",
            state.PluginId,
            PluginUninstallDataChoice.DeleteData,
            CorrelationId.New());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
        staging.DidNotReceive().Cleanup(Arg.Any<string>());
        dataRetentionStore.DidNotReceive().DeletePluginData(Arg.Any<string>());
        stateStore.DidNotReceive().Remove(Arg.Any<string>());
    }

    [Fact]
    public void Uninstall_keep_data_does_not_delete_plugin_storage()
    {
        var stateStore = Substitute.For<IInstalledPluginStateStore>();
        var metadataCache = Substitute.For<IInstalledPluginMetadataCache>();
        var staging = Substitute.For<IPluginInstallStagingArea>();
        var dataRetentionStore = Substitute.For<IPluginDataRetentionStore>();
        var activationTracker = Substitute.For<IPluginCurrentProcessActivationTracker>();
        var state = CreateState(isEnabled: true);
        stateStore.Get(state.PluginId).Returns(Result.Success<InstalledPluginState?>(state));
        activationTracker.IsActivatedInCurrentProcess(state.PluginId).Returns(false);
        staging.Cleanup(state.RuntimePath).Returns(Result.Success());
        stateStore.Remove(state.PluginId).Returns(Result.Success());
        metadataCache.Remove(state.PluginId).Returns(Result.Success());
        var coordinator = CreateCoordinator(
            stateStore: stateStore,
            metadataCache: metadataCache,
            staging: staging,
            dataRetentionStore: dataRetentionStore,
            activationTracker: activationTracker);

        var result = coordinator.UninstallPlugin(
            "owner",
            state.PluginId,
            PluginUninstallDataChoice.KeepData,
            CorrelationId.New());

        result.IsSuccess.ShouldBeTrue();
        dataRetentionStore.DidNotReceive().DeletePluginData(Arg.Any<string>());
    }

    [Fact]
    public void File_system_plugin_data_retention_store_deletes_shared_and_user_scoped_plugin_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "dobby-plugin-data-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sharedPath = Path.Combine(root, "plugins", "storage", "shared-domain", "com.dobby.sample");
            var userPath = Path.Combine(root, "plugins", "storage", "user-scoped", "com.dobby.sample");
            Directory.CreateDirectory(sharedPath);
            Directory.CreateDirectory(userPath);
            File.WriteAllText(Path.Combine(sharedPath, "record.json"), "{}");
            File.WriteAllText(Path.Combine(userPath, "draft.json"), "{}");
            var store = new PluginDataRetentionStore(root);

            var result = store.DeletePluginData("com.dobby.sample");

            result.IsSuccess.ShouldBeTrue();
            Directory.Exists(sharedPath).ShouldBeFalse();
            Directory.Exists(userPath).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void File_system_plugin_data_retention_store_rejects_path_like_plugin_ids_without_deleting_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "dobby-plugin-data-" + Guid.NewGuid().ToString("N"));
        try
        {
            var unrelatedPath = Path.Combine(root, "plugins", "storage", "shared-domain", "plugin_sample");
            Directory.CreateDirectory(unrelatedPath);
            File.WriteAllText(Path.Combine(unrelatedPath, "record.json"), "{}");
            var store = new PluginDataRetentionStore(root);

            var result = store.DeletePluginData("plugin/sample");

            result.IsSuccess.ShouldBeFalse();
            result.ErrorCode.ShouldBe(ErrorCode.InvalidInput);
            Directory.Exists(unrelatedPath).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HostPluginLifecycleCoordinator CreateCoordinator(
        IInstalledPluginStateStore? stateStore = null,
        IInstalledPluginMetadataCache? metadataCache = null,
        IPluginInstallStagingArea? staging = null,
        IPluginDataRetentionStore? dataRetentionStore = null,
        IPluginCurrentProcessActivationTracker? activationTracker = null,
        OperationAuthorizationPolicy? policy = null,
        InMemoryAuditTrail? auditTrail = null)
    {
        activationTracker ??= Substitute.For<IPluginCurrentProcessActivationTracker>();
        return new HostPluginLifecycleCoordinator(
            stateStore ?? Substitute.For<IInstalledPluginStateStore>(),
            metadataCache ?? Substitute.For<IInstalledPluginMetadataCache>(),
            staging ?? Substitute.For<IPluginInstallStagingArea>(),
            dataRetentionStore ?? Substitute.For<IPluginDataRetentionStore>(),
            activationTracker,
            policy ?? CreatePolicy(new UserPermissionState("owner", IsOwner: true, IsActive: true)),
            new PluginRuntimeAuditCoordinator(
                auditTrail ?? new InMemoryAuditTrail(),
                new FakeTimeProvider(DateTimeOffset.Parse("2026-06-20T00:00:00Z"))));
    }

    private static OperationAuthorizationPolicy CreatePolicy(UserPermissionState state)
    {
        return new OperationAuthorizationPolicy(
            OperationTargetRegistry.CreateDefault(),
            new StaticGrantReader(state));
    }

    private static InstalledPluginState CreateState(bool isEnabled)
    {
        var now = DateTimeOffset.Parse("2026-06-20T00:00:00Z");
        return new InstalledPluginState(
            "com.dobby.sample",
            "1.0.0",
            "fingerprint",
            "Sample",
            "Sample plugin",
            isEnabled,
            "verified",
            "runtime-path",
            isEnabled ? "Enabled" : "Disabled",
            now,
            now);
    }

    private sealed class StaticGrantReader(UserPermissionState state) : IPermissionGrantReader
    {
        public Result<UserPermissionState> LoadUserState(string userId)
        {
            return userId.Equals(state.UserId, StringComparison.Ordinal)
                ? Result.Success(state)
                : Result.Failure<UserPermissionState>(ErrorCode.NotFound);
        }

        public Result<bool> HasGrant(string userId, OperationKey key)
        {
            return Result.Success(false);
        }
    }
}
