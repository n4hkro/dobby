using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;

namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceMaintenanceService
{
    private readonly HostPersistenceOptions options;
    private readonly IProtectedMaterialSource protectedMaterialSource;
    private readonly IHostPersistenceConnectionFactory connectionFactory;

    public HostPersistenceMaintenanceService(
        HostPersistenceOptions options,
        IProtectedMaterialSource protectedMaterialSource,
        IHostPersistenceConnectionFactory connectionFactory)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.protectedMaterialSource = protectedMaterialSource ?? throw new ArgumentNullException(nameof(protectedMaterialSource));
        this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
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
            return PersistenceResult.Success();
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.IoFailed));
        }
    }

    public PersistenceResult<string> CreateSupportSnapshot(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return PersistenceResult<string>.Failure(PersistenceError.Create(
                PersistenceError.ConstraintViolation,
                "Support snapshot destination path is required."));
        }

        var fullDestinationPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(destinationPath));
        var backupRoot = Path.GetFullPath(Path.Combine(
            Environment.ExpandEnvironmentVariables(options.DataRoot),
            "backup"));
        if (!IsUnderRoot(fullDestinationPath, backupRoot))
        {
            return PersistenceResult<string>.Failure(PersistenceError.Create(
                PersistenceError.ConstraintViolation,
                "Support snapshot destination must be under the runtime backup root."));
        }

        var checkpoint = ForceWalCheckpoint();
        if (!checkpoint.IsSuccess)
        {
            return PersistenceResult<string>.Failure(checkpoint.Error);
        }

        var open = connectionFactory.Open();
        if (!open.IsSuccess)
        {
            return PersistenceResult<string>.Failure(open.Error);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullDestinationPath)!);
            DeleteExistingSnapshotFiles(fullDestinationPath);

            using var source = open.Value;
            using var destination = OpenEncryptedDestination(fullDestinationPath);
            source.Connection.BackupDatabase(destination);
            return PersistenceResult<string>.Success(fullDestinationPath);
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return PersistenceResult<string>.Failure(PersistenceError.Create(
                PersistenceError.IoFailed));
        }
    }

    private SqliteConnection OpenEncryptedDestination(string destinationPath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
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
            Execute(connection, "PRAGMA journal_mode=WAL;");
            Execute(connection, "PRAGMA temp_store=MEMORY;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void DeleteExistingSnapshotFiles(string destinationPath)
    {
        File.Delete(destinationPath);
        File.Delete(destinationPath + "-wal");
        File.Delete(destinationPath + "-shm");
    }

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

    private static bool IsUnderRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
