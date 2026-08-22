using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Dobby.Infrastructure.Persistence;

public sealed record HostPersistenceBackupPolicy(
    int MaximumRetainedSnapshots = 10,
    long MinimumFreeSpaceReserveBytes = 64L * 1024 * 1024)
{
    internal void Validate()
    {
        if (MaximumRetainedSnapshots is < 1 or > 1000 ||
            MinimumFreeSpaceReserveBytes is < 0 or > 1024L * 1024 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(HostPersistenceBackupPolicy));
        }
    }
}

public sealed record HostPersistenceSnapshot(
    string DatabasePath,
    string MetadataPath,
    string Sha256,
    long Length,
    DateTimeOffset CreatedAtUtc);

public sealed record HostPersistenceRestoreOutcome(
    string RestoredDatabasePath,
    string QuarantinedPreviousStoreDirectory);

public enum HostPersistenceRestoreFaultPoint
{
    AfterCurrentStoreQuarantined,
    AfterCandidatePromoted
}

public interface IHostPersistenceMaintenanceFaultInjector
{
    void OnRestore(HostPersistenceRestoreFaultPoint faultPoint);
}

public interface IHostPersistenceMaintenanceService
{
    PersistenceResult ForceWalCheckpoint();

    PersistenceResult<HostPersistenceSnapshot> CreateSupportSnapshot(
        string destinationPath,
        HostPersistenceBackupPolicy policy,
        CancellationToken cancellationToken);

    PersistenceResult<HostPersistenceRestoreOutcome> RestoreSupportSnapshot(
        string candidatePath,
        CancellationToken cancellationToken);
}

public sealed class HostPersistenceMaintenanceService : IHostPersistenceMaintenanceService
{
    private const string SnapshotMetadataSuffix = ".metadata.json";
    private static readonly string[] RequiredCoreTables =
    [
        "AuditLog",
        "DeviceIdentity",
        "HostConfiguration",
        "HostOutbox",
        "StorageMetadata",
        "Users"
    ];

    private readonly HostPersistenceOptions options;
    private readonly IProtectedMaterialSource protectedMaterialSource;
    private readonly IHostPersistenceConnectionFactory connectionFactory;
    private readonly Func<string, long> availableFreeSpace;
    private readonly IHostPersistenceMaintenanceFaultInjector? faultInjector;

    public HostPersistenceMaintenanceService(
        HostPersistenceOptions options,
        IProtectedMaterialSource protectedMaterialSource,
        IHostPersistenceConnectionFactory connectionFactory,
        Func<string, long>? availableFreeSpace = null,
        IHostPersistenceMaintenanceFaultInjector? faultInjector = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.protectedMaterialSource = protectedMaterialSource ?? throw new ArgumentNullException(nameof(protectedMaterialSource));
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        this.availableFreeSpace = availableFreeSpace ?? ResolveAvailableFreeSpace;
        this.faultInjector = faultInjector;
    }

    public PersistenceResult ForceWalCheckpoint()
    {
        var open = connectionFactory.Open();
        if (!open.IsSuccess)
        {
            return PersistenceResult.Failure(open.Error);
        }

        try
        {
            using var connection = open.Value;
            Execute(connection.Connection, "PRAGMA wal_checkpoint(TRUNCATE);");
            return VerifyIntegrity(connection.Connection);
        }
        catch (Exception exception) when (IsMaintenanceFailure(exception))
        {
            return PersistenceResult.Failure(PersistenceError.Create(PersistenceError.IoFailed));
        }
    }

    public PersistenceResult<string> CreateSupportSnapshot(string destinationPath)
    {
        var created = CreateSupportSnapshot(
            destinationPath,
            new HostPersistenceBackupPolicy(),
            CancellationToken.None);
        return created.IsSuccess
            ? PersistenceResult<string>.Success(created.Value.DatabasePath)
            : PersistenceResult<string>.Failure(created.Error);
    }

