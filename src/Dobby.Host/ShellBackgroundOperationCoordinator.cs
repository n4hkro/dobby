using Dobby.Application;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Messaging.UiDispatch;
using Dobby.Shell;

namespace Dobby.Host;

public sealed class ShellBackgroundOperationCoordinator(
    ShellViewModel shellViewModel,
    IBackgroundOperationReporter reporter,
    IUiDispatcher dispatcher) : IDisposable
{
    private BackgroundOperationReporter? observableReporter;
    private int attached;
    private int disposed;

    public void Attach()
    {
        ArgumentNullException.ThrowIfNull(shellViewModel);
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(dispatcher);

        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (Interlocked.CompareExchange(ref attached, 1, 0) != 0 ||
            reporter is not BackgroundOperationReporter concreteReporter)
        {
            return;
        }
        observableReporter = concreteReporter;

        if (concreteReporter.LatestSnapshot is not null)
        {
            Post(concreteReporter.LatestSnapshot);
        }

        concreteReporter.SnapshotReported += Post;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (observableReporter is not null)
        {
            observableReporter.SnapshotReported -= Post;
            observableReporter = null;
        }
    }

    private void Post(BackgroundOperationSnapshot snapshot)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return;
        }

        dispatcher.Post(() =>
        {
            if (Volatile.Read(ref disposed) == 0)
            {
                shellViewModel.ShowBackgroundOperation(snapshot);
            }
        });
    }
}
