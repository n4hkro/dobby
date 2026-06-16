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
                        StatusSummary, InstalledAtUtc, UpdatedAtUtc
                    ) VALUES (
                        @PluginId, @DisplayName, @Version, @Description, @PackageFingerprint, @IsEnabled,
                        @StatusSummary, @InstalledAtUtc, @UpdatedAtUtc
                    )
                    ON CONFLICT(PluginId) DO UPDATE SET
                        DisplayName = excluded.DisplayName,
                        Version = excluded.Version,
                        Description = excluded.Description,
                        PackageFingerprint = excluded.PackageFingerprint,
                        IsEnabled = excluded.IsEnabled,
                        StatusSummary = excluded.StatusSummary,
                        UpdatedAtUtc = excluded.UpdatedAtUtc;
                    """,
                    metadata);
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
                StatusSummary, InstalledAtUtc, UpdatedAtUtc
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
                StatusSummary, InstalledAtUtc, UpdatedAtUtc
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
                tx.Execute("DELETE FROM InstalledPluginMetadataCache;");
                foreach (var state in installedStates)
                {
                    tx.Execute(
                        """
                        INSERT INTO InstalledPluginMetadataCache (
                            PluginId, DisplayName, Version, Description, PackageFingerprint, IsEnabled,
                            StatusSummary, InstalledAtUtc, UpdatedAtUtc
                        ) VALUES (
                            @PluginId, @DisplayName, @Version, @Description, @PackageFingerprint, @IsEnabled,
                            @StatusSummary, @InstalledAtUtc, @UpdatedAtUtc
                        );
                        """,
                        new InstalledPluginMetadata(
                            state.PluginId,
                            state.DisplayName,
                            state.Version,
                            state.Description,
                            state.PackageFingerprint,
                            state.IsEnabled,
                            state.StatusSummary,
                            state.InstalledAtUtc,
                            state.UpdatedAtUtc));
                }
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
                DateTimeOffset.Parse(UpdatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind));
        }
    }
}
