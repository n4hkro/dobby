using Dobby.Contracts.PluginStorage;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using System.Data.Common;
using System.IO;
using Dobby.Contracts.Licensing;

namespace Dobby.Host.Plugins;

[PluginRuntimeActionOwner(PluginRuntimeAction.StorageRead, PluginRuntimeAction.StorageWrite)]
public sealed class HostPluginStorageCompartment : IPluginStorageCompartment
{
    private readonly string pluginId;
    private readonly string scope;
    private readonly Func<Result<string>> userScopeHashProvider;
    private readonly IHostPersistenceCommandExecutor executor;
    private readonly IHostPersistenceTransactionScope transactionScope;
    private readonly HostRuntimeModeMutationGate mutationGate;

    public HostPluginStorageCompartment(
        string pluginId,
        string scope,
        Func<Result<string>> userScopeHashProvider,
        IHostPersistenceCommandExecutor executor,
        IHostPersistenceTransactionScope transactionScope,
        HostRuntimeModeMutationGate mutationGate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(userScopeHashProvider);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(transactionScope);
        ArgumentNullException.ThrowIfNull(mutationGate);

        this.pluginId = pluginId;
        this.scope = scope;
        this.userScopeHashProvider = userScopeHashProvider;
        this.executor = executor;
        this.transactionScope = transactionScope;
        this.mutationGate = mutationGate;
    }

