using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint6ReleaseEvidenceTests
{
    [Fact]
    public void Sprint6_evidence_records_authorization_users_settings_and_verification_closure()
    {
        var evidence = Sprint6Evidence();

        evidence.ShouldContain("## Sprint 6 Implementation Evidence");
        evidence.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        evidence.ShouldContain("AC-022");
        evidence.ShouldContain("AC-030");
        evidence.ShouldContain("AC-035");
        evidence.ShouldContain("AC-039");
        evidence.ShouldContain("users.management");
        evidence.ShouldContain("D-20");
        evidence.ShouldContain("D-21");
        evidence.ShouldContain("No new package pins");
        evidence.ShouldContain("Dobby.Security -> Dobby.Audit edge: absent");
    }

    private static string Sprint6Evidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 6 Implementation Evidence", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);

        return text[start..];
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
