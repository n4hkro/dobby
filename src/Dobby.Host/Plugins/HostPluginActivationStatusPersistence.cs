using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.PluginFramework.Packaging;

namespace Dobby.Host.Plugins;

public interface IHostPluginActivationStatusPersistence
{
    Result Persist(
        IReadOnlyList<InstalledPluginState> installedStates,
        IReadOnlyList<InstalledPluginState> changedStates);
}

public sealed class HostPluginActivationStatusPersistence(
    HostInstalledPluginStateStore stateStore,
    HostInstalledPluginMetadataCache metadataCache,
    IHostPersistenceTransactionScope transactionScope) : IHostPluginActivationStatusPersistence
{
    private readonly HostInstalledPluginStateStore stateStore =
        stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    private readonly HostInstalledPluginMetadataCache metadataCache =
        metadataCache ?? throw new ArgumentNullException(nameof(metadataCache));
    private readonly IHostPersistenceTransactionScope transactionScope =
        transactionScope ?? throw new ArgumentNullException(nameof(transactionScope));

    public Result Persist(
        IReadOnlyList<InstalledPluginState> installedStates,
        IReadOnlyList<InstalledPluginState> changedStates)
    {
        ArgumentNullException.ThrowIfNull(installedStates);
        ArgumentNullException.ThrowIfNull(changedStates);

        if (changedStates.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            transactionScope.ExecuteInTransaction(tx =>
            {
                foreach (var state in changedStates)
                {
                    stateStore.Upsert(tx, state);
                }

                metadataCache.RebuildFrom(tx, installedStates);
            });

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }
}
