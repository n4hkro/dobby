using System.Text;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PluginManifestSchemaResourcePackagingTests
{
    private const string ManifestSchemaResourceName = "Dobby.PluginFramework.Schemas.dobby-plugin-manifest.schema.json";

    [Fact]
    public void Plugin_framework_embeds_manifest_schema_from_canonical_docs_file()
    {
        var assembly = typeof(PluginPackageReader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ManifestSchemaResourceName);

        stream.ShouldNotBeNull();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var embeddedSchema = reader.ReadToEnd();
        var docsSchema = File.ReadAllText(Path.Combine(
            RepoRoot().FullName,
            "docs",
            "schemas",
            "dobby-plugin-manifest.schema.json"));

        embeddedSchema.ShouldBe(docsSchema);
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
