using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ReleaseEvidenceTests
{
    [Fact]
    public void Skeleton_architecture_baseline_evidence_is_present()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        File.Exists(path).ShouldBeTrue();
        File.ReadAllText(path).ShouldContain("## Static Guardrail Status");
    }

    [Fact]
    public void Skeleton_toolchain_pin_gate_evidence_is_present()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "toolchain-pin-gate.md");

        File.Exists(path).ShouldBeTrue();
        File.ReadAllText(path).ShouldContain("## Package Pin Approval Status");
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
