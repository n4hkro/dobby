using Dobby.Application;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Messaging.UiDispatch;
using Dobby.Shell;

namespace Dobby.Host;

public sealed class ShellBackgroundOperationCoordinator(
    ShellViewModel shellViewModel,
    IBackgroundOperationReporter reporter,
    IUiDispatcher dispatcher)
{
    public void Attach()
    {
        ArgumentNullException.ThrowIfNull(shellViewModel);
        ArgumentNullException.ThrowIfNull(reporter);
        ArgumentNullException.ThrowIfNull(dispatcher);

        if (reporter is not BackgroundOperationReporter observableReporter)
        {
            return;
        }

        if (observableReporter.LatestSnapshot is not null)
        {
            Post(observableReporter.LatestSnapshot);
        }

        observableReporter.SnapshotReported += Post;
    }

    private void Post(BackgroundOperationSnapshot snapshot)
    {
        dispatcher.Post(() => shellViewModel.ShowBackgroundOperation(snapshot));
    }
}
