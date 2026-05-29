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

        var open = factory.Open();
        if (!open.IsSuccess)
        {
            throw new InvalidOperationException(open.Error.Message);
        }

        using var connection = open.Value;
        using var transaction = connection.Connection.BeginTransaction();
        using var transactionConnection = new HostPersistenceConnection(connection.Connection, transaction);
        var executor = new HostPersistenceCommandExecutor(transactionConnection);

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
}
