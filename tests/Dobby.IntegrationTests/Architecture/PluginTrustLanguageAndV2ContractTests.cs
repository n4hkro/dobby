using System.Reflection;
using Dobby.PluginFramework.Manifest;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginTrustLanguageAndV2ContractTests
{
    [Fact]
    public void Current_authority_and_product_resources_do_not_claim_in_process_hostile_code_sandboxing()
    {
        var root = RepoRoot();
        var authoritativeFiles = new[]
        {
            "docs/01_PRODUCT_SCOPE.md",
            "docs/02_ARCHITECTURE_MAP.md",
            "docs/03_TECH_STACK_AND_VERSIONS.md",
            "docs/05_FUNCTIONAL_REQUIREMENTS.md",
            "docs/06_NON_FUNCTIONAL_REQUIREMENTS.md",
            "docs/07_ACCEPTANCE_CRITERIA.md",
            "docs/adr/ADR-0011-plugin-sandbox-enforcement-principle.md",
            "docs/adr/ADR-0059-plugin-trust-package-and-ui-contract-v2.md",
            "docs/specs/plugins-manager.md",
            "src/Dobby.Host/Trust/README.md"
        };

        var combined = string.Join('\n', authoritativeFiles.Select(path =>
            File.ReadAllText(Path.Combine(root.FullName, path.Replace('/', Path.DirectorySeparatorChar)))));

        combined.ShouldNotContain("P1 sandbox OS/process");
        combined.ShouldNotContain("sandbox həmin wrapper");
        combined.ShouldNotContain("Plugin Isolation / Sandbox | namespace");
        combined.ShouldContain("fully trusted in-process");
        combined.ShouldContain("hostile-code");

        var localizedResources = Directory.GetFiles(
                Path.Combine(root.FullName, "src", "Dobby.Localization"),
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText);
        string.Join('\n', localizedResources).ShouldNotContain("sandbox", Case.Insensitive);
    }

    [Fact]
    public void Manifest_v2_contract_and_embedded_schema_are_versioned_separately_from_v1()
    {
        PluginManifestContract.LegacySchemaVersion.ShouldBe(1);
        PluginManifestContract.CurrentSchemaVersion.ShouldBe(2);
        PluginManifestContract.PackagePurpose.ShouldBe("dobby.plugin-package");
        PluginManifestContract.MaximumFiles.ShouldBe(256);

        var resources = typeof(PluginManifestV2).Assembly.GetManifestResourceNames();
        resources.ShouldContain("Dobby.PluginFramework.Schemas.dobby-plugin-manifest.schema.json");
        resources.ShouldContain("Dobby.PluginFramework.Schemas.dobby-plugin-manifest-v2.schema.json");

        typeof(PluginManifestV2).GetProperty(nameof(PluginManifestV2.SchemaVersion)).ShouldNotBeNull();
        typeof(PluginManifestV2).GetProperty(nameof(PluginManifestV2.HostApiVersion)).ShouldNotBeNull();
        typeof(PluginManifestV2).GetProperty(nameof(PluginManifestV2.NativeLibraries)).ShouldNotBeNull();
        typeof(PluginPackageSignatureV2).GetProperty(nameof(PluginPackageSignatureV2.KeyId)).ShouldNotBeNull();
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
