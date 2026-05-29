using Dobby.Configuration;
using Dobby.Core;

namespace Dobby.Host.Settings;

public sealed class AppearanceSettingsModel(ISettingsPreferenceSource preferenceSource)
{
    public AppThemePreference CurrentTheme => preferenceSource.Load().Value.Appearance.Theme;

    public Result SetTheme(AppThemePreference theme)
    {
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
