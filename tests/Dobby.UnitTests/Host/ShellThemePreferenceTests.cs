using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host;
using Dobby.Host.Settings;
using Shouldly;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace Dobby.UnitTests.Host;

public sealed class ShellThemePreferenceTests
{
    [Fact]
    public void Top_toolbar_theme_coordinator_writes_same_preference_consumed_by_settings_appearance()
    {
        var source = new MemoryPreferenceSource();
        var coordinator = new ShellThemeCoordinator(source);
        var appearance = new AppearanceSettingsModel(source);

        coordinator.SetTheme(AppThemePreference.Dark).IsSuccess.ShouldBeTrue();

        appearance.CurrentTheme.ShouldBe(AppThemePreference.Dark);
        coordinator.CurrentTheme.ShouldBe(AppThemePreference.Dark);
    }

    [Fact]
    public void Accessibility_preferences_can_override_high_contrast_and_reduced_motion_resources()
    {
        var method = typeof(ShellThemeCoordinator).GetMethod(
            "ApplyAccessibilityPreferences",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(ResourceDictionary), typeof(bool), typeof(bool)],
            modifiers: null);
        method.ShouldNotBeNull();
        var resources = new ResourceDictionary
        {
            ["Dobby.Motion.Fast"] = new Duration(TimeSpan.FromMilliseconds(120)),
            ["Dobby.Motion.Reduced"] = new Duration(TimeSpan.Zero)
        };

        method!.Invoke(null, [resources, true, false]);

        resources["Dobby.Motion.Fast"].ShouldBe(new Duration(TimeSpan.Zero));
        resources["Dobby.Brush.Background"].ShouldBeAssignableTo<Brush>();
        resources["Dobby.Brush.Text"].ShouldBeAssignableTo<Brush>();
        resources["Dobby.Brush.Focus"].ShouldBeAssignableTo<Brush>();
    }

    private sealed class MemoryPreferenceSource : ISettingsPreferenceSource
    {
        private SettingsPreferences preferences = SettingsPreferences.Default;

        public Result<SettingsPreferences> Load()
        {
            return Result.Success(preferences);
        }

        public Result Save(SettingsPreferences preferences)
        {
            this.preferences = preferences;
            return Result.Success();
        }
    }
}
