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
                        ValidationSummary, RuntimePath, StatusSummary, InstalledAtUtc, UpdatedAtUtc
                    ) VALUES (
                        @PluginId, @Version, @PackageFingerprint, @DisplayName, @Description, @IsEnabled,
                        @ValidationSummary, @RuntimePath, @StatusSummary, @InstalledAtUtc, @UpdatedAtUtc
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
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """,
                    state);
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
                ValidationSummary, RuntimePath, StatusSummary, InstalledAtUtc, UpdatedAtUtc
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
                ValidationSummary, RuntimePath, StatusSummary, InstalledAtUtc, UpdatedAtUtc
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
                DateTimeOffset.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
        }
    }
}
