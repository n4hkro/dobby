namespace Dobby.Infrastructure.Persistence;

public interface IHostPersistenceConnectionFactory
{
    PersistenceResult<HostPersistenceConnection> Open();
}
