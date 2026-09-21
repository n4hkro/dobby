using System.Text.Json;
using System.Text.Json.Nodes;
using Dobby.PluginFramework.Packaging;
using Shouldly;

namespace Dobby.UnitTests.PluginFramework.Packaging;

/// <summary>
/// Behavioral proof (NFR-027) that manifest schema validation enforces the v2 successor schema.
/// The frozen v1 schema is also embedded in the assembly, so resource presence alone proves
/// nothing: only running the validator over shapes the two schemas disagree about shows which
/// contract actually gates installation.
/// </summary>
public sealed class PluginManifestSchemaEnforcementTests
{
    [Fact]
    public void Validation_accepts_the_canonical_v2_shape_that_the_frozen_v1_schema_forbids()
    {
        // schemaVersion, purpose and hostApiVersion are v2-only members, and the frozen v1 schema
        // sets additionalProperties: false. A validator still bound to the v1 resource would
        // therefore reject this canonical manifest outright.
        using var canonical = JsonDocument.Parse(PluginPackageTestData.CreateManifestJson());

        PluginManifestSchemaValidator.Validate(canonical.RootElement).ShouldBeEmpty(
            "docs/schemas/artifact-compatibility-matrix.md declares v2 the only accepted manifest " +
            "version, so the enforced schema must accept the canonical v2 shape");
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("purpose")]
    [InlineData("hostApiVersion")]
    public void Validation_rejects_a_v1_shaped_manifest_that_omits_a_v2_required_member(string omittedMember)
    {
        var v1Shaped = WithoutRootMember(PluginPackageTestData.CreateManifestJson(), omittedMember);
        using var document = JsonDocument.Parse(v1Shaped);

        PluginManifestSchemaValidator.Validate(document.RootElement)
            .Select(reason => reason.Code)
            .ShouldContain(
                "invalid-manifest",
                $"'{omittedMember}' is required by v2 and unknown to v1, so a manifest without it " +
                "must fail schema validation; accepting it would mean the frozen v1 schema is the " +
                "one being enforced");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(3)]
    public void Validation_rejects_any_manifest_schema_version_other_than_two(int schemaVersion)
    {
        var mutated = WithRootMember(
            PluginPackageTestData.CreateManifestJson(),
            "schemaVersion",
            JsonValue.Create(schemaVersion));
        using var document = JsonDocument.Parse(mutated);

        PluginManifestSchemaValidator.Validate(document.RootElement)
            .Select(reason => reason.Code)
            .ShouldContain(
                "invalid-manifest",
                "the v2 schema pins schemaVersion with a const, so the schema stage must reject a " +
                "legacy or unknown version before any signature or file verification work happens");
    }

    private static string WithoutRootMember(string manifestJson, string memberName)
    {
        var root = JsonNode.Parse(manifestJson)!.AsObject();
        root.Remove(memberName).ShouldBeTrue($"fixture must contain '{memberName}' before removing it");

        return root.ToJsonString();
    }

    private static string WithRootMember(string manifestJson, string memberName, JsonNode value)
    {
        var root = JsonNode.Parse(manifestJson)!.AsObject();
        root[memberName] = value;

        return root.ToJsonString();
    }
}
