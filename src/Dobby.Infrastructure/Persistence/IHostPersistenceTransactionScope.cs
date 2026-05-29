namespace Dobby.Infrastructure.Persistence;

public interface IHostPersistenceTransactionScope
{
    void ExecuteInTransaction(Action<IHostPersistenceCommandExecutor> action);
}
