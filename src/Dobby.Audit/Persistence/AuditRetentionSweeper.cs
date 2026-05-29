using Dobby.Infrastructure.Persistence;

namespace Dobby.Audit;

public sealed class AuditRetentionSweeper
{
    private readonly IHostPersistenceCommandExecutor executor;
    private readonly int retentionDays;

    public AuditRetentionSweeper(IHostPersistenceCommandExecutor executor, int retentionDays = 365)
    {
        if (retentionDays < 365)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), retentionDays, "Audit retention cannot be below 365 days.");
        }

        this.executor = executor;
        this.retentionDays = retentionDays;
    }

    public int Sweep()
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays).ToString("O");
        return executor.Execute(
            "DELETE FROM AuditLog WHERE OccurredAtUtc < @Cutoff;",
            new { Cutoff = cutoff });
    }
}
