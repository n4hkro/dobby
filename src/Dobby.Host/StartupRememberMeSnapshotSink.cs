using Dobby.Security.Authentication;

namespace Dobby.Host;

internal sealed class StartupRememberMeSnapshotSink : IRememberMeStartupSnapshotSink
{
    public static StartupRememberMeSnapshotSink Instance { get; } = new();

    private StartupRememberMeSnapshotSink()
    {
    }

    public void Save(RememberMeSavedLogin savedLogin)
    {
        StartupRememberMeSnapshotStore.Save(savedLogin);
    }

    public void Clear()
    {
        StartupRememberMeSnapshotStore.Clear();
    }
}
