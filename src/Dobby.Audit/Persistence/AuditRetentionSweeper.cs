using Dobby.Infrastructure.Persistence;

namespace Dobby.Audit;

public sealed class AuditRetentionSweeper
{
    private readonly IHostPersistenceCommandExecutor executor;
    private readonly int retentionDays;
    private readonly TimeProvider timeProvider;

    public AuditRetentionSweeper(
        IHostPersistenceCommandExecutor executor,
        int retentionDays = 365,
        TimeProvider? timeProvider = null)
    {
        if (retentionDays < 365)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), retentionDays, "Audit retention cannot be below 365 days.");
        }

        this.executor = executor;
        this.retentionDays = retentionDays;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int Sweep()
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-retentionDays).ToString("O");
        return executor.Execute(
            """
            DELETE FROM AuditLog
            WHERE OccurredAtUtc < @Cutoff
              AND NOT EXISTS (
                  SELECT 1
                  FROM HostOutbox
                  WHERE CompletedAtUtc IS NULL
                    AND (
                        EvidenceCorrelationId IS NULL OR
                        EvidenceCorrelationId = AuditLog.CorrelationId));
            """,
            new { Cutoff = cutoff });
    }
}
