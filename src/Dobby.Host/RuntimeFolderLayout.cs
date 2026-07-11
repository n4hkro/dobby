using Dobby.Configuration;
using System.IO;

namespace Dobby.Host;

public static class RuntimeFolderLayout
{
    public static IReadOnlyList<string> RequiredFolderNames { get; } =
    [
        "data",
        "config",
        "logs",
        "plugins",
        "licenses",
        "recovery",
        "backup",
        "temp"
    ];

    public static IReadOnlyList<string> RequiredDirectories(RuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var dataRoot = Environment.ExpandEnvironmentVariables(options.DataRoot);
        return RequiredFolderNames.Select(folder => Path.Combine(dataRoot, folder)).ToArray();
    }

    public static string PluginInstallStaging(RuntimeOptions options)
    {
        return UnderDataRoot(options, "plugins", "staging");
    }

    public static string DiagnosticsLogs(RuntimeOptions options)
    {
        return UnderDataRoot(options, "logs");
    }

    public static string TemporaryFiles(RuntimeOptions options)
    {
        return UnderDataRoot(options, "temp");
    }

    private static string UnderDataRoot(RuntimeOptions options, params string[] segments)
    {
        ArgumentNullException.ThrowIfNull(options);
        var dataRoot = Environment.ExpandEnvironmentVariables(options.DataRoot);
        return Path.Combine([dataRoot, .. segments]);
    }
}
