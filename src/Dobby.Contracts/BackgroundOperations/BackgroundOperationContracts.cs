using Dobby.Core;

namespace Dobby.Contracts.BackgroundOperations;

public enum BackgroundOperationOwnerKind
{
    Host,
    Plugin
}

public enum BackgroundOperationStatus
{
    Queued,
    Running,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}

public sealed record BackgroundOperationProgress(
    int? Percent,
    bool IsIndeterminate)
{
    public static BackgroundOperationProgress Indeterminate() => new(null, true);

    public static BackgroundOperationProgress Determinate(int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "Progress percent must be between 0 and 100.");
        }

        return new BackgroundOperationProgress(percent, false);
    }
}

public sealed record BackgroundOperationSnapshot(
    Guid OperationId,
    BackgroundOperationOwnerKind OwnerKind,
    string? OwnerId,
    string DisplayNameKey,
    string? ShortDetailKey,
    BackgroundOperationStatus Status,
    BackgroundOperationProgress Progress,
    bool CanCancel,
    Guid CorrelationId,
    ErrorCode? FailureReason);

public interface IBackgroundOperationReporter
{
    IProgress<BackgroundOperationSnapshot> Progress { get; }
}

public interface IBackgroundOperationCancellation
{
    CancellationToken CancellationToken { get; }
}
