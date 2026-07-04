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

    public static void ApplyTheme(WpfApplication application, AppThemePreference theme)
    {
        ArgumentNullException.ThrowIfNull(application);

        ApplyTheme(application.Resources, theme);
        ApplyAccessibilityPreferences(application);
    }

    public static void ApplyTheme(ResourceDictionary resources, AppThemePreference theme)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var source = theme == AppThemePreference.Dark
            ? new Uri("pack://application:,,,/Dobby.UI;component/Themes/DarkTheme.xaml", UriKind.Absolute)
            : new Uri("pack://application:,,,/Dobby.UI;component/Themes/LightTheme.xaml", UriKind.Absolute);
        var dictionaries = resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source is not null &&
            IsAnyThemeSource(dictionary.Source.OriginalString));
        if (existing?.Source is not null &&
            IsThemeSource(existing.Source.OriginalString, theme))
        {
            return;
        }

        if (existing is not null)
        {
            dictionaries.Remove(existing);
        }

        dictionaries.Add(new ResourceDictionary { Source = source });
    }

    private static bool IsThemeSource(string source, AppThemePreference theme)
    {
        var expectedFileName = theme == AppThemePreference.Dark
            ? "DarkTheme.xaml"
            : "LightTheme.xaml";
        return source.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnyThemeSource(string source)
    {
        return IsThemeSource(source, AppThemePreference.Light) ||
            IsThemeSource(source, AppThemePreference.Dark);
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
