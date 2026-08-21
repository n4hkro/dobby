namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceTransactionScope : IHostPersistenceTransactionScope
{
    private readonly IHostPersistenceConnectionFactory factory;
    private readonly IHostPersistenceTransactionFaultInjector? faultInjector;

    public HostPersistenceTransactionScope(
        IHostPersistenceConnectionFactory factory,
        IHostPersistenceTransactionFaultInjector? faultInjector = null)
    {
        this.factory = factory;
        this.faultInjector = faultInjector;
    }

    public void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var ambientConnection = HostPersistenceCommandExecutor.GetCurrentAmbientConnection();
        if (ambientConnection is not null)
        {
            ExecuteInSavepoint(ambientConnection, action);
            return;
        }

        var open = factory.Open();
        if (!open.IsSuccess)
        {
            throw new InvalidOperationException(open.Error.Message);
        }

        using var connection = open.Value;
        using var transaction = connection.Connection.BeginTransaction();
        using var transactionConnection = new HostPersistenceConnection(connection.Connection, transaction);
        var executor = new HostPersistenceCommandExecutor(transactionConnection);
        using var ambientScope = HostPersistenceCommandExecutor.PushAmbientConnection(transactionConnection);

        try
        {
            action(CreateExecutor(executor));
            faultInjector?.BeforeCommit();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void ExecuteInSavepoint(
        HostPersistenceConnection connection,
        Action<IHostPersistenceCommandExecutor> action)
    {
        var savepointName = "dobby_nested_" + Guid.NewGuid().ToString("N");
        ExecuteControlCommand(connection, $"SAVEPOINT {savepointName};");

        try
        {
            action(CreateExecutor(new HostPersistenceCommandExecutor(connection)));
            ExecuteControlCommand(connection, $"RELEASE SAVEPOINT {savepointName};");
        }
        catch
        {
            ExecuteControlCommand(connection, $"ROLLBACK TO SAVEPOINT {savepointName};");
            ExecuteControlCommand(connection, $"RELEASE SAVEPOINT {savepointName};");
            throw;
        }
    }

    private static void ExecuteControlCommand(HostPersistenceConnection connection, string sql)
    {
        using var command = connection.Connection.CreateCommand();
        command.Transaction = connection.Transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private IHostPersistenceCommandExecutor CreateExecutor(IHostPersistenceCommandExecutor executor)
    {
        return faultInjector is null
            ? executor
            : new FaultInjectingExecutor(executor, faultInjector);
    }

    private sealed class FaultInjectingExecutor(
        IHostPersistenceCommandExecutor inner,
        IHostPersistenceTransactionFaultInjector injector) : IHostPersistenceCommandExecutor
    {
        private int commandNumber;

        public int Execute(string sql, object? parameters = null)
        {
            Before(HostPersistenceCommandKind.Execute, sql);
            return inner.Execute(sql, parameters);
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            Before(HostPersistenceCommandKind.Query, sql);
            return inner.Query<T>(sql, parameters);
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            Before(HostPersistenceCommandKind.QueryScalar, sql);
            return inner.QueryScalar<T>(sql, parameters);
        }

        private void Before(HostPersistenceCommandKind kind, string commandText)
        {
            injector.BeforeCommand(Interlocked.Increment(ref commandNumber), kind, commandText);
        }
    }
}
