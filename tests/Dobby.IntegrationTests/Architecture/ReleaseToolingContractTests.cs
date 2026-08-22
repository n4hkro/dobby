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

    [Fact]
    public void Supply_chain_and_release_runner_fail_closed_and_record_external_blockers()
    {
        var root = RepoRoot();
        var sbom = Read(root, "tools", "generate-sbom", "generate-sbom.ps1");
        var runner = Read(root, "tools", "verify-release-candidate", "verify-release-candidate.ps1");

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
        script.ShouldNotContain("New-PlaceholderAsset");
        script.ShouldNotContain("10.0.26100.0");
        script.ShouldNotContain("10.0.22621.0");
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
