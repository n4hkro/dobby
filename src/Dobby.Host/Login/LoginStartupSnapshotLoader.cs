namespace Dobby.Host.Login;

internal static class LoginStartupSnapshotLoader
{
    public static LoginStartupSnapshot Load()
    {
        StartupRememberMeLegacyCleaner.Clear();
        return new LoginStartupSnapshot(StartupThemeSnapshotStore.LoadTheme());
    }
}
