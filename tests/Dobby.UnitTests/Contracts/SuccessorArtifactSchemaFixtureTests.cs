using System.Text.Json;
using Json.Schema;
using Shouldly;

namespace Dobby.UnitTests.Contracts;

public sealed class SuccessorArtifactSchemaFixtureTests
{
    private static readonly IReadOnlyDictionary<string, string> SchemaByFixturePrefix =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plugin-manifest-v2"] = "dobby-plugin-manifest-v2.schema.json",
            ["plugin-license-v3"] = "dobby-plugin-license-v3.schema.json",
            ["branch-setup-authorization-v1"] = "dobby-branch-setup-authorization-v1.schema.json",
            ["recovery-request-v2"] = "dobby-recovery-request-v2.schema.json",
            ["recovery-response-v2"] = "dobby-recovery-response-v2.schema.json"
        };

    public static TheoryData<string> PositiveFixtures()
    {
        return Fixtures("positive");
    }

    public static TheoryData<string> NegativeFixtures()
    {
        return Fixtures("negative");
    }

    [Theory]
    [MemberData(nameof(PositiveFixtures))]
    public void Successor_schema_accepts_committed_positive_fixture(string fixturePath)
    {
        Evaluate(fixturePath).IsValid.ShouldBeTrue(fixturePath);
    }

    [Theory]
    [MemberData(nameof(NegativeFixtures))]
    public void Successor_schema_rejects_committed_negative_fixture(string fixturePath)
    {
        Evaluate(fixturePath).IsValid.ShouldBeFalse(fixturePath);
    }

    [Fact]
    public void Every_successor_contract_has_positive_and_bounded_negative_evidence()
    {
        var positiveNames = Directory.GetFiles(FixtureDirectory("positive"), "*.json")
            .Select(Path.GetFileName)
            .ToArray();
        var negativeNames = Directory.GetFiles(FixtureDirectory("negative"), "*.json")
            .Select(Path.GetFileName)
            .ToArray();

        foreach (var prefix in SchemaByFixturePrefix.Keys)
        {
            positiveNames.Count(name => name!.StartsWith(prefix + ".", StringComparison.Ordinal)).ShouldBe(1);
            negativeNames.Count(name => name!.StartsWith(prefix + ".", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public void Legacy_schemas_are_frozen_and_transition_matrix_has_no_silent_fallback()
    {
        var schemaRoot = SchemaDirectory();
        File.ReadAllText(Path.Combine(schemaRoot, "dobby-plugin-manifest.schema.json"))
            .ShouldContain("Frozen legacy v1");
        File.ReadAllText(Path.Combine(schemaRoot, "dobby-plugin-license.schema.json"))
            .ShouldContain("Frozen legacy v2");

        var matrix = File.ReadAllText(Path.Combine(schemaRoot, "artifact-compatibility-matrix.md"));
        matrix.ShouldContain("There is no silent fallback");
        matrix.ShouldContain("Reject v1 authorization");
        matrix.ShouldContain("no Standalone runtime handler");
    }

    private static EvaluationResults Evaluate(string fixturePath)
    {
        var prefix = SchemaByFixturePrefix.Keys.Single(candidate =>
            Path.GetFileName(fixturePath).StartsWith(candidate + ".", StringComparison.Ordinal));
        var schema = JsonSchema.FromText(
            File.ReadAllText(Path.Combine(SchemaDirectory(), SchemaByFixturePrefix[prefix])),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        return schema.Evaluate(fixture.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
    }

    private static TheoryData<string> Fixtures(string kind)
    {
        var data = new TheoryData<string>();
        foreach (var fixture in Directory.GetFiles(FixtureDirectory(kind), "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(fixture);
        }

        return data;
    }

    private static string FixtureDirectory(string kind)
    {
        return Path.Combine(RepoRoot().FullName, "tests", "Dobby.UnitTests", "ContractFixtures", kind);
    }

    private static string SchemaDirectory()
    {
        return Path.Combine(RepoRoot().FullName, "docs", "schemas");
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
