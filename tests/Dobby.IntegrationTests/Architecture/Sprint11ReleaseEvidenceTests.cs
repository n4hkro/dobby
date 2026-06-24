using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint11ReleaseEvidenceTests
{
    private static readonly string[] RequiredEvidenceFiles =
    [
        "release-run-manifest.md",
        "auth-kdf-selection.md",
        "storage-provider-proof.md",
        "packaging-static-scan.md",
        "startup-performance.md",
        "installer-verification.md",
        "unit-integration-smoke-results.md",
        "release-smoke.md",
        "production-recovery-handover.md"
    ];

    [Fact]
    public void Sprint11_release_candidate_evidence_records_required_files_and_blocked_gates()
    {
        var root = Sprint11EvidenceRoot();

        foreach (var fileName in RequiredEvidenceFiles)
        {
            File.Exists(Path.Combine(root.FullName, fileName))
                .ShouldBeTrue($"Sprint 11 release evidence is missing {fileName}.");
        }

        Directory.Exists(Path.Combine(root.FullName, "ui-snapshots"))
            .ShouldBeTrue("Sprint 11 release evidence is missing ui-snapshots/.");

        var manifest = Read(root, "release-run-manifest.md");
        manifest.ShouldContain("Sprint 11");
        manifest.ShouldContain("Release readiness: BLOCKED PENDING HUMAN DECISION");
        manifest.ShouldContain("repository CI ownership is absent");
        manifest.ShouldContain("local-only equivalent gate acceptance is blocked pending human approval");
        manifest.ShouldContain("preRunSourceConfigState=clean");
        manifest.ShouldContain("postRunDiffPolicy=allowlisted-generated-evidence-only");
        manifest.ShouldContain("DobbyPluginTrustProfile=Production");
        manifest.ShouldContain("oldBootstrapProductionHash=00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f");
        manifest.ShouldNotContain("CI passed");
        manifest.ShouldNotContain("Status: READY FOR RELEASE REVIEW");

        foreach (var gateId in new[] { "HD-S11-001", "HD-S11-002", "HD-S11-003", "HD-S11-004", "HD-S11-005" })
        {
            manifest.ShouldContain(gateId);
            manifest.ShouldContain($"{gateId}: BLOCKED PENDING HUMAN DECISION");
        }
    }

    [Fact]
    public void Sprint11_release_candidate_evidence_maps_acceptance_criteria_to_artifacts()
    {
        var root = Sprint11EvidenceRoot();

        Read(root, "release-smoke.md").ShouldContain("AC-008");
        Read(root, "startup-performance.md").ShouldContain("AC-014");
        Read(root, "installer-verification.md").ShouldContain("AC-024");
        Read(root, "unit-integration-smoke-results.md").ShouldContain("AC-041");
        Read(root, "release-smoke.md").ShouldContain("AC-046");
        Read(root, "release-run-manifest.md").ShouldContain("AC-047");
        Read(root, "auth-kdf-selection.md").ShouldContain("AC-051");
    }

    [Fact]
    public void Sprint11_release_candidate_evidence_preserves_boundary_markers()
    {
        var root = Sprint11EvidenceRoot();

        var kdfEvidence = Read(root, "auth-kdf-selection.md");
        kdfEvidence.ShouldContain("PBKDF2-HMAC-SHA256");
        kdfEvidence.ShouldContain("Argon2id remains deferred");
        kdfEvidence.ShouldContain("no new package pin");

        var storageEvidence = Read(root, "storage-provider-proof.md");
        storageEvidence.ShouldContain("SQLite3MultipleCiphers");
        storageEvidence.ShouldContain("3744689bf8427b40b222c1ec046ebc5e4f08c5c9898d56573112006ff2c4def8");
        storageEvidence.ShouldContain("No storage-provider switch");

        var staticScan = Read(root, "packaging-static-scan.md");
        staticScan.ShouldContain("No production output path includes sample plugin artifacts");
        staticScan.ShouldContain("dev/QA trust anchor absent");
        staticScan.ShouldContain("no production private keys");

        var productionRecovery = Read(root, "production-recovery-handover.md");
        productionRecovery.ShouldContain("HD-S11-001: BLOCKED PENDING HUMAN DECISION");
        productionRecovery.ShouldContain("D-31");
        productionRecovery.ShouldContain("D-33");
        productionRecovery.ShouldContain("No production private key material is stored in this evidence root");
    }

    private static string Read(DirectoryInfo root, string fileName)
    {
        return File.ReadAllText(Path.Combine(root.FullName, fileName));
    }

    private static DirectoryInfo Sprint11EvidenceRoot()
    {
        var releaseEvidenceRoot = new DirectoryInfo(Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence"));

        releaseEvidenceRoot.Exists.ShouldBeTrue();

        var candidates = releaseEvidenceRoot
            .EnumerateDirectories("local-rc-*", SearchOption.TopDirectoryOnly)
            .Select(directory => new
            {
                Directory = directory,
                ManifestPath = Path.Combine(directory.FullName, "release-run-manifest.md")
            })
            .Where(candidate => File.Exists(candidate.ManifestPath))
            .Where(candidate => File.ReadAllText(candidate.ManifestPath).Contains("Sprint 11", StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Directory.Name, StringComparer.Ordinal)
            .ToArray();

        candidates.ShouldNotBeEmpty("Sprint 11 local release-candidate evidence root was not found.");

        return candidates[0].Directory;
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
