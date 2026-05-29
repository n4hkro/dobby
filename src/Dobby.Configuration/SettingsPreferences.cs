namespace Dobby.Configuration;

public sealed record SettingsPreferences(
    AppearancePreference Appearance,
    SecuritySessionPreference SecuritySession,
    ShellViewPreference ShellView)
{
    public static SettingsPreferences Default { get; } = new(
        new AppearancePreference(AppThemePreference.Light),
        new SecuritySessionPreference(AutoLockEnabled: false, AutoLockMinutes: 30),
        ShellViewPreference.Default);
}
