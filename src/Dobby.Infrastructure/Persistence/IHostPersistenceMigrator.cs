namespace Dobby.Infrastructure.Persistence;

public interface IHostPersistenceMigrator
{
    PersistenceResult ApplyPreLoginMigrations();

    PersistenceResult ApplyPostLoginMigrations(IReadOnlyDictionary<string, string> migrations);
}
