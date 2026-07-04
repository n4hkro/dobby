using Dapper;
using System.Threading;

namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceCommandExecutor : IHostPersistenceCommandExecutor
{
    private static readonly AsyncLocal<HostPersistenceConnection?> CurrentAmbientConnection = new();
    private readonly IHostPersistenceConnectionFactory? factory;
    private readonly HostPersistenceConnection? ambientConnection;

    public HostPersistenceCommandExecutor(IHostPersistenceConnectionFactory factory)
    {
        this.factory = factory;
    }

    internal HostPersistenceCommandExecutor(HostPersistenceConnection ambientConnection)
    {
        this.ambientConnection = ambientConnection;
    }

    public int Execute(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return WithConnection(connection => connection.Connection.Execute(sql, parameters, connection.Transaction));
    }

    public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return WithConnection(connection => connection.Connection.Query<T>(sql, parameters, connection.Transaction).AsList());
    }

    public T QueryScalar<T>(string sql, object? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return WithConnection(connection => connection.Connection.ExecuteScalar<T>(sql, parameters, connection.Transaction)!);
    }

    private T WithConnection<T>(Func<HostPersistenceConnection, T> action)
    {
        if (ambientConnection is not null)
        {
            return action(ambientConnection);
        }

        var currentAmbientConnection = CurrentAmbientConnection.Value;
        if (currentAmbientConnection is not null)
        {
            return action(currentAmbientConnection);
        }

        var open = factory!.Open();
        if (!open.IsSuccess)
        {
            throw new InvalidOperationException(open.Error.Message);
        }

        using var connection = open.Value;
        return action(connection);
    }

    internal static HostPersistenceConnection? GetCurrentAmbientConnection()
    {
        return CurrentAmbientConnection.Value;
    }

    internal static IDisposable PushAmbientConnection(HostPersistenceConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var previousConnection = CurrentAmbientConnection.Value;
        CurrentAmbientConnection.Value = connection;
        return new AmbientConnectionLease(previousConnection);
    }

    private sealed class AmbientConnectionLease(HostPersistenceConnection? previousConnection) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            CurrentAmbientConnection.Value = previousConnection;
            disposed = true;
        }
    }
}
