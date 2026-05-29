using System.IO;
using System.Xml.Linq;
using Shouldly;

namespace Dobby.UnitTests.Infrastructure;

public sealed class ProtectedMaterialUnitTestBoundaryTests
{
    [Fact]
    public void Protected_material_unit_tests_do_not_touch_real_dpapi_state()
    {
        var root = RepoRoot();
        var unitTestRoot = Path.Combine(root.FullName, "tests", "Dobby.UnitTests");
        var violations = new List<string>();

        foreach (var file in new DirectoryInfo(unitTestRoot).EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            if (file.Name == nameof(ProtectedMaterialUnitTestBoundaryTests) + ".cs" || IsGeneratedPath(file.FullName))
            {
                continue;
            }

            var text = File.ReadAllText(file.FullName);
            if (text.Contains("ProtectedMaterialAvailabilityProbe.CreateForDataRoot", StringComparison.Ordinal) ||
                text.Contains("new DpapiLocalMachineProtector", StringComparison.Ordinal) ||
                text.Contains("new DpapiCurrentUserProtector", StringComparison.Ordinal) ||
                text.Contains("ProtectedData.", StringComparison.Ordinal))
            {
                violations.Add(file.FullName);
            }
        }

        violations.ShouldBeEmpty("Sprint 4 unit tests must use injected fake protected-storage seams; real DPAPI proofs belong in integration tests.");
    }

    [Fact]
    public void Neutral_infrastructure_target_does_not_compile_concrete_dpapi_implementation_types()
    {
        var root = RepoRoot();
        var projectPath = Path.Combine(root.FullName, "src", "Dobby.Infrastructure", "Dobby.Infrastructure.csproj");
        var xml = XDocument.Load(projectPath);

        var exclusions = xml
            .Descendants()
            .Where(element => element.Name.LocalName == "Compile" && element.Attribute("Remove") is not null)
            .Select(element => new
            {
                Remove = element.Attribute("Remove")!.Value,
                Condition = element.Attribute("Condition")?.Value
                    ?? element.Parent?.Attribute("Condition")?.Value
                    ?? string.Empty
            })
            .ToArray();

        exclusions.ShouldContain(
            exclusion => exclusion.Remove == @"ProtectedStorage\DpapiLocalMachineProtector.cs" &&
                exclusion.Condition.Contains("'$(TargetFramework)' == 'net10.0'", StringComparison.Ordinal));
        exclusions.ShouldContain(
            exclusion => exclusion.Remove == @"ProtectedStorage\DpapiCurrentUserProtector.cs" &&
                exclusion.Condition.Contains("'$(TargetFramework)' == 'net10.0'", StringComparison.Ordinal));
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
