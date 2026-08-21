using System.IO;

namespace Dobby.Host;

public static class StartupRememberMeLegacyCleaner
{
    private const string LegacyPasswordFileName = "startup-remember-me.json";
    private const string ObsoleteUsernameFileName = "startup-remembered-account.json";

    public static void Clear()
    {
        var directory = ResolveSnapshotDirectory();
        Clear(
            Path.Combine(directory, LegacyPasswordFileName),
            Path.Combine(directory, ObsoleteUsernameFileName));
    }

    public static void Clear(string legacyPasswordPath, string obsoleteUsernamePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyPasswordPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(obsoleteUsernamePath);

        DeleteKnownFile(legacyPasswordPath);
        DeleteKnownFile(obsoleteUsernamePath);
    }

    private static string ResolveSnapshotDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;
        return Path.Combine(root, "Dobby");
    }

    private static void DeleteKnownFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup is best-effort. The canonical preference lives in the encrypted database.
        }
    }
}
