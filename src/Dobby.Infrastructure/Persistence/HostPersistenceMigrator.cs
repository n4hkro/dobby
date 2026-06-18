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
            MirrorDeviceIdentity(executor);
            return PersistenceResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.MigrationFailed,
                $"Pre-login persistence migration failed: {ex.GetType().Name}: {ex.Message}"));
        }
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

                executor.Execute(migration.Value);
                RecordMigration(executor, migration.Key, checksum);
            }

            return PersistenceResult.Success();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.MigrationFailed,
                $"Post-login persistence migration failed: {ex.GetType().Name}: {ex.Message}"));
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
                RedactedSupportPrefix = Convert.ToHexString(SHA256.HashData(material.StableDeviceId))[..12]
            });
    }

    private static string Checksum(string sql)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
    }
}
