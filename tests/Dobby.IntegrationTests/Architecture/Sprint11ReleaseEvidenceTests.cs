using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint11ReleaseEvidenceTests
{
    private const string ExpectedTask1PreRunSourceConfigState = """
        ## main...origin/main
         M SPRINT_11_EXECUTION_PLAN.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/auth-kdf-selection.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/installer-verification.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/packaging-static-scan.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/production-recovery-handover.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/release-run-manifest.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/release-smoke.md
         M artifacts/release-evidence/local-rc-20260624T134528Z-f153ca9c373f/startup-performance.md
         M artifacts/release-evidence/skeleton-local/decision-log.md
         M src/Dobby.UI/ThemeTokens.cs
         M tests/Dobby.IntegrationTests/Architecture/PortablePublishScriptTests.cs
         M tests/Dobby.IntegrationTests/Architecture/ProductionReleasePackagingStaticScanTests.cs
         M tests/Dobby.IntegrationTests/Architecture/Sprint11ReleaseEvidenceTests.cs
        ?? docs/superpowers/plans/2026-07-07-p1-release-gate-closure.md
        """;

    [Fact]
    public void Sprint11_release_run_preflight_manifest_matches_the_current_task1_evidence_root()
    {
        var root = CurrentTask1EvidenceRoot();
        var manifestPath = Path.Combine(root.FullName, "release-run-manifest.md");

        File.Exists(manifestPath)
            .ShouldBeTrue($"Sprint 11 preflight manifest is missing from {root.FullName}.");

        var manifest = NormalizeLineEndings(File.ReadAllText(manifestPath));
        var sourceShortSha = root.Name.Split('-').Last();
        var sourceFullSha = manifest
            .Split('\n')
            .Single(line => line.StartsWith("Source commit full SHA: ", StringComparison.Ordinal))
            ["Source commit full SHA: ".Length..]
            .Trim();

        manifest.ShouldContain($"Build ID: {root.Name}");
        manifest.ShouldContain($"Evidence root: artifacts\\release-evidence\\{root.Name}");
        sourceFullSha.Length.ShouldBe(40);
        sourceFullSha.ShouldStartWith(sourceShortSha);
        manifest.ShouldContain($"Source commit short SHA: {sourceShortSha}");
        manifest.ShouldContain("Source branch: main");
        manifest.ShouldContain(NormalizeLineEndings(
            $$"""
            Pre-run source config state:
            ```text
            {{ExpectedTask1PreRunSourceConfigState}}
            ```
            """));
        manifest.ShouldContain("Authority decisions approve the path; P1/release-ready is not closed until evidence passes.");
        manifest.ShouldContain("Repository CI status: repository CI ownership is absent; equivalent local release gate human-approved.");
        manifest.ShouldContain("Release readiness: BLOCKED UNTIL EVIDENCE PASSES");
        manifest.ShouldNotContain("READY FOR RELEASE REVIEW");
    }

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
        manifest.ShouldContain("Release readiness: BLOCKED UNTIL EVIDENCE PASSES");
        manifest.ShouldContain("repository CI ownership is absent");
        manifest.ShouldContain("equivalent local release gate human-approved");
        manifest.ShouldContain("preRunSourceConfigState=clean");
        manifest.ShouldContain("postRunDiffPolicy=allowlisted-generated-evidence-only");
        manifest.ShouldContain("DobbyPluginTrustProfile=Production");
        manifest.ShouldContain("oldBootstrapProductionHash=00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f");
        manifest.ShouldNotContain("CI passed");
        manifest.ShouldNotContain("Status: READY FOR RELEASE REVIEW");

        manifest.ShouldContain("HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES");
        manifest.ShouldContain("HD-S11-002: POLICY PATH ACCEPTED ONLY; CLOSURE BLOCKED");
        manifest.ShouldContain("HD-S11-003: MSIX-PRIMARY RC MECHANICS ACCEPTED ONLY");
        manifest.ShouldContain("HD-S11-004: APPROVED");
        manifest.ShouldContain("HD-S11-005: APPROVED");
        manifest.ShouldContain("Authority decisions approve the path; P1/release-ready is not closed until evidence passes.");
        manifest.ShouldContain("Informational measurements on this machine do not close AC-014/AC-051.");
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
        productionRecovery.ShouldContain("HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES");
        productionRecovery.ShouldContain("D-31");
        productionRecovery.ShouldContain("D-33");
        productionRecovery.ShouldContain("No production private key material is stored in this evidence root");

        var installerEvidence = Read(root, "installer-verification.md");
        installerEvidence.ShouldContain("MSIX-primary RC mechanics accepted only");
        installerEvidence.ShouldContain("does not imply GA/customer production signing readiness");

        var startupEvidence = Read(root, "startup-performance.md");
        startupEvidence.ShouldContain("policy path accepted only");
        startupEvidence.ShouldContain("Current local machine evidence");
        startupEvidence.ShouldContain("baseline disqualification evidence");
    }

    private static string Read(DirectoryInfo root, string fileName)
    {
        return File.ReadAllText(Path.Combine(root.FullName, fileName));
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static DirectoryInfo CurrentTask1EvidenceRoot()
    {
        var releaseEvidenceRoot = new DirectoryInfo(Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence"));

        releaseEvidenceRoot.Exists.ShouldBeTrue();

        var candidates = releaseEvidenceRoot
            .EnumerateDirectories("local-rc-*", SearchOption.TopDirectoryOnly)
            .Where(ManifestMatchesCurrentTask1Root)
            .ToArray();

        candidates.Length.ShouldBe(
            1,
            "Expected exactly one Task 1 local release-candidate evidence root with an internally consistent manifest state.");

        return candidates[0];
    }

    private static bool ManifestMatchesCurrentTask1Root(DirectoryInfo directory)
    {
        var manifestPath = Path.Combine(directory.FullName, "release-run-manifest.md");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        var manifest = NormalizeLineEndings(File.ReadAllText(manifestPath));
        var sourceShortSha = directory.Name.Split('-').Last();
        return manifest.Contains($"Build ID: {directory.Name}", StringComparison.Ordinal)
            && manifest.Contains($"Evidence root: artifacts\\release-evidence\\{directory.Name}", StringComparison.Ordinal)
            && manifest.Contains($"Source commit short SHA: {sourceShortSha}", StringComparison.Ordinal)
            && manifest.Contains("Source branch: main", StringComparison.Ordinal)
            && manifest.Contains("Pre-run source config state:", StringComparison.Ordinal)
            && manifest.Contains(NormalizeLineEndings(ExpectedTask1PreRunSourceConfigState), StringComparison.Ordinal);
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
            .Where(candidate => RequiredEvidenceFiles.All(fileName => File.Exists(Path.Combine(candidate.Directory.FullName, fileName))))
            .Where(candidate => Directory.Exists(Path.Combine(candidate.Directory.FullName, "ui-snapshots")))
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
