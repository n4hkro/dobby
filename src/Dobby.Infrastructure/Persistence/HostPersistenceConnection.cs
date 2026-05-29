using Microsoft.Data.Sqlite;

namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceConnection : IDisposable
{
    internal HostPersistenceConnection(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        Connection = connection;
        Transaction = transaction;
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction? Transaction { get; }

    public void Dispose()
    {
        Transaction?.Dispose();
        Connection.Dispose();
    }
}
