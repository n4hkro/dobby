using System.Reflection;
using System.Text;
using System.Text.Json;
using Dobby.PluginFramework.Manifest;
using Json.Schema;

namespace Dobby.PluginFramework.Packaging;

internal static class PluginManifestSchemaValidator
{
    private const string ManifestSchemaResourceName = "Dobby.PluginFramework.Schemas.dobby-plugin-manifest-v2.schema.json";

    private static readonly Lazy<JsonSchema> ManifestSchema = new(LoadManifestSchema);

    public static IReadOnlyList<PluginManifestValidationReason> Validate(JsonElement manifestRoot)
    {
        var result = ManifestSchema.Value.Evaluate(
            manifestRoot,
            new EvaluationOptions
            {
                OutputFormat = OutputFormat.Flag
            });

        return result.IsValid
            ? []
            : [new PluginManifestValidationReason(
                "invalid-manifest",
                "Manifest does not match docs/schemas/dobby-plugin-manifest-v2.schema.json.")];
    }

    private static JsonSchema LoadManifestSchema()
    {
        var assembly = typeof(PluginManifestSchemaValidator).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ManifestSchemaResourceName)
            ?? throw new InvalidOperationException($"Embedded plugin manifest schema resource '{ManifestSchemaResourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return JsonSchema.FromText(
            reader.ReadToEnd(),
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry()
            });
    }
}
