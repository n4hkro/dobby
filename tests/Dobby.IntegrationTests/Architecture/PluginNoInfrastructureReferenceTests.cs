using System.IO;
using System.Xml.Linq;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginNoInfrastructureReferenceTests
{
    [Fact]
    public void Plugin_projects_do_not_reference_host_infrastructure()
    {
        var root = RepoRoot();
        var pluginProjects = PluginProjects(root);
        var violations = PluginInfrastructureViolations(pluginProjects);

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Plugin_boundary_scan_rejects_direct_persistence_stack_package_references()
    {
        using var temp = new TemporaryDirectory();
        var pluginProject = Path.Combine(temp.Path, "Plugin.csproj");
        File.WriteAllText(pluginProject, """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Dapper" />
                <PackageReference Include="Microsoft.Data.Sqlite.Core" />
                <PackageReference Include="SQLitePCLRaw.provider.dynamic_cdecl" />
              </ItemGroup>
            </Project>
            """);

        var violations = PluginInfrastructureViolations([new FileInfo(pluginProject)]);

        violations.Count.ShouldBe(3);
        violations.ShouldContain(violation => violation.Contains("Dapper", StringComparison.Ordinal));
        violations.ShouldContain(violation => violation.Contains("Microsoft.Data.Sqlite.Core", StringComparison.Ordinal));
        violations.ShouldContain(violation => violation.Contains("SQLitePCLRaw.provider.dynamic_cdecl", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> PluginInfrastructureViolations(IEnumerable<FileInfo> pluginProjects)
    {
        var blockedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dapper",
            "Microsoft.Data.Sqlite",
            "Microsoft.Data.Sqlite.Core"
        };
        var violations = new List<string>();

        foreach (var project in pluginProjects)
        {
            var xml = XDocument.Load(project.FullName);
            foreach (var reference in xml.Descendants().Where(element => element.Name.LocalName is "ProjectReference" or "PackageReference"))
            {
                var include = reference.Attribute("Include")?.Value ?? string.Empty;
                if (include.Contains("Dobby.Infrastructure", StringComparison.Ordinal))
                {
                    violations.Add($"{project.FullName}: plugin project references host Infrastructure through {include}.");
                }

                if (reference.Name.LocalName == "PackageReference" &&
                    (blockedPackageIds.Contains(include) || include.StartsWith("SQLitePCLRaw.", StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{project.FullName}: plugin project references host persistence package {include}.");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<FileInfo> PluginProjects(DirectoryInfo root)
    {
        foreach (var segment in new[] { "samples", "plugins" })
        {
            var directory = Path.Combine(root.FullName, segment);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var project in new DirectoryInfo(directory)
                         .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
                         .Where(file => !IsGeneratedPath(file.FullName)))
            {
                yield return project;
            }
        }
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dobby-plugin-boundary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
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
