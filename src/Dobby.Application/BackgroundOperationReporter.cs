using Dobby.Contracts.BackgroundOperations;

namespace Dobby.Application;

public sealed class BackgroundOperationReporter : IBackgroundOperationReporter
{
    private readonly ReportingProgress progress;

    public BackgroundOperationReporter()
    {
        progress = new ReportingProgress(this);
    }

    public event Action<BackgroundOperationSnapshot>? SnapshotReported;

    public BackgroundOperationSnapshot? LatestSnapshot { get; private set; }

    public IProgress<BackgroundOperationSnapshot> Progress => progress;

    private void Report(BackgroundOperationSnapshot snapshot)
    {
        LatestSnapshot = snapshot;
        SnapshotReported?.Invoke(snapshot);
    }

    private sealed class ReportingProgress(BackgroundOperationReporter owner)
        : IProgress<BackgroundOperationSnapshot>
    {
        public void Report(BackgroundOperationSnapshot value)
        {
            owner.Report(value);
        }
    }
}
