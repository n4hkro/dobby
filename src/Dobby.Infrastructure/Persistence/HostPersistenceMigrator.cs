using System.Security.Cryptography;
using System.Text;
using Dobby.Infrastructure.ProtectedStorage;

namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceMigrator : IHostPersistenceMigrator
{
    private readonly IHostPersistenceConnectionFactory factory;
    private readonly IProtectedMaterialSource protectedMaterialSource;

    public HostPersistenceMigrator(
        IHostPersistenceConnectionFactory factory,
        IProtectedMaterialSource protectedMaterialSource)
    {
        this.factory = factory;
        this.protectedMaterialSource = protectedMaterialSource;
    }

    public PersistenceResult ApplyPreLoginMigrations()
    {
        try
        {
            var executor = new HostPersistenceCommandExecutor(factory);
            executor.Execute(PreLoginMigrationV1.Sql);
            RecordMigration(executor, PreLoginMigrationV1.MigrationId, Checksum(PreLoginMigrationV1.Sql));
            ApplyRememberedAccountMigration(executor);
            ApplyLoginThrottleMigration(executor);
            ApplyOutboxMigration(executor);
            ApplyRecoveryRequestMigration(executor);
            ApplyOutboxEvidenceMigration(executor);
            MirrorDeviceIdentity(executor);
            return PersistenceResult.Success();
        }
        catch (Exception ex) when (IsRecoverableMigrationFailure(ex))
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.MigrationFailed));
        }
    }

    private void ApplyLoginThrottleMigration(IHostPersistenceCommandExecutor executor)
    {
        var checksum = Checksum(PreLoginThrottleMigrationV8.Sql);
        if (MigrationAlreadyRecorded(
            executor,
            PreLoginThrottleMigrationV8.MigrationId,
            checksum))
        {
            return;
        }

        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
        {
            transactionExecutor.Execute(PreLoginThrottleMigrationV8.Sql);
            RecordMigration(
                transactionExecutor,
                PreLoginThrottleMigrationV8.MigrationId,
                checksum);
        });
    }

    private void ApplyOutboxMigration(IHostPersistenceCommandExecutor executor)
    {
        var checksum = Checksum(PreLoginOutboxMigrationV9.Sql);
        if (MigrationAlreadyRecorded(executor, PreLoginOutboxMigrationV9.MigrationId, checksum))
        {
            return;
        }

        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
        {
            transactionExecutor.Execute(PreLoginOutboxMigrationV9.Sql);
            RecordMigration(transactionExecutor, PreLoginOutboxMigrationV9.MigrationId, checksum);
        });
    }

    private void ApplyRecoveryRequestMigration(IHostPersistenceCommandExecutor executor)
    {
        var checksum = Checksum(PreLoginRecoveryRequestMigrationV10.Sql);
        if (MigrationAlreadyRecorded(executor, PreLoginRecoveryRequestMigrationV10.MigrationId, checksum))
        {
            return;
        }

        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
        {
            transactionExecutor.Execute(PreLoginRecoveryRequestMigrationV10.Sql);
            RecordMigration(
                transactionExecutor,
                PreLoginRecoveryRequestMigrationV10.MigrationId,
                checksum);
        });
    }

    private void ApplyOutboxEvidenceMigration(IHostPersistenceCommandExecutor executor)
    {
        var checksum = Checksum(PreLoginOutboxEvidenceMigrationV11.Sql);
        if (MigrationAlreadyRecorded(
            executor,
            PreLoginOutboxEvidenceMigrationV11.MigrationId,
            checksum))
        {
            return;
        }

        var evidenceColumnExists = executor.Query<string>(
                "SELECT name FROM pragma_table_info('HostOutbox');")
            .Contains("EvidenceCorrelationId", StringComparer.Ordinal);
        if (evidenceColumnExists)
        {
            new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
            {
                transactionExecutor.Execute(
                    "CREATE INDEX IF NOT EXISTS IX_HostOutbox_PendingEvidence ON HostOutbox(CompletedAtUtc, EvidenceCorrelationId);");
                RecordMigration(
                    transactionExecutor,
                    PreLoginOutboxEvidenceMigrationV11.MigrationId,
                    checksum);
            });
            return;
        }

        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
        {
            transactionExecutor.Execute(PreLoginOutboxEvidenceMigrationV11.Sql);
            RecordMigration(
                transactionExecutor,
                PreLoginOutboxEvidenceMigrationV11.MigrationId,
                checksum);
        });
    }

    private void ApplyRememberedAccountMigration(IHostPersistenceCommandExecutor executor)
    {
        var checksum = Checksum(PreLoginRememberedAccountMigrationV7.Sql);
        if (MigrationAlreadyRecorded(
            executor,
            PreLoginRememberedAccountMigrationV7.MigrationId,
            checksum))
        {
            return;
        }

        new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
        {
            transactionExecutor.Execute("PRAGMA secure_delete=ON;");
            transactionExecutor.Execute(
                PreLoginRememberedAccountMigrationV7.Sql,
                new { MigratedAtUtc = DateTimeOffset.UtcNow.ToString("O") });
            RecordMigration(
                transactionExecutor,
                PreLoginRememberedAccountMigrationV7.MigrationId,
                checksum);
        });
    }

    public PersistenceResult ApplyPostLoginMigrations(IReadOnlyDictionary<string, string> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        try
        {
            var executor = new HostPersistenceCommandExecutor(factory);
            foreach (var migration in migrations.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!IsPostLoginMigrationId(migration.Key))
                {
                    return PersistenceResult.Failure(PersistenceError.Create(
                        PersistenceError.SchemaMismatch,
                        "Post-login migration id must use a numbered post-login migration prefix."));
                }

                var checksum = Checksum(migration.Value);
                if (MigrationAlreadyRecorded(executor, migration.Key, checksum))
                {
                    continue;
                }

                new HostPersistenceTransactionScope(factory).ExecuteInTransaction(transactionExecutor =>
                {
                    transactionExecutor.Execute(migration.Value);
                    RecordMigration(transactionExecutor, migration.Key, checksum);
                });
            }

            return PersistenceResult.Success();
        }
        catch (Exception ex) when (IsRecoverableMigrationFailure(ex))
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.MigrationFailed));
        }
    }

    private static bool IsPostLoginMigrationId(string migrationId)
    {
        if (migrationId.Length < 6 || migrationId[4] != '_')
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (!char.IsDigit(migrationId[i]))
            {
                return false;
            }
        }

        var number = int.Parse(migrationId.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
        return number > 1 && migrationId[5..].All(character =>
            char.IsLower(character) ||
            char.IsDigit(character) ||
            character == '_');
    }

    private static void RecordMigration(IHostPersistenceCommandExecutor executor, string migrationId, string checksum)
    {
        var existing = executor.Query<string>(
            "SELECT ChecksumSha256 FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = migrationId }).SingleOrDefault();

        if (existing is not null)
        {
            if (!existing.Equals(checksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Migration checksum mismatch.");
            }

            return;
        }

        executor.Execute(
            "INSERT INTO StorageMetadata(MigrationId, AppliedAtUtc, ChecksumSha256, AppliedByVersion) VALUES (@MigrationId, @AppliedAtUtc, @ChecksumSha256, @AppliedByVersion);",
            new
            {
                MigrationId = migrationId,
                AppliedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                ChecksumSha256 = checksum,
                AppliedByVersion = "Sprint4"
            });
    }

    private static bool MigrationAlreadyRecorded(
        IHostPersistenceCommandExecutor executor,
        string migrationId,
        string checksum)
    {
        var existing = executor.Query<string>(
            "SELECT ChecksumSha256 FROM StorageMetadata WHERE MigrationId = @MigrationId;",
            new { MigrationId = migrationId }).SingleOrDefault();

        if (existing is null)
        {
            return false;
        }

        if (!existing.Equals(checksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Migration checksum mismatch.");
        }

        return true;
    }

    private void MirrorDeviceIdentity(IHostPersistenceCommandExecutor executor)
    {
        var material = protectedMaterialSource.LoadRequired();
        var existing = executor.Query<byte[]>(
            "SELECT ProtectedStableDeviceIdBlob FROM DeviceIdentity WHERE IdentityId = 'DobbyStableDeviceId';").SingleOrDefault();

        if (existing is not null)
        {
            if (!existing.SequenceEqual(material.ProtectedStableDeviceIdBlob))
            {
                throw new InvalidOperationException("DeviceIdentity mirror does not match canonical file-backed stable device id material.");
            }

            return;
        }

        executor.Execute(
            "INSERT INTO DeviceIdentity(IdentityId, ProtectedStableDeviceIdBlob, AlgorithmVersion, ProtectionVersion, CreatedAtUtc, UpdatedAtUtc, RedactedSupportPrefix) VALUES ('DobbyStableDeviceId', @ProtectedStableDeviceIdBlob, 'random-32-v1', 'dpapi-local-machine-v1', @CreatedAtUtc, @UpdatedAtUtc, @RedactedSupportPrefix);",
            new
            {
                ProtectedStableDeviceIdBlob = material.ProtectedStableDeviceIdBlob,
                CreatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                RedactedSupportPrefix = DeviceIdentityDiscriminator(material.StableDeviceId)
            });
    }

    private static string DeviceIdentityDiscriminator(ReadOnlySpan<byte> stableDeviceId)
    {
        var domain = "dobby-device-identity-discriminator-v1\0"u8;
        var input = new byte[domain.Length + stableDeviceId.Length];
        domain.CopyTo(input);
        stableDeviceId.CopyTo(input.AsSpan(domain.Length));
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant()[..12];
    }

    private static string Checksum(string sql)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
    }

    private static bool IsRecoverableMigrationFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException;
}
