using System.IO;
using System.Text;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Data.Sqlite;
using Shouldly;
using System.Security.Cryptography;

namespace Dobby.IntegrationTests.Persistence;

public sealed class HostPersistenceTests
{
    private const string RequiredProviderProofMarker = "__dobby_storage_marker_v1__";

    private static readonly string[] ApprovedPreLoginTables =
    [
        "AuditLog",
        "ConsumedRecoveryNonces",
        "Credentials",
        "CustomerDeploymentContext",
        "DeviceIdentity",
        "HostConfiguration",
        "HostOutbox",
        "LoginThrottleBuckets",
        "Permissions",
        "RecoveryRequests",
        "RememberMeSlot",
        "Sessions",
        "StorageMetadata",
        "Users"
    ];

    [Fact]
    public void Pre_login_migration_creates_only_approved_closed_table_set()
    {
        using var fixture = HostPersistenceFixture.Create();

        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        var tables = fixture.Executor.Query<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;")
            .ToArray();
        tables.ShouldBe(ApprovedPreLoginTables);

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0001_prelogin_bootstrap';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0007_prelogin_remembered_account';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0008_prelogin_login_throttle';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0009_prelogin_host_outbox';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0010_prelogin_recovery_requests';")
            .ShouldBe(1);

        fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('RememberMeSlot') ORDER BY cid;")
            .ShouldBe([
                "SlotId",
                "UserId",
                "UsernameNormalized",
                "Version",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            ]);

        var permissionColumns = fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('Permissions');")
            .ToArray();
        permissionColumns.ShouldNotContain("GrantState");
    }

    [Fact]
    public void Pre_login_migration_preserves_valid_username_and_physically_removes_legacy_password_columns()
    {
        using var fixture = HostPersistenceFixture.Create();
        var now = DateTimeOffset.UtcNow.ToString("O");
        fixture.Executor.Execute(
            """
            CREATE TABLE Users (
                UserId TEXT PRIMARY KEY,
                UsernameNormalized TEXT NOT NULL,
                DisplayName TEXT,
                IsOwner INTEGER NOT NULL CHECK (IsOwner IN (0,1)),
                IsActive INTEGER NOT NULL CHECK (IsActive IN (0,1)),
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE TABLE RememberMeSlot (
                SlotId TEXT PRIMARY KEY,
                UserId TEXT,
                MaskedUsername TEXT,
                ProtectedRememberMeBlob BLOB,
                ProtectionVersion TEXT,
                CreatedAtUtc TEXT,
                UpdatedAtUtc TEXT,
                ExpiresAtUtc TEXT,
                FOREIGN KEY (UserId) REFERENCES Users(UserId)
            );
            INSERT INTO Users(
                UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('user-1', 'owner', 'Owner', 1, 1, @Now, @Now);
            INSERT INTO RememberMeSlot(
                SlotId, UserId, MaskedUsername, ProtectedRememberMeBlob, ProtectionVersion,
                CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc)
            VALUES ('single', 'user-1', 'owner', @LegacyBlob, 'dpapi-current-user-v2',
                @Now, @Now, NULL);
            """,
            new { Now = now, LegacyBlob = new byte[] { 1, 2, 3, 4 } });

        var first = fixture.Migrator.ApplyPreLoginMigrations();
        var second = fixture.Migrator.ApplyPreLoginMigrations();

        first.IsSuccess.ShouldBeTrue(first.IsSuccess ? null : first.Error.Message);
        second.IsSuccess.ShouldBeTrue(second.IsSuccess ? null : second.Error.Message);
        fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('RememberMeSlot') ORDER BY cid;")
            .ShouldBe([
                "SlotId",
                "UserId",
                "UsernameNormalized",
                "Version",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            ]);
        fixture.Executor.QueryScalar<string>(
            "SELECT UsernameNormalized FROM RememberMeSlot WHERE SlotId = 'single';")
            .ShouldBe("owner");
        fixture.Executor.QueryScalar<string>(
            "SELECT Version FROM RememberMeSlot WHERE SlotId = 'single';")
            .ShouldBe("username-only-v1");
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0007_prelogin_remembered_account';")
            .ShouldBe(1);
    }

