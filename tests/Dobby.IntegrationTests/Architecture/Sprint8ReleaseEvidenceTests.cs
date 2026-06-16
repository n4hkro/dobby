using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint8ReleaseEvidenceTests
{
    [Fact]
    public void Sprint8_evidence_records_package_governance_trust_blocker_and_boundary_markers()
    {
        var evidence = Sprint8Evidence();

        evidence.ShouldContain("## Sprint 8 Implementation Evidence");
        evidence.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        evidence.ShouldContain("SPRINT8-PACKAGE-GOVERNANCE");
        evidence.ShouldContain("SPRINT8-SOURCE-PACKAGE-GATE");
        evidence.ShouldContain("SPRINT8-TRUST-RESOURCE-PACKAGING");
        evidence.ShouldContain("SPRINT8-ACTIVATION-REVALIDATION");
        evidence.ShouldContain("SPRINT8-COMPATIBILITY-DEPENDENCY");
        evidence.ShouldContain("SPRINT8-SESSION-CANCELLATION");
        evidence.ShouldContain("SPRINT8-ALC-ISOLATION");
        evidence.ShouldContain("SPRINT8-EVENT-BUS-HOSTING");
        evidence.ShouldContain("SPRINT8-PAGE-RENDER-HOSTING");
        evidence.ShouldContain("SPRINT8-PLUGIN-PROGRESS-HOSTING");
        evidence.ShouldContain("SPRINT8-BACKGROUND-OPERATION");
        evidence.ShouldContain("SPRINT8-ACTIVATION-SCHEDULING");
        evidence.ShouldContain("SPRINT8-BOUNDARY-VERIFICATION");
        evidence.ShouldContain("unsupported-package-source");
        evidence.ShouldContain("package-fingerprint-mismatch");
        evidence.ShouldContain("PluginTextPageModel");
        evidence.ShouldContain("IBackgroundOperationReporter");
        evidence.ShouldContain("00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f");
        evidence.ShouldContain("cb0d35dafea42031e18c778ff208d7b142b280d73ba68983d5f3afccb6a08afc");
        evidence.ShouldContain("dotnet build .\\Dobby.sln -c Release --no-restore -warnaserror -v minimal");
        evidence.ShouldContain("dotnet test .\\Dobby.sln -c Release --no-restore --no-build --logger \"console;verbosity=minimal\"");
        evidence.ShouldContain("Unit 447, Integration 197, Smoke 6");
        evidence.ShouldNotContain("dotnet test .\\Dobby.sln -c Release --no-build --logger \"console;verbosity=minimal\"");
    }

    private static string Sprint8Evidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 8 Implementation Evidence", StringComparison.Ordinal);
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
