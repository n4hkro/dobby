using Dobby.Host;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.IntegrationTests.Licensing;

public sealed class LicensePersistenceMigrationTests
{
    [Fact]
    public void Post_login_migration_creates_license_state_tables_and_records_metadata()
    {
        using var fixture = LicensePersistenceFixture.Create();

        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();

        fixture.Executor.Query<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'PluginLicense%' ORDER BY name;")
            .ShouldBe(["PluginLicenseImportAttestation", "PluginLicenseState"]);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = PostLoginPluginPersistenceMigrations.LicenseStateMigrationId })
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = PostLoginPluginPersistenceMigrations.LicenseLifecycleInputsMigrationId })
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PluginLicenseState';")
            .ShouldBe(1);
        var licenseStateColumns = fixture.Executor.Query<string>(
                "SELECT name FROM pragma_table_info('PluginLicenseState') ORDER BY name;")
            .ToArray();
        licenseStateColumns.ShouldContain("ExpiresAtUtc");
        licenseStateColumns.ShouldContain("GracePeriodDays");
        licenseStateColumns.ShouldContain("NotBeforeUtc");
        licenseStateColumns.ShouldContain("SourceStatus");
    }

    [Fact]
    public void Host_license_state_store_round_trips_state_and_limited_attestation()
    {
        using var fixture = LicensePersistenceFixture.Create();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();
        var store = new HostLicenseStateStore(fixture.Executor, fixture.TransactionScope);
        var key = new RevisionSeriesKey("plugin.sample", "default", "default");
        var importedAt = new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero);
        var state = new LicenseStateRecord(
            key,
            "plugin.sample",
            "default",
            "default",
            new string('a', 64),
            new string('b', 64),
            7,
            LicenseLifecycleState.Active,
            importedAt,
            new DateTimeOffset(2026, 06, 18, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 07, 18, 0, 0, 0, TimeSpan.Zero),
            3,
            LicenseArtifactStatus.Active);

        store.Save(state);
        store.AppendAttestation(new LicenseImportAttestation(
            new string('c', 64),
            7,
            importedAt,
            "succeeded",
            "corr-1"));

        store.Read(key).ShouldBe(state);
        var attestationColumns = fixture.Executor.Query<string>(
                "SELECT name FROM pragma_table_info('PluginLicenseImportAttestation') ORDER BY name;")
            .ToArray();
        attestationColumns.ShouldBe([
            "AttestationId",
            "CorrelationId",
            "ImportedAtUtc",
            "LicenseRevision",
            "Result",
            "SourceArtifactHash"
        ]);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM PluginLicenseImportAttestation;")
            .ShouldBe(1);
    }

    private sealed class LicensePersistenceFixture : IDisposable
    {
        private LicensePersistenceFixture(string root)
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

        public static LicensePersistenceFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dobby-license-state-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new LicensePersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
