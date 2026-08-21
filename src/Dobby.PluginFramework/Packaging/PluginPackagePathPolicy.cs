using System.Text;

namespace Dobby.PluginFramework.Packaging;

public static class PluginPackagePathPolicy
{
    private static readonly HashSet<string> ReservedWindowsBasenames = new(
        [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 512 ||
            value.Contains('\\') ||
            value.Contains(':') ||
            value.StartsWith('/') ||
            value.EndsWith('/') ||
            Path.IsPathRooted(value) ||
            value.Any(char.IsControl))
        {
            return false;
        }

        normalized = value.Normalize(NormalizationForm.FormC);
        var segments = normalized.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 || segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.EndsWith('.') ||
                segment.EndsWith(' ') ||
                ReservedWindowsBasenames.Contains(segment.Split('.', 2)[0])))
        {
            normalized = string.Empty;
            return false;
        }

        return true;
    }

    public static bool HasCaseOrUnicodeCollision(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!TryNormalize(path, out var normalized) || !seen.Add(normalized))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasFileDirectoryPrefixCollision(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var normalized = paths
            .Select(path => TryNormalize(path, out var result) ? result : string.Empty)
            .Where(path => path.Length > 0)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        for (var index = 0; index < normalized.Length - 1; index++)
        {
            if (normalized[index + 1].StartsWith(
                    normalized[index] + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