    public PersistenceResult<HostPersistenceSnapshot> CreateSupportSnapshot(
        string destinationPath,
        HostPersistenceBackupPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        var destination = ValidateSnapshotPath(destinationPath, mustExist: false);
        if (!destination.IsSuccess)
        {
            return PersistenceResult<HostPersistenceSnapshot>.Failure(destination.Error);
        }

        var fullDestinationPath = destination.Value;
        var metadataPath = fullDestinationPath + SnapshotMetadataSuffix;
        var partialPath = fullDestinationPath + $".partial.{Guid.CreateVersion7():N}";
        var partialMetadataPath = partialPath + SnapshotMetadataSuffix;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullDestinationPath) || File.Exists(metadataPath))
            {
                return PersistenceResult<HostPersistenceSnapshot>.Failure(PersistenceError.Create(
                    PersistenceError.ConstraintViolation,
                    "A support snapshot already exists at the destination."));
            }

            EnsureSafeDirectory(Path.GetDirectoryName(fullDestinationPath)!, BackupRoot());
            EnsureSufficientFreeSpace(fullDestinationPath, policy.MinimumFreeSpaceReserveBytes);
            var checkpoint = ForceWalCheckpoint();
            if (!checkpoint.IsSuccess)
            {
                return PersistenceResult<HostPersistenceSnapshot>.Failure(checkpoint.Error);
            }

            var open = connectionFactory.Open();
            if (!open.IsSuccess)
            {
                return PersistenceResult<HostPersistenceSnapshot>.Failure(open.Error);
            }

            using (var source = open.Value)
            using (var target = OpenEncryptedConnection(partialPath, SqliteOpenMode.ReadWriteCreate, configureWal: true))
            {
                BackupCancellable(source.Connection, target, cancellationToken);
                Execute(target, "PRAGMA wal_checkpoint(TRUNCATE);");
                var integrity = VerifyIntegrity(target);
                if (!integrity.IsSuccess)
                {
                    return PersistenceResult<HostPersistenceSnapshot>.Failure(integrity.Error);
                }
                VerifySchemaAndIdentity(target);
            }

            cancellationToken.ThrowIfCancellationRequested();
            FlushFile(partialPath);
            var createdAt = DateTimeOffset.UtcNow;
            var length = new FileInfo(partialPath).Length;
            var sha256 = ComputeSha256(partialPath);
            WriteSnapshotMetadata(
                partialMetadataPath,
                new SnapshotMetadata(1, createdAt, length, sha256));
            File.Move(partialPath, fullDestinationPath);
            try
            {
                File.Move(partialMetadataPath, metadataPath);
            }
            catch
            {
                File.Move(fullDestinationPath, partialPath);
                throw;
            }

            ApplySnapshotRetention(policy.MaximumRetainedSnapshots, fullDestinationPath);
            return PersistenceResult<HostPersistenceSnapshot>.Success(new HostPersistenceSnapshot(
                fullDestinationPath,
                metadataPath,
                sha256,
                length,
                createdAt));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return PersistenceResult<HostPersistenceSnapshot>.Failure(PersistenceError.Create(
                PersistenceError.TransactionConflict,
                "Support snapshot creation was cancelled."));
        }
        catch (Exception exception) when (IsMaintenanceFailure(exception))
        {
            return PersistenceResult<HostPersistenceSnapshot>.Failure(PersistenceError.Create(PersistenceError.IoFailed));
        }
        finally
        {
            DeleteIfExists(partialPath);
            DeleteIfExists(partialPath + "-wal");
            DeleteIfExists(partialPath + "-shm");
            DeleteIfExists(partialMetadataPath);
        }
    }

    public PersistenceResult<HostPersistenceRestoreOutcome> RestoreSupportSnapshot(
        string candidatePath,
        CancellationToken cancellationToken)
    {
        var candidate = ValidateSnapshotPath(candidatePath, mustExist: true);
        if (!candidate.IsSuccess)
        {
            return PersistenceResult<HostPersistenceRestoreOutcome>.Failure(candidate.Error);
        }

        var canonicalCandidate = candidate.Value;
        var livePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(options.DatabasePath));
        var dataRoot = Path.GetDirectoryName(livePath)!;
        var stagedPath = Path.Combine(dataRoot, $"host.db.restore.{Guid.CreateVersion7():N}.partial");
        string? quarantineDirectory = null;
        var liveWasQuarantined = false;
        IDisposable? exclusive = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifySnapshotMetadataIfPresent(canonicalCandidate);
            var verifiedCandidateHash = ComputeSha256(canonicalCandidate);
            using (var verifiedCandidate = OpenEncryptedConnection(
                       canonicalCandidate,
                       SqliteOpenMode.ReadOnly,
                       configureWal: false))
            {
                var integrity = VerifyIntegrity(verifiedCandidate);
                if (!integrity.IsSuccess)
                {
                    return PersistenceResult<HostPersistenceRestoreOutcome>.Failure(integrity.Error);
                }
                VerifySchemaAndIdentity(verifiedCandidate);
            }

            EnsureSufficientFreeSpace(livePath, new FileInfo(canonicalCandidate).Length);
            File.Copy(canonicalCandidate, stagedPath, overwrite: false);
            FlushFile(stagedPath);
            if (!verifiedCandidateHash.Equals(ComputeSha256(stagedPath), StringComparison.Ordinal))
            {
                throw new InvalidDataException("The restore candidate changed during staging.");
            }
            using (var staged = OpenEncryptedConnection(stagedPath, SqliteOpenMode.ReadWrite, configureWal: false))
            {
                var stagedIntegrity = VerifyIntegrity(staged);
                if (!stagedIntegrity.IsSuccess)
                {
                    throw new InvalidDataException("The staged restore candidate failed integrity verification.");
                }
                VerifySchemaAndIdentity(staged);
            }

            exclusive = HostPersistenceAccessGate.EnterExclusive(livePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(livePath))
            {
                throw new FileNotFoundException("The current host store is missing.", livePath);
            }
            using (var current = OpenEncryptedConnection(livePath, SqliteOpenMode.ReadWrite, configureWal: true))
            {
                Execute(current, "PRAGMA wal_checkpoint(TRUNCATE);");
                var currentIntegrity = VerifyIntegrity(current);
                if (!currentIntegrity.IsSuccess)
                {
                    throw new InvalidDataException("The current store failed pre-restore integrity verification.");
                }
            }
            using (var current = OpenEncryptedConnection(livePath, SqliteOpenMode.ReadOnly, configureWal: false))
            using (var staged = OpenEncryptedConnection(stagedPath, SqliteOpenMode.ReadWrite, configureWal: true))
            {
                MergeImmutableEvidence(current, staged);
                Execute(staged, "PRAGMA wal_checkpoint(TRUNCATE);");
                if (!VerifyIntegrity(staged).IsSuccess)
                {
                    throw new InvalidDataException("The evidence-preserving restore candidate failed integrity verification.");
                }
            }

            quarantineDirectory = CreateRestoreQuarantineDirectory();
            MoveStoreSet(livePath, Path.Combine(quarantineDirectory, "host.db"));
            liveWasQuarantined = true;
            faultInjector?.OnRestore(HostPersistenceRestoreFaultPoint.AfterCurrentStoreQuarantined);
            MoveStoreSet(stagedPath, livePath);
            faultInjector?.OnRestore(HostPersistenceRestoreFaultPoint.AfterCandidatePromoted);

            using (var restored = OpenEncryptedConnection(livePath, SqliteOpenMode.ReadWrite, configureWal: true))
            {
                var integrity = VerifyIntegrity(restored);
                if (!integrity.IsSuccess)
                {
                    throw new InvalidDataException("The promoted restore candidate failed integrity verification.");
                }
                VerifySchemaAndIdentity(restored);
                Execute(restored, "PRAGMA wal_checkpoint(TRUNCATE);");
            }

            return PersistenceResult<HostPersistenceRestoreOutcome>.Success(
                new HostPersistenceRestoreOutcome(livePath, quarantineDirectory));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryRollBackRestore(livePath, quarantineDirectory, liveWasQuarantined);
            return PersistenceResult<HostPersistenceRestoreOutcome>.Failure(PersistenceError.Create(
                PersistenceError.TransactionConflict,
                "Support snapshot restore was cancelled."));
        }
        catch (Exception exception) when (IsMaintenanceFailure(exception))
        {
            TryRollBackRestore(livePath, quarantineDirectory, liveWasQuarantined);
            return PersistenceResult<HostPersistenceRestoreOutcome>.Failure(PersistenceError.Create(PersistenceError.IoFailed));
        }
        finally
        {
            exclusive?.Dispose();
            DeleteIfExists(stagedPath);
            DeleteIfExists(stagedPath + "-wal");
            DeleteIfExists(stagedPath + "-shm");
        }
    }

    private void BackupCancellable(
        SqliteConnection source,
        SqliteConnection destination,
        CancellationToken cancellationToken)
    {
        sqlite3_backup? backup = null;
        var completed = false;
        try
        {
            backup = raw.sqlite3_backup_init(destination.Handle, "main", source.Handle, "main") ??
                throw new InvalidOperationException("SQLite backup initialization failed.");
            var busyAttempts = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = raw.sqlite3_backup_step(backup, 128);
                if (result == raw.SQLITE_DONE)
                {
                    completed = true;
                    return;
                }
                if (result == raw.SQLITE_OK)
                {
                    busyAttempts = 0;
                    continue;
                }
                if (result is raw.SQLITE_BUSY or raw.SQLITE_LOCKED &&
                    busyAttempts++ < Math.Max(1, options.BusyTimeoutMilliseconds / 25))
                {
                    cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(25));
                    continue;
                }
                throw new IOException("SQLite online backup could not make progress.");
            }
        }
        finally
        {
            if (backup is not null)
            {
                var finish = raw.sqlite3_backup_finish(backup);
                if (completed && finish != raw.SQLITE_OK)
                {
                    throw new IOException("SQLite online backup finalization failed.");
                }
            }
        }
    }

    private SqliteConnection OpenEncryptedConnection(
        string path,
        SqliteOpenMode mode,
        bool configureWal)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());

        connection.Open();
        try
        {
            ApplyCipherPragmas(connection, protectedMaterialSource.LoadRequired().DbOpenSecret);
            Execute(connection, "PRAGMA foreign_keys=ON;");
            Execute(connection, string.Create(
                CultureInfo.InvariantCulture,
                $"PRAGMA busy_timeout={options.BusyTimeoutMilliseconds};"));
            if (configureWal)
            {
                Execute(connection, "PRAGMA journal_mode=WAL;");
            }
            Execute(connection, "PRAGMA temp_store=MEMORY;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private void VerifySchemaAndIdentity(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
            using var reader = command.ExecuteReader();
            var tables = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
            if (RequiredCoreTables.Any(table => !tables.Contains(table)))
            {
                throw new InvalidDataException("The snapshot schema is incomplete.");
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT COUNT(*) FROM StorageMetadata WHERE MigrationId = '0001_prelogin_bootstrap';";
            if (Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidDataException("The snapshot bootstrap migration is missing.");
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT ProtectedStableDeviceIdBlob FROM DeviceIdentity WHERE IdentityId = 'DobbyStableDeviceId';";
            var mirrored = command.ExecuteScalar() as byte[];
            if (mirrored is null ||
                !mirrored.SequenceEqual(protectedMaterialSource.LoadRequired().ProtectedStableDeviceIdBlob))
            {
                throw new InvalidDataException("The snapshot belongs to a different protected device identity.");
            }
        }
    }

    private static void MergeImmutableEvidence(
        SqliteConnection current,
        SqliteConnection staged)
    {
        using var transaction = staged.BeginTransaction();
        using (var readAudit = current.CreateCommand())
        {
            readAudit.CommandText =
                "SELECT AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson FROM AuditLog ORDER BY AuditId;";
            using var reader = readAudit.ExecuteReader();
            while (reader.Read())
            {
                var values = Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index) ? null : reader.GetString(index))
                    .ToArray();
                var alreadyPresent = false;
                using (var existing = staged.CreateCommand())
                {
                    existing.Transaction = transaction;
                    existing.CommandText =
                        "SELECT OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson FROM AuditLog WHERE AuditId = @AuditId;";
                    existing.Parameters.AddWithValue("@AuditId", values[0]!);
                    using var existingReader = existing.ExecuteReader();
                    if (existingReader.Read())
                    {
                        alreadyPresent = true;
                        for (var index = 0; index < 7; index++)
                        {
                            var stagedValue = existingReader.IsDBNull(index) ? null : existingReader.GetString(index);
                            if (!StringComparer.Ordinal.Equals(stagedValue, values[index + 1]))
                            {
                                throw new InvalidDataException("The restore candidate conflicts with immutable audit evidence.");
                            }
                        }
                    }
                }
                if (alreadyPresent)
                {
                    continue;
                }

                using var insert = staged.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText =
                    "INSERT INTO AuditLog(AuditId, OccurredAtUtc, EventName, ActorId, Target, Result, CorrelationId, DetailsJson) VALUES (@AuditId, @OccurredAtUtc, @EventName, @ActorId, @Target, @Result, @CorrelationId, @DetailsJson);";
                AddNullableTextParameters(
                    insert,
                    ["AuditId", "OccurredAtUtc", "EventName", "ActorId", "Target", "Result", "CorrelationId", "DetailsJson"],
                    values);
                insert.ExecuteNonQuery();
            }
        }

        using (var readOutbox = current.CreateCommand())
        {
            readOutbox.CommandText =
                "SELECT OutboxId, IdempotencyKey, Purpose, MessageType, PayloadJson, CreatedAtUtc, NextAttemptAtUtc, AttemptCount, CompletedAtUtc, LastErrorCode, LeaseId, LeaseExpiresAtUtc, EvidenceCorrelationId FROM HostOutbox ORDER BY OutboxId;";
            using var reader = readOutbox.ExecuteReader();
            while (reader.Read())
            {
                var textValues = new object?[reader.FieldCount];
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    textValues[index] = reader.IsDBNull(index)
                        ? null
                        : index == 7
                            ? reader.GetInt32(index)
                            : reader.GetString(index);
                }

                using (var deleteCandidateVersion = staged.CreateCommand())
                {
                    deleteCandidateVersion.Transaction = transaction;
                    deleteCandidateVersion.CommandText =
                        "DELETE FROM HostOutbox WHERE IdempotencyKey = @IdempotencyKey;";
                    deleteCandidateVersion.Parameters.AddWithValue("@IdempotencyKey", textValues[1]!);
                    deleteCandidateVersion.ExecuteNonQuery();
                }
                using var insert = staged.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText =
                    """
                    INSERT INTO HostOutbox(
                        OutboxId, IdempotencyKey, Purpose, MessageType, PayloadJson,
                        CreatedAtUtc, NextAttemptAtUtc, AttemptCount, CompletedAtUtc,
                        LastErrorCode, LeaseId, LeaseExpiresAtUtc, EvidenceCorrelationId)
                    VALUES (
                        @OutboxId, @IdempotencyKey, @Purpose, @MessageType, @PayloadJson,
                        @CreatedAtUtc, @NextAttemptAtUtc, @AttemptCount, @CompletedAtUtc,
                        @LastErrorCode, @LeaseId, @LeaseExpiresAtUtc, @EvidenceCorrelationId);
                    """;
                AddNullableObjectParameters(
                    insert,
                    [
                        "OutboxId", "IdempotencyKey", "Purpose", "MessageType", "PayloadJson",
                        "CreatedAtUtc", "NextAttemptAtUtc", "AttemptCount", "CompletedAtUtc",
                        "LastErrorCode", "LeaseId", "LeaseExpiresAtUtc", "EvidenceCorrelationId"
                    ],
                    textValues);
                insert.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }

    private static void AddNullableTextParameters(
        SqliteCommand command,
        IReadOnlyList<string> names,
        IReadOnlyList<string?> values)
    {
        for (var index = 0; index < names.Count; index++)
        {
            command.Parameters.AddWithValue("@" + names[index], (object?)values[index] ?? DBNull.Value);
        }
    }

    private static void AddNullableObjectParameters(
        SqliteCommand command,
        IReadOnlyList<string> names,
        IReadOnlyList<object?> values)
    {
        for (var index = 0; index < names.Count; index++)
        {
            command.Parameters.AddWithValue("@" + names[index], values[index] ?? DBNull.Value);
        }
    }

    private static PersistenceResult VerifyIntegrity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)
            ? PersistenceResult.Success()
            : PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.SchemaMismatch,
                "SQLite integrity verification failed."));
    }

    private PersistenceResult<string> ValidateSnapshotPath(string path, bool mustExist)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Snapshot path is required.", nameof(path));
            }
            var canonical = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            if (!IsUnderRoot(canonical, BackupRoot()) ||
                !Path.GetExtension(canonical).Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                canonical.Equals(Path.GetFullPath(options.DatabasePath), StringComparison.OrdinalIgnoreCase) ||
                canonical.Contains($"{Path.DirectorySeparatorChar}restore-quarantine{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The snapshot path is outside the approved backup boundary.");
            }
            if (mustExist && !File.Exists(canonical))
            {
                throw new FileNotFoundException("The support snapshot does not exist.", canonical);
            }
            if (mustExist && (File.GetAttributes(canonical) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Reparse-point snapshots are not allowed.");
            }
            return PersistenceResult<string>.Success(canonical);
        }
        catch (Exception exception) when (IsMaintenanceFailure(exception))
        {
            return PersistenceResult<string>.Failure(PersistenceError.Create(
                PersistenceError.ConstraintViolation));
        }
    }

    private void VerifySnapshotMetadataIfPresent(string candidatePath)
    {
        var metadataPath = candidatePath + SnapshotMetadataSuffix;
        if (!File.Exists(metadataPath))
        {
            return;
        }
        if ((File.GetAttributes(metadataPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Reparse-point snapshot metadata is not allowed.");
        }
        var metadata = JsonSerializer.Deserialize<SnapshotMetadata>(File.ReadAllText(metadataPath)) ??
            throw new InvalidDataException("Snapshot metadata is malformed.");
        var file = new FileInfo(candidatePath);
        if (metadata.FormatVersion != 1 ||
            metadata.Length != file.Length ||
            !metadata.Sha256.Equals(ComputeSha256(candidatePath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Snapshot metadata verification failed.");
        }
    }

    private void EnsureSufficientFreeSpace(string destinationPath, long reserveBytes)
    {
        var sourceLength = File.Exists(options.DatabasePath)
            ? new FileInfo(options.DatabasePath).Length
            : 1024L * 1024;
        var walLength = File.Exists(options.DatabasePath + "-wal")
            ? new FileInfo(options.DatabasePath + "-wal").Length
            : 0;
        var required = checked(Math.Max(sourceLength + walLength, 1024L * 1024) + reserveBytes);
        if (availableFreeSpace(destinationPath) < required)
        {
            throw new IOException("Insufficient free space for the persistence maintenance operation.");
        }
    }

    private void ApplySnapshotRetention(int maximumRetainedSnapshots, string newestPath)
    {
        var candidates = EnumerateSafeFiles(BackupRoot())
            .Where(path => Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}restore-quarantine{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(".partial.", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Where(path => !path.Equals(newestPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var expired in candidates.Skip(maximumRetainedSnapshots - 1))
        {
            if (!IsUnderRoot(expired, BackupRoot()))
            {
                continue;
            }
            DeleteIfExists(expired);
            DeleteIfExists(expired + SnapshotMetadataSuffix);
        }
    }

    private string CreateRestoreQuarantineDirectory()
    {
        var directory = Path.Combine(
            BackupRoot(),
            "restore-quarantine",
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.CreateVersion7():N}");
        EnsureSafeDirectory(directory, BackupRoot());
        return directory;
    }

    private static void MoveStoreSet(string sourceDatabasePath, string destinationDatabasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationDatabasePath)!);
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var source = sourceDatabasePath + suffix;
            if (File.Exists(source))
            {
                File.Move(source, destinationDatabasePath + suffix);
            }
        }
    }

    private void TryRollBackRestore(
        string livePath,
        string? quarantineDirectory,
        bool liveWasQuarantined)
    {
        try
        {
            if (!liveWasQuarantined || quarantineDirectory is null)
            {
                return;
            }

            var quarantinedPath = Path.Combine(quarantineDirectory, "host.db");
            if (!File.Exists(quarantinedPath))
            {
                return;
            }
            if (File.Exists(livePath))
            {
                MoveStoreSet(livePath, Path.Combine(quarantineDirectory, "failed-promoted-host.db"));
            }
            MoveStoreSet(quarantinedPath, livePath);
            using var rolledBack = OpenEncryptedConnection(livePath, SqliteOpenMode.ReadWrite, configureWal: true);
            if (!VerifyIntegrity(rolledBack).IsSuccess)
            {
                throw new InvalidDataException("The rolled-back current store failed integrity verification.");
            }
        }
        catch (Exception exception) when (IsMaintenanceFailure(exception))
        {
            // Both the previous and failed promoted stores remain quarantined for support recovery.
        }
    }

    private static void EnsureSafeDirectory(string directory, string approvedRoot)
    {
        var canonicalRoot = Path.GetFullPath(approvedRoot);
        var canonicalDirectory = Path.GetFullPath(directory);
        if (!IsUnderRoot(canonicalDirectory, canonicalRoot))
        {
            throw new UnauthorizedAccessException("Directory escaped the approved root.");
        }

        Directory.CreateDirectory(canonicalRoot);
        if ((File.GetAttributes(canonicalRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException("A reparse-point backup root is not allowed.");
        }
        var relative = Path.GetRelativePath(canonicalRoot, canonicalDirectory);
        var current = canonicalRoot;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException("Reparse-point backup directories are not allowed.");
            }
            Directory.CreateDirectory(current);
        }

        var permissionProbe = Path.Combine(canonicalDirectory, $".permission-probe-{Guid.CreateVersion7():N}");
        using (File.Open(permissionProbe, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }
        File.Delete(permissionProbe);
    }

    private static IEnumerable<string> EnumerateSafeFiles(string approvedRoot)
    {
        var root = Path.GetFullPath(approvedRoot);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var canonical = Path.GetFullPath(entry);
                if (!IsUnderRoot(canonical, root))
                {
                    throw new UnauthorizedAccessException("Backup retention escaped the approved root.");
                }
                var attributes = File.GetAttributes(canonical);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnauthorizedAccessException("Backup retention does not traverse reparse points.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(canonical);
                }
                else
                {
                    yield return canonical;
                }
            }
        }
    }

    private static void WriteSnapshotMetadata(string path, SnapshotMetadata metadata)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, metadata);
        stream.Flush(flushToDisk: true);
    }

    private static void FlushFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        stream.Flush(flushToDisk: true);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static long ResolveAvailableFreeSpace(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path)) ??
            throw new IOException("The destination drive could not be resolved.");
        return new DriveInfo(root).AvailableFreeSpace;
    }

    private string BackupRoot() => Path.GetFullPath(Path.Combine(
        Environment.ExpandEnvironmentVariables(options.DataRoot),
        "backup"));

    private static void ApplyCipherPragmas(SqliteConnection connection, byte[] dbOpenSecret)
    {
        Execute(connection, "PRAGMA cipher='sqlcipher';");
        Execute(connection, "PRAGMA cipher_compatibility=4;");
        Execute(connection, "PRAGMA legacy=4;");
        Execute(connection, "PRAGMA kdf_iter=256000;");
        Execute(connection, "PRAGMA cipher_page_size=4096;");
        Execute(connection, "PRAGMA hmac_use=1;");
        Execute(connection, "PRAGMA cipher_use_hmac=ON;");
        Execute(connection, "PRAGMA kdf_algorithm=2;");
        Execute(connection, "PRAGMA hmac_algorithm=2;");
        Execute(connection, FormattableString.Invariant($"PRAGMA key='{Convert.ToHexString(dbOpenSecret)}';"));
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMaintenanceFailure(Exception exception) => exception is
        SqliteException or
        InvalidOperationException or
        IOException or
        UnauthorizedAccessException or
        ArgumentException or
        JsonException or
        CryptographicException;

    private sealed record SnapshotMetadata(
        int FormatVersion,
        DateTimeOffset CreatedAtUtc,
        long Length,
        string Sha256);
}
