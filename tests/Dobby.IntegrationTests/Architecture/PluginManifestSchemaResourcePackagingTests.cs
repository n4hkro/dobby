using System.Text;
using System.Text.Json;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginManifestSchemaResourcePackagingTests
{
    private const string V2ResourceName = "Dobby.PluginFramework.Schemas.dobby-plugin-manifest-v2.schema.json";
    private const string V1ResourceName = "Dobby.PluginFramework.Schemas.dobby-plugin-manifest.schema.json";

    [Theory]
    [InlineData(V2ResourceName, "dobby-plugin-manifest-v2.schema.json")]
    [InlineData(V1ResourceName, "dobby-plugin-manifest.schema.json")]
    public void Embedded_manifest_schema_is_byte_identical_to_its_canonical_docs_file(
        string resourceName,
        string docsFileName)
    {
        var embeddedSchema = ReadEmbeddedResource(resourceName);
        var docsSchema = File.ReadAllText(Path.Combine(
            RepoRoot().FullName,
            "docs",
            "schemas",
            docsFileName));

        embeddedSchema.ShouldBe(
            docsSchema,
            $"docs/schemas/{docsFileName} is the documented authority; if the embedded copy drifts, " +
            "installation is gated by a contract nobody reviewed");
    }

    [Fact]
    public void Embedded_v2_schema_declares_the_only_accepted_manifest_schema_version()
    {
        using var v2 = JsonDocument.Parse(ReadEmbeddedResource(V2ResourceName));

        v2.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetInt32()
            .ShouldBe(
                2,
                "docs/schemas/artifact-compatibility-matrix.md declares v2 the only accepted manifest " +
                "version, so the shipped v2 schema must pin schemaVersion to 2");
        v2.RootElement.GetProperty("required").EnumerateArray().Select(value => value.GetString())
            .ShouldContain("schemaVersion");
    }

    [Fact]
    public void Embedded_v1_schema_is_shipped_only_as_a_frozen_negative_reference()
    {
        using var v1 = JsonDocument.Parse(ReadEmbeddedResource(V1ResourceName));

        v1.RootElement.GetProperty("title").GetString().ShouldBe(
            "Dobby Plugin Manifest v1 (Legacy)",
            "the v1 resource must stay self-describing as frozen legacy so no reader mistakes it for " +
            "the enforced contract");
        v1.RootElement.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        v1.RootElement.GetProperty("properties").TryGetProperty("schemaVersion", out _).ShouldBeFalse(
            "v1 and v2 manifests are mutually exclusive shapes: v1 rejects the v2 discriminator " +
            "members as additional properties, which is what makes " +
            "PluginManifestSchemaEnforcementTests a behavioral proof that the enforced embedded " +
            "schema is v2 and never the frozen v1 file");
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        using var stream = typeof(PluginPackageReader).Assembly.GetManifestResourceStream(resourceName);

        stream.ShouldNotBeNull(
            $"{resourceName} must stay embedded so validation never depends on a runtime file path");
        using var reader = new StreamReader(stream, Encoding.UTF8);

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
}
