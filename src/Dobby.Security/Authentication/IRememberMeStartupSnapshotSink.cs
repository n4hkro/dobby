namespace Dobby.Security.Authentication;

public interface IRememberMeStartupSnapshotSink
{
    void Save(RememberMeSavedLogin savedLogin);

    void Clear();
}

internal sealed class NullRememberMeStartupSnapshotSink : IRememberMeStartupSnapshotSink
{
    public static NullRememberMeStartupSnapshotSink Instance { get; } = new();

    private NullRememberMeStartupSnapshotSink()
    {
    }

    public void Save(RememberMeSavedLogin savedLogin)
    {
    }

    public void Clear()
    {
    }
}
