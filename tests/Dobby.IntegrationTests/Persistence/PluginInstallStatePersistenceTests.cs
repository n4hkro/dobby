using System.Text;
using System.Security.Cryptography;
using Dobby.Audit;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.IntegrationTests.Plugins;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
using NSubstitute;
using Shouldly;

namespace Dobby.IntegrationTests.Persistence;

public sealed class PluginInstallStatePersistenceTests
{
    [Fact]
    public void Post_login_migration_creates_authoritative_plugin_state_and_staging_tables()
    {
        using var fixture = PluginPersistenceFixture.Create();

        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();

        var tables = fixture.Executor.Query<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND (name LIKE 'InstalledPlugin%' OR name LIKE 'PluginInstall%') ORDER BY name;")
            .ToArray();

        tables.ShouldContain("InstalledPluginState");
        tables.ShouldContain("InstalledPluginMetadataCache");
        tables.ShouldContain("PluginInstallStaging");
        tables.ShouldContain("InstalledPluginVersion");
        tables.ShouldContain("InstalledPluginVersionPointer");
        tables.ShouldContain("PluginInstallOperation");
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = PostLoginPluginPersistenceMigrations.MigrationId })
            .ShouldBe(1);
    }

    [Fact]
    public void Post_login_migration_registers_append_only_plugin_lifecycle_state_columns()
    {
        using var fixture = PluginPersistenceFixture.Create();

        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();

        PostLoginPluginPersistenceMigrations.All.Keys.ShouldContain("0005_plugin_lifecycle_state");
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = "0005_plugin_lifecycle_state" })
            .ShouldBe(1);
        var stateColumns = fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('InstalledPluginState');")
            .ToHashSet(StringComparer.Ordinal);
        stateColumns.ShouldContain("IsPendingUninstall");
        stateColumns.ShouldContain("DataRetentionChoice");
        stateColumns.ShouldContain("RestartRequired");
        stateColumns.ShouldContain("LastActivatedAtUtc");
        var cacheColumns = fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('InstalledPluginMetadataCache');")
            .ToHashSet(StringComparer.Ordinal);
        cacheColumns.ShouldContain("IsPendingUninstall");
        cacheColumns.ShouldContain("RestartRequired");
    }

    [Fact]
    public void Post_login_migration_registers_plugin_storage_table_append_only()
    {
        using var fixture = PluginPersistenceFixture.Create();

        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();

        PostLoginPluginPersistenceMigrations.All.Keys.ShouldContain("0006_plugin_storage_values");
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = "0006_plugin_storage_values" })
            .ShouldBe(1);
        var columns = fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('PluginStorageValue');")
            .ToHashSet(StringComparer.Ordinal);
        columns.ShouldContain("PluginId");
        columns.ShouldContain("Scope");
        columns.ShouldContain("UserScopeHash");
        columns.ShouldContain("StorageKey");
        columns.ShouldContain("Payload");
        columns.ShouldContain("ContentType");
        columns.ShouldContain("SchemaVersion");
        columns.ShouldContain("CreatedAtUtc");
        columns.ShouldContain("UpdatedAtUtc");
    }

    [Fact]
    public void Plugin_lifecycle_state_migration_applies_after_existing_post_login_migrations()
    {
        using var fixture = PluginPersistenceFixture.Create();
        var priorMigrations = PostLoginPluginPersistenceMigrations.All
            .Where(pair => !pair.Key.Equals(
                PostLoginPluginPersistenceMigrations.PluginLifecycleStateMigrationId,
                StringComparison.Ordinal))
            .ToDictionary(StringComparer.Ordinal);
        var lifecycleMigration = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PostLoginPluginPersistenceMigrations.PluginLifecycleStateMigrationId] =
                PostLoginPluginPersistenceMigrations.All[
                    PostLoginPluginPersistenceMigrations.PluginLifecycleStateMigrationId]
        };

        fixture.Migrator.ApplyPostLoginMigrations(priorMigrations).IsSuccess.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = PostLoginPluginPersistenceMigrations.PluginLifecycleStateMigrationId })
            .ShouldBe(0);

        fixture.Migrator.ApplyPostLoginMigrations(lifecycleMigration).IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = PostLoginPluginPersistenceMigrations.PluginLifecycleStateMigrationId })
            .ShouldBe(1);
        var stateColumns = fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('InstalledPluginState');")
            .ToHashSet(StringComparer.Ordinal);
        stateColumns.ShouldContain("IsPendingUninstall");
        stateColumns.ShouldContain("DataRetentionChoice");
        stateColumns.ShouldContain("RestartRequired");
        stateColumns.ShouldContain("LastActivatedAtUtc");
        var cacheColumns = fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('InstalledPluginMetadataCache');")
            .ToHashSet(StringComparer.Ordinal);
        cacheColumns.ShouldContain("IsPendingUninstall");
        cacheColumns.ShouldContain("RestartRequired");
    }

    [Fact]
    public void Installed_plugin_state_and_metadata_cache_expose_lifecycle_fields()
    {
        typeof(InstalledPluginState).GetProperty("IsPendingUninstall").ShouldNotBeNull();
        typeof(InstalledPluginState).GetProperty("DataRetentionChoice").ShouldNotBeNull();
        typeof(InstalledPluginState).GetProperty("RestartRequired").ShouldNotBeNull();
        typeof(InstalledPluginState).GetProperty("LastActivatedAtUtc").ShouldNotBeNull();
        typeof(InstalledPluginMetadata).GetProperty("IsPendingUninstall").ShouldNotBeNull();
        typeof(InstalledPluginMetadata).GetProperty("RestartRequired").ShouldNotBeNull();
    }

    [Fact]
    public void Plugin_package_installer_writes_state_cache_and_cleans_staging_artifacts()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var package = CreatePackage();
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);
        var stateStore = new HostInstalledPluginStateStore(fixture.Executor, fixture.TransactionScope);
        var metadataCache = new HostInstalledPluginMetadataCache(fixture.Executor, fixture.TransactionScope);
        var auditTrail = new DurableAuditTrail(fixture.Executor);
        var committer = new HostPluginInstallStateCommitter(
            stateStore,
            metadataCache,
            new HostMutationUnitOfWork(
                fixture.TransactionScope,
                auditTrail,
                new HostTransactionOutbox(TimeProvider.System)),
            TimeProvider.System);
        var installer = new PluginPackageInstaller(staging, stateStore, metadataCache, committer);
        var correlationId = CorrelationId.New();

        var result = installer.Install(
            package,
            new PluginInstallCommitContext("owner", correlationId));

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        var state = stateStore.Get(package.Manifest.PluginId).Value;
        state.ShouldNotBeNull();
        state!.DisplayName.ShouldBe(package.Manifest.DisplayName);
        state.Description.ShouldBe(package.Manifest.Description);
        var reread = PluginPackageReader.Read(state.RuntimePath);
        reread.IsSuccess.ShouldBeTrue(reread.IsSuccess ? null : "Installed plugin runtime manifest must remain readable.");
        reread.Value.Manifest.PluginId.ShouldBe(package.Manifest.PluginId);
        metadataCache.Get(package.Manifest.PluginId).Value.ShouldNotBeNull();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM PluginInstallOperation WHERE State <> 'Completed';").ShouldBe(0);
        fixture.Executor.Query<string>(
            "SELECT ActiveFingerprint FROM InstalledPluginVersionPointer WHERE PluginId = @PluginId;",
            new { PluginId = package.Manifest.PluginId }).Single().ShouldBe(package.Fingerprint);
        fixture.Executor.Query<string>(
            "SELECT LastKnownGoodFingerprint FROM InstalledPluginVersionPointer WHERE PluginId = @PluginId;",
            new { PluginId = package.Manifest.PluginId }).Single().ShouldBe(package.Fingerprint);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE Purpose = 'plugin-install' AND MessageType = 'state-committed';")
            .ShouldBe(1);
        auditTrail.Snapshot().Single(item => item.EventName == AuditEventName.PluginInstallVerified)
            .CorrelationId.ShouldBe(correlationId);
        Directory.Exists(Path.Combine(fixture.Root, "plugins", "staging")).ShouldBeTrue();
        Directory.GetFiles(Path.Combine(fixture.Root, "plugins", "installed"), "*", SearchOption.AllDirectories).ShouldNotBeEmpty();
    }

    [Fact]
    public void Staging_area_recovery_removes_orphaned_rows_and_files_before_next_activation()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var package = CreatePackage();
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);
        var staged = staging.Stage(package).Value;
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM PluginInstallOperation WHERE State = 'Verified';").ShouldBe(1);

        staging.Recover().Value.ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM PluginInstallOperation WHERE State = 'Completed';").ShouldBe(1);
        Directory.Exists(staged).ShouldBeFalse();
        staging.Recover().Value.ShouldBe(0);
    }

    [Fact]
    public void Failed_update_activation_restores_last_known_good_version_and_orphan_cleanup_preserves_authority()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
        var stateStore = new HostInstalledPluginStateStore(fixture.Executor, fixture.TransactionScope);
        var metadataCache = new HostInstalledPluginMetadataCache(fixture.Executor, fixture.TransactionScope);
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);
        var auditTrail = new DurableAuditTrail(fixture.Executor);
        var committer = new HostPluginInstallStateCommitter(
            stateStore,
            metadataCache,
            new HostMutationUnitOfWork(
                fixture.TransactionScope,
                auditTrail,
                new HostTransactionOutbox(TimeProvider.System)),
            TimeProvider.System);
        var installer = new PluginPackageInstaller(staging, stateStore, metadataCache, committer);

        var v1 = installer.Install(
            CreatePackage("1.0.0"),
            new PluginInstallCommitContext("owner", CorrelationId.New())).Value;
        var activationPersistence = new HostPluginActivationStatusPersistence(
            stateStore,
            metadataCache,
            fixture.TransactionScope);
        activationPersistence.Persist([v1], [v1]).IsSuccess.ShouldBeTrue();
        fixture.Executor.Query<string>(
            "SELECT PendingActivationFingerprint FROM InstalledPluginVersionPointer WHERE PluginId = @PluginId;",
            new { v1.PluginId }).SingleOrDefault().ShouldBeNull();

        var v2 = installer.Install(
            CreatePackage("2.0.0"),
            new PluginInstallCommitContext("owner", CorrelationId.New())).Value;
        var beforeFailure = fixture.Executor.Query<PointerSnapshot>(
            """
            SELECT ActiveFingerprint, LastKnownGoodFingerprint, PendingActivationFingerprint
            FROM InstalledPluginVersionPointer WHERE PluginId = @PluginId;
            """,
            new { v2.PluginId }).Single();
        beforeFailure.ActiveFingerprint.ShouldBe(v2.PackageFingerprint);
        beforeFailure.LastKnownGoodFingerprint.ShouldBe(v1.PackageFingerprint);
        beforeFailure.PendingActivationFingerprint.ShouldBe(v2.PackageFingerprint);

        var failed = v2 with
        {
            StatusSummary = "Faulted - Plugin activation failed",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        activationPersistence.Persist([failed], [failed]).IsSuccess.ShouldBeTrue();

        var restored = stateStore.Get(v1.PluginId).Value;
        restored.ShouldNotBeNull();
        restored!.PackageFingerprint.ShouldBe(v1.PackageFingerprint);
        restored.RuntimePath.ShouldBe(v1.RuntimePath);
        metadataCache.Get(v1.PluginId).Value!.PackageFingerprint.ShouldBe(v1.PackageFingerprint);
        var afterFailure = fixture.Executor.Query<PointerSnapshot>(
            """
            SELECT ActiveFingerprint, LastKnownGoodFingerprint, PendingActivationFingerprint
            FROM InstalledPluginVersionPointer WHERE PluginId = @PluginId;
            """,
            new { v1.PluginId }).Single();
        afterFailure.ActiveFingerprint.ShouldBe(v1.PackageFingerprint);
        afterFailure.LastKnownGoodFingerprint.ShouldBe(v1.PackageFingerprint);
        afterFailure.PendingActivationFingerprint.ShouldBeNull();

        staging.CleanupRuntimeOrphans([v1.RuntimePath]).Value.ShouldBe(1);
        Directory.Exists(v1.RuntimePath).ShouldBeTrue();
        Directory.Exists(v2.RuntimePath).ShouldBeFalse();
    }

    [Fact]
    public void Staging_area_removes_materialized_stage_when_staging_row_write_fails()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var package = CreatePackage();
        var failingExecutor = new FailingExecuteCommandExecutor(fixture.Executor, "INSERT INTO PluginInstallOperation");
        var staging = new PluginInstallStagingArea(fixture.Root, failingExecutor);

        var result = staging.Stage(package);

        result.IsSuccess.ShouldBeFalse();
        var stagingRoot = Path.Combine(fixture.Root, "plugins", "staging");
        Directory.EnumerateDirectories(stagingRoot).ShouldBeEmpty();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallOperation;").ShouldBe(0);
    }

    [Fact]
    public void Staging_area_rejects_hostile_paths_before_any_payload_can_escape_the_stage()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
        var package = CreatePackage();
        var content = Encoding.UTF8.GetBytes("escape");
        var hostile = package with
        {
            Entries =
            [
                .. package.Entries,
                new PluginPackageEntry(
                    "../outside.txt",
                    content,
                    Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                    false)
            ]
        };
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);

        var result = staging.Stage(hostile);

        result.IsSuccess.ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Root, "plugins", "outside.txt")).ShouldBeFalse();
        Directory.EnumerateDirectories(Path.Combine(fixture.Root, "plugins", "staging")).ShouldBeEmpty();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM PluginInstallOperation WHERE State <> 'Completed';").ShouldBe(0);
    }

    [Fact]
    public void Staging_area_rehashes_streamed_bytes_and_cleans_up_when_content_changes_after_validation()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
        var package = CreatePackage();
        var payload = package.Entries.Single(entry => entry.Path == "content/hello.txt");
        payload.Content[0] ^= 0xff;
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);

        var result = staging.Stage(package);

        result.IsSuccess.ShouldBeFalse();
        Directory.EnumerateDirectories(Path.Combine(fixture.Root, "plugins", "staging")).ShouldBeEmpty();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM PluginInstallOperation WHERE State <> 'Completed';").ShouldBe(0);
    }

    [Fact]
    public void Staging_area_recovery_removes_promoted_runtime_when_post_rename_state_update_fails()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var package = CreatePackage();
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);
        var staged = staging.Stage(package).Value;
        var failingExecutor = new FailingNthExecuteCommandExecutor(
            fixture.Executor,
            "UPDATE PluginInstallOperation",
            failOccurrence: 2);
        var failingStaging = new PluginInstallStagingArea(fixture.Root, failingExecutor);
        var runtimePath = Path.Combine(
            fixture.Root,
            "plugins",
            "installed",
            package.Manifest.PluginId,
            package.Manifest.Version,
            package.Fingerprint);

        var result = failingStaging.Promote(staged, package);

        result.IsSuccess.ShouldBeFalse();
        Directory.Exists(runtimePath).ShouldBeTrue();
        Directory.Exists(staged).ShouldBeFalse();
        fixture.Executor.Query<string>(
            "SELECT State FROM PluginInstallOperation;").Single().ShouldBe("ReadyToPromote");

        staging.Recover().Value.ShouldBe(1);

        Directory.Exists(runtimePath).ShouldBeFalse();
        fixture.Executor.Query<string>(
            "SELECT State FROM PluginInstallOperation;").Single().ShouldBe("Completed");
    }

    [Fact]
    public async Task Post_login_recovery_removes_runtime_orphans_and_rebuilds_metadata_cache_from_state()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var stateStore = new HostInstalledPluginStateStore(fixture.Executor, fixture.TransactionScope);
        var metadataCache = new HostInstalledPluginMetadataCache(fixture.Executor, fixture.TransactionScope);
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);
        var installedRoot = Path.Combine(fixture.Root, "plugins", "installed");
        var retainedRuntimePath = Path.Combine(installedRoot, "com.dobby.keep", "1.0.0", "fingerprint-keep");
        var orphanRuntimePath = Path.Combine(installedRoot, "com.dobby.orphan", "1.0.0", "fingerprint-orphan");
        Directory.CreateDirectory(retainedRuntimePath);
        Directory.CreateDirectory(orphanRuntimePath);
        File.WriteAllText(Path.Combine(retainedRuntimePath, "state-backed.txt"), "keep", Encoding.UTF8);
        File.WriteAllText(Path.Combine(orphanRuntimePath, "orphan.txt"), "remove", Encoding.UTF8);
        var now = DateTimeOffset.UtcNow;
        var state = new InstalledPluginState(
            "com.dobby.keep",
            "1.0.0",
            "fingerprint-keep",
            "Kept Plugin",
            "State-backed plugin",
            IsEnabled: true,
            ValidationSummary: "verified",
            RuntimePath: retainedRuntimePath,
            StatusSummary: "Enabled",
            InstalledAtUtc: now,
            UpdatedAtUtc: now);
        stateStore.Upsert(state).IsSuccess.ShouldBeTrue();
        metadataCache.Upsert(new InstalledPluginMetadata(
            "com.dobby.stale",
            "Stale Plugin",
            "1.0.0",
            "Stale cache row",
            "fingerprint-stale",
            IsEnabled: true,
            StatusSummary: "Enabled",
            InstalledAtUtc: now,
            UpdatedAtUtc: now)).IsSuccess.ShouldBeTrue();
        var extensionPointRegistry = new PluginExtensionPointRegistry();
        await using var lifetimeManager = new PluginLifetimeManager();
        var coordinator = new HostPluginRuntimeCoordinator(
            fixture.Migrator,
            stateStore,
            metadataCache,
            new HostPluginActivationStatusPersistence(stateStore, metadataCache, fixture.TransactionScope),
            staging,
            new PluginDataRetentionStore(fixture.Root, fixture.TransactionScope),
            OperationTargetRegistry.CreateDefault(),
            extensionPointRegistry,
            lifetimeManager,
            new HostPluginActivationCancellation(),
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionPointRegistry, lifetimeManager)),
            new PluginCurrentProcessActivationTracker(),
            new AllowAllTrustVerifier());

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Error.Message);
        Directory.Exists(retainedRuntimePath).ShouldBeTrue();
        Directory.Exists(orphanRuntimePath).ShouldBeFalse();
        metadataCache.Snapshot().Value.Select(item => item.PluginId).ShouldBe(["com.dobby.keep"]);
    }

    [Fact]
    public async Task Activation_status_persistence_is_atomic_when_metadata_cache_rebuild_fails()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();
        using var runtimeFixture = SamplePluginPackageFixture.CreateRuntimeDirectory();
        var archive = PluginPackageReader.Read(runtimeFixture.PackagePath);
        archive.IsSuccess.ShouldBeTrue();
        var packageFingerprint = PluginPackageFingerprint.Compute(archive.Value.Manifest).Fingerprint;
        var originalState = new InstalledPluginState(
            SamplePluginPackageFixture.PluginId,
            SamplePluginPackageFixture.Version,
            packageFingerprint,
            "Sample Plugin",
            "Sample plugin description",
            IsEnabled: true,
            ValidationSummary: "verified",
            RuntimePath: runtimeFixture.PackagePath,
            StatusSummary: "Pending activation",
            InstalledAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);
        var seedStateStore = new HostInstalledPluginStateStore(fixture.Executor, fixture.TransactionScope);
        var seedMetadataCache = new HostInstalledPluginMetadataCache(fixture.Executor, fixture.TransactionScope);
        seedStateStore.Upsert(originalState).IsSuccess.ShouldBeTrue();
        seedMetadataCache.RebuildFrom([originalState]).IsSuccess.ShouldBeTrue();

        var failingScope = new InterceptingTransactionScope(
            fixture.TransactionScope,
            sql => sql.Contains("DELETE FROM InstalledPluginMetadataCache;", StringComparison.Ordinal));
        var stateStore = new HostInstalledPluginStateStore(fixture.Executor, failingScope);
        var metadataCache = new HostInstalledPluginMetadataCache(fixture.Executor, failingScope);
        var activationStatusPersistence = new HostPluginActivationStatusPersistence(
            stateStore,
            metadataCache,
            failingScope);
        var extensionPointRegistry = new PluginExtensionPointRegistry();
        await using var lifetimeManager = new PluginLifetimeManager();
        var activationTracker = NSubstitute.Substitute.For<IPluginCurrentProcessActivationTracker>();
        await using var coordinator = new HostPluginRuntimeCoordinator(
            fixture.Migrator,
            stateStore,
            metadataCache,
            activationStatusPersistence,
            new PluginInstallStagingArea(fixture.Root, fixture.Executor),
            new PluginDataRetentionStore(fixture.Root, failingScope),
            OperationTargetRegistry.CreateDefault(),
            extensionPointRegistry,
            lifetimeManager,
            new HostPluginActivationCancellation(),
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionPointRegistry, lifetimeManager)),
            activationTracker,
            new AllowAllTrustVerifier());

        var activation = await coordinator.ActivateInstalledPluginsAsync(new Dobby.Core.Versioning.HostVersion(1, 0, 0), CancellationToken.None);

        activation.IsSuccess.ShouldBeFalse();
        activation.ErrorCode.ShouldBe(ErrorCode.DatabaseUnavailable);
        stateStore.Get(SamplePluginPackageFixture.PluginId).Value!.StatusSummary.ShouldBe("Pending activation");
        metadataCache.Get(SamplePluginPackageFixture.PluginId).Value!.StatusSummary.ShouldBe("Pending activation");
        extensionPointRegistry.GetRegistrationsSnapshot<Dobby.Contracts.PluginPages.IPluginPage>().ShouldBeEmpty();
        activationTracker.DidNotReceive().MarkActivatedInCurrentProcess(Arg.Any<IReadOnlyCollection<string>>());
    }

    private static PluginPackageDescriptor CreatePackage(string version = "1.0.0")
    {
        var fileContent = Encoding.UTF8.GetBytes("hello plugin");
        var fileHash = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();
        var manifest = new PluginManifest(
            "com.dobby.sample",
            "dobby.vendor",
            version,
            "1.0.0",
            "bin/plugins/com.dobby.sample.dll",
            "Sample Plugin",
            "Sample plugin description",
            null,
            [],
            [new PluginCapability("plugin-page", "plugin-page")],
            [new PluginOperationTarget("com.dobby.sample.greet", ["execute"], "sample.greet.label")],
            [new PluginExtensionPoint("sample.home", "plugin-page-region", "sample.home", "sample.home.label", null, "default", 100)],
            [new PluginPackageFile("content/hello.txt", fileHash)],
            new PluginPackageSignature("ECDSA-P256-SHA256", 1, "AQ"));
        var fingerprint = PluginPackageFingerprint.Compute(manifest);
        var manifestJson =
            "{\"schemaVersion\":2,\"purpose\":\"dobby.plugin-package\",\"pluginId\":\"com.dobby.sample\",\"version\":\"" + version + "\",\"vendorId\":\"dobby.vendor\",\"hostApiVersion\":\"2.0\",\"minHostVersion\":\"1.0.0\",\"entryAssembly\":\"bin/plugins/com.dobby.sample.dll\",\"displayName\":\"Sample Plugin\",\"description\":\"Sample plugin description\",\"dependsOn\":[],\"capabilities\":[{\"name\":\"plugin-page\",\"scope\":\"plugin-page\"}],\"operationTargets\":[{\"id\":\"com.dobby.sample.greet\",\"verbs\":[\"execute\"],\"labelKey\":\"sample.greet.label\"}],\"extensionPoints\":[{\"id\":\"sample.home\",\"target\":\"plugin-page-region\",\"routeKey\":\"sample.home\",\"labelKey\":\"sample.home.label\",\"group\":\"default\",\"weight\":100}],\"files\":[{\"path\":\"content/hello.txt\",\"sha256\":\"" +
            fileHash +
            "\"}],\"signature\":{\"purpose\":\"dobby.plugin-package\",\"algorithm\":\"ECDSA-P256-SHA256\",\"format\":\"IeeeP1363FixedFieldConcatenation\",\"keyId\":\"plugin-prod-2026-01\",\"value\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}}";
        return new PluginPackageDescriptor(
            "package.dobbyplugin",
            fingerprint.Fingerprint,
            fingerprint.CanonicalPayload,
            manifest,
            [
                new PluginPackageEntry(PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson))).ToLowerInvariant(), false),
                new PluginPackageEntry("content/hello.txt", fileContent, fileHash, false)
            ],
            PluginPackageReader.ManifestFileName,
            manifestJson);
    }

    private sealed record PointerSnapshot(
        string ActiveFingerprint,
        string LastKnownGoodFingerprint,
        string? PendingActivationFingerprint);

    private sealed class PluginPersistenceFixture : IDisposable
    {
        private PluginPersistenceFixture(string root)
        {
            Root = root;
            var options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            Probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            Probe.IsAvailable().ShouldBeTrue();
            Factory = new EncryptedSqliteConnectionFactory(options, Probe);
            Migrator = new HostPersistenceMigrator(Factory, Probe);
            Executor = new HostPersistenceCommandExecutor(Factory);
            TransactionScope = new HostPersistenceTransactionScope(Factory);
            Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        }

        public string Root { get; }

        public ProtectedMaterialAvailabilityProbe Probe { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public HostPersistenceTransactionScope TransactionScope { get; }

        public static PluginPersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-plugin-state-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new PluginPersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class AllowAllTrustVerifier : IPluginTrustVerifier
    {
        public PluginTrustVerificationResult Verify(PluginTrustPayload payload) => PluginTrustVerificationResult.Success();
    }

    private sealed class FailingExecuteCommandExecutor(
        IHostPersistenceCommandExecutor inner,
        string failSqlSnippet) : IHostPersistenceCommandExecutor
    {
        public int Execute(string sql, object? parameters = null)
        {
            if (sql.Contains(failSqlSnippet, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Injected persistence failure.");
            }

            return inner.Execute(sql, parameters);
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            return inner.Query<T>(sql, parameters);
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            return inner.QueryScalar<T>(sql, parameters);
        }
    }

    private sealed class FailingNthExecuteCommandExecutor(
        IHostPersistenceCommandExecutor inner,
        string failSqlSnippet,
        int failOccurrence) : IHostPersistenceCommandExecutor
    {
        private int matchingExecuteCount;

        public int Execute(string sql, object? parameters = null)
        {
            if (sql.Contains(failSqlSnippet, StringComparison.Ordinal) &&
                Interlocked.Increment(ref matchingExecuteCount) == failOccurrence)
            {
                throw new InvalidOperationException("Injected persistence failure.");
            }

            return inner.Execute(sql, parameters);
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null) =>
            inner.Query<T>(sql, parameters);

        public T QueryScalar<T>(string sql, object? parameters = null) =>
            inner.QueryScalar<T>(sql, parameters);
    }

    private sealed class InterceptingTransactionScope(
        IHostPersistenceTransactionScope inner,
        Func<string, bool> shouldFail) : IHostPersistenceTransactionScope
    {
        public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
        {
            inner.ExecuteInTransaction(tx => action(new InterceptingCommandExecutor(tx, shouldFail)));
        }
    }

    private sealed class InterceptingCommandExecutor(
        IHostPersistenceCommandExecutor inner,
        Func<string, bool> shouldFail) : IHostPersistenceCommandExecutor
    {
        public int Execute(string sql, object? parameters = null)
        {
            if (shouldFail(sql))
            {
                throw new InvalidOperationException("Injected transaction failure.");
            }

            return inner.Execute(sql, parameters);
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            return inner.Query<T>(sql, parameters);
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            return inner.QueryScalar<T>(sql, parameters);
        }
    }
}
