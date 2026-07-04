namespace Dobby.Host.Login;

internal static class LoginStartupSnapshotLoader
{
    public static LoginStartupSnapshot Load()
    {
        return new LoginStartupSnapshot(
            StartupThemeSnapshotStore.LoadTheme(),
            StartupRememberMeSnapshotStore.LoadSavedLoginPrefill());
    }
}
