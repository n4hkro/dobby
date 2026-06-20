using System.Text;
using System.Security.Cryptography;
using Dobby.Core;
using Dobby.Host.Plugins;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.PluginFramework.Activation;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authorization;
using Dobby.Security.Trust;
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
                "SELECT name FROM sqlite_master WHERE type='table' AND (name LIKE 'InstalledPlugin%' OR name = 'PluginInstallStaging') ORDER BY name;")
            .ToArray();

        tables.ShouldContain("InstalledPluginState");
        tables.ShouldContain("InstalledPluginMetadataCache");
        tables.ShouldContain("PluginInstallStaging");
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
        var installer = new PluginPackageInstaller(staging, stateStore, metadataCache);

        var result = installer.Install(package);

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.ErrorCode.ToString());
        var state = stateStore.Get(package.Manifest.PluginId).Value;
        state.ShouldNotBeNull();
        state!.DisplayName.ShouldBe(package.Manifest.DisplayName);
        state.Description.ShouldBe(package.Manifest.Description);
        var reread = PluginPackageReader.Read(state.RuntimePath);
        reread.IsSuccess.ShouldBeTrue(reread.IsSuccess ? null : "Installed plugin runtime manifest must remain readable.");
        reread.Value.Manifest.PluginId.ShouldBe(package.Manifest.PluginId);
        metadataCache.Get(package.Manifest.PluginId).Value.ShouldNotBeNull();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallStaging;").ShouldBe(0);
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
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallStaging;").ShouldBe(1);

        staging.Recover().Value.ShouldBe(1);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallStaging;").ShouldBe(0);
        Directory.Exists(staged).ShouldBeFalse();
    }

    [Fact]
    public void Staging_area_removes_materialized_stage_when_staging_row_write_fails()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var package = CreatePackage();
        var failingExecutor = new FailingExecuteCommandExecutor(fixture.Executor, "INSERT INTO PluginInstallStaging");
        var staging = new PluginInstallStagingArea(fixture.Root, failingExecutor);

        var result = staging.Stage(package);

        result.IsSuccess.ShouldBeFalse();
        var stagingRoot = Path.Combine(fixture.Root, "plugins", "staging");
        Directory.EnumerateDirectories(stagingRoot).ShouldBeEmpty();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallStaging;").ShouldBe(0);
    }

    [Fact]
    public void Staging_area_removes_promoted_runtime_when_staging_row_update_fails()
    {
        using var fixture = PluginPersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All).IsSuccess.ShouldBeTrue();

        var package = CreatePackage();
        var staging = new PluginInstallStagingArea(fixture.Root, fixture.Executor);
        var staged = staging.Stage(package).Value;
        var failingExecutor = new FailingExecuteCommandExecutor(fixture.Executor, "UPDATE PluginInstallStaging");
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
        Directory.Exists(runtimePath).ShouldBeFalse();
        Directory.Exists(staged).ShouldBeFalse();
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginInstallStaging;").ShouldBe(0);
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
            staging,
            OperationTargetRegistry.CreateDefault(),
            extensionPointRegistry,
            lifetimeManager,
            new HostPluginActivationCancellation(),
            new PostLoginPluginActivator(new PluginServiceRegistrationBridge(extensionPointRegistry, lifetimeManager)),
            new AllowAllTrustVerifier());

        var result = coordinator.ApplyPostLoginMigrations();

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Error.Message);
        Directory.Exists(retainedRuntimePath).ShouldBeTrue();
        Directory.Exists(orphanRuntimePath).ShouldBeFalse();
        metadataCache.Snapshot().Value.Select(item => item.PluginId).ShouldBe(["com.dobby.keep"]);
    }

    private static PluginPackageDescriptor CreatePackage()
    {
        var fileContent = Encoding.UTF8.GetBytes("hello plugin");
        var fileHash = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();
        var manifest = new PluginManifest(
            "com.dobby.sample",
            "dobby.vendor",
            "1.0.0",
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
            "{\"pluginId\":\"com.dobby.sample\",\"version\":\"1.0.0\",\"vendorId\":\"dobby.vendor\",\"minHostVersion\":\"1.0.0\",\"entryAssembly\":\"bin/plugins/com.dobby.sample.dll\",\"displayName\":\"Sample Plugin\",\"description\":\"Sample plugin description\",\"dependsOn\":[],\"capabilities\":[{\"name\":\"plugin-page\",\"scope\":\"plugin-page\"}],\"operationTargets\":[{\"id\":\"com.dobby.sample.greet\",\"verbs\":[\"execute\"],\"labelKey\":\"sample.greet.label\"}],\"extensionPoints\":[{\"id\":\"sample.home\",\"target\":\"plugin-page-region\",\"routeKey\":\"sample.home\",\"labelKey\":\"sample.home.label\",\"group\":\"default\",\"weight\":100}],\"files\":[{\"path\":\"content/hello.txt\",\"sha256\":\"" +
            fileHash +
            "\"}],\"signature\":{\"algorithm\":\"ECDSA-P256-SHA256\",\"keyVersion\":1,\"value\":\"AQ\"}}";
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
}
