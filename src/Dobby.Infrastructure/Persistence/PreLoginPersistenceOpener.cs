using Dobby.Infrastructure.ProtectedStorage;

namespace Dobby.Infrastructure.Persistence;

public sealed class PreLoginPersistenceOpener
{
    private readonly ProtectedMaterialAvailabilityProbe protectedMaterialProbe;

    public PreLoginPersistenceOpener(ProtectedMaterialAvailabilityProbe protectedMaterialProbe)
    {
        this.protectedMaterialProbe = protectedMaterialProbe;
    }

    public PersistenceResult Open(HostPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!protectedMaterialProbe.IsAvailable())
        {
            return PersistenceResult.Failure(PersistenceError.Create(
                PersistenceError.ProviderUnavailable,
                "Protected material is unavailable."));
        }

        var factory = new EncryptedSqliteConnectionFactory(options, protectedMaterialProbe);
        var migrator = new HostPersistenceMigrator(factory, protectedMaterialProbe);
        return migrator.ApplyPreLoginMigrations();
    }
}
