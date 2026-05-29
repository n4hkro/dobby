using System.Diagnostics;
using Dobby.Audit;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Core;
using Dobby.Infrastructure.Persistence;

namespace Dobby.Host.Login;

internal static class AuditRetentionMaintenance
{
    public static void StartOneShot(
        IHostPersistenceCommandExecutor executor,
        int retentionDays,
        IBackgroundOperationReporter backgroundOperations)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(backgroundOperations);

        var operationId = Guid.CreateVersion7();
        var correlationId = Guid.NewGuid();
        Report(backgroundOperations, operationId, correlationId, BackgroundOperationStatus.Running, null);
        _ = Task.Factory.StartNew(
            () =>
            {
                var started = Stopwatch.GetTimestamp();
                try
                {
                    var deleted = new AuditRetentionSweeper(executor, retentionDays).Sweep();
                    Trace.TraceInformation(
                        "Audit retention sweep completed in {0} ms; rows deleted: {1}.",
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                        deleted);
                    Report(backgroundOperations, operationId, correlationId, BackgroundOperationStatus.Completed, null);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Audit retention sweep failed closed: {0}.", ex.GetType().Name);
                    Report(backgroundOperations, operationId, correlationId, BackgroundOperationStatus.Failed, ErrorCode.DatabaseUnavailable);
                }
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private static void Report(
        IBackgroundOperationReporter backgroundOperations,
        Guid operationId,
        Guid correlationId,
        BackgroundOperationStatus status,
        ErrorCode? failureReason)
    {
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Host,
            OwnerId: null,
            "Dobby.Host.AuditRetentionSweep",
            ShortDetailKey: null,
            status,
            BackgroundOperationProgress.Indeterminate(),
            CanCancel: false,
            correlationId,
            failureReason));
    }
}
