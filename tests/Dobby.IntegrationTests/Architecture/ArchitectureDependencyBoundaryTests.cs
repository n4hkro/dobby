using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dobby.Core;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ArchitectureDependencyBoundaryTests
{
    [Fact]
    public void Source_project_references_are_subset_of_architecture_map()
    {
        var root = RepoRoot();
        var allowedReferences = LoadArchitectureMapReferences(root);
        var violations = new List<string>();

        foreach (var project in SourceProjects(root))
        {
            var projectName = Path.GetFileNameWithoutExtension(project.Name);
            if (!allowedReferences.TryGetValue(projectName, out var allowed))
            {
                violations.Add($"{project.FullName}: {projectName} is missing from docs/02_ARCHITECTURE_MAP.md Cədvəl 2.");
                continue;
            }

            foreach (var reference in ProjectReferences(project))
            {
                if (allowed.Contains(reference))
                {
                    continue;
                }

                violations.Add(
                    $"{project.FullName}: {projectName} references {reference}, which is not allowed by docs/02_ARCHITECTURE_MAP.md Cədvəl 2 row for {projectName}.");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Infrastructure_no_longer_references_application_for_print_boundary()
    {
        var root = RepoRoot();
        var infrastructureProject = new FileInfo(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Infrastructure",
            "Dobby.Infrastructure.csproj"));

        ProjectReferences(infrastructureProject).ShouldNotContain("Dobby.Application");
    }

    [Fact]
    public void Deferred_branch_shared_runtime_projects_are_absent()
    {
        var root = RepoRoot();

        Directory.Exists(Path.Combine(root.FullName, "src", "Dobby.BranchSetup")).ShouldBeFalse();
        Directory.Exists(Path.Combine(root.FullName, "src", "Dobby.Infrastructure.Postgres")).ShouldBeFalse();
        Directory.Exists(Path.Combine(root.FullName, "src", "Dobby.Update")).ShouldBeFalse();
    }

    [Fact]
    public void ErrorCode_registry_matches_documented_baseline_exactly()
    {
        var declared = typeof(ErrorCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ErrorCode))
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] baseline =
        [
            "AlreadyExists",
            "Cancelled",
            "DatabaseUnavailable",
            "Forbidden",
            "InvalidInput",
            "IoFailed",
            "LicenseBindingMismatch",
            "LicenseDenied",
            "LicensePending",
            "LicenseReadOnly",
            "NetworkUnavailable",
            "NotFound",
            "PluginInternalError",
            "ResourceBusy",
            "SerializationFailed",
            "Timeout",
            "Unauthorized",
            "UnsupportedOperation",
            "ValidationFailed"
        ];

        declared.ShouldBe(baseline, "docs/04_RESULT_VS_EXCEPTION_POLICY.md defines the Dobby.Core/ErrorCodes baseline registry.");
    }

    [Fact]
    public void Plugin_lifecycle_production_paths_do_not_use_uncancellable_none_token()
    {
        var root = RepoRoot().FullName;
        string[] lifecycleFiles =
        [
            Path.Combine(root, "src", "Dobby.Contracts", "PluginFramework", "PluginContracts.cs"),
            Path.Combine(root, "src", "Dobby.PluginFramework", "Activation", "PluginHostServices.cs"),
            Path.Combine(root, "src", "Dobby.PluginFramework", "Activation", "PluginServiceRegistrationBridge.cs"),
            Path.Combine(root, "src", "Dobby.PluginFramework", "Activation", "PostLoginPluginActivator.cs"),
            Path.Combine(root, "src", "Dobby.Host", "Plugins", "HostPluginRuntimeCoordinator.cs")
        ];

        lifecycleFiles
            .Where(path => File.ReadAllText(path).Contains("CancellationToken.None", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Fact]
    public void Audit_and_security_do_not_receive_application_through_infrastructure_print_boundary()
    {
        var root = RepoRoot();
        var violations = new List<string>();

        foreach (var projectName in new[] { "Dobby.Audit", "Dobby.Security" })
        {
            var closure = TransitiveProjectReferences(root, projectName);
            if (closure.Contains("Dobby.Application"))
            {
                violations.Add($"{projectName} receives Dobby.Application through a transitive project reference.");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Audit_and_security_build_output_do_not_copy_application_assembly()
    {
        var root = RepoRoot();
        var violations = new List<string>();

        foreach (var projectName in new[] { "Dobby.Audit", "Dobby.Security" })
        {
            var projectDirectory = Path.Combine(root.FullName, "src", projectName);
            if (!Directory.Exists(projectDirectory))
            {
                continue;
            }

            foreach (var assembly in Directory.EnumerateFiles(projectDirectory, "Dobby.Application.dll", SearchOption.AllDirectories)
                         .Where(path => IsBuildOutputPath(path)))
            {
                violations.Add($"{projectName} build output contains forbidden transitive application assembly: {assembly}");
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Sprint5_security_edges_are_present_without_direct_audit_edge()
    {
        var root = RepoRoot();
        var securityProject = new FileInfo(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Security",
            "Dobby.Security.csproj"));

        var references = ProjectReferences(securityProject);

        references.ShouldContain("Dobby.Infrastructure");
        references.ShouldContain("Dobby.Configuration");
        references.ShouldNotContain("Dobby.Audit");
    }

    [Fact]
    public void Dobby_owned_namespaces_do_not_use_dumping_ground_segments()
    {
        var root = RepoRoot();
        var bannedSegments = new HashSet<string>(StringComparer.Ordinal)
        {
            "Common",
            "Utils",
            "Helpers",
            "Extensions"
        };
        var namespacePattern = new Regex(@"^\s*namespace\s+(Dobby(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*(?:;|\{)", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in CodeFiles(root))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file.FullName))
            {
                lineNumber++;
                var match = namespacePattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var namespaceName = match.Groups[1].Value;
                var banned = namespaceName
                    .Split('.')
                    .Where(segment => bannedSegments.Contains(segment))
                    .ToArray();

                if (banned.Length > 0)
                {
                    violations.Add($"{file.FullName}:{lineNumber}: namespace {namespaceName} uses banned Dobby-owned segment(s): {string.Join(", ", banned)}.");
                }
            }
        }

        violations.ShouldBeEmpty("docs/08_CODING_STANDARDS.md bans dumping-ground namespace segments for Dobby-owned namespace declarations.");
    }

    private static Dictionary<string, HashSet<string>> LoadArchitectureMapReferences(DirectoryInfo root)
    {
        var table = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var path = Path.Combine(root.FullName, "docs", "02_ARCHITECTURE_MAP.md");
        var inTable = false;

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("## Cədvəl 2", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (!inTable)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("---", StringComparison.Ordinal))
            {
                if (table.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (!line.StartsWith("|", StringComparison.Ordinal) || line.Contains("|---", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = line
                .Split('|', StringSplitOptions.TrimEntries)
                .Where(column => column.Length > 0)
                .ToArray();

            if (columns.Length < 2 || columns[0] == "Ad")
            {
                continue;
            }

            table[columns[0]] = ExtractProjectNames(columns[1]);
        }

        return table;
    }

    private static HashSet<string> ExtractProjectNames(string value)
    {
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return Regex
            .Matches(value, @"\bDobby\.[A-Za-z0-9.]+\b")
            .Select(match => match.Value.TrimEnd('.'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<FileInfo> SourceProjects(DirectoryInfo root)
    {
        return new DirectoryInfo(Path.Combine(root.FullName, "src"))
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedPath(file.FullName));
    }

    private static IEnumerable<FileInfo> CodeFiles(DirectoryInfo root)
    {
        return new[]
            {
                Path.Combine(root.FullName, "src"),
                Path.Combine(root.FullName, "tests"),
                Path.Combine(root.FullName, "samples")
            }
            .Where(Directory.Exists)
            .SelectMany(path => new DirectoryInfo(path).EnumerateFiles("*.cs", SearchOption.AllDirectories))
            .Where(file => !IsGeneratedPath(file.FullName));
    }

    private static string[] ProjectReferences(FileInfo project)
    {
        var xml = XDocument.Load(project.FullName);

        return xml
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include))
            .Order(StringComparer.Ordinal)
            .ToArray()!;
    }

    private static HashSet<string> TransitiveProjectReferences(DirectoryInfo root, string projectName)
    {
        var projects = SourceProjects(root).ToDictionary(
            project => Path.GetFileNameWithoutExtension(project.Name),
            StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(projectName);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!projects.TryGetValue(current, out var project))
            {
                continue;
            }

            foreach (var reference in ProjectReferencesWithMetadata(project))
            {
                if (!reference.PrivateAssetsAll && visited.Add(reference.ProjectName))
                {
                    stack.Push(reference.ProjectName);
                }
            }
        }

        visited.Remove(projectName);
        return visited;
    }

    private static IEnumerable<ProjectReferenceInfo> ProjectReferencesWithMetadata(FileInfo project)
    {
        var xml = XDocument.Load(project.FullName);
        foreach (var element in xml.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            var privateAssets = element.Attribute("PrivateAssets")?.Value
                ?? element.Elements().SingleOrDefault(child => child.Name.LocalName == "PrivateAssets")?.Value
                ?? string.Empty;

            yield return new ProjectReferenceInfo(
                Path.GetFileNameWithoutExtension(include),
                privateAssets.Equals("all", StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed record ProjectReferenceInfo(string ProjectName, bool PrivateAssetsAll);

    private static bool IsBuildOutputPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("TestResults", StringComparison.OrdinalIgnoreCase));
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
