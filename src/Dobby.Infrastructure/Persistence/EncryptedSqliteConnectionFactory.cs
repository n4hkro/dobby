using System.Globalization;
using System.IO;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Data.Sqlite;

namespace Dobby.Infrastructure.Persistence;

public sealed class EncryptedSqliteConnectionFactory : IHostPersistenceConnectionFactory
{
    private readonly HostPersistenceOptions options;
    private readonly IProtectedMaterialSource protectedMaterialSource;
    private readonly string nativeDllPath;

    public EncryptedSqliteConnectionFactory(
        HostPersistenceOptions options,
        IProtectedMaterialSource protectedMaterialSource)
        : this(options, protectedMaterialSource, Path.Combine(AppContext.BaseDirectory, "native", "win-x64", "sqlite3mc_x64.dll"))
    {
    }

    public EncryptedSqliteConnectionFactory(
        HostPersistenceOptions options,
        IProtectedMaterialSource protectedMaterialSource,
        string nativeDllPath)
    {
        this.options = options;
        this.protectedMaterialSource = protectedMaterialSource;
        this.nativeDllPath = nativeDllPath;
    }

    public PersistenceResult<HostPersistenceConnection> Open()
    {
        if (!options.ProviderMode.Equals("SQLite3MultipleCiphers", StringComparison.Ordinal))
        {
            return PersistenceResult<HostPersistenceConnection>.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable,
                "Storage provider mode is not approved for encrypted production persistence."));
        }

        if (!options.RequireDataAtRestProtection)
        {
            return PersistenceResult<HostPersistenceConnection>.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable,
                "Data-at-rest protection is required."));
        }

        if (options.BusyTimeoutMilliseconds is < 100 or > 30000)
        {
            return PersistenceResult<HostPersistenceConnection>.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable,
                "SQLite busy_timeout guardrail is outside the approved bounded range."));
        }

        var providerResult = SqliteProviderInitializer.EnsureInitialized(nativeDllPath);
        if (!providerResult.IsSuccess)
        {
            return PersistenceResult<HostPersistenceConnection>.Failure(providerResult.Error);
        }

        try
        {
            var material = protectedMaterialSource.LoadRequired();
            Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);

            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = options.DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false
            }.ToString());

            connection.Open();
            try
            {
                ApplyCipherPragmas(connection, material.DbOpenSecret);
                ApplyGuardrails(connection);
                VerifyReadable(connection);
                return PersistenceResult<HostPersistenceConnection>.Success(new HostPersistenceConnection(connection));
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return PersistenceResult<HostPersistenceConnection>.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable));
        }
    }

    private void ApplyCipherPragmas(SqliteConnection connection, byte[] dbOpenSecret)
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

    private void ApplyGuardrails(SqliteConnection connection)
    {
        Execute(connection, "PRAGMA foreign_keys=ON;");
        Execute(connection, string.Create(CultureInfo.InvariantCulture, $"PRAGMA busy_timeout={options.BusyTimeoutMilliseconds};"));
        Execute(connection, "PRAGMA journal_mode=WAL;");
        Execute(connection, "PRAGMA temp_store=MEMORY;");
        Execute(connection, "PRAGMA secure_delete=ON;");

        if (QueryPragmaInt(connection, "PRAGMA foreign_keys;") != 1)
        {
            throw new InvalidOperationException("SQLite foreign_keys guardrail was not applied.");
        }

        if (QueryPragmaInt(connection, "PRAGMA busy_timeout;") != options.BusyTimeoutMilliseconds)
        {
            throw new InvalidOperationException("SQLite busy_timeout guardrail was not applied.");
        }

        if (!QueryPragmaText(connection, "PRAGMA journal_mode;").Equals("wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite WAL journal guardrail was not applied.");
        }

        if (QueryPragmaInt(connection, "PRAGMA temp_store;") != 2)
        {
            throw new InvalidOperationException("SQLite temp_store memory guardrail was not applied.");
        }

        if (QueryPragmaInt(connection, "PRAGMA secure_delete;") != 1)
        {
            throw new InvalidOperationException("SQLite secure_delete guardrail was not applied.");
        }
    }

    private static void VerifyReadable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
        _ = command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long QueryPragmaInt(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static string QueryPragmaText(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
