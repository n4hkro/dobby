using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Dobby.Audit;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Host.Login;
using Dobby.Infrastructure.Persistence;
using Dobby.Infrastructure.ProtectedStorage;
using Dobby.Licensing;
using Dobby.Security.Authorization;
using Microsoft.Data.Sqlite;
using NSubstitute;
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
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0011_prelogin_outbox_evidence';")
            .ShouldBe(1);
        fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('HostOutbox');")
            .ShouldContain("EvidenceCorrelationId");

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

    [Theory]
    [InlineData("o***r", "owner", "masked surrogate value")]
    [InlineData("owner", "someoneelse", "no reconciling user row")]
    [InlineData("OWNER", "OWNER", "charset violation")]
    [InlineData("ow", "ow", "below the minimum username length")]
    public void Pre_login_migration_drops_a_legacy_slot_that_does_not_reconcile(
        string legacyMaskedUsername,
        string userUsername,
        string reason)
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
            VALUES ('user-1', @UserUsername, 'Owner', 1, 1, @Now, @Now);
            INSERT INTO RememberMeSlot(
                SlotId, UserId, MaskedUsername, ProtectedRememberMeBlob, ProtectionVersion,
                CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc)
            VALUES ('single', 'user-1', @LegacyMaskedUsername, @LegacyBlob, 'dpapi-current-user-v2',
                @Now, @Now, NULL);
            """,
            new
            {
                Now = now,
                UserUsername = userUsername,
                LegacyMaskedUsername = legacyMaskedUsername,
                LegacyBlob = new byte[] { 1, 2, 3, 4 }
            });

        var migration = fixture.Migrator.ApplyPreLoginMigrations();

        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);
        fixture.Executor.QueryScalar<int>("SELECT COUNT(*) FROM RememberMeSlot;")
            .ShouldBe(
                0,
                "docs/specs/storage-and-data-protection.md requires a legacy slot with " +
                reason +
                " to be dropped rather than carried forward: prefilling an unreconciled legacy " +
                "value would legitimise a masked surrogate under the username-only schema (ADR-0058)");
        fixture.Executor.Query<string>("SELECT name FROM pragma_table_info('RememberMeSlot') ORDER BY cid;")
            .ShouldBe([
                "SlotId",
                "UserId",
                "UsernameNormalized",
                "Version",
                "CreatedAtUtc",
                "UpdatedAtUtc"
            ]);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0007_prelogin_remembered_account';")
            .ShouldBe(1);
    }

    [Fact]
    public void Migrated_slot_schema_refuses_to_store_a_masked_surrogate_shape()
    {
        using var fixture = HostPersistenceFixture.Create();
        var migration = fixture.Migrator.ApplyPreLoginMigrations();
        migration.IsSuccess.ShouldBeTrue(migration.IsSuccess ? null : migration.Error.Message);

        var now = DateTimeOffset.UtcNow.ToString("O");
        fixture.Executor.Execute(
            "INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc) VALUES ('user-1', 'owner', 'Owner', 1, 1, @Now, @Now);",
            new { Now = now });

        Should.Throw<SqliteException>(() => fixture.Executor.Execute(
                "INSERT INTO RememberMeSlot(SlotId, UserId, UsernameNormalized, Version, CreatedAtUtc, UpdatedAtUtc) VALUES ('single', 'user-1', 'o***r', 'username-only-v1', @Now, @Now);",
                new { Now = now }))
            .Message.ShouldNotBeNullOrWhiteSpace(
                "the rebuilt slot table constrains the stored username charset, so a masked " +
                "surrogate cannot be written even by a caller that bypasses the slot writer");

        Should.Throw<SqliteException>(() => fixture.Executor.Execute(
                "INSERT INTO RememberMeSlot(SlotId, UserId, UsernameNormalized, Version, CreatedAtUtc, UpdatedAtUtc) VALUES ('single', 'user-1', 'owner', 'masked-v1', @Now, @Now);",
                new { Now = now }))
            .Message.ShouldNotBeNullOrWhiteSpace(
                "the Version CHECK pins the slot to the username-only contract, so a row " +
                "claiming an older masked shape is not storable");
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
                "0010_prelogin_recovery_requests",
                "0011_prelogin_outbox_evidence"
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
    public void Pre_login_outbox_evidence_migration_repairs_prebuilt_column_without_metadata()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "DELETE FROM StorageMetadata WHERE MigrationId = '0011_prelogin_outbox_evidence';");

        var recovered = fixture.Migrator.ApplyPreLoginMigrations();
        var repeated = fixture.Migrator.ApplyPreLoginMigrations();

        recovered.IsSuccess.ShouldBeTrue();
        repeated.IsSuccess.ShouldBeTrue();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0011_prelogin_outbox_evidence';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_HostOutbox_PendingEvidence';")
            .ShouldBe(1);
    }

    [Fact]
    public void Post_login_migration_schema_and_metadata_roll_back_together_on_statement_failure()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        const string migrationId = "9998_atomic_failure_probe";

        var result = fixture.Migrator.ApplyPostLoginMigrations(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [migrationId] =
                "CREATE TABLE MigrationAtomicProbe (Id TEXT PRIMARY KEY);" +
                "INSERT INTO MissingMigrationTable(Id) VALUES ('failure');"
        });

        result.IsSuccess.ShouldBeFalse();
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='MigrationAtomicProbe';")
            .ShouldBe(0);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = migrationId }).ShouldBe(0);
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

    [Fact]
    public void Support_snapshot_is_integrity_checked_encrypted_and_has_hash_metadata()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('snapshot.proof', 'preserved', 'string', 'None', 'test', @UpdatedAtUtc);",
            new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-proof.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);

        var snapshot = maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None);

        snapshot.IsSuccess.ShouldBeTrue(snapshot.IsSuccess ? null : snapshot.Error.Message);
        File.Exists(snapshot.Value.MetadataPath).ShouldBeTrue();
        snapshot.Value.Sha256.ShouldBe(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath))).ToLowerInvariant());
        new FileInfo(snapshotPath).Length.ShouldBe(snapshot.Value.Length);
        using var plaintext = new SqliteConnection($"Data Source={snapshotPath};Mode=ReadOnly;Pooling=False");
        plaintext.Open();
        Should.Throw<SqliteException>(() =>
        {
            using var command = plaintext.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            command.ExecuteScalar();
        });
    }

    [Fact]
    public void Verified_snapshot_restore_quarantines_current_store_and_restores_snapshot_state()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('restore.proof', 'snapshot', 'string', 'None', 'test', @UpdatedAtUtc);",
            new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        new HostTransactionOutbox(TimeProvider.System).EnqueueInScope(
            fixture.Executor,
            new HostOutboxMessage(
                "restore-completed-current",
                "audit",
                "persistence.snapshot.created",
                new Dictionary<string, string> { ["result"] = "success" }));
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-restore.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "UPDATE HostConfiguration SET ConfigValue = 'current' WHERE ConfigKey = 'restore.proof';");
        fixture.Executor.Execute(
            "UPDATE HostOutbox SET CompletedAtUtc = @CompletedAtUtc WHERE IdempotencyKey = 'restore-completed-current';",
            new { CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        var evidenceCorrelationId = Guid.NewGuid().ToString("D");
        fixture.Executor.Execute(
            "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES ('restore-current-evidence', @OccurredAtUtc, 'persistence.restore.requested', 'owner', 'persistence', 'success', @CorrelationId, '{}');",
            new
            {
                OccurredAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                CorrelationId = evidenceCorrelationId
            });
        new HostTransactionOutbox(TimeProvider.System).EnqueueInScope(
            fixture.Executor,
            new HostOutboxMessage(
                "restore-current-evidence",
                "audit",
                "persistence.restore.requested",
                new Dictionary<string, string> { ["result"] = "success" },
                evidenceCorrelationId));

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeTrue(restored.IsSuccess ? null : restored.Error.Message);
        fixture.Executor.QueryScalar<string>(
            "SELECT ConfigValue FROM HostConfiguration WHERE ConfigKey = 'restore.proof';")
            .ShouldBe("snapshot");
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM AuditLog WHERE AuditId = 'restore-current-evidence';")
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = 'restore-current-evidence' AND CompletedAtUtc IS NULL AND EvidenceCorrelationId = @EvidenceCorrelationId;",
            new { EvidenceCorrelationId = evidenceCorrelationId })
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM HostOutbox WHERE IdempotencyKey = 'restore-completed-current' AND CompletedAtUtc IS NOT NULL;")
            .ShouldBe(1);
        File.Exists(Path.Combine(restored.Value.QuarantinedPreviousStoreDirectory, "host.db"))
            .ShouldBeTrue();
        fixture.Executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
    }

    [Fact]
    public async Task Coordinated_restore_keeps_its_own_audit_evidence_in_the_promoted_store()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var auditTrail = new DurableAuditTrail(fixture.Executor);
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-audited.db");
        // The snapshot is taken before any maintenance audit row exists: if the promoted store simply
        // replaced the audit log, the restore would erase the only evidence that it ever ran.
        maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();
        var grants = Substitute.For<IPermissionGrantReader>();
        grants.LoadUserState("owner").Returns(Result.Success(
            new UserPermissionState("owner", IsOwner: true, IsActive: true)));
        var reporter = new CollectingBackgroundOperationReporter();
        using var authenticated = new CancellationTokenSource();
        var coordinator = new AuthenticatedPersistenceMaintenanceCoordinator(
            maintenance,
            new OperationAuthorizationPolicy(OperationTargetRegistry.CreateDefault(), grants),
            reporter,
            auditTrail,
            TimeProvider.System,
            authenticated.Token);

        var restored = await coordinator.RestoreSnapshotAsync(
            "owner",
            snapshotPath,
            confirmCurrentStoreQuarantine: true,
            CorrelationId.New());

        restored.IsSuccess.ShouldBeTrue(restored.IsSuccess ? null : restored.ErrorCode.ToString());
        reporter.Statuses.ShouldBe(
            [BackgroundOperationStatus.Running, BackgroundOperationStatus.Completed]);
        var promoted = auditTrail.Snapshot()
            .Where(item => item.EventName.StartsWith("persistence.restore", StringComparison.Ordinal))
            .ToArray();
        promoted.Select(item => item.EventName).ShouldBe(
        [
            AuditEventName.PersistenceRestoreRequested,
            AuditEventName.PersistenceRestoreCompleted
        ]);
        promoted.ShouldAllBe(item => item.Result == AuditResult.Success);
        promoted[0].Details!["candidateName"].ShouldBe("host-backup-audited.db");
        promoted[1].Details!["quarantineName"].ShouldBe(
            Path.GetFileName(restored.Value.QuarantinedPreviousStoreDirectory));
        promoted.SelectMany(item => item.Details!.Values)
            .ShouldAllBe(value => !value.Contains(fixture.Root, StringComparison.OrdinalIgnoreCase));
        fixture.Executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
    }

    [Theory]
    [InlineData(HostPersistenceRestoreFaultPoint.AfterCurrentStoreQuarantined)]
    [InlineData(HostPersistenceRestoreFaultPoint.AfterCandidatePromoted)]
    public void Restore_fault_rolls_back_original_store_without_partial_live_state(
        HostPersistenceRestoreFaultPoint faultPoint)
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('restore.rollback', 'snapshot', 'string', 'None', 'test', @UpdatedAtUtc);",
            new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        var snapshotPath = Path.Combine(fixture.Root, "backup", $"host-backup-rollback-{faultPoint}.db");
        var creator = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        creator.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "UPDATE HostConfiguration SET ConfigValue = 'current' WHERE ConfigKey = 'restore.rollback';");
        var maintenance = new HostPersistenceMaintenanceService(
            fixture.Options,
            fixture.Probe,
            fixture.Factory,
            faultInjector: new ThrowingRestoreFaultInjector(faultPoint));

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeFalse();
        fixture.Executor.QueryScalar<string>(
            "SELECT ConfigValue FROM HostConfiguration WHERE ConfigKey = 'restore.rollback';")
            .ShouldBe("current");
        fixture.Executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
        Directory.EnumerateFiles(Path.Combine(fixture.Root, "data"), "*.partial")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Corrupt_restore_candidate_is_rejected_before_current_store_is_quarantined()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('restore.unchanged', 'current', 'string', 'None', 'test', @UpdatedAtUtc);",
            new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        var corruptPath = Path.Combine(fixture.Root, "backup", "corrupt.db");
        Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
        File.WriteAllBytes(corruptPath, RandomBytes(4096));
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);

        var restored = maintenance.RestoreSupportSnapshot(corruptPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeFalse();
        fixture.Executor.QueryScalar<string>(
            "SELECT ConfigValue FROM HostConfiguration WHERE ConfigKey = 'restore.unchanged';")
            .ShouldBe("current");
        Directory.Exists(Path.Combine(fixture.Root, "backup", "restore-quarantine"))
            .ShouldBeFalse();
    }

    [Fact]
    public void Snapshot_low_disk_and_pre_cancel_paths_leave_no_partial_output()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var backupRoot = Path.Combine(fixture.Root, "backup");
        var lowDiskPath = Path.Combine(backupRoot, "low-disk.db");
        var lowDisk = new HostPersistenceMaintenanceService(
            fixture.Options,
            fixture.Probe,
            fixture.Factory,
            _ => 0);

        lowDisk.CreateSupportSnapshot(
            lowDiskPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeFalse();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var cancelledPath = Path.Combine(backupRoot, "cancelled.db");
        var cancelledResult = new HostPersistenceMaintenanceService(
            fixture.Options,
            fixture.Probe,
            fixture.Factory).CreateSupportSnapshot(
                cancelledPath,
                new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
                cancelled.Token);

        cancelledResult.IsSuccess.ShouldBeFalse();
        File.Exists(lowDiskPath).ShouldBeFalse();
        File.Exists(cancelledPath).ShouldBeFalse();
        Directory.EnumerateFiles(backupRoot, "*.partial.*", SearchOption.AllDirectories)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Snapshot_retention_keeps_configured_floor_and_removes_matching_metadata()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var backupRoot = Path.Combine(fixture.Root, "backup");
        Directory.CreateDirectory(backupRoot);
        var oldest = Path.Combine(backupRoot, "host-backup-oldest.db");
        var recent = Path.Combine(backupRoot, "host-backup-recent.db");
        File.WriteAllText(oldest, "oldest");
        File.WriteAllText(oldest + ".metadata.json", "{}");
        File.WriteAllText(recent, "recent");
        File.SetLastWriteTimeUtc(oldest, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(recent, DateTime.UtcNow.AddDays(-1));
        var newest = Path.Combine(backupRoot, "host-backup-newest.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);

        var created = maintenance.CreateSupportSnapshot(
            newest,
            new HostPersistenceBackupPolicy(
                MaximumRetainedSnapshots: 2,
                MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None);

        created.IsSuccess.ShouldBeTrue();
        File.Exists(newest).ShouldBeTrue();
        File.Exists(recent).ShouldBeTrue();
        File.Exists(oldest).ShouldBeFalse();
        File.Exists(oldest + ".metadata.json").ShouldBeFalse();
    }

    [Fact]
    public void Restore_cannot_re_arm_a_consumed_owner_recovery_nonce()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var requestId = Guid.NewGuid().ToString("D");
        var nonceHash = Convert.ToHexString(SHA256.HashData(RandomBytes(32))).ToLowerInvariant();
        var issuedAt = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            """
            INSERT INTO RecoveryRequests(
                RequestId, NonceHash, CustomerId, DeploymentId, DeviceId, RecoveryPublicKeyHash,
                VendorKeyId, PayloadHash, IssuedAtUtc, ExpiresAtUtc, CreatedAtUtc)
            VALUES (
                @RequestId, @NonceHash, 'customer-1', 'deployment-1', 'device-1', @RecoveryPublicKeyHash,
                'recovery-prod-2026-01', @PayloadHash, @IssuedAtUtc, @ExpiresAtUtc, @IssuedAtUtc);
            """,
            new
            {
                RequestId = requestId,
                NonceHash = nonceHash,
                RecoveryPublicKeyHash = Convert.ToHexString(SHA256.HashData(RandomBytes(32))).ToLowerInvariant(),
                PayloadHash = Convert.ToHexString(SHA256.HashData(RandomBytes(32))).ToLowerInvariant(),
                IssuedAtUtc = issuedAt.ToString("O"),
                ExpiresAtUtc = issuedAt.AddHours(24).ToString("O")
            });
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-recovery-nonce.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();
        var consumedAt = issuedAt.AddMinutes(5).ToString("O");
        fixture.Executor.Execute(
            "UPDATE RecoveryRequests SET ConsumedAtUtc = @ConsumedAtUtc, ResponseId = @ResponseId WHERE RequestId = @RequestId;",
            new { ConsumedAtUtc = consumedAt, ResponseId = Guid.NewGuid().ToString("D"), RequestId = requestId });
        fixture.Executor.Execute(
            "INSERT INTO ConsumedRecoveryNonces(NonceHash, ConsumedAtUtc, RequestId) VALUES (@NonceHash, @ConsumedAtUtc, @RequestId);",
            new { NonceHash = nonceHash, ConsumedAtUtc = consumedAt, RequestId = requestId });

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeTrue(restored.IsSuccess ? null : restored.Error.Message);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM ConsumedRecoveryNonces WHERE NonceHash = @NonceHash;",
            new { NonceHash = nonceHash })
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM RecoveryRequests WHERE RequestId = @RequestId AND ConsumedAtUtc IS NOT NULL AND ResponseId IS NOT NULL;",
            new { RequestId = requestId })
            .ShouldBe(1);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM RecoveryRequests WHERE ConsumedAtUtc IS NULL;")
            .ShouldBe(0);
    }

    [Fact]
    public void Restore_cannot_clear_or_shorten_an_active_login_throttle_penalty()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var baseline = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            """
            INSERT INTO LoginThrottleBuckets(
                BucketKey, BucketKind, FailedAttempts, WindowStartedAtUtc, ThrottledUntilUtc, UpdatedAtUtc, Version)
            VALUES ('account:owner', 'account', 1, @WindowStartedAtUtc, NULL, @UpdatedAtUtc, 1);
            """,
            new { WindowStartedAtUtc = baseline.ToString("O"), UpdatedAtUtc = baseline.ToString("O") });
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-throttle.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();
        var penaltyUntil = baseline.AddMinutes(10).ToString("O");
        fixture.Executor.Execute(
            """
            UPDATE LoginThrottleBuckets
            SET FailedAttempts = 9, ThrottledUntilUtc = @ThrottledUntilUtc, UpdatedAtUtc = @UpdatedAtUtc, Version = 4
            WHERE BucketKey = 'account:owner';
            """,
            new { ThrottledUntilUtc = penaltyUntil, UpdatedAtUtc = baseline.AddMinutes(1).ToString("O") });
        fixture.Executor.Execute(
            """
            INSERT INTO LoginThrottleBuckets(
                BucketKey, BucketKind, FailedAttempts, WindowStartedAtUtc, ThrottledUntilUtc, UpdatedAtUtc, Version)
            VALUES ('device:global', 'device', 5, @WindowStartedAtUtc, @ThrottledUntilUtc, @UpdatedAtUtc, 2);
            """,
            new
            {
                WindowStartedAtUtc = baseline.AddMinutes(1).ToString("O"),
                ThrottledUntilUtc = penaltyUntil,
                UpdatedAtUtc = baseline.AddMinutes(1).ToString("O")
            });

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeTrue(restored.IsSuccess ? null : restored.Error.Message);
        fixture.Executor.QueryScalar<int>(
            "SELECT FailedAttempts FROM LoginThrottleBuckets WHERE BucketKey = 'account:owner';")
            .ShouldBe(9);
        DateTimeOffset.Parse(
                fixture.Executor.QueryScalar<string>(
                    "SELECT ThrottledUntilUtc FROM LoginThrottleBuckets WHERE BucketKey = 'account:owner';"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind)
            .ShouldBe(DateTimeOffset.Parse(penaltyUntil, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        fixture.Executor.QueryScalar<int>(
            "SELECT Version FROM LoginThrottleBuckets WHERE BucketKey = 'account:owner';")
            .ShouldBeGreaterThanOrEqualTo(4);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM LoginThrottleBuckets WHERE BucketKey = 'device:global' AND ThrottledUntilUtc IS NOT NULL;")
            .ShouldBe(1);
    }

    [Fact]
    public void Restore_cannot_lower_an_accepted_license_revision_or_erase_import_attestations()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Migrator.ApplyPostLoginMigrations(PostLoginPluginPersistenceMigrations.All)
            .IsSuccess.ShouldBeTrue();
        var importedAt = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            """
            INSERT INTO PluginLicenseState(
                PluginId, CustomerId, DeploymentId, DeviceFingerprint, PackageFingerprint,
                LicenseRevision, LifecycleState, ImportedAtUtc, RevisionSeriesId, KeyId, TrustedTimeHealth)
            VALUES (
                'sample.plugin', 'customer-1', 'deployment-1', 'device-fp', 'package-fp',
                1, 'Active', @ImportedAtUtc, 'series-1', 'license-prod-2026-01', 'Healthy');
            """,
            new { ImportedAtUtc = importedAt.ToString("O") });
        InsertLicenseAttestation(fixture, "attestation-snapshot", 1, importedAt);
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-license.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            """
            UPDATE PluginLicenseState
            SET LicenseRevision = 2, ImportedAtUtc = @ImportedAtUtc
            WHERE PluginId = 'sample.plugin' AND CustomerId = 'customer-1' AND DeploymentId = 'deployment-1';
            """,
            new { ImportedAtUtc = importedAt.AddMinutes(5).ToString("O") });
        InsertLicenseAttestation(fixture, "attestation-current", 2, importedAt.AddMinutes(5));

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeTrue(restored.IsSuccess ? null : restored.Error.Message);
        fixture.Executor.QueryScalar<int>(
            "SELECT LicenseRevision FROM PluginLicenseState WHERE PluginId = 'sample.plugin' AND CustomerId = 'customer-1' AND DeploymentId = 'deployment-1';")
            .ShouldBe(2);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM PluginLicenseImportAttestation WHERE AttestationId IN ('attestation-snapshot', 'attestation-current');")
            .ShouldBe(2);
    }

    [Fact]
    public void Restore_invalidates_every_session_the_promoted_candidate_believed_live()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var now = DateTimeOffset.UtcNow;
        fixture.Executor.Execute(
            """
            INSERT INTO Users(UserId, UsernameNormalized, DisplayName, IsOwner, IsActive, CreatedAtUtc, UpdatedAtUtc)
            VALUES ('user-1', 'owner', 'Owner', 1, 1, @Now, @Now);
            INSERT INTO Sessions(
                SessionId, UserId, CreatedAtUtc, LastActivityAtUtc, ExpiresAtUtc,
                InvalidatedAtUtc, InvalidationReason, SessionVersion)
            VALUES ('session-1', 'user-1', @Now, @Now, @ExpiresAtUtc, NULL, NULL, 1);
            """,
            new { Now = now.ToString("O"), ExpiresAtUtc = now.AddHours(8).ToString("O") });
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-sessions.db");
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        maintenance.CreateSupportSnapshot(
            snapshotPath,
            new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
            CancellationToken.None).IsSuccess.ShouldBeTrue();

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeTrue(restored.IsSuccess ? null : restored.Error.Message);
        fixture.Executor.QueryScalar<int>(
            "SELECT COUNT(*) FROM Sessions WHERE InvalidatedAtUtc IS NULL;")
            .ShouldBe(0);
        fixture.Executor.QueryScalar<string>(
            "SELECT InvalidationReason FROM Sessions WHERE SessionId = 'session-1';")
            .ShouldBe("restore");
    }

    [Fact]
    public void Restore_rejects_a_snapshot_bound_to_a_different_protected_device_identity()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "INSERT INTO HostConfiguration(ConfigKey, ConfigValue, ValueKind, ProtectionScope, Source, UpdatedAtUtc) VALUES ('restore.identity', 'snapshot', 'string', 'None', 'test', @UpdatedAtUtc);",
            new { UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
        var snapshotPath = Path.Combine(fixture.Root, "backup", "host-backup-identity.db");
        new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory)
            .CreateSupportSnapshot(
                snapshotPath,
                new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
                CancellationToken.None).IsSuccess.ShouldBeTrue();
        fixture.Executor.Execute(
            "UPDATE HostConfiguration SET ConfigValue = 'current' WHERE ConfigKey = 'restore.identity';");
        var foreignDevice = new StaticProtectedMaterialSource(
            fixture.Material with { ProtectedStableDeviceIdBlob = RandomBytes(32) });
        var maintenance = new HostPersistenceMaintenanceService(
            fixture.Options,
            foreignDevice,
            fixture.Factory);

        var restored = maintenance.RestoreSupportSnapshot(snapshotPath, CancellationToken.None);

        restored.IsSuccess.ShouldBeFalse();
        restored.Error.Code.ShouldBe(PersistenceError.IoFailed);
        restored.Error.Message.ShouldNotContain(fixture.Root);
        fixture.Executor.QueryScalar<string>(
            "SELECT ConfigValue FROM HostConfiguration WHERE ConfigKey = 'restore.identity';")
            .ShouldBe("current");
        fixture.Executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
        Directory.Exists(Path.Combine(fixture.Root, "backup", "restore-quarantine")).ShouldBeFalse();
        Directory.EnumerateFiles(Path.Combine(fixture.Root, "data"), "*.partial").ShouldBeEmpty();
    }

    [Fact]
    public void Snapshot_destination_outside_the_approved_backup_boundary_is_rejected()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);
        var policy = new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0);
        var backupRoot = Path.Combine(fixture.Root, "backup");
        string[] rejectedDestinations =
        [
            Path.Combine(fixture.Root, "escape.db"),
            Path.Combine(backupRoot, "..", "escape-relative.db"),
            Path.Combine(backupRoot, "snapshot.txt"),
            fixture.DatabasePath,
            Path.Combine(backupRoot, "restore-quarantine", "smuggled.db")
        ];

        foreach (var destination in rejectedDestinations)
        {
            var result = maintenance.CreateSupportSnapshot(destination, policy, CancellationToken.None);

            result.IsSuccess.ShouldBeFalse(destination);
            result.Error.Code.ShouldBe(PersistenceError.ConstraintViolation, destination);
            result.Error.Message.ShouldNotContain(fixture.Root);
        }

        File.Exists(Path.Combine(fixture.Root, "escape.db")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Root, "escape-relative.db")).ShouldBeFalse();
        Directory.Exists(Path.Combine(backupRoot, "restore-quarantine")).ShouldBeFalse();
        fixture.Executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
    }

    [Fact]
    public void Snapshot_destination_behind_a_reparse_point_directory_is_rejected()
    {
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();
        var backupRoot = Path.Combine(fixture.Root, "backup");
        Directory.CreateDirectory(backupRoot);
        var outsideTarget = Path.Combine(fixture.Root, "outside-target");
        Directory.CreateDirectory(outsideTarget);
        var junction = Path.Combine(backupRoot, "linked");
        CreateDirectoryJunction(junction, outsideTarget);
        (File.GetAttributes(junction) & FileAttributes.ReparsePoint).ShouldNotBe(default(FileAttributes));
        var maintenance = new HostPersistenceMaintenanceService(fixture.Options, fixture.Probe, fixture.Factory);

        PersistenceResult<HostPersistenceSnapshot> result;
        try
        {
            result = maintenance.CreateSupportSnapshot(
                Path.Combine(junction, "smuggled.db"),
                new HostPersistenceBackupPolicy(MinimumFreeSpaceReserveBytes: 0),
                CancellationToken.None);
        }
        finally
        {
            Directory.Delete(junction);
        }

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(PersistenceError.IoFailed);
        result.Error.Message.ShouldNotContain(fixture.Root);
        Directory.EnumerateFiles(outsideTarget).ShouldBeEmpty();
        fixture.Executor.QueryScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
    }

    private static void CreateDirectoryJunction(string linkPath, string targetPath)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe")
        {
            Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000).ShouldBeTrue();
        process.ExitCode.ShouldBe(0, error);
    }

    private static void InsertLicenseAttestation(
        HostPersistenceFixture fixture,
        string attestationId,
        int licenseRevision,
        DateTimeOffset importedAt)
    {
        fixture.Executor.Execute(
            """
            INSERT INTO PluginLicenseImportAttestation(
                AttestationId, SourceArtifactHash, LicenseRevision, ImportedAtUtc, Result, CorrelationId)
            VALUES (@AttestationId, @SourceArtifactHash, @LicenseRevision, @ImportedAtUtc, 'succeeded', @CorrelationId);
            """,
            new
            {
                AttestationId = attestationId,
                SourceArtifactHash = Convert.ToHexString(SHA256.HashData(RandomBytes(32))).ToLowerInvariant(),
                LicenseRevision = licenseRevision,
                ImportedAtUtc = importedAt.ToString("O"),
                CorrelationId = Guid.NewGuid().ToString("D")
            });
    }

    [Fact]
    public void Persisted_device_discriminator_is_domain_separated_from_the_license_binding_digest()
    {
        // ADR-0063 § 3: `RedactedSupportPrefix` is an internal uniqueness discriminator
        // derived with the `dobby-device-identity-discriminator-v1` domain over the
        // protected stable device id. It is NOT the license binding digest and not a
        // prefix of it -- license binding is a pure value comparison with no possession
        // proof, so publishing any prefix of that digest would hand out an offline
        // guessing shortcut against the comparison. This test pins the separation:
        // knowing the stored discriminator must reveal nothing about the binding value
        // derived from the very same stable device id.
        using var fixture = HostPersistenceFixture.Create();
        fixture.Migrator.ApplyPreLoginMigrations().IsSuccess.ShouldBeTrue();

        var storedDiscriminator = fixture.Executor.QueryScalar<string>(
            "SELECT RedactedSupportPrefix FROM DeviceIdentity WHERE IdentityId = 'DobbyStableDeviceId';");
        storedDiscriminator.ShouldNotBeNull();
        storedDiscriminator!.Length.ShouldBe(12);

        // Derive the license binding digest over the same raw stable device id the
        // migrator bound, exactly as production does.
        var binding = LicenseDeviceFingerprintV1.Compute(
            "machine-guid-under-test",
            fixture.Material.StableDeviceId);
        binding.IsSuccess.ShouldBeTrue();

        // Domain separation: the discriminator is neither equal to, a prefix of, nor
        // contained anywhere in the binding digest.
        binding.Value.Value.ShouldNotStartWith(storedDiscriminator);
        binding.Value.Value.ShouldNotContain(storedDiscriminator);
        storedDiscriminator.ShouldNotBe(binding.Value.Value[..12]);
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

    private sealed class CollectingBackgroundOperationReporter : IBackgroundOperationReporter
    {
        private readonly List<BackgroundOperationStatus> statuses = [];

        public IProgress<BackgroundOperationSnapshot> Progress => new CollectingProgress(statuses);

        public IReadOnlyList<BackgroundOperationStatus> Statuses
        {
            get
            {
                lock (statuses)
                {
                    return statuses.ToArray();
                }
            }
        }

        private sealed class CollectingProgress(List<BackgroundOperationStatus> statuses)
            : IProgress<BackgroundOperationSnapshot>
        {
            public void Report(BackgroundOperationSnapshot value)
            {
                lock (statuses)
                {
                    statuses.Add(value.Status);
                }
            }
        }
    }

    private sealed class StaticProtectedMaterialSource(ProtectedMaterialSet material) : IProtectedMaterialSource
    {
        public ProtectedMaterialSet LoadRequired() => material;
    }

    private sealed class ThrowingRestoreFaultInjector(HostPersistenceRestoreFaultPoint faultPoint)
        : IHostPersistenceMaintenanceFaultInjector
    {
        public void OnRestore(HostPersistenceRestoreFaultPoint current)
        {
            if (current == faultPoint)
            {
                throw new IOException("Injected restore fault.");
            }
        }
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
