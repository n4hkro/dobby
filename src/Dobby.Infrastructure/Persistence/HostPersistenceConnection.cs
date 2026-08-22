using Microsoft.Data.Sqlite;

namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceConnection : IDisposable
{
    private readonly IDisposable? accessLease;

    internal HostPersistenceConnection(
        SqliteConnection connection,
        SqliteTransaction? transaction = null,
        IDisposable? accessLease = null)
    {
        Connection = connection;
        Transaction = transaction;
        this.accessLease = accessLease;
    }

    internal SqliteConnection Connection { get; }

    internal SqliteTransaction? Transaction { get; }

    public void Dispose()
    {
        try
        {
            Transaction?.Dispose();
            Connection.Dispose();
        }
        finally
        {
            accessLease?.Dispose();
        }
    }
}
