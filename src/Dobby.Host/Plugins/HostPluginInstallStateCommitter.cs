using Dobby.Audit;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.PluginFramework.Packaging;

namespace Dobby.Host.Plugins;

public sealed class HostPluginInstallStateCommitter(
    HostInstalledPluginStateStore stateStore,
    HostInstalledPluginMetadataCache metadataCache,
    IHostMutationUnitOfWork mutationUnitOfWork,
    TimeProvider timeProvider) : IPluginInstallStateCommitter
{
    public Result Commit(
        PluginPackageDescriptor package,
        InstalledPluginState installedState,
        InstalledPluginMetadata metadata,
        PluginInstallCommitContext context)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(installedState);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ActorId);

        try
        {
            return mutationUnitOfWork.ExecuteResult(mutation =>
            {
                var operation = mutation.Commands.Query<InstallOperationRow>(
                    """
                    SELECT OperationId, State
                    FROM PluginInstallOperation
                    WHERE RuntimePath = @RuntimePath
                    ORDER BY CreatedAtUtc DESC
                    LIMIT 1;
                    """,
                    new { installedState.RuntimePath }).SingleOrDefault();
                if (operation is null ||
                    (!operation.State.Equals("Promoted", StringComparison.Ordinal) &&
                     !operation.State.Equals("StateCommitted", StringComparison.Ordinal)))
                {
                    return Result.Failure(ErrorCode.ValidationFailed);
                }

                InsertImmutableVersion(mutation.Commands, installedState);
                var pointer = mutation.Commands.Query<VersionPointerRow>(
                    """
                    SELECT ActiveFingerprint, LastKnownGoodFingerprint, PendingActivationFingerprint
                    FROM InstalledPluginVersionPointer
                    WHERE PluginId = @PluginId;
                    """,
                    new { installedState.PluginId }).SingleOrDefault();
                var lastKnownGood = pointer?.ActiveFingerprint ?? installedState.PackageFingerprint;
                mutation.Commands.Execute(
                    """
                    INSERT INTO InstalledPluginVersionPointer (
                        PluginId, ActiveFingerprint, LastKnownGoodFingerprint,
                        PendingActivationFingerprint, UpdatedAtUtc)
                    VALUES (
                        @PluginId, @ActiveFingerprint, @LastKnownGoodFingerprint,
                        @PendingActivationFingerprint, @UpdatedAtUtc)
                    ON CONFLICT(PluginId) DO UPDATE SET
                        ActiveFingerprint = excluded.ActiveFingerprint,
                        LastKnownGoodFingerprint = excluded.LastKnownGoodFingerprint,
                        PendingActivationFingerprint = excluded.PendingActivationFingerprint,
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """,
                    new
                    {
                        installedState.PluginId,
                        ActiveFingerprint = installedState.PackageFingerprint,
                        LastKnownGoodFingerprint = lastKnownGood,
                        PendingActivationFingerprint = installedState.PackageFingerprint,
                        UpdatedAtUtc = timeProvider.GetUtcNow().ToString("O")
                    });

                stateStore.Upsert(mutation.Commands, installedState);
                metadataCache.Upsert(mutation.Commands, metadata);
                if (operation.State.Equals("Promoted", StringComparison.Ordinal))
                {
                    var affected = mutation.Commands.Execute(
                        """
                        UPDATE PluginInstallOperation
                        SET State = 'StateCommitted', UpdatedAtUtc = @UpdatedAtUtc
                        WHERE OperationId = @OperationId AND State = 'Promoted';
                        """,
                        new
                        {
                            operation.OperationId,
                            UpdatedAtUtc = timeProvider.GetUtcNow().ToString("O")
                        });
                    if (affected != 1)
                    {
                        return Result.Failure(ErrorCode.ResourceBusy);
                    }
                }

                mutation.AppendAudit(new AuditEvent(
                    AuditEventName.PluginInstallVerified,
                    context.ActorId,
                    $"plugin:{installedState.PluginId}",
                    AuditResult.Success,
                    context.CorrelationId,
                    timeProvider.GetUtcNow(),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["pluginId"] = installedState.PluginId,
                        ["fingerprint"] = installedState.PackageFingerprint,
                        ["diagnosticMarker"] = AuditEventName.PluginInstallVerified
                    }));
                mutation.EnqueueOutbox(new HostOutboxMessage(
                    $"plugin-install:{operation.OperationId}",
                    PluginInstallCommittedOutboxHandler.Route.Purpose,
                    PluginInstallCommittedOutboxHandler.Route.MessageType,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["operationId"] = operation.OperationId,
                        ["pluginId"] = installedState.PluginId,
                        ["version"] = installedState.Version,
                        ["correlationId"] = context.CorrelationId.ToString()
                    },
                    context.CorrelationId.ToString()));
                return Result.Success();
            });
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    private static void InsertImmutableVersion(
        IHostPersistenceCommandExecutor commands,
        InstalledPluginState state)
    {
        commands.Execute(
            """
            INSERT OR IGNORE INTO InstalledPluginVersion (
                PluginId, PackageFingerprint, Version, DisplayName, Description,
                RuntimePath, ValidationSummary, IsEnabled, InstalledAtUtc, UpdatedAtUtc)
            VALUES (
                @PluginId, @PackageFingerprint, @Version, @DisplayName, @Description,
                @RuntimePath, @ValidationSummary, @IsEnabled, @InstalledAtUtc, @UpdatedAtUtc);
            """,
            new
            {
                state.PluginId,
                state.PackageFingerprint,
                state.Version,
                state.DisplayName,
                state.Description,
                state.RuntimePath,
                state.ValidationSummary,
                IsEnabled = state.IsEnabled ? 1 : 0,
                InstalledAtUtc = state.InstalledAtUtc.ToString("O"),
                UpdatedAtUtc = state.UpdatedAtUtc.ToString("O")
            });
        var exact = commands.QueryScalar<int>(
            """
            SELECT COUNT(*) FROM InstalledPluginVersion
            WHERE PluginId = @PluginId
              AND PackageFingerprint = @PackageFingerprint
              AND Version = @Version
              AND RuntimePath = @RuntimePath;
            """,
            new
            {
                state.PluginId,
                state.PackageFingerprint,
                state.Version,
                state.RuntimePath
            });
        if (exact != 1)
        {
            throw new InvalidOperationException("Immutable plugin version identity conflicts with durable state.");
        }
    }

    private sealed record InstallOperationRow(string OperationId, string State);

    private sealed record VersionPointerRow(
        string ActiveFingerprint,
        string LastKnownGoodFingerprint,
        string? PendingActivationFingerprint);
}

public sealed class PluginInstallCommittedOutboxHandler : IHostOutboxMessageHandler
{
    public static HostOutboxRoute Route { get; } = new("plugin-install", "state-committed");

    public Task DispatchAsync(HostOutboxDelivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
