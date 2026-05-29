using Dobby.Configuration;
using Dobby.Core;
using Dobby.UI;
using System.Windows;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;

namespace Dobby.Host;

public sealed class ShellThemeCoordinator(ISettingsPreferenceSource preferenceSource)
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

    public static void ApplyTheme(WpfApplication application, AppThemePreference theme)
    {
        ArgumentNullException.ThrowIfNull(application);

        var source = theme == AppThemePreference.Dark
            ? new Uri("pack://application:,,,/Dobby.UI;component/Themes/DarkTheme.xaml", UriKind.Absolute)
            : new Uri("pack://application:,,,/Dobby.UI;component/Themes/LightTheme.xaml", UriKind.Absolute);
        var dictionaries = application.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("/Dobby.UI;component/Themes/LightTheme.xaml", StringComparison.OrdinalIgnoreCase) == true ||
            dictionary.Source?.OriginalString.Contains("/Dobby.UI;component/Themes/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) == true);
        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        dictionaries.Add(new System.Windows.ResourceDictionary { Source = source });
        ApplyAccessibilityPreferences(application);
    }

    public static void ApplyAccessibilityPreferences(WpfApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        ApplyAccessibilityPreferences(
            application.Resources,
            SystemParameters.HighContrast,
            SystemParameters.ClientAreaAnimation);
    }

    public static void ApplyAccessibilityPreferences(
        ResourceDictionary resources,
        bool highContrast,
        bool clientAreaAnimation)
    {
        ArgumentNullException.ThrowIfNull(resources);

        if (!clientAreaAnimation)
        {
            resources[ThemeTokens.MotionFastDuration] = resources.Contains(ThemeTokens.ReducedMotionDuration)
                ? resources[ThemeTokens.ReducedMotionDuration]
                : new Duration(TimeSpan.Zero);
        }

        if (!highContrast)
        {
            return;
        }

        resources[ThemeTokens.BackgroundBrush] = SystemColors.WindowBrush;
        resources[ThemeTokens.SurfaceBrush] = SystemColors.ControlBrush;
        resources[ThemeTokens.AccentBrush] = SystemColors.HighlightBrush;
        resources[ThemeTokens.TextBrush] = SystemColors.WindowTextBrush;
        resources[ThemeTokens.FocusBrush] = SystemColors.HighlightBrush;
    }
}