    public ValueTask<Result<PluginStorageValue?>> GetAsync(
        PluginStorageKey key,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateKey(key, out var validatedKey))
        {
            return ValueTask.FromResult(Result.Failure<PluginStorageValue?>(ErrorCode.InvalidInput));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Result.Cancelled<PluginStorageValue?>());
        }

        var userScopeHash = ResolveUserScopeHash();
        if (!userScopeHash.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginStorageValue?>(userScopeHash.ErrorCode!.Value));
        }

        var readAllowed = mutationGate.EnsurePluginStorageReadAllowed(pluginId);
        if (!readAllowed.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginStorageValue?>(readAllowed.ErrorCode!.Value));
        }

        try
        {
            var row = executor.Query<PluginStorageValueRow>(
                    """
                    SELECT Payload, ContentType, SchemaVersion
                    FROM PluginStorageValue
                    WHERE PluginId = @PluginId
                      AND Scope = @Scope
                      AND UserScopeHash = @UserScopeHash
                      AND StorageKey = @StorageKey;
                    """,
                    new
                    {
                        PluginId = pluginId,
                        Scope = scope,
                        UserScopeHash = userScopeHash.Value,
                        StorageKey = validatedKey.Value
                    })
                .SingleOrDefault();

            return ValueTask.FromResult(Result.Success<PluginStorageValue?>(
                row is null
                    ? null
                    : new PluginStorageValue(row.Payload, row.ContentType, row.SchemaVersion)));
        }
        catch (Exception exception) when (IsRecoverablePersistenceException(exception))
        {
            return ValueTask.FromResult(Result.Failure<PluginStorageValue?>(ErrorCode.DatabaseUnavailable));
        }
    }

    public ValueTask<Result> SetAsync(
        PluginStorageKey key,
        PluginStorageValue value,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateKey(key, out var validatedKey))
        {
            return ValueTask.FromResult(Result.Failure(ErrorCode.InvalidInput));
        }

        ArgumentNullException.ThrowIfNull(value);

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Result.Failure(ErrorCode.Cancelled));
        }

        var userScopeHash = ResolveUserScopeHash();
        if (!userScopeHash.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure(userScopeHash.ErrorCode!.Value));
        }

        var mutationAllowed = mutationGate.EnsurePluginMutationAllowed(pluginId);
        if (!mutationAllowed.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure(mutationAllowed.ErrorCode!.Value));
        }

        try
        {
            transactionScope.ExecuteInTransaction(transactionExecutor =>
            {
                var now = DateTimeOffset.UtcNow.ToString("O");
                transactionExecutor.Execute(
                    """
                    INSERT INTO PluginStorageValue (
                        PluginId,
                        Scope,
                        UserScopeHash,
                        StorageKey,
                        Payload,
                        ContentType,
                        SchemaVersion,
                        CreatedAtUtc,
                        UpdatedAtUtc)
                    VALUES (
                        @PluginId,
                        @Scope,
                        @UserScopeHash,
                        @StorageKey,
                        @Payload,
                        @ContentType,
                        @SchemaVersion,
                        @Now,
                        @Now)
                    ON CONFLICT(PluginId, Scope, UserScopeHash, StorageKey)
                    DO UPDATE SET
                        Payload = excluded.Payload,
                        ContentType = excluded.ContentType,
                        SchemaVersion = excluded.SchemaVersion,
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """,
                    new
                    {
                        PluginId = pluginId,
                        Scope = scope,
                        UserScopeHash = userScopeHash.Value,
                        StorageKey = validatedKey.Value,
                        Payload = value.Payload.ToArray(),
                        value.ContentType,
                        value.SchemaVersion,
                        Now = now
                    });
            });

            return ValueTask.FromResult(Result.Success());
        }
        catch (Exception exception) when (IsRecoverablePersistenceException(exception))
        {
            return ValueTask.FromResult(Result.Failure(ErrorCode.DatabaseUnavailable));
        }
    }

    public ValueTask<Result<bool>> DeleteAsync(
        PluginStorageKey key,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateKey(key, out var validatedKey))
        {
            return ValueTask.FromResult(Result.Failure<bool>(ErrorCode.InvalidInput));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Result.Cancelled<bool>());
        }

        var userScopeHash = ResolveUserScopeHash();
        if (!userScopeHash.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<bool>(userScopeHash.ErrorCode!.Value));
        }

        var mutationAllowed = mutationGate.EnsurePluginMutationAllowed(pluginId);
        if (!mutationAllowed.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<bool>(mutationAllowed.ErrorCode!.Value));
        }

        try
        {
            var deleted = 0;
            transactionScope.ExecuteInTransaction(transactionExecutor =>
            {
                deleted = transactionExecutor.Execute(
                    """
                    DELETE FROM PluginStorageValue
                    WHERE PluginId = @PluginId
                      AND Scope = @Scope
                      AND UserScopeHash = @UserScopeHash
                      AND StorageKey = @StorageKey;
                    """,
                    new
                    {
                        PluginId = pluginId,
                        Scope = scope,
                        UserScopeHash = userScopeHash.Value,
                        StorageKey = validatedKey.Value
                    });
            });

            return ValueTask.FromResult(Result.Success(deleted > 0));
        }
        catch (Exception exception) when (IsRecoverablePersistenceException(exception))
        {
            return ValueTask.FromResult(Result.Failure<bool>(ErrorCode.DatabaseUnavailable));
        }
    }

    public ValueTask<Result<bool>> ExistsAsync(
        PluginStorageKey key,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateKey(key, out var validatedKey))
        {
            return ValueTask.FromResult(Result.Failure<bool>(ErrorCode.InvalidInput));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Result.Cancelled<bool>());
        }

        var userScopeHash = ResolveUserScopeHash();
        if (!userScopeHash.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<bool>(userScopeHash.ErrorCode!.Value));
        }

        var readAllowed = mutationGate.EnsurePluginStorageReadAllowed(pluginId);
        if (!readAllowed.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<bool>(readAllowed.ErrorCode!.Value));
        }

        try
        {
            var count = executor.QueryScalar<int>(
                """
                SELECT COUNT(*)
                FROM PluginStorageValue
                WHERE PluginId = @PluginId
                  AND Scope = @Scope
                  AND UserScopeHash = @UserScopeHash
                  AND StorageKey = @StorageKey;
                """,
                new
                {
                    PluginId = pluginId,
                    Scope = scope,
                    UserScopeHash = userScopeHash.Value,
                    StorageKey = validatedKey.Value
                });

            return ValueTask.FromResult(Result.Success(count > 0));
        }
        catch (Exception exception) when (IsRecoverablePersistenceException(exception))
        {
            return ValueTask.FromResult(Result.Failure<bool>(ErrorCode.DatabaseUnavailable));
        }
    }

    public ValueTask<Result<IReadOnlyList<PluginStorageKey>>> ListKeysAsync(
        PluginStorageKeyPrefix prefix,
        CancellationToken cancellationToken = default)
    {
        if (!ValidatePrefix(prefix, out var validatedPrefix))
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<PluginStorageKey>>(ErrorCode.InvalidInput));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Result.Cancelled<IReadOnlyList<PluginStorageKey>>());
        }

        var userScopeHash = ResolveUserScopeHash();
        if (!userScopeHash.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<PluginStorageKey>>(userScopeHash.ErrorCode!.Value));
        }

        var readAllowed = mutationGate.EnsurePluginStorageReadAllowed(pluginId);
        if (!readAllowed.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<PluginStorageKey>>(readAllowed.ErrorCode!.Value));
        }

        try
        {
            var keys = executor.Query<string>(
                    """
                    SELECT StorageKey
                    FROM PluginStorageValue
                    WHERE PluginId = @PluginId
                      AND Scope = @Scope
                      AND UserScopeHash = @UserScopeHash
                      AND StorageKey LIKE @Prefix ESCAPE '\'
                    ORDER BY StorageKey;
                    """,
                    new
                    {
                        PluginId = pluginId,
                        Scope = scope,
                        UserScopeHash = userScopeHash.Value,
                        Prefix = EscapeLikePrefix(validatedPrefix.Value) + "%"
                    })
                .Select(key => new PluginStorageKey(key))
                .ToArray();

            return ValueTask.FromResult(Result.Success<IReadOnlyList<PluginStorageKey>>(keys));
        }
        catch (Exception exception) when (IsRecoverablePersistenceException(exception))
        {
            return ValueTask.FromResult(Result.Failure<IReadOnlyList<PluginStorageKey>>(ErrorCode.DatabaseUnavailable));
        }
    }

    private Result<string> ResolveUserScopeHash()
    {
        return userScopeHashProvider();
    }

    private static bool ValidateKey(PluginStorageKey key, out PluginStorageKey validatedKey)
    {
        var validation = PluginStorageKey.Create(key.Value);
        validatedKey = validation.IsSuccess ? validation.Value : default;
        return validation.IsSuccess;
    }

    private static bool ValidatePrefix(PluginStorageKeyPrefix prefix, out PluginStorageKeyPrefix validatedPrefix)
    {
        var validation = PluginStorageKeyPrefix.Create(prefix.Value ?? string.Empty);
        validatedPrefix = validation.IsSuccess ? validation.Value : default;
        return validation.IsSuccess;
    }

    private static string EscapeLikePrefix(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static bool IsRecoverablePersistenceException(Exception exception)
    {
        return exception is DbException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException;
    }

    private sealed class PluginStorageValueRow
    {
        public byte[] Payload { get; init; } = [];

        public string? ContentType { get; init; }

        public int? SchemaVersion { get; init; }
    }
}
