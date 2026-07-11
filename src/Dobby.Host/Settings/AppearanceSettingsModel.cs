using Dobby.Configuration;
using Dobby.Core;
using Dobby.Localization;

namespace Dobby.Host.Settings;

public sealed class AppearanceSettingsModel(ISettingsPreferenceSource preferenceSource)
{
    public AppThemePreference CurrentTheme => preferenceSource.Load().Value.Appearance.Theme;

    public AppearanceSettingsSection BuildSection()
    {
        return new AppearanceSettingsSection(
            HostResourceKeys.SettingsAppearanceTheme,
            [
                HostResourceKeys.SettingsAppearanceThemeLight,
                HostResourceKeys.SettingsAppearanceThemeDark
            ],
            CurrentTheme);
    }

    public Result SetTheme(AppThemePreference theme)
    {
        if (preferenceSource is IThemePreferenceSource themePreferenceSource)
        {
            return themePreferenceSource.SaveThemePreference(theme);
        }

        var preferences = preferenceSource.Load();
        if (!preferences.IsSuccess)
        {
            return Result.Failure(preferences.ErrorCode!.Value);
        }

        return preferenceSource.Save(preferences.Value with
        {
            Appearance = new AppearancePreference(theme)
        });
    }
}
