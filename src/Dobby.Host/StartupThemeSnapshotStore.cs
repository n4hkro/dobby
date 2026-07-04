using Dobby.Configuration;
using System.IO;
using System.Text.Json;

namespace Dobby.Host;

public static class StartupThemeSnapshotStore
{
    private const string FileName = "startup-theme.json";

    public static string DefaultPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(AppContext.BaseDirectory, "Dobby")
                : Path.Combine(localAppData, "Dobby");
            return Path.Combine(root, FileName);
        }
    }

    public static AppThemePreference LoadTheme()
    {
        return LoadTheme(DefaultPath);
    }

    public static AppThemePreference LoadTheme(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return AppThemePreference.Light;
            }

            var snapshot = JsonSerializer.Deserialize(
                File.ReadAllBytes(path),
                HostStartupJsonSerializerContext.Default.StartupThemeSnapshot);
            return snapshot is not null &&
                Enum.TryParse<AppThemePreference>(snapshot.Theme, ignoreCase: false, out var theme)
                    ? theme
                    : AppThemePreference.Light;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return AppThemePreference.Light;
        }
    }

    public static void SaveTheme(AppThemePreference theme)
    {
        SaveTheme(DefaultPath, theme);
    }

    public static void SaveTheme(string path, AppThemePreference theme)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(
                path,
                JsonSerializer.SerializeToUtf8Bytes(
                    new StartupThemeSnapshot(theme.ToString()),
                    HostStartupJsonSerializerContext.Default.StartupThemeSnapshot));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
        }
    }
}
