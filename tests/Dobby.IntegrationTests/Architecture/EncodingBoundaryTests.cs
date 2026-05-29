using System.IO;
using System.Text;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class EncodingBoundaryTests
{
    [Fact]
    public void Repo_text_files_are_utf8_without_bom()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var violations = new List<string>();

        foreach (var file in InScopeTextFiles(RepoRoot()))
        {
            var bytes = File.ReadAllBytes(file.FullName);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                violations.Add($"{file.FullName}: UTF-8 BOM found; repo-owned text files must be UTF-8 without BOM.");
                continue;
            }

            try
            {
                _ = utf8.GetString(bytes);
            }
            catch (DecoderFallbackException exception)
            {
                violations.Add($"{file.FullName}: invalid UTF-8 byte sequence: {exception.Message}");
            }
        }

        violations.ShouldBeEmpty();
    }

    private static IEnumerable<FileInfo> InScopeTextFiles(DirectoryInfo root)
    {
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".csproj",
            ".props",
            ".targets",
            ".sln",
            ".json",
            ".md",
            ".xaml",
            ".resx"
        };
        var exactFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".editorconfig",
            ".gitignore"
        };
        var binaryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".ico",
            ".ttf",
            ".otf",
            ".woff",
            ".woff2",
            ".nupkg",
            ".dobbyplugin",
            ".dobbylicense",
            ".dobbyrecovery"
        };

        return root
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => !IsExcludedPath(file.FullName))
            .Where(file => !binaryExtensions.Contains(file.Extension))
            .Where(file => allowedExtensions.Contains(file.Extension) || exactFileNames.Contains(file.Name));
    }

    private static bool IsExcludedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("TestResults", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("publish", StringComparison.OrdinalIgnoreCase));
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
