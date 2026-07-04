using Dobby.Configuration;

namespace Dobby.Host.Login;

public sealed record LoginStartupSnapshot(
    AppThemePreference Theme,
    StartupRememberMePrefill? SavedLogin)
{
    public static LoginStartupSnapshot Default { get; } = new(AppThemePreference.Light, null);

    public LoginViewModel CreateLoginViewModel()
    {
        return SavedLogin is null
            ? new LoginViewModel(null)
            : LoginViewModel.CreateStartupPrefill(
                SavedLogin.MaskedUsername,
                () => StartupRememberMeSnapshotStore.LoadSavedLogin()?.Password);
    }
}
