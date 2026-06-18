using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint9ReleaseEvidenceTests
{
    [Fact]
    public void Sprint9_evidence_records_license_runtime_ui_and_print_boundary_markers()
    {
        var evidence = Sprint9Evidence();

        evidence.ShouldContain("## Sprint 9 Implementation Evidence");
        evidence.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        evidence.ShouldContain("SPRINT9-LICENSE-ARTIFACT-V2");
        evidence.ShouldContain("SPRINT9-SIGNATURE-VALIDATION");
        evidence.ShouldContain("SPRINT9-CURRENT-CONTEXT");
        evidence.ShouldContain("SPRINT9-LICENSE-STATE-MIGRATION");
        evidence.ShouldContain("SPRINT9-RUNTIME-MODE-CAPABILITY");
        evidence.ShouldContain("SPRINT9-ACTIVATION-GATE");
        evidence.ShouldContain("SPRINT9-READONLY-GATES");
        evidence.ShouldContain("SPRINT9-LICENSE-UI");
        evidence.ShouldContain("SPRINT9-PRINT-BOUNDARY-SUNSET");
        evidence.ShouldContain("SPRINT9-BOUNDARY-VERIFICATION");
        evidence.ShouldContain("Unit 570, Integration 213, Smoke 6");
        evidence.ShouldContain("Dobby.Infrastructure -> Dobby.Application ProjectReference removed");
        evidence.ShouldNotContain("IsTemporaryPrintBoundaryEdge");
        evidence.ShouldNotContain("PrintBoundaryBlockerIsRecorded");
    }

    private static string Sprint9Evidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 9 Implementation Evidence", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextSection = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        return nextSection >= 0
            ? text[start..nextSection]
            : text[start..];
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
