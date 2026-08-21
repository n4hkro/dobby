using System.Text;
using System.Text.Json;
using Dobby.Admin.Commands;
using Dobby.Licensing;
using Shouldly;

namespace Dobby.UnitTests.Licensing;

public sealed class LicenseV3ContractTests
{
    [Fact]
    public void Contract_freezes_purpose_bounds_and_authority_intent()
    {
        LicenseV3Contract.SchemaVersion.ShouldBe(3);
        LicenseV3Contract.Purpose.ShouldBe("dobby.plugin-license");
        LicenseV3Contract.MaximumArtifactBytes.ShouldBe(256 * 1024);
        Enum.GetNames<LicenseGrantIntentV3>().ShouldBe(["Granted", "Revoked"]);
    }

    [Fact]
    public void Canonical_json_orders_properties_and_normalizes_unicode_to_nfc()
    {
        using var document = JsonDocument.Parse("""{"z":1,"name":"e\u0301","a":2147483647}""");

        var canonical = Encoding.UTF8.GetString(LicenseCanonicalJsonWriter.Write(document.RootElement));

        canonical.ShouldBe("{\"a\":2147483647,\"name\":\"é\",\"z\":1}");
    }

    [Fact]
    public void Lifecycle_truth_table_has_only_derived_runtime_states_and_checked_overflow_rule()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "docs",
            "schemas",
            "license-v3-lifecycle-truth-table.md");
        var truthTable = File.ReadAllText(path);

        truthTable.ShouldContain("malformed, bad signature");
        truthTable.ShouldContain("Pending");
        truthTable.ShouldContain("Active");
        truthTable.ShouldContain("Grace");
        truthTable.ShouldContain("ReadOnlyExpired");
        truthTable.ShouldContain("checked arithmetic");
        truthTable.ShouldContain("status` never overrides");
    }

    [Fact]
    public void Standalone_admin_tool_does_not_treat_license_as_branch_setup_authority()
    {
        var result = AdminCommandHost.Run(["--cli", "create-branch-setup-authorization"]);

        result.ExitCode.ShouldNotBe(0);
        result.StandardError.ShouldContain("unknown-command");
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
