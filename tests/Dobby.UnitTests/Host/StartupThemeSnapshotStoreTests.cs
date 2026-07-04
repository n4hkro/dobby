using Dobby.Configuration;
using Shouldly;

namespace Dobby.UnitTests.Host;

public sealed class StartupThemeSnapshotStoreTests
{
    [Fact]
    public void Theme_snapshot_round_trips_last_theme_without_runtime_services()
    {
        using var temp = new TempFile();
        var store = Type.GetType("Dobby.Host.StartupThemeSnapshotStore, Dobby.Host");
        store.ShouldNotBeNull();
        var saveTheme = store.GetMethod("SaveTheme", [typeof(string), typeof(AppThemePreference)]);
        var loadTheme = store.GetMethod("LoadTheme", [typeof(string)]);
        saveTheme.ShouldNotBeNull();
        loadTheme.ShouldNotBeNull();

        saveTheme.Invoke(null, [temp.Path, AppThemePreference.Dark]);

        loadTheme.Invoke(null, [temp.Path]).ShouldBe(AppThemePreference.Dark);
    }

    [Fact]
    public void Missing_or_invalid_theme_snapshot_falls_back_to_light_theme()
    {
        using var temp = new TempFile();
        var store = Type.GetType("Dobby.Host.StartupThemeSnapshotStore, Dobby.Host");
        store.ShouldNotBeNull();
        var loadTheme = store.GetMethod("LoadTheme", [typeof(string)]);
        loadTheme.ShouldNotBeNull();

        loadTheme.Invoke(null, [temp.Path]).ShouldBe(AppThemePreference.Light);
        File.WriteAllText(temp.Path, "{ invalid");
        loadTheme.Invoke(null, [temp.Path]).ShouldBe(AppThemePreference.Light);
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"dobby-theme-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
