using Dobby.Core;
using Dobby.Infrastructure.Persistence;
using Dobby.PluginFramework.Packaging;

namespace Dobby.Host.Plugins;

public sealed class HostInstalledPluginMetadataCache(
    IHostPersistenceCommandExecutor executor,
    IHostPersistenceTransactionScope transactionScope) : IInstalledPluginMetadataCache
{
    public Result Upsert(InstalledPluginMetadata metadata)
    {
        try
        {
            transactionScope.ExecuteInTransaction(tx =>
            {
                tx.Execute(
                    """
                    INSERT INTO InstalledPluginMetadataCache (
                        PluginId, DisplayName, Version, Description, PackageFingerprint, IsEnabled,
                        StatusSummary, InstalledAtUtc, UpdatedAtUtc, IsPendingUninstall, RestartRequired
                    ) VALUES (
                        @PluginId, @DisplayName, @Version, @Description, @PackageFingerprint, @IsEnabled,
                        @StatusSummary, @InstalledAtUtc, @UpdatedAtUtc, @IsPendingUninstall, @RestartRequired
                    )
                    ON CONFLICT(PluginId) DO UPDATE SET
                        DisplayName = excluded.DisplayName,
                        Version = excluded.Version,
                        Description = excluded.Description,
                        PackageFingerprint = excluded.PackageFingerprint,
                        IsEnabled = excluded.IsEnabled,
                        StatusSummary = excluded.StatusSummary,
                        IsPendingUninstall = excluded.IsPendingUninstall,
                        RestartRequired = excluded.RestartRequired,
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """,
                    ToParameters(metadata));
            });

            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    public Result<InstalledPluginMetadata?> Get(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var row = executor.Query<InstalledPluginMetadataRow>(
            """
            SELECT
                PluginId, DisplayName, Version, Description, PackageFingerprint, IsEnabled,
                StatusSummary, InstalledAtUtc, UpdatedAtUtc, IsPendingUninstall, RestartRequired
            FROM InstalledPluginMetadataCache
            WHERE PluginId = @PluginId;
            """,
            new { PluginId = pluginId })
            .SingleOrDefault();

        return Result.Success<InstalledPluginMetadata?>(row is null ? null : row.ToMetadata());
    }

    public Result<IReadOnlyList<InstalledPluginMetadata>> Snapshot()
    {
        var rows = executor.Query<InstalledPluginMetadataRow>(
            """
            SELECT
                PluginId, DisplayName, Version, Description, PackageFingerprint, IsEnabled,
                StatusSummary, InstalledAtUtc, UpdatedAtUtc, IsPendingUninstall, RestartRequired
            FROM InstalledPluginMetadataCache
            ORDER BY PluginId;
            """);

        return Result.Success<IReadOnlyList<InstalledPluginMetadata>>(rows.Select(row => row.ToMetadata()).ToArray());
    }

    public Result RebuildFrom(IReadOnlyList<InstalledPluginState> installedStates)
    {
        try
        {
            transactionScope.ExecuteInTransaction(tx =>
            {
                RebuildFrom(tx, installedStates);
            });

            return Result.Success();
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
            executor.Execute("DELETE FROM InstalledPluginMetadataCache WHERE PluginId = @PluginId;", new { PluginId = pluginId });
            return Result.Success();
        }
        catch
        {
            return Result.Failure(ErrorCode.DatabaseUnavailable);
        }
    }

    private sealed class InstalledPluginMetadataRow
    {
        public string PluginId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PackageFingerprint { get; set; } = string.Empty;
        public long IsEnabled { get; set; }
        public string StatusSummary { get; set; } = string.Empty;
        public string InstalledAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public long IsPendingUninstall { get; set; }
        public long RestartRequired { get; set; }

        public InstalledPluginMetadata ToMetadata()
        {
            return new InstalledPluginMetadata(
                PluginId,
                DisplayName,
                Version,
                Description,
                PackageFingerprint,
                IsEnabled != 0,
                StatusSummary,
                DateTimeOffset.Parse(InstalledAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                DateTimeOffset.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                IsPendingUninstall != 0,
                RestartRequired != 0);
        }
    }

    private static object ToParameters(InstalledPluginMetadata metadata)
    {
        return new
        {
            metadata.PluginId,
            metadata.DisplayName,
            metadata.Version,
            metadata.Description,
            metadata.PackageFingerprint,
            IsEnabled = metadata.IsEnabled ? 1 : 0,
            metadata.StatusSummary,
            InstalledAtUtc = metadata.InstalledAtUtc.ToString("O"),
            UpdatedAtUtc = metadata.UpdatedAtUtc.ToString("O"),
            IsPendingUninstall = metadata.IsPendingUninstall ? 1 : 0,
            RestartRequired = metadata.RestartRequired ? 1 : 0
        };
    }

    internal void RebuildFrom(
        IHostPersistenceCommandExecutor transactionExecutor,
        IReadOnlyList<InstalledPluginState> installedStates)
    {
        ArgumentNullException.ThrowIfNull(transactionExecutor);
        ArgumentNullException.ThrowIfNull(installedStates);

        transactionExecutor.Execute("DELETE FROM InstalledPluginMetadataCache;");
        foreach (var state in installedStates)
        {
            transactionExecutor.Execute(
                """
                INSERT INTO InstalledPluginMetadataCache (
                    PluginId, DisplayName, Version, Description, PackageFingerprint, IsEnabled,
                    StatusSummary, InstalledAtUtc, UpdatedAtUtc, IsPendingUninstall, RestartRequired
                ) VALUES (
                    @PluginId, @DisplayName, @Version, @Description, @PackageFingerprint, @IsEnabled,
                    @StatusSummary, @InstalledAtUtc, @UpdatedAtUtc, @IsPendingUninstall, @RestartRequired
                );
                """,
                ToParameters(new InstalledPluginMetadata(
                    state.PluginId,
                    state.DisplayName,
                    state.Version,
                    state.Description,
                    state.PackageFingerprint,
                    state.IsEnabled,
                    state.StatusSummary,
                    state.InstalledAtUtc,
                    state.UpdatedAtUtc,
                    state.IsPendingUninstall,
                    state.RestartRequired)));
        }
    }
}
