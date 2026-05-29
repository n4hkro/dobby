using System.IO;
using System.Text.RegularExpressions;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint1ReleaseEvidenceContractTests
{
    [Fact]
    public void ReleaseEvidenceTests_remains_heading_only_for_sprint1_contract()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "tests",
            "Dobby.IntegrationTests",
            "Architecture",
            "ReleaseEvidenceTests.cs");

        var source = File.ReadAllText(path);
        var factCount = Regex.Matches(source, @"^\s*\[Fact\]\s*$", RegexOptions.Multiline).Count;

        factCount.ShouldBe(2, "Sprint 1 D-023.1 limits ReleaseEvidenceTests.cs to the architecture and toolchain heading-presence checks.");
        source.ShouldNotContain("Sprint5", Case.Sensitive, "later-sprint detailed evidence assertions belong in sprint-specific evidence tests.");
        source.ShouldNotContain("Sprint 5", Case.Sensitive, "later-sprint detailed evidence assertions belong in sprint-specific evidence tests.");
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
