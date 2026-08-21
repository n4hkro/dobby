namespace Dobby.Infrastructure.Persistence;

public enum HostPersistenceCommandKind
{
    Execute,
    Query,
    QueryScalar
}

public interface IHostPersistenceTransactionFaultInjector
{
    void BeforeCommand(int commandNumber, HostPersistenceCommandKind kind, string commandText);

    void BeforeCommit();
}
