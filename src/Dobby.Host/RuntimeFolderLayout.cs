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
}
