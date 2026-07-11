using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace Dobby.Host.Plugins;

public sealed class HostPluginStorage : IPluginStorage
{
    private const string SharedDomainScope = "shared-domain";
    private const string UserScopedScope = "user-scoped";
    private const string UserScopeHashPurpose = "Dobby.PluginStorage.UserScope.v1";

    private readonly string pluginId;
    private readonly Func<string> userScopeProvider;

    public HostPluginStorage(
        string pluginId,
        IHostPersistenceCommandExecutor executor,
        IHostPersistenceTransactionScope transactionScope,
        Func<string> userScopeProvider,
        HostRuntimeModeMutationGate mutationGate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(transactionScope);
        ArgumentNullException.ThrowIfNull(userScopeProvider);
        ArgumentNullException.ThrowIfNull(mutationGate);

        this.pluginId = pluginId;
        this.userScopeProvider = userScopeProvider;
        SharedDomainStorage = new HostPluginStorageCompartment(
            pluginId,
            SharedDomainScope,
            ResolveSharedScope,
            executor,
            transactionScope,
            mutationGate);
        UserScopedStorage = new HostPluginStorageCompartment(
            pluginId,
            UserScopedScope,
            ResolveUserScopeHash,
            executor,
            transactionScope,
            mutationGate);
    }

    public IPluginStorageCompartment SharedDomainStorage { get; }

    public IPluginStorageCompartment UserScopedStorage { get; }

    private Result<string> ResolveSharedScope()
    {
        return string.IsNullOrWhiteSpace(userScopeProvider())
            ? Result.Failure<string>(ErrorCode.Unauthorized)
            : Result.Success(string.Empty);
    }

    private Result<string> ResolveUserScopeHash()
    {
        var userScope = userScopeProvider();
        if (string.IsNullOrWhiteSpace(userScope))
        {
            return Result.Failure<string>(ErrorCode.Unauthorized);
        }

        var material = Encoding.UTF8.GetBytes($"{UserScopeHashPurpose}:{pluginId}:{userScope}");
        return Result.Success(Convert.ToHexString(SHA256.HashData(material)).ToLowerInvariant());
    }
}