    [Fact]
    public void Pre_login_schema_allows_only_one_owner_user()
    {
        using var fixture = HostPersistenceFixture.Create();

        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);
        var now = DateTimeOffset.UtcNow.ToString("O");

        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES ('owner-1', 'owner1', 'Owner One', 1, 1, @Now, @Now);",
            new { Now = now });

        var exception = Should.Throw<SqliteException>(() =>
            fixture.Executor.Execute(
                "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES ('owner-2', 'owner2', 'Owner Two', 1, 1, @Now, @Now);",
                new { Now = now }));
        exception.SqliteErrorCode.ShouldBe(19);
    }

    [Fact]
    public void Persistence_error_accepts_approved_codes_and_never_exposes_diagnostic_detail()
    {
        string[] approvedCodes =
        [
            "ConstraintViolation",
            "TransactionConflict",
            "SchemaMismatch",
            "MigrationFailed",
            "ProviderUnavailable",
            "IoFailed",
            "SerializationFailed",
            "QueryFailed",
            "Unknown"
        ];

        foreach (var code in approvedCodes)
        {
            var error = PersistenceError.Create(
                code,
                "C:\\private\\host.db SELECT PasswordHash FROM Users token=abc");
            error.Code.ShouldBe(code);
            error.Message.ShouldNotContain("private");
            error.Message.ShouldNotContain("PasswordHash");
            error.Message.ShouldNotContain("abc");
            Enum.IsDefined(error.Category).ShouldBeTrue();
            error.CorrelationId.Value.ShouldNotBe(Guid.Empty);
        }
    }

    [Fact]
    public void Encrypted_provider_fails_closed_when_opened_without_key()
    {
        using var fixture = HostPersistenceFixture.Create();
        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = fixture.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";

        Should.Throw<SqliteException>(() => command.ExecuteScalar());
    }

    [Fact]
    public void Encrypted_provider_uses_guardrails_and_does_not_write_plaintext_header_or_sentinel()
    {
        using var fixture = HostPersistenceFixture.Create();
        const string sentinel = RequiredProviderProofMarker;

        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES (@ConfigKey, @ConfigValue, 'string', 'None', 'test', @UpdatedAtUtc);",
            new { ConfigKey = "test.sentinel", ConfigValue = sentinel, UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });

        using (var connection = fixture.Factory.Open().Value)
        {
            fixture.Executor.QueryScalar<int>("PRAGMA foreign_keys;").ShouldBe(1);
            fixture.Executor.QueryScalar<int>("PRAGMA busy_timeout;").ShouldBe(5000);
            fixture.Executor.QueryScalar<string>("PRAGMA journal_mode;").ShouldBe("wal");
            fixture.Executor.QueryScalar<int>("PRAGMA secure_delete;").ShouldBe(1);
        }

        var rawDatabase = File.ReadAllBytes(fixture.DatabasePath);
        Encoding.ASCII.GetString(rawDatabase.Take(16).ToArray()).ShouldNotBe("SQLite format 3\0");
        Encoding.UTF8.GetString(rawDatabase).ShouldNotContain(sentinel);

        var wrongFactory = new EncryptedSqliteConnectionFactory(
            fixture.Options,
            new StaticProtectedMaterialSource(fixture.Material with { DbOpenSecret = RandomBytes(32) }));
        wrongFactory.Open().Error.Code.ShouldBe(PersistenceError.ProviderUnavailable);
    }

    [Fact]
    public void Encrypted_provider_fails_closed_when_native_provider_path_is_missing_after_initialization()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();

        var missingNativePath = Path.Combine(fixture.Root, "missing", "sqlite3mc_x64.dll");
        var missingNativeFactory = new EncryptedSqliteConnectionFactory(fixture.Options, fixture.Probe, missingNativePath);

        var result = missingNativeFactory.Open();
        if (result.IsSuccess)
        {
            result.Value.Dispose();
        }

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(PersistenceError.ProviderUnavailable);
    }

    [Fact]
    public void Encrypted_provider_rejects_invalid_busy_timeout_guardrail()
    {
        using var fixture = HostPersistenceFixture.Create();
        var options = fixture.Options with { BusyTimeoutMilliseconds = 0 };
        var invalidFactory = new EncryptedSqliteConnectionFactory(options, fixture.Probe);

        var result = invalidFactory.Open();
        if (result.IsSuccess)
        {
            result.Value.Dispose();
        }

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(PersistenceError.ProviderUnavailable);
    }

    [Fact]
    public void Approved_cipher_pragma_sequence_records_sqlcipher_v4_and_sqlite3mc_mapping()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot().FullName,
            "src",
            "Dobby.Infrastructure",
            "Persistence",
            "EncryptedSqliteConnectionFactory.cs"));

        source.ShouldContain("PRAGMA cipher_compatibility=4;");
        source.ShouldContain("PRAGMA legacy=4;");
        source.ShouldContain("PRAGMA hmac_use=1;");
        source.ShouldContain("PRAGMA kdf_algorithm=2;");
        source.ShouldContain("PRAGMA hmac_algorithm=2;");
    }

    [Fact]
    public void Encrypted_provider_sidecar_files_do_not_write_plaintext_sentinel()
    {
        using var fixture = HostPersistenceFixture.Create();
        const string sentinel = "SPRINT4_WAL_SIDEFILE_SENTINEL_DO_NOT_STORE_PLAINTEXT";

        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);
        var databaseDirectory = Path.GetDirectoryName(fixture.DatabasePath)!;
        var scope = new HostPersistenceTransactionScope(fixture.Factory);

        Should.Throw<InvalidOperationException>(() =>
            scope.ExecuteInTransaction(executor =>
            {
                for (var i = 0; i < 20; i++)
                {
                    executor.Execute(
                        "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES (@ConfigKey, @ConfigValue, 'string', 'None', 'test', @UpdatedAtUtc);",
                        new
                        {
                            ConfigKey = $"test.sidecar.{i}",
                            ConfigValue = sentinel + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
                        });
                }

                var sidecarFiles = Directory
                    .EnumerateFiles(databaseDirectory, "host.db*", SearchOption.TopDirectoryOnly)
                    .Where(IsDataSidecarFile)
                    .ToArray();
                sidecarFiles.ShouldNotBeEmpty("WAL/journal side-files must be observed and scanned for provider-boundary proof.");

                foreach (var sidecarFile in sidecarFiles)
                {
                    Encoding.UTF8.GetString(ReadSharedFile(sidecarFile)).ShouldNotContain(sentinel);
                }

                throw new InvalidOperationException("rollback after sidecar proof");
            }));
    }

    [Fact]
    public void Transaction_scope_rolls_back_partial_multi_step_writes()
    {
        using var fixture = HostPersistenceFixture.Create();
        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        var scope = new HostPersistenceTransactionScope(fixture.Factory);

        Should.Throw<InvalidOperationException>(() =>
            scope.ExecuteInTransaction(executor =>
            {
                executor.Execute(
                    "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('tx.rollback', 'created', 'string', 'None', 'test', @UpdatedAtUtc);",
                    new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });

                throw new InvalidOperationException("simulate crash before commit");
            }));

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'tx.rollback';")
            .ShouldBe(0);
    }

    [Fact]
    public void Transaction_scope_enlists_calls_made_through_existing_executor_instance()
    {
        using var fixture = HostPersistenceFixture.Create();
        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        var scope = new HostPersistenceTransactionScope(fixture.Factory);

        Should.Throw<InvalidOperationException>(() =>
            scope.ExecuteInTransaction(_ =>
            {
                fixture.Executor.Execute(
                    "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('tx.ambient', 'created', 'string', 'None', 'test', @UpdatedAtUtc);",
                    new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });

                throw new InvalidOperationException("simulate crash before commit");
            }));

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'tx.ambient';")
            .ShouldBe(0);
    }

    [Fact]
    public void Nested_transaction_scope_joins_outer_transaction()
    {
        using var fixture = HostPersistenceFixture.Create();
        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        var scope = new HostPersistenceTransactionScope(fixture.Factory);

        Should.Throw<InvalidOperationException>(() =>
            scope.ExecuteInTransaction(_ =>
            {
                fixture.Executor.Execute(
                    "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('tx.outer', 'created', 'string', 'None', 'test', @UpdatedAtUtc);",
                    new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });

                scope.ExecuteInTransaction(_ =>
                {
                    fixture.Executor.Execute(
                        "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('tx.inner', 'created', 'string', 'None', 'test', @UpdatedAtUtc);",
                        new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
                });

                throw new InvalidOperationException("simulate crash before commit");
            }));

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey IN ('tx.outer', 'tx.inner');")
            .ShouldBe(0);
    }

    [Fact]
    public void Nested_transaction_scope_rolls_back_inner_savepoint_when_inner_scope_fails()
    {
        using var fixture = HostPersistenceFixture.Create();
        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        var scope = new HostPersistenceTransactionScope(fixture.Factory);

        scope.ExecuteInTransaction(_ =>
        {
            fixture.Executor.Execute(
                "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('tx.savepoint.outer', 'created', 'string', 'None', 'test', @UpdatedAtUtc);",
                new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });

            Should.Throw<InvalidOperationException>(() =>
                scope.ExecuteInTransaction(_ =>
                {
                    fixture.Executor.Execute(
                        "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('tx.savepoint.inner', 'created', 'string', 'None', 'test', @UpdatedAtUtc);",
                        new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });

                    throw new InvalidOperationException("inner scope failed");
                }));
        });

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'tx.savepoint.outer';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostConfiguration WHERE ConfigKey = 'tx.savepoint.inner';")
            .ShouldBe(0);
    }

    [Fact]
    public void Post_login_migrations_are_distinct_from_pre_login_bootstrap()
    {
        using var fixture = HostPersistenceFixture.Create();

        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);
        fixture.Executor.Query<string>("SELECT MigrationId FROM StorageMetadata ORDER BY MigrationId;")
            .ShouldBe([
                "0001_prelogin_bootstrap",
                "0007_prelogin_remembered_account",
                "0008_prelogin_login_throttle",
                "0009_prelogin_host_outbox",
                "0010_prelogin_recovery_requests"
            ]);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PostLoginOnly';")
            .ShouldBe(0);

        fixture.Migrator.ApplyPostLoginMigrations(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["0002_postlogin_bootstrap"] = "CREATE TABLE IF NOT EXISTS PostLoginOnly (Id TEXT PRIMARY KEY);"
        }).IsSuccess.ShouldBeTrue();

        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PostLoginOnly';")
            .ShouldBe(1);
    }

    [Fact]
    public void Post_login_migrations_accept_future_numbered_ids_after_reserved_bootstrap()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();

        var result = fixture.Migrator.ApplyPostLoginMigrations(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["9999_future_consumer_migration"] = "CREATE TABLE IF NOT EXISTS FutureConsumerState (Id TEXT PRIMARY KEY);"
        });

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Error.Message);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FutureConsumerState';")
            .ShouldBe(1);
    }

    [Fact]
    public void Pre_login_migration_fails_closed_when_device_identity_mirror_does_not_match_protected_material()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();

        fixture.Executor.Execute(
            "UPDATE DeviceIdentity SET ProtectedStableDeviceIdBlob = @ProtectedStableDeviceIdBlob WHERE IdentityId = 'DobbyStableDeviceId';",
            new { ProtectedStableDeviceIdBlob = RandomBytes(32) });

        var result = fixture.Migrator.ApplyPreLoginMigrations();

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(PersistenceError.MigrationFailed);
        result.Error.Message.ShouldBe("The persistence migration could not be completed.");
        result.Error.Category.ShouldBe(PersistenceErrorCategory.Schema);
        result.Error.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public void Support_snapshot_truncates_wal_under_backup_root()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('snapshot.test', 'value', 'string', 'None', 'test', @UpdatedAtUtc);",
            new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        var backupRoot = Path.Combine(fixture.Root, "backup");
        var snapshotPath = Path.Combine(backupRoot, "support", "host-support.db");
        var walPath = fixture.DatabasePath + "-wal";
        using (var openConnection = fixture.Factory.Open().Value)
        {
            fixture.Executor.Execute(
                "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('snapshot.test.2', 'value', 'string', 'None', 'test', @UpdatedAtUtc);",
                new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
            File.Exists(walPath).ShouldBeTrue();
        }

        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        var checkpoint = maintenance.ForceWalCheckpoint();
        var snapshot = maintenance.CreateSupportSnapshot(snapshotPath);

        checkpoint.IsSuccess.ShouldBeTrue(checkpoint.IsSuccess ? null : checkpoint.Error.Message);
        snapshot.IsSuccess.ShouldBeTrue(snapshot.IsSuccess ? null : snapshot.Error.Message);
        snapshot.Value.ShouldBe(snapshotPath);
        snapshot.Value.ShouldStartWith(backupRoot, Case.Insensitive);
        File.Exists(snapshotPath).ShouldBeTrue();
        new FileInfo(snapshotPath).Length.ShouldBeGreaterThan(0);
        if (File.Exists(walPath))
        {
            new FileInfo(walPath).Length.ShouldBe(0);
        }
    }

    private sealed class HostPersistenceFixture : IDisposable
    {
        private HostPersistenceFixture(string root)
        {
            Root = root;
            Options = new HostPersistenceOptions(root, "SQLite3MultipleCiphers", true, 5000);
            Probe = ProtectedMaterialAvailabilityProbe.CreateForDataRoot(root);
            Probe.IsAvailable().ShouldBeTrue();
            Material = Probe.LoadRequired();
            Factory = new EncryptedSqliteConnectionFactory(Options, Probe);
            Migrator = new HostPersistenceMigrator(Factory, Probe);
            Executor = new HostPersistenceCommandExecutor(Factory);
            DatabasePath = System.IO.Path.Combine(root, "data", "host.db");
        }

        public string Root { get; }

        public string DatabasePath { get; }

        public HostPersistenceOptions Options { get; }

        public ProtectedMaterialAvailabilityProbe Probe { get; }

        public ProtectedMaterialSet Material { get; }

        public EncryptedSqliteConnectionFactory Factory { get; }

        public HostPersistenceMigrator Migrator { get; }

        public HostPersistenceCommandExecutor Executor { get; }

        public static HostPersistenceFixture Create()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new HostPersistenceFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class StaticProtectedMaterialSource(ProtectedMaterialSet material) : IProtectedMaterialSource
    {
        public ProtectedMaterialSet LoadRequired() => material;
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private static byte[] ReadSharedFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool IsDataSidecarFile(string path)
    {
        return path.EndsWith("-wal", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("-journal", StringComparison.OrdinalIgnoreCase);
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
