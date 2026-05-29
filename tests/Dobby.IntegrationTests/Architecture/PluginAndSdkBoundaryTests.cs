using System.IO;
using System.Xml.Linq;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginAndSdkBoundaryTests
{
    [Fact]
    public void Contracts_project_references_only_core()
    {
        ProjectReferences("src", "Dobby.Contracts", "Dobby.Contracts.csproj")
            .ShouldBe(["Dobby.Core"]);
    }

    [Fact]
    public void Sdk_project_references_stay_within_documented_boundary()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Dobby.Core",
            "Dobby.Contracts",
            "Dobby.Localization"
        };

        var violations = ProjectReferences("src", "Dobby.SDK", "Dobby.SDK.csproj")
            .Where(reference => !allowed.Contains(reference))
            .ToArray();

        violations.ShouldBeEmpty("ADR-0021 allows SDK references only to Core, Contracts, and Localization in Sprint 1.");
    }

    [Fact]
    public void PluginFramework_project_references_stay_within_architecture_map_boundary()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Dobby.Core",
            "Dobby.Contracts",
            "Dobby.Security",
            "Dobby.Licensing",
            "Dobby.Configuration",
            "Dobby.Messaging"
        };

        var violations = ProjectReferences("src", "Dobby.PluginFramework", "Dobby.PluginFramework.csproj")
            .Where(reference => !allowed.Contains(reference))
            .ToArray();

        violations.ShouldBeEmpty("docs/02_ARCHITECTURE_MAP.md Cədvəl 2 defines PluginFramework's allowed project references.");
    }

    [Fact]
    public void Sample_plugin_references_only_contracts_and_sdk()
    {
        ProjectReferences("samples", "Dobby.Sample.Plugin", "Dobby.Sample.Plugin.csproj")
            .ShouldBe(["Dobby.Contracts", "Dobby.SDK"]);
    }

    private static string[] ProjectReferences(params string[] pathParts)
    {
        var path = Path.Combine(new[] { RepoRoot().FullName }.Concat(pathParts).ToArray());
        var xml = XDocument.Load(path);

        return xml
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include))
            .Order(StringComparer.Ordinal)
            .ToArray()!;
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
