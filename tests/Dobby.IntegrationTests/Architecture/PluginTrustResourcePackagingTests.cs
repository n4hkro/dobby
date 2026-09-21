using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginTrustResourcePackagingTests
{
    private const string ApprovedProductionPluginPemSha256 = "fc8945a5b118db0951a29c0093af17f53f809d54bc6e490e930f423f04b4bf1f";
    private const string ApprovedProductionLicensePemSha256 = "74bc41acb824f32052fdead5acb25599a91f3ce57408dd1b3c7af0fd9af49d9b";
    private const string Sprint8BootstrapPemSha256 = "00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f";
    private const string DevQaPluginPemSha256 = "c50affc2f2e4e3659eaf4c4c4fc3646eb2b117624b57a3fc034249e7b1d56e7a";
    private const string DevQaLicensePemSha256 = "02f1ba638b092f314efa90f38323506ae053bd44438515bf021ec9e8b3ff135f";
    private const string TestOnlyRecoveryPemSha256 = "1a30c878be9f0d69adcfbad0a7244e19738a3c81fb9ca7eb2581a8b1c2455a19";

    private static readonly string[] CanonicalAnchorFileNames =
    [
        "vendor-plugin-package-public-key.pem",
        "vendor-plugin-license-public-key.pem",
        "vendor-recovery-public-key.pem",
        "dev-qa-plugin-package-public-key.pem",
        "dev-qa-plugin-license-public-key.pem",
        "dev-qa-recovery-public-key.pem"
    ];

    [Fact]
    public void Production_source_public_pem_parses_and_matches_approved_hash()
    {
        ParseAndHashPem("vendor-plugin-package-public-key.pem").ShouldBe(ApprovedProductionPluginPemSha256);
        ParseAndHashPem("vendor-plugin-license-public-key.pem").ShouldBe(ApprovedProductionLicensePemSha256);
        ApprovedProductionPluginPemSha256.ShouldNotBe(ApprovedProductionLicensePemSha256);
    }

    [Fact]
    public void Bootstrap_dev_qa_and_test_only_public_keys_remain_negative_controls_for_production_hash()
    {
        Sprint8BootstrapPemSha256.ShouldNotBe(ApprovedProductionPluginPemSha256);

        PemSha256("dev-qa-plugin-package-public-key.pem").ShouldBe(DevQaPluginPemSha256);
        PemSha256("dev-qa-plugin-license-public-key.pem").ShouldBe(DevQaLicensePemSha256);
        DevQaPluginPemSha256.ShouldNotBe(ApprovedProductionPluginPemSha256);
        DevQaLicensePemSha256.ShouldNotBe(ApprovedProductionLicensePemSha256);
        DevQaPluginPemSha256.ShouldNotBe(DevQaLicensePemSha256);

        PemSha256(Path.Combine("Recovery", "Fixtures", "TEST-ONLY", "vendor-recovery-public-key.TEST-ONLY.txt"))
            .ShouldBe(TestOnlyRecoveryPemSha256);
        TestOnlyRecoveryPemSha256.ShouldNotBe(ApprovedProductionPluginPemSha256);
    }

    [Fact]
    public void Security_project_embeds_only_profile_selected_purpose_separated_trust_anchors()
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
            @"..\Dobby.Host\Trust\vendor-plugin-package-public-key.pem",
            PluginTrustPublicKeyProvider.ProductionResourceName);
        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'Production'",
            @"..\Dobby.Host\Trust\vendor-plugin-license-public-key.pem",
            PluginTrustPublicKeyProvider.ProductionLicenseResourceName);
        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'DevQa'",
            @"..\Dobby.Host\Trust\dev-qa-plugin-package-public-key.pem",
            PluginTrustPublicKeyProvider.DevQaResourceName);
        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'DevQa'",
            @"..\Dobby.Host\Trust\dev-qa-plugin-license-public-key.pem",
            PluginTrustPublicKeyProvider.DevQaLicenseResourceName);
        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'Production'",
            @"..\Dobby.Host\Trust\vendor-recovery-public-key.pem",
            VendorRecoveryPublicKey.ProductionResourceName);
        AssertResource(
            project,
            "'$(DobbyPluginTrustProfile)' == 'DevQa'",
            @"..\Dobby.Host\Trust\dev-qa-recovery-public-key.pem",
            VendorRecoveryPublicKey.DevQaResourceName);
    }

    [Fact]
    public void Default_production_security_assembly_contains_no_dev_qa_trust_anchor()
    {
        var assembly = typeof(PluginTrustPublicKeyProvider).Assembly;
        var resources = assembly.GetManifestResourceNames();

        resources.ShouldContain(PluginTrustPublicKeyProvider.ProductionResourceName);
        resources.ShouldContain(PluginTrustPublicKeyProvider.ProductionLicenseResourceName);
        resources.ShouldNotContain(PluginTrustPublicKeyProvider.DevQaResourceName);
        resources.ShouldNotContain(PluginTrustPublicKeyProvider.DevQaLicenseResourceName);
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "DobbyPluginTrustProfile")
            .Value.ShouldBe("Production");
    }

    [Fact]
    public void Recovery_trust_anchor_is_embedded_separately_from_plugin_trust_and_excludes_test_key_in_production()
    {
        var assembly = typeof(VendorRecoveryPublicKey).Assembly;
        var resources = assembly.GetManifestResourceNames();

        VendorRecoveryPublicKey.ProductionResourceName.ShouldNotBe(PluginTrustPublicKeyProvider.ProductionResourceName);
        resources.ShouldContain(VendorRecoveryPublicKey.ProductionResourceName);
        resources.ShouldNotContain(VendorRecoveryPublicKey.DevQaResourceName);
        Should.NotThrow(() => new VendorRecoveryPublicKey(DobbyPluginTrustProfile.Production));
    }

    [Fact]
    public void Canonical_public_trust_anchors_are_tracked_in_version_control()
    {
        // ADR-0062 § 3: public anchors are approved, version-controlled build input.
        // An ignored/untracked anchor makes a fresh clone or CI build unreproducible.
        var tracked = RunGit("ls-files", "--", "src/Dobby.Host/Trust")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var anchor in CanonicalAnchorFileNames)
        {
            tracked.ShouldContain(
                $"src/Dobby.Host/Trust/{anchor}",
                $"Trust anchor `{anchor}` is not tracked in version control (ADR-0062 § 3).");
        }
    }

    [Fact]
    public void Trust_anchor_folder_discloses_pre_ga_placeholder_provenance_while_custody_handover_is_open()
    {
        // HD-S11-001 is open: the committed `vendor-*` anchors are locally generated
        // GA-candidates, not authorized custody material. The obligation here is a
        // disclosure obligation, so asserting the disclosure text is the correct proof
        // shape; it is not standing in for a runtime or cryptographic gate (NFR-027).
        var readme = File.ReadAllText(Path.Combine(
            RepoRoot().FullName,
            "src",
            "Dobby.Host",
            "Trust",
            "README.md"));

        readme.ShouldContain("locally generated");
        readme.ShouldContain("HD-S11-001");
        readme.ShouldContain("must be replaced with the approved production public PEMs");
        readme.ShouldContain("matches by construction and is");
    }

    [Fact]
    public void Private_key_material_is_never_tracked_under_the_trust_anchor_folder()
    {
        var tracked = RunGit("ls-files", "--", "src/Dobby.Host/Trust")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        foreach (var path in tracked)
        {
            Path.GetFileName(path).ShouldNotContain("private", Case.Insensitive);
            if (!path.EndsWith(".pem", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CanonicalAnchorFileNames.ShouldContain(Path.GetFileName(path));
            File.ReadAllText(Path.Combine(RepoRoot().FullName, path))
                .ShouldNotContain("PRIVATE KEY", Case.Insensitive);
        }
    }

    [Fact]
    public void Production_assembly_embeds_bytes_identical_to_the_version_controlled_source_anchors()
    {
        var assembly = typeof(PluginTrustPublicKeyProvider).Assembly;
        var expected = new (string ResourceName, string FileName)[]
        {
            (PluginTrustPublicKeyProvider.ProductionResourceName, "vendor-plugin-package-public-key.pem"),
            (PluginTrustPublicKeyProvider.ProductionLicenseResourceName, "vendor-plugin-license-public-key.pem"),
            (VendorRecoveryPublicKey.ProductionResourceName, "vendor-recovery-public-key.pem")
        };

        var embeddedHashes = new List<string>();
        foreach (var (resourceName, fileName) in expected)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            stream.ShouldNotBeNull($"Production assembly is missing `{resourceName}`.");

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var embedded = Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
            embedded.ShouldBe(PemSha256(fileName), $"`{resourceName}` does not match `{fileName}`.");
            embeddedHashes.Add(embedded);
        }

        embeddedHashes.Distinct(StringComparer.Ordinal).Count().ShouldBe(expected.Length);
        embeddedHashes.ShouldNotContain(Sprint8BootstrapPemSha256);
    }

    private static string RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = RepoRoot().FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull("git must be available to verify trust-anchor version control.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, process.StandardError.ReadToEnd());

        return output.Replace("\r\n", "\n", StringComparison.Ordinal);
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
            .Single(element =>
                element.Name.LocalName == "EmbeddedResource" &&
                element.Attribute("Include")?.Value == expectedInclude &&
                element.Attribute("LogicalName")?.Value == expectedLogicalName);

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
