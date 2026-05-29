using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PublicTypeLayoutTests
{
    [Fact]
    public void Sprint3_touched_files_keep_one_public_top_level_type_per_file()
    {
        var root = RepoRoot();
        var files = Sprint3TouchedSourceFiles(root);
        var violations = files
            .Select(file => new
            {
                File = Path.GetRelativePath(root.FullName, file.FullName),
                Declarations = File.ReadLines(file.FullName)
                    .Where(IsTopLevelPublicTypeDeclaration)
                    .ToArray()
            })
            .Where(item => item.Declarations.Length > 1)
            .Select(item => $"{item.File}: {item.Declarations.Length} public top-level types")
            .ToArray();

        violations.ShouldBeEmpty();
    }

    private static IEnumerable<FileInfo> Sprint3TouchedSourceFiles(DirectoryInfo root)
    {
        var relativePaths = new[]
        {
            Path.Combine("src", "Dobby.Host", "HostConfigurationSources.cs"),
            Path.Combine("src", "Dobby.Host", "LocalDiagnosticsConfigurator.cs"),
            Path.Combine("src", "Dobby.Host", "RuntimeFolderLayout.cs"),
            Path.Combine("src", "Dobby.Host", "RuntimeFolderPreparer.cs"),
            Path.Combine("src", "Dobby.Host", "Startup", "HostBootstrapper.cs"),
            Path.Combine("src", "Dobby.Host", "Startup", "StartupPlan.cs")
        };

        foreach (var relativePath in relativePaths)
        {
            yield return new FileInfo(Path.Combine(root.FullName, relativePath));
        }

        foreach (var directory in new[]
        {
            Path.Combine(root.FullName, "src", "Dobby.Configuration"),
            Path.Combine(root.FullName, "src", "Dobby.Audit"),
            Path.Combine(root.FullName, "src", "Dobby.Diagnostics")
        })
        {
            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.cs", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static bool IsTopLevelPublicTypeDeclaration(string line)
    {
        if (!line.StartsWith("public ", StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(token => token is "class" or "record" or "interface" or "enum");
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
