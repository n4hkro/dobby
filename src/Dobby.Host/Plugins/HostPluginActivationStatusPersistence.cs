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
                    ReconcileVersionPointer(tx, state);
                }

                var authoritativeStates = stateStore.Snapshot();
                if (!authoritativeStates.IsSuccess)
                {
                    throw new InvalidOperationException("Installed plugin state could not be reloaded in the activation transaction.");
                }

                metadataCache.RebuildFrom(tx, authoritativeStates.Value);
            });

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    private void ReconcileVersionPointer(
        IHostPersistenceCommandExecutor transactionExecutor,
        InstalledPluginState activationState)
    {
        var pointer = transactionExecutor.Query<VersionPointerRow>(
            """
            SELECT ActiveFingerprint, LastKnownGoodFingerprint, PendingActivationFingerprint
            FROM InstalledPluginVersionPointer
            WHERE PluginId = @PluginId;
            """,
            new { activationState.PluginId }).SingleOrDefault();
        if (pointer?.PendingActivationFingerprint is null ||
            !pointer.PendingActivationFingerprint.Equals(activationState.PackageFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        if (activationState.StatusSummary is "Enabled" or "Disabled")
        {
            transactionExecutor.Execute(
                """
                UPDATE InstalledPluginVersionPointer
                SET LastKnownGoodFingerprint = ActiveFingerprint,
                    PendingActivationFingerprint = NULL,
                    UpdatedAtUtc = @UpdatedAtUtc
                WHERE PluginId = @PluginId;
                """,
                new
                {
                    activationState.PluginId,
                    UpdatedAtUtc = activationState.UpdatedAtUtc.ToString("O")
                });
            return;
        }

        if (!IsActivationFailure(activationState.StatusSummary) ||
            pointer.ActiveFingerprint.Equals(pointer.LastKnownGoodFingerprint, StringComparison.Ordinal))
        {
            transactionExecutor.Execute(
                """
                UPDATE InstalledPluginVersionPointer
                SET PendingActivationFingerprint = NULL, UpdatedAtUtc = @UpdatedAtUtc
                WHERE PluginId = @PluginId;
                """,
                new
                {
                    activationState.PluginId,
                    UpdatedAtUtc = activationState.UpdatedAtUtc.ToString("O")
                });
            return;
        }

        var lastKnownGood = transactionExecutor.Query<InstalledPluginVersionRow>(
            """
            SELECT
                PluginId, PackageFingerprint, Version, DisplayName, Description,
                RuntimePath, ValidationSummary, IsEnabled, InstalledAtUtc, UpdatedAtUtc
            FROM InstalledPluginVersion
            WHERE PluginId = @PluginId AND PackageFingerprint = @PackageFingerprint;
            """,
            new
            {
                activationState.PluginId,
                PackageFingerprint = pointer.LastKnownGoodFingerprint
            }).SingleOrDefault() ?? throw new InvalidOperationException("Last-known-good plugin version is missing.");
        var restored = lastKnownGood.ToState(activationState);
        stateStore.Upsert(transactionExecutor, restored);
        transactionExecutor.Execute(
            """
            UPDATE InstalledPluginVersionPointer
            SET ActiveFingerprint = LastKnownGoodFingerprint,
                PendingActivationFingerprint = NULL,
                UpdatedAtUtc = @UpdatedAtUtc
            WHERE PluginId = @PluginId;
            """,
            new
            {
                activationState.PluginId,
                UpdatedAtUtc = activationState.UpdatedAtUtc.ToString("O")
            });
    }

    private static bool IsActivationFailure(string statusSummary) =>
        statusSummary.StartsWith("Faulted", StringComparison.Ordinal) ||
        statusSummary.StartsWith("Rejected", StringComparison.Ordinal) ||
        statusSummary.StartsWith("Skipped", StringComparison.Ordinal);

    private sealed record VersionPointerRow(
        string ActiveFingerprint,
        string LastKnownGoodFingerprint,
        string? PendingActivationFingerprint);

    private sealed class InstalledPluginVersionRow
    {
        public string PluginId { get; init; } = string.Empty;
        public string PackageFingerprint { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string RuntimePath { get; init; } = string.Empty;
        public string ValidationSummary { get; init; } = string.Empty;
        public long IsEnabled { get; init; }
        public string InstalledAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;

        public InstalledPluginState ToState(InstalledPluginState failedState)
        {
            var isEnabled = IsEnabled != 0;
            return new InstalledPluginState(
                PluginId,
                Version,
                PackageFingerprint,
                DisplayName,
                Description,
                isEnabled,
                ValidationSummary,
                RuntimePath,
                isEnabled ? "Enabled" : "Disabled",
                DateTimeOffset.Parse(InstalledAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTimeOffset.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                IsPendingUninstall: false,
                DataRetentionChoice: failedState.DataRetentionChoice,
                RestartRequired: false,
                LastActivatedAtUtc: failedState.LastActivatedAtUtc);
        }
    }
}
