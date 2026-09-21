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
            ["recovery-response-v2"] = "dobby-recovery-response-v2.schema.json",

            // Branch Shared runtime is deferred, so this schema had no committed fixture at
            // all: every rule it states -- the pinned profileVersion and the closed
            // credential-free property set -- was enforced by pattern alone with nothing
            // executing it. A deferred runtime is a reason the schema is the only gate, not a
            // reason to leave that gate unproven.
            ["branchshared"] = "branchshared.schema.json"
        };

    // A negative fixture is only evidence for the rule it is named after. `IsValid == false`
    // alone does not prove that: a fixture can drift into failing for an unrelated reason
    // (a stale identifier, a tightened shared `$defs`) and keep passing while the rule it was
    // written for goes untested. Each fixture therefore declares the complete set of instance
    // locations the schema must reject it at, so a newly introduced second violation is a test
    // failure rather than a silent replacement of the rule under test (NFR-027, ADR-0061).
    private static readonly IReadOnlyDictionary<string, string[]> NegativeFixtureRejectionLocations =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // The credential-bearing fixture carries the `<redacted>` placeholder as its
            // value, not a secret-shaped literal. Nothing is lost: `branchshared.schema.json`
            // closes `connectionProfile` with `additionalProperties: false`, so the rejection
            // is triggered by the property *name* and the value is never examined -- while a
            // secret-shaped literal would have to be excused in
            // `NoPlaintextSecretContentScanTests`, and every such excuse widens the committed
            // secret scan for the sake of a byte the schema does not read. Do not "restore" a
            // realistic value here. Removing the field itself is already a failure: the
            // declared rejection location below would no longer be reported.
            ["branchshared.credential-bearing-field.invalid.json"] = ["/connectionProfile/password"],
            ["branchshared.unresolvable-host.invalid.json"] = ["/connectionProfile/host"],
            ["branchshared.unpinned-profile-version.invalid.json"] = ["/connectionProfile/profileVersion"],
            ["branch-setup-authorization-v1.cross-ring-keyid.invalid.json"] = ["/payload/keyId", "/signature/keyId"],
            ["branch-setup-authorization-v1.date-range-overflow.invalid.json"] = ["/payload/issuedAt"],
            ["branch-setup-authorization-v1.monthless-keyid.invalid.json"] = ["/payload/keyId", "/signature/keyId"],
            ["branch-setup-authorization-v1.license-purpose.invalid.json"] = ["/payload/purpose", "/signature/purpose"],
            ["branch-setup-authorization-v1.unbounded-id.invalid.json"] = ["/payload/authorizationId"],
            ["plugin-license-v3.cross-ring-keyid.invalid.json"] = ["/payload/keyId", "/signature/keyId"],
            ["plugin-license-v3.date-range-overflow.invalid.json"] = ["/payload/issuedAt"],
            ["plugin-license-v3.extra-authority.invalid.json"] = ["/payload/status"],
            ["plugin-license-v3.monthless-keyid.invalid.json"] = ["/payload/keyId", "/signature/keyId"],
            ["plugin-license-v3.non-utc-date.invalid.json"] = ["/payload/issuedAt"],
            ["plugin-license-v3.revision-overflow.invalid.json"] = ["/payload/licenseRevision"],
            ["plugin-license-v3.signed-runtime-state.invalid.json"] = ["/payload/intent"],
            ["plugin-manifest-v2.child-without-labelkey.invalid.json"] = ["/extensionPoints/1"],
            ["plugin-manifest-v2.cross-purpose.invalid.json"] = ["/purpose", "/signature/purpose"],
            ["plugin-manifest-v2.cross-ring-keyid.invalid.json"] = ["/signature/keyId"],
            ["plugin-manifest-v2.monthless-keyid.invalid.json"] = ["/signature/keyId"],
            ["plugin-manifest-v2.nondefault-group.invalid.json"] = ["/extensionPoints/0/group"],
            ["plugin-manifest-v2.sidebar-without-labelkey.invalid.json"] = ["/extensionPoints/0"],
            ["plugin-manifest-v2.unbounded-path.invalid.json"] = ["/entryAssembly", "/files/0/path"],
            ["recovery-request-v2.cross-ring-keyid.invalid.json"] = ["/payload/vendorKeyId"],
            ["recovery-request-v2.date-range-overflow.invalid.json"] = ["/payload/expiresAt"],
            ["recovery-request-v2.device-selector-in-vendor-slot.invalid.json"] = ["/payload/vendorKeyId"],
            ["recovery-request-v2.monthless-keyid.invalid.json"] = ["/payload/vendorKeyId"],
            ["recovery-request-v2.missing-proof.invalid.json"] = [""],
            ["recovery-request-v2.short-nonce.invalid.json"] = ["/payload/nonce"],
            ["recovery-request-v2.vendor-selector-in-device-slot.invalid.json"] = ["/possessionProof/keyId"],
            ["recovery-response-v2.cross-purpose.invalid.json"] = ["/signature/purpose"],
            ["recovery-response-v2.cross-ring-keyid.invalid.json"] = ["/payload/keyId", "/signature/keyId"],
            ["recovery-response-v2.date-range-overflow.invalid.json"] = ["/payload/issuedAt"],
            ["recovery-response-v2.monthless-keyid.invalid.json"] = ["/payload/keyId", "/signature/keyId"],
            ["recovery-response-v2.wrong-action.invalid.json"] = ["/payload/action"]
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
        var fixtureName = Path.GetFileName(fixturePath);
        NegativeFixtureRejectionLocations.TryGetValue(fixtureName, out var expectedLocations)
            .ShouldBeTrue(
                $"{fixtureName} must declare the instance locations whose rejection it is evidence for");

        var results = Evaluate(fixturePath);
        results.IsValid.ShouldBeFalse(fixturePath);

        RejectedInstanceLocations(results).ShouldBe(
            expectedLocations!.Order(StringComparer.Ordinal),
            ignoreOrder: false,
            customMessage:
            $"{fixtureName} must be rejected at exactly the locations it is named after; a " +
            "different or additional location means the fixture no longer tests its own rule");
    }

    [Fact]
    public void Every_negative_fixture_declares_its_rejection_locations()
    {
        var negativeNames = Directory.GetFiles(FixtureDirectory("negative"), "*.json")
            .Select(Path.GetFileName)
            .ToArray();

        NegativeFixtureRejectionLocations.Keys.Order(StringComparer.Ordinal)
            .ShouldBe(
                negativeNames.Order(StringComparer.Ordinal)!,
                "the declared rejection locations and the committed negative fixtures must be " +
                "the same set, so neither an undocumented fixture nor a stale entry can hide");
    }

    // Where a document was rejected, as instance locations. `Evaluate` asks for
    // `OutputFormat.List`, so the result is a flat roster of evaluated nodes rather than a
    // tree: each node carries its own evaluation path, its instance location, and any errors
    // recorded against it.
    private static IReadOnlyList<string> RejectedInstanceLocations(EvaluationResults results)
    {
        var nodes = new List<EvaluationResults>();
        Flatten(results);

        // Errors recorded outside a branching subschema name the rejection directly: the node
        // owning the violated keyword is the node reporting the failure.
        var locations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node.Errors is { Count: > 0 } && !IsBranchingSubschema(node))
            {
                locations.Add(node.InstanceLocation.ToString());
            }
        }

        // A failure recorded *inside* an `if`, `not`, `anyOf` or `oneOf` subschema is usually
        // bookkeeping rather than a rejection: `if` records its condition failing even though
        // that is precisely how `else` gets selected, `not` needs its child to fail in order to
        // succeed, and under `anyOf`/`oneOf` every losing branch is expected to fail whenever a
        // sibling branch matches. Reporting those instance locations would let the shape of a
        // constraint masquerade as the rule under test.
        //
        // Dropping them unconditionally is the opposite error, and it is the one this harness
        // made: when *no* branch of an `anyOf` matches, every failure sits inside a branch, so
        // an invalid document produced zero rejection locations -- the harness still saw the
        // rejection but could no longer say where it happened, which is the one thing these
        // fixtures exist to pin. So attribute such a failure to the node that owns the
        // branching keyword, guarded by that owner's own validity: a satisfied `anyOf`/`if`/`not`
        // is not a rejection at all, however many branches failed beneath it. When every branch
        // fails the owner genuinely is a rejection site, so it is reported alongside any more
        // specific violation in the same subtree rather than instead of it -- two real rejections
        // are two locations.
        //
        // `branchshared.schema.json` is the first fixture-covered family whose shape uses
        // `anyOf` -- its `host` accepts DNS, IPv4 or bracketed IPv6 -- so it is both what
        // exposed the gap and what pins the fix: `branchshared.unresolvable-host.invalid.json`
        // matches none of the three alternatives and must still be rejected at exactly
        // `/connectionProfile/host`, while the other two `branchshared` negatives carry a valid
        // host whose two losing branches must stay invisible.
        foreach (var node in nodes)
        {
            if (node.Errors is not { Count: > 0 } || !IsBranchingSubschema(node))
            {
                continue;
            }

            var ownerPath = BranchOwnerEvaluationPath(node);
            var owner = nodes.Find(candidate =>
                string.Equals(candidate.EvaluationPath.ToString(), ownerPath, StringComparison.Ordinal));

            if (owner is null || owner.IsValid)
            {
                continue;
            }

            locations.Add(owner.InstanceLocation.ToString());
        }

        return [.. locations];

        void Flatten(EvaluationResults node)
        {
            nodes.Add(node);
            foreach (var detail in node.Details ?? [])
            {
                Flatten(detail);
            }
        }
    }

    // True when this node was reached through an `if`, `not`, `anyOf` or `oneOf` keyword, so its
    // failure is a branch outcome rather than a rejection in its own right.
    private static bool IsBranchingSubschema(EvaluationResults node)
    {
        return BranchOwnerEvaluationPath(node) is not null;
    }

    // The evaluation path of the node owning the outermost branching keyword above this one, or
    // null when there is none. `/properties/host/anyOf/0` is owned by `/properties/host`.
    private static string? BranchOwnerEvaluationPath(EvaluationResults node)
    {
        var segments = node.EvaluationPath.ToString().Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index] is "if" or "not" or "anyOf" or "oneOf")
            {
                return string.Join('/', segments[..index]);
            }
        }

        return null;
    }

    // Vendor-signed artifact families: every one of these carries at least one
    // `<purpose-short>-<prod|test>-<YYYY-MM>` selector, so each must prove BOTH selector
    // rules from `docs/schemas/artifact-compatibility-matrix.md` § `Vendor selector
    // namespace fail-closed` -- a cross-ring selector and a month-less selector. A bare
    // ">= 2 negative fixtures" count cannot express that: it was satisfied while only
    // plugin-license-v3 actually pinned the month rule, leaving the other three families'
    // `<YYYY-MM>` requirement enforced by pattern alone with no committed evidence.
    // Every family whose payload carries a vendor `keyId` drawn from a ring-pinned namespace
    // (`ADR-0062` § 1). `recovery-request-v2` belongs here: its `payload.vendorKeyId` names the
    // expected vendor recovery key and is `$ref`-ed to the same ring-pinned `$defs/keyId` as the
    // response. It was previously exempted on the false premise that the request had no vendor
    // selector slot, which left the request pattern free to be loosened with every test still
    // green. The disjoint `device-` possession-proof namespace is a *separate* obligation,
    // covered by the selector-swap fixtures, not a substitute for this one.
    private static readonly string[] VendorSelectorFixturePrefixes =
    [
        "branch-setup-authorization-v1",
        "plugin-license-v3",
        "plugin-manifest-v2",
        "recovery-request-v2",
        "recovery-response-v2"
    ];

    [Fact]
    public void Every_vendor_selector_family_pins_both_cross_ring_and_monthless_rejection()
    {
        var negativeNames = Directory.GetFiles(FixtureDirectory("negative"), "*.json")
            .Select(Path.GetFileName)
            .ToArray();

        foreach (var prefix in VendorSelectorFixturePrefixes)
        {
            foreach (var rule in new[] { "cross-ring-keyid", "monthless-keyid" })
            {
                negativeNames.ShouldContain(
                    $"{prefix}.{rule}.invalid.json",
                    $"{prefix} carries a vendor keyId, so the {rule} rule needs a committed " +
                    "negative fixture; the schema pattern alone is not evidence");
            }
        }
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
