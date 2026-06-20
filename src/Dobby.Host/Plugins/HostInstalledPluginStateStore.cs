using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.PluginFramework.Packaging;

namespace Dobby.Host.Plugins;

public sealed class HostInstalledPluginStateStore(
    IHostPersistenceCommandExecutor executor,
    IHostPersistenceTransactionScope transactionScope) : IInstalledPluginStateStore
{
    public Result Upsert(InstalledPluginState state)
    {
        try
        {
            transactionScope.ExecuteInTransaction(tx =>
            {
                tx.Execute(
                    """
                    INSERT INTO InstalledPluginState (
                        PluginId, Version, PackageFingerprint, DisplayName, Description, IsEnabled,
                        ValidationSummary, RuntimePath, StatusSummary, InstalledAtUtc, UpdatedAtUtc,
                        IsPendingUninstall, DataRetentionChoice, RestartRequired, LastActivatedAtUtc
                    ) VALUES (
                        @PluginId, @Version, @PackageFingerprint, @DisplayName, @Description, @IsEnabled,
                        @ValidationSummary, @RuntimePath, @StatusSummary, @InstalledAtUtc, @UpdatedAtUtc,
                        @IsPendingUninstall, @DataRetentionChoice, @RestartRequired, @LastActivatedAtUtc
                    )
                    ON CONFLICT(PluginId) DO UPDATE SET
                        Version = excluded.Version,
                        PackageFingerprint = excluded.PackageFingerprint,
                        DisplayName = excluded.DisplayName,
                        Description = excluded.Description,
                        IsEnabled = excluded.IsEnabled,
                        ValidationSummary = excluded.ValidationSummary,
                        RuntimePath = excluded.RuntimePath,
                        StatusSummary = excluded.StatusSummary,
                        IsPendingUninstall = excluded.IsPendingUninstall,
                        DataRetentionChoice = excluded.DataRetentionChoice,
                        RestartRequired = excluded.RestartRequired,
                        LastActivatedAtUtc = excluded.LastActivatedAtUtc,
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """,
                    ToParameters(state));
            });

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    public Result<InstalledPluginState?> Get(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var row = executor.Query<InstalledPluginStateRow>(
            """
            SELECT
                PluginId, Version, PackageFingerprint, DisplayName, Description, IsEnabled,
                ValidationSummary, RuntimePath, StatusSummary, InstalledAtUtc, UpdatedAtUtc,
                IsPendingUninstall, DataRetentionChoice, RestartRequired, LastActivatedAtUtc
            FROM InstalledPluginState
            WHERE PluginId = @PluginId;
            """,
            new { PluginId = pluginId })
            .SingleOrDefault();

        return Result.Success<InstalledPluginState?>(row is null ? null : row.ToState());
    }

    public Result<IReadOnlyList<InstalledPluginState>> Snapshot()
    {
        var rows = executor.Query<InstalledPluginStateRow>(
            """
            SELECT
                PluginId, Version, PackageFingerprint, DisplayName, Description, IsEnabled,
                ValidationSummary, RuntimePath, StatusSummary, InstalledAtUtc, UpdatedAtUtc,
                IsPendingUninstall, DataRetentionChoice, RestartRequired, LastActivatedAtUtc
            FROM InstalledPluginState
            ORDER BY PluginId;
            """);

        return Result.Success<IReadOnlyList<InstalledPluginState>>(rows.Select(row => row.ToState()).ToArray());
    }

    public Result SetEnabled(string pluginId, bool isEnabled, string statusSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(statusSummary);

        try
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var affected = executor.Execute(
                """
                UPDATE InstalledPluginState
                SET IsEnabled = @IsEnabled,
                    StatusSummary = @StatusSummary,
                    UpdatedAtUtc = @UpdatedAtUtc
                WHERE PluginId = @PluginId;
                """,
                new { PluginId = pluginId, IsEnabled = isEnabled ? 1 : 0, StatusSummary = statusSummary, UpdatedAtUtc = now });

            return affected == 0 ? Result.Failure(ErrorCode.NotFound) : Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    public Result Remove(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        try
        {
            executor.Execute("DELETE FROM InstalledPluginState WHERE PluginId = @PluginId;", new { PluginId = pluginId });
            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    private sealed class InstalledPluginStateRow
    {
        public string PluginId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PackageFingerprint { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long IsEnabled { get; set; }
        public string ValidationSummary { get; set; } = string.Empty;
        public string RuntimePath { get; set; } = string.Empty;
        public string StatusSummary { get; set; } = string.Empty;
        public string InstalledAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public long IsPendingUninstall { get; set; }
        public string DataRetentionChoice { get; set; } = "keep";
        public long RestartRequired { get; set; }
        public string? LastActivatedAtUtc { get; set; }

        public InstalledPluginState ToState()
        {
            return new InstalledPluginState(
                PluginId,
                Version,
                PackageFingerprint,
                DisplayName,
                Description,
                IsEnabled != 0,
                ValidationSummary,
                RuntimePath,
                StatusSummary,
                DateTimeOffset.Parse(InstalledAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTimeOffset.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                IsPendingUninstall != 0,
                DataRetentionChoice,
                RestartRequired != 0,
                string.IsNullOrWhiteSpace(LastActivatedAtUtc)
                    ? null
                    : DateTimeOffset.Parse(LastActivatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
        }
    }

    private static object ToParameters(InstalledPluginState state)
    {
        return new
        {
            state.PluginId,
            state.Version,
            state.PackageFingerprint,
            state.DisplayName,
            state.Description,
            IsEnabled = state.IsEnabled ? 1 : 0,
            state.ValidationSummary,
            state.RuntimePath,
            state.StatusSummary,
            InstalledAtUtc = state.InstalledAtUtc.ToString("O"),
            UpdatedAtUtc = state.UpdatedAtUtc.ToString("O"),
            IsPendingUninstall = state.IsPendingUninstall ? 1 : 0,
            state.DataRetentionChoice,
            RestartRequired = state.RestartRequired ? 1 : 0,
            LastActivatedAtUtc = state.LastActivatedAtUtc?.ToString("O")
        };
    }
}
