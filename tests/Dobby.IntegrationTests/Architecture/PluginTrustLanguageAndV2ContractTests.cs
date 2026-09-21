using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dobby.Core;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginTrustLanguageAndV2ContractTests
{
    private const string EmbeddedV2SchemaResourceName =
        "Dobby.PluginFramework.Schemas.dobby-plugin-manifest-v2.schema.json";

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
            "docs/adr/ADR-0010-plugin-communication-policy.md",
            "docs/adr/ADR-0011-plugin-trust-and-policy-enforcement-principle.md",
            "docs/adr/ADR-0012-plugin-capability-boundary.md",
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

        // NFR-027 / ADR-0061: reflection over a record that no reader consumes is not
        // proof of a contract. The enforced v2 contract lives in the schema document
        // `PluginManifestSchemaValidator` loads at runtime, so assert against that
        // embedded copy and against its identity with the documented schema.
        var assembly = typeof(PluginManifestContract).Assembly;
        var resources = assembly.GetManifestResourceNames();
        resources.ShouldContain("Dobby.PluginFramework.Schemas.dobby-plugin-manifest.schema.json");
        resources.ShouldContain(EmbeddedV2SchemaResourceName);

        var embeddedSchemaText = ReadEmbeddedV2Schema(assembly);
        embeddedSchemaText.ShouldBe(
            File.ReadAllText(Path.Combine(
                RepoRoot().FullName, "docs", "schemas", "dobby-plugin-manifest-v2.schema.json")),
            "the schema the reader enforces must be the documented schema, not a drifted copy");

        var embeddedSchema = JsonNode.Parse(embeddedSchemaText)!.AsObject();
        embeddedSchema["properties"]!["schemaVersion"]!["const"]!.GetValue<int>()
            .ShouldBe(PluginManifestContract.CurrentSchemaVersion);

        // ADR-0045 / ADR-0059 / ADR-0062 § 1: exactly five signature properties, so the
        // retired numeric `keyVersion` selector has no interpretation path, and exactly
        // one signature encoding, so there is no DER/P1363 ambiguity.
        var signature = embeddedSchema["$defs"]!["pluginSignature"]!.AsObject();
        signature["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
        string[] signatureProperties = ["purpose", "algorithm", "format", "keyId", "value"];
        signature["required"]!.AsArray().Select(node => node!.GetValue<string>())
            .ShouldBe(signatureProperties);
        signature["properties"]!.AsObject().Select(property => property.Key)
            .ShouldBe(signatureProperties);
        signature["properties"]!["purpose"]!["const"]!.GetValue<string>()
            .ShouldBe(PluginManifestContract.PackagePurpose);
        signature["properties"]!["algorithm"]!["const"]!.GetValue<string>()
            .ShouldBe(PluginManifestContract.SignatureAlgorithm);
        signature["properties"]!["format"]!["const"]!.GetValue<string>()
            .ShouldBe(PluginManifestContract.SignatureFormat);
        embeddedSchema["$defs"]!["base64urlP1363"]!["minLength"]!.GetValue<int>().ShouldBe(86);
        embeddedSchema["$defs"]!["base64urlP1363"]!["maxLength"]!.GetValue<int>().ShouldBe(86);

        // The record the reader actually projects a manifest signature into must expose
        // exactly those five members; a sixth would reopen the retired-selector surface.
        typeof(PluginPackageSignature)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ShouldBe(["Purpose", "Algorithm", "Format", "KeyId", "Value"], ignoreOrder: true);
    }

    [Fact]
    public void Manifest_reader_refuses_a_signature_block_the_embedded_v2_schema_forbids()
    {
        // Behavioural half of the contract: the embedded schema is not merely present in
        // the assembly, it is the gate a package must clear. A block carrying the retired
        // numeric `keyVersion` selector is refused, so the structural assertions above
        // describe enforced behaviour rather than a checked-in document (NFR-027,
        // ADR-0061).
        using var temp = new TemporaryDirectory();

        var canonicalSignature =
            "{\"purpose\":\"dobby.plugin-package\",\"algorithm\":\"ECDSA-P256-SHA256\"," +
            "\"format\":\"IeeeP1363FixedFieldConcatenation\",\"keyId\":\"plugin-prod-2026-01\"," +
            "\"value\":\"" + new string('A', 86) + "\"}";
        var retiredSelectorSignature = canonicalSignature.Replace(
            "\"keyId\":\"plugin-prod-2026-01\"",
            "\"keyId\":\"plugin-prod-2026-01\",\"keyVersion\":1",
            StringComparison.Ordinal);
        retiredSelectorSignature.ShouldNotBe(canonicalSignature);

        ReadPackage(temp.Path, canonicalSignature).IsSuccess.ShouldBeTrue(
            "the baseline package must read successfully, so the reject below is " +
            "attributable to the retired selector alone");
        ReadPackage(temp.Path, retiredSelectorSignature).ErrorCode.ShouldBe(
            ErrorCode.InvalidInput,
            "additionalProperties: false on pluginSignature must refuse the retired " +
            "numeric keyVersion selector (ADR-0062 § 1, ADR-0045)");
    }

    private static Result<PluginPackageArchive> ReadPackage(string root, string signatureBlock)
    {
        var entryAssemblyContent = Encoding.UTF8.GetBytes("v2 contract entry assembly");
        var fileContent = Encoding.UTF8.GetBytes("v2 contract payload");
        var manifestJson =
            "{\"schemaVersion\":2,\"purpose\":\"dobby.plugin-package\"," +
            "\"pluginId\":\"com.dobby.v2contract\",\"version\":\"1.0.0\"," +
            "\"vendorId\":\"dobby.vendor\",\"hostApiVersion\":\"2.0\",\"minHostVersion\":\"1.0.0\"," +
            "\"entryAssembly\":\"bin/plugins/com.dobby.v2contract.dll\"," +
            "\"displayName\":\"V2 Contract\",\"description\":\"V2 contract package\"," +
            "\"dependsOn\":[],\"capabilities\":[{\"name\":\"plugin-page\",\"scope\":\"plugin-page\"}]," +
            "\"operationTargets\":[],\"extensionPoints\":[],\"files\":[" +
            "{\"path\":\"bin/plugins/com.dobby.v2contract.dll\",\"sha256\":\"" + HashHex(entryAssemblyContent) + "\"}," +
            "{\"path\":\"content/readme.txt\",\"sha256\":\"" + HashHex(fileContent) + "\"}]," +
            "\"signature\":" + signatureBlock + "}";

        var packagePath = Path.Combine(root, $"{Guid.NewGuid():N}.dobbyplugin");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
            WriteEntry(archive, "bin/plugins/com.dobby.v2contract.dll", entryAssemblyContent);
            WriteEntry(archive, "content/readme.txt", fileContent);
        }

        return PluginPackageReader.Read(packagePath);
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        using var stream = archive.CreateEntry(path, CompressionLevel.Optimal).Open();
        stream.Write(content);
    }

    private static string HashHex(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static string ReadEmbeddedV2Schema(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream(EmbeddedV2SchemaResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema resource '{EmbeddedV2SchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dobby-v2-contract-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
