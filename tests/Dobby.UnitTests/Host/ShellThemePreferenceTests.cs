using Dobby.Configuration;
using Dobby.Core;
using Dobby.Host;
using Dobby.Host.Settings;
using Dobby.Infrastructure.Persistence;
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
    public void Shell_theme_flow_updates_startup_theme_snapshot_for_next_launch()
    {
        var root = RepoRoot();
        var mainWindowSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var presenterSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "PostLoginShellPresenter.cs"));

        mainWindowSource.ShouldContain("StartupThemeSnapshotStore.SaveTheme(nextTheme)");
        presenterSource.ShouldContain("StartupThemeSnapshotStore.SaveTheme(currentTheme)");
    }

    [Fact]
    public void Theme_toggle_updates_visual_state_before_queueing_preference_persistence()
    {
        var root = RepoRoot();
        var mainWindowSource = File.ReadAllText(Path.Combine(root.FullName, "src", "Dobby.Host", "MainWindow.xaml.cs"));
        var methodStart = mainWindowSource.IndexOf("private void ThemeToggle_Click", StringComparison.Ordinal);
        methodStart.ShouldBeGreaterThanOrEqualTo(0);
        var methodEnd = mainWindowSource.IndexOf("private void ApplyInitialShellPreference", methodStart, StringComparison.Ordinal);
        methodEnd.ShouldBeGreaterThan(methodStart);
        var method = mainWindowSource[methodStart..methodEnd];

        method.ShouldNotContain("themeCoordinator?.SetTheme(nextTheme)");
        method.ShouldNotContain("themeCoordinator.SetTheme(nextTheme)");
        method.ShouldContain("shellViewModel.SetTheme(nextTheme)");
        method.ShouldContain("ShellThemeCoordinator.ApplyTheme(System.Windows.Application.Current, nextTheme)");
        method.ShouldContain("StartupThemeSnapshotStore.SaveTheme(nextTheme)");
        method.ShouldContain("QueueThemePreferencePersist(nextTheme)");
        method.IndexOf("QueueThemePreferencePersist(nextTheme)", StringComparison.Ordinal)
            .ShouldBeGreaterThan(method.IndexOf("ShellThemeCoordinator.ApplyTheme", StringComparison.Ordinal));
    }

    [Fact]
    public void Top_toolbar_theme_persistence_writes_only_theme_key_without_full_settings_save()
    {
        var executor = new RecordingPreferenceExecutor();
        var source = new HostConfigurationSettingsPreferenceSource(
            executor,
            SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local"));
        var coordinator = new ShellThemeCoordinator(source);

        coordinator.SetTheme(AppThemePreference.Dark).IsSuccess.ShouldBeTrue();

        executor.QueryCount.ShouldBe(0);
        executor.ConfigKeys.Count.ShouldBe(1);
        executor.ConfigKeys.Single().ShouldEndWith(".appearance.theme");
    }

    [Fact]
    public void Settings_appearance_theme_persistence_writes_only_theme_key_without_full_settings_save()
    {
        var executor = new RecordingPreferenceExecutor();
        var source = new HostConfigurationSettingsPreferenceSource(
            executor,
            SettingsPreferenceScope.ForAuthenticatedUser("owner@example.local"));
        var appearance = new AppearanceSettingsModel(source);

        appearance.SetTheme(AppThemePreference.Dark).IsSuccess.ShouldBeTrue();

        executor.QueryCount.ShouldBe(0);
        executor.ConfigKeys.Count.ShouldBe(1);
        executor.ConfigKeys.Single().ShouldEndWith(".appearance.theme");
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

    [Fact]
    public void Apply_theme_keeps_existing_dictionary_when_requested_theme_is_already_active()
    {
        var method = typeof(ShellThemeCoordinator).GetMethod(
            "IsThemeSource",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        ((bool)method!.Invoke(null, ["pack://application:,,,/Dobby.UI;component/Themes/LightTheme.xaml", AppThemePreference.Light])!).ShouldBeTrue();
        ((bool)method.Invoke(null, ["/Dobby.UI;component/Themes/LightTheme.xaml", AppThemePreference.Light])!).ShouldBeTrue();
        ((bool)method.Invoke(null, ["pack://application:,,,/Dobby.UI;component/Themes/DarkTheme.xaml", AppThemePreference.Light])!).ShouldBeFalse();
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

    private sealed class RecordingPreferenceExecutor : IHostPersistenceCommandExecutor
    {
        public int QueryCount { get; private set; }

        public List<string> ConfigKeys { get; } = [];

        public int Execute(string sql, object? parameters = null)
        {
            ConfigKeys.Add(
                parameters?.GetType().GetProperty("ConfigKey")?.GetValue(parameters)?.ToString() ??
                string.Empty);
            return 1;
        }

        public IReadOnlyList<T> Query<T>(string sql, object? parameters = null)
        {
            QueryCount++;
            return [];
        }

        public T QueryScalar<T>(string sql, object? parameters = null)
        {
            QueryCount++;
            return default!;
        }
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
