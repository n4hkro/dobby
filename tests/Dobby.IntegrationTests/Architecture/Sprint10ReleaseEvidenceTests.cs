using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint10ReleaseEvidenceTests
{
    [Fact]
    public void Sprint10_evidence_records_plugin_admin_packaging_and_verification_markers()
    {
        var evidence = Sprint10Evidence();

        evidence.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        evidence.ShouldContain("SPRINT10-INSTALL-AUTHORIZATION");
        evidence.ShouldContain("SPRINT10-UNINSTALL-CONFIRMATION");
        evidence.ShouldContain("SPRINT10-SAMPLE-PLUGIN-ACTIONS");
        evidence.ShouldContain("SPRINT10-ADMIN-ARTIFACT-VERIFICATION");
        evidence.ShouldContain("SPRINT10-PACKAGING-STATIC-SCAN");
        evidence.ShouldContain("Unit ");
        evidence.ShouldContain("Integration ");
        evidence.ShouldContain("Smoke ");
        evidence.ShouldContain("dotnet build .\\Dobby.sln -c Release --no-restore -warnaserror -v minimal");
        evidence.ShouldContain("dotnet test .\\Dobby.sln -c Release --no-build --no-restore --logger \"console;verbosity=minimal\"");
        evidence.ShouldContain("dotnet format .\\Dobby.sln --verify-no-changes --no-restore --verbosity minimal");
    }

    [Fact]
    public void Sprint10_packaging_static_scan_evidence_is_present()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "sprint10-local-2026-06-20",
            "packaging-static-scan.md");

        File.Exists(path).ShouldBeTrue();
        var scan = File.ReadAllText(path);
        scan.ShouldContain("## Sprint 10 Packaging Static Scan");
        scan.ShouldContain("samples/Dobby.Sample.Plugin");
        scan.ShouldContain("Production release scan");
        scan.ShouldContain("dev/QA trust anchor");
        scan.ShouldContain("No production output path includes sample plugin artifacts");
    }

    private static string Sprint10Evidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 10 Implementation Evidence", StringComparison.Ordinal);
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
