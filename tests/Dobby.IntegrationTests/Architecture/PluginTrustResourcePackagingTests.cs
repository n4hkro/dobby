using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginTrustResourcePackagingTests
{
    private const string ApprovedProductionPemSha256 = "74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f";
    private const string Sprint8BootstrapPemSha256 = "00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f";
    private const string DevQaPemSha256 = "cb0d35dafea42031e18c778ff208d7b142b280d73ba68983d5f3afccb6a08afc";
    private const string TestOnlyRecoveryPemSha256 = "1a30c878be9f0d69adcfbad0a7244e19738a3c81fb9ca7eb2581a8b1c2455a19";

    [Fact]
    public void Production_source_public_pem_parses_and_matches_approved_hash()
    {
        ParseAndHashPem("vendor-master-public-key.pem").ShouldBe(ApprovedProductionPemSha256);
    }

    [Fact]
    public void Bootstrap_dev_qa_and_test_only_public_keys_remain_negative_controls_for_production_hash()
    {
        Sprint8BootstrapPemSha256.ShouldNotBe(ApprovedProductionPemSha256);

        PemSha256("dev-qa-public-key.pem").ShouldBe(DevQaPemSha256);
        DevQaPemSha256.ShouldNotBe(ApprovedProductionPemSha256);

        PemSha256(Path.Combine("Recovery", "Fixtures", "TEST-ONLY", "vendor-recovery-public-key.TEST-ONLY.txt"))
            .ShouldBe(TestOnlyRecoveryPemSha256);
        TestOnlyRecoveryPemSha256.ShouldNotBe(ApprovedProductionPemSha256);
    }

    [Fact]
    public void Security_project_embeds_exactly_one_profile_selected_trust_anchor()
    {
        var project = XDocument.Load(Path.Combine(
            RepoRoot().FullName,
            "src",
            "Dobby.Security",
            "Dobby.Security.csproj"));

        var defaultProfile = project
            .Descendants()
            .Single(element => element.Name.LocalName == "DobbyPluginTrustProfile");
        defaultProfile.Value.ShouldBe("Production");
        defaultProfile.Attribute("Condition")?.Value.ShouldBe("'$(DobbyPluginTrustProfile)' == ''");

        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'Production'",
            @"..\Dobby.Host\Trust\vendor-master-public-key.pem",
            PluginTrustPublicKeyProvider.ProductionResourceName);
        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'DevQa'",
            @"..\Dobby.Host\Trust\dev-qa-public-key.pem",
            PluginTrustPublicKeyProvider.DevQaResourceName);
    }

    [Fact]
    public void Default_production_security_assembly_contains_no_dev_qa_trust_anchor()
    {
        var assembly = typeof(PluginTrustPublicKeyProvider).Assembly;
        var resources = assembly.GetManifestResourceNames();

        resources.ShouldContain(PluginTrustPublicKeyProvider.ProductionResourceName);
        resources.ShouldNotContain(PluginTrustPublicKeyProvider.DevQaResourceName);
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "DobbyPluginTrustProfile")
            .Value.ShouldBe("Production");
    }

    [Fact]
    public void Admin_recovery_concrete_verifier_proof_uses_embedded_dev_qa_trust_anchor_without_injected_resource_map()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot().FullName,
            "tests",
            "Dobby.UnitTests",
            "Admin",
            "AdminCommandHostTests.cs"));
        var start = source.IndexOf(
            "Create_recovery_response_writes_host_compatible_envelope_through_concrete_verifier",
            StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var end = source.IndexOf("Create_plugin_license_writes_importable_license", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start);
        var proof = source[start..end];

        proof.ShouldContain("ApprovedDevQaBootstrapSigningKeyPath");
        proof.ShouldContain("new PluginTrustPublicKeyProvider(DobbyPluginTrustProfile.DevQa)");
        proof.ShouldNotContain("new Dictionary<string, byte[]>", Case.Sensitive);
        proof.ShouldNotContain("new InMemorySigningMaterialProvider(\"dev-qa\", key)", Case.Sensitive);
        proof.ShouldNotContain("new VendorRecoveryPublicKey(publicKeyProvider)", Case.Sensitive);
    }

    private static void AssertResource(
        XDocument project,
        string expectedCondition,
        string expectedInclude,
        string expectedLogicalName)
    {
        var itemGroup = project
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "ItemGroup" &&
                element.Attribute("Condition")?.Value == expectedCondition);
        var resource = itemGroup
            .Elements()
            .Single(element => element.Name.LocalName == "EmbeddedResource");

        resource.Attribute("Include")?.Value.ShouldBe(expectedInclude);
        resource.Attribute("LogicalName")?.Value.ShouldBe(expectedLogicalName);
    }

    private static string PemSha256(string fileName)
    {
        var bytes = File.ReadAllBytes(PemPath(fileName));

        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ParseAndHashPem(string fileName)
    {
        var pem = File.ReadAllText(PemPath(fileName));
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);

        return PemSha256(fileName);
    }

    private static string PemPath(string fileName)
    {
        var root = RepoRoot().FullName;
        return fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar)
            ? Path.Combine(root, "tests", "Dobby.IntegrationTests", fileName)
            : Path.Combine(root, "src", "Dobby.Host", "Trust", fileName);
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
