namespace Dobby.Infrastructure.Persistence;

public sealed class HostPersistenceTransactionScope : IHostPersistenceTransactionScope
{
    private readonly IHostPersistenceConnectionFactory factory;

    public HostPersistenceTransactionScope(IHostPersistenceConnectionFactory factory)
    {
        this.factory = factory;
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
            action(executor);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void ExecuteInSavepoint(
        HostPersistenceConnection connection,
        Action<IHostPersistenceCommandExecutor> action)
    {
        var savepointName = "dobby_nested_" + Guid.NewGuid().ToString("N");
        ExecuteControlCommand(connection, $"SAVEPOINT {savepointName};");

        try
        {
            action(new HostPersistenceCommandExecutor(connection));
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
}
