using System.Diagnostics;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ReleaseToolingContractTests
{
    [Fact]
    public void Independent_plugin_verifier_wrapper_and_contract_are_present()
    {
        var root = RepoRoot();
        var script = Read(root, "tools", "verify-plugin-package", "verify-plugin-package.ps1");
        var admin = Read(root, "src", "Dobby.Admin", "Commands", "AdminCommandHost.cs");

        script.ShouldContain("--cli verify-plugin-package");
        admin.ShouldContain("VerifyPluginPackageCommand");
        admin.ShouldContain("PluginPackageValidator.Validate");
        admin.ShouldContain("--host-version");
        admin.ShouldContain("--trust-profile");
        admin.ShouldContain("verification-failed");
    }

    // NFR-027 / ADR-0061 boundary: every assertion below is a source-text scan over release
    // scripts, so it may only enforce the *documented shape* of the release tooling -- which
    // gates exist, which vocabulary they use, which sub-script they call. It is deliberately
    // NOT evidence that the runner fails closed, and it closes no release gate.
    //
    // The executable proof lives elsewhere and must stay there:
    //   * `ReleaseExternalEvidenceValidatorTests` really invokes validate-external-evidence.ps1
    //     via pwsh and pins accept / wrong-commit / missing-claim / malformed-hash outcomes.
    //   * `Release_runner_creates_each_gate_log_even_when_the_command_is_silent` pins an
    //     ordering property of the runner.
    //   * Portable-launch and install/upgrade gates are closed by their own real-artifact runs.
    //
    // The previous name ("..._fail_closed_and_record_external_blockers") asserted runtime
    // fail-closed behavior that no line here exercises -- exactly the "static test masquerading
    // as runtime proof" shape NFR-027 forbids from closing a release gate. Renamed rather than
    // deleted: pinning the documented vocabulary still catches silent drift between the release
    // scripts and docs/09, which is a legitimate shape obligation.
    [Fact]
    public void Supply_chain_and_release_runner_declare_the_documented_gate_vocabulary()
    {
        var root = RepoRoot();
        var sbom = Read(root, "tools", "generate-sbom", "generate-sbom.ps1");
        var runner = Read(root, "tools", "verify-release-candidate", "verify-release-candidate.ps1");
        var externalEvidenceValidator = Read(root, "tools", "verify-release-candidate", "validate-external-evidence.ps1");
        var externalEvidenceSchema = Read(root, "docs", "schemas", "release-external-evidence-v1.schema.json");

        sbom.ShouldContain("CycloneDX");
        sbom.ShouldContain("specVersion = '1.6'");
        sbom.ShouldContain("project.assets.json");
        sbom.ShouldContain("--vulnerable");
        sbom.ShouldContain("owner-review-required");
        runner.ShouldContain("EvidenceRoot must be new or empty");
        runner.ShouldContain("clean tracked source tree");
        runner.ShouldContain("externalBlockers");
        runner.ShouldContain("installed-msix-upgrade");
        runner.ShouldContain("production-signing");
        runner.ShouldContain("license-review");
        runner.ShouldContain("releaseReady = $blockers.Count -eq 0");
        runner.ShouldContain("verify-portable-launch.ps1");
        runner.ShouldContain("logSha256");
        runner.ShouldContain("logLength");
        runner.ShouldContain("evidenceFile");
        runner.ShouldContain("Copy-Item -LiteralPath $resolved -Destination $capturedPath");
        runner.ShouldContain("validate-external-evidence.ps1");
        externalEvidenceValidator.ShouldContain("ExpectedSourceCommit");
        externalEvidenceValidator.ShouldContain("requiredClaims");
        externalEvidenceSchema.ShouldContain("release-external-evidence-v1");
    }

    [Fact]
    public void Release_runner_creates_each_gate_log_even_when_the_command_is_silent()
    {
        var runner = Read(RepoRoot(), "tools", "verify-release-candidate", "verify-release-candidate.ps1");

        runner.ShouldContain("[System.IO.File]::WriteAllText($logPath, [string]::Empty)");
        runner.IndexOf("[System.IO.File]::WriteAllText($logPath, [string]::Empty)", StringComparison.Ordinal)
            .ShouldBeLessThan(runner.IndexOf("& $Action *>&1 | Tee-Object -FilePath $logPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Msix_builder_discovers_sdk_and_stages_only_hashed_portable_manifest_files()
    {
        var script = Read(RepoRoot(), "installer", "msix", "build-rc-msix.ps1");

        script.ShouldContain("Windows Kits\\Installed Roots");
        script.ShouldContain("WindowsSdkVersion");
        script.ShouldContain("minimumSdkVersion");
        script.ShouldContain("artifact-manifest.json");
        script.ShouldContain("Portable manifest hash mismatch");
        script.ShouldContain("ArtifactMode = 'Installed'");
        script.ShouldContain("New-BrandedAsset -Path (Join-Path $assetsRoot 'Wide310x150Logo.png') -Width 310 -Height 150");
        script.ShouldNotContain("New-PlaceholderAsset");
        script.ShouldNotContain("New-BrandedAsset -Path (Join-Path $assetsRoot 'Wide310x150Logo.png') -Size 310");
        script.ShouldNotContain("10.0.26100.0");
        script.ShouldNotContain("10.0.22621.0");
    }

    [Fact]
    public void Flake_repeat_matrix_runs_test_projects_sequentially()
    {
        var script = Read(RepoRoot(), "tools", "verify-flake-sensitive-tests", "verify-flake-sensitive-tests.ps1");

        script.ShouldContain("-Phase 'matrix-unit'");
        script.ShouldContain("-Phase 'matrix-integration'");
        script.ShouldContain("-Phase 'matrix-smoke'");
        script.ShouldNotContain("-Phase 'solution'");
    }

    [Fact]
    public void Repository_ignore_rules_do_not_swallow_build_id_release_evidence()
    {
        // docs/09_BUILD_RUN_AND_RELEASE.md: an evidence file that exists on disk but is
        // silently ignored is not recorded evidence, because the reviewer sees a clean
        // working tree while a required gate artifact is missing from the commit. Reading
        // .gitignore text cannot prove this — precedence between overlapping patterns
        // decides the outcome — so ask git itself (NFR-027).
        var root = RepoRoot();
        Directory.Exists(Path.Combine(root, ".git"))
            .ShouldBeTrue("this check needs the repository's own ignore rules");

        var buildIdRoots = new DirectoryInfo(Path.Combine(root, "artifacts", "release-evidence"))
            .EnumerateDirectories("local-rc-*", SearchOption.TopDirectoryOnly)
            .ToArray();
        buildIdRoots.ShouldNotBeEmpty("no build-id release evidence root was found to check");

        foreach (var buildIdRoot in buildIdRoots)
        {
            IsIgnored(root, Path.Combine(buildIdRoot.FullName, "storage-provider-proof.md"))
                .ShouldBeFalse(
                    $"a required evidence file added to {buildIdRoot.Name} must be committable");
            IsIgnored(root, Path.Combine(buildIdRoot.FullName, "ui-snapshots", "login-window.png"))
                .ShouldBeFalse(
                    $"a UI snapshot added to {buildIdRoot.Name} must be committable");
            IsIgnored(root, Path.Combine(buildIdRoot.FullName, "verbose-run.log"))
                .ShouldBeTrue(
                    "a generated log inside an evidence root is not evidence, so it stays ignored");
        }
    }

    private static bool IsIgnored(string repositoryRoot, string path)
    {
        using var process = Process.Start(new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "check-ignore", "-q", path }
        }) ?? throw new InvalidOperationException("git could not be started.");
        process.WaitForExit();

        // git check-ignore: 0 = ignored, 1 = not ignored, anything else = failure.
        return process.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => throw new InvalidOperationException(
                $"git check-ignore failed for '{path}': {process.StandardError.ReadToEnd()}")
        };
    }

    private static string Read(string root, params string[] segments)
    {
        var path = segments.Aggregate(root, Path.Combine);
        File.Exists(path).ShouldBeTrue($"Expected release tool at {path}.");
        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Dobby.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
