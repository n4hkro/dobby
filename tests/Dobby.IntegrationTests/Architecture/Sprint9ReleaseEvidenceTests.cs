using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint9ReleaseEvidenceTests
{
    [Fact]
    public void Sprint9_evidence_records_license_runtime_ui_and_print_boundary_markers()
    {
        var evidence = Sprint9Evidence();

        evidence.ShouldContain("## Sprint 9 Implementation Evidence");
        evidence.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        evidence.ShouldContain("SPRINT9-LICENSE-ARTIFACT-V2");
        evidence.ShouldContain("SPRINT9-SIGNATURE-VALIDATION");
        evidence.ShouldContain("SPRINT9-CURRENT-CONTEXT");
        evidence.ShouldContain("SPRINT9-LICENSE-STATE-MIGRATION");
        evidence.ShouldContain("SPRINT9-RUNTIME-MODE-CAPABILITY");
        evidence.ShouldContain("SPRINT9-ACTIVATION-GATE");
        evidence.ShouldContain("SPRINT9-READONLY-GATES");
        evidence.ShouldContain("SPRINT9-LICENSE-UI");
        evidence.ShouldContain("SPRINT9-PRINT-BOUNDARY-SUNSET");
        evidence.ShouldContain("SPRINT9-BOUNDARY-VERIFICATION");
        evidence.ShouldContain("Unit 570, Integration 213, Smoke 6");
        evidence.ShouldContain("Dobby.Infrastructure -> Dobby.Application ProjectReference removed");
        evidence.ShouldNotContain("IsTemporaryPrintBoundaryEdge");
        evidence.ShouldNotContain("PrintBoundaryBlockerIsRecorded");
    }

    [Fact]
    public void Sprint9_evidence_records_license_lifecycle_inputs_migration_authority()
    {
        var evidence = Sprint9Evidence();
        var storageReview = Sprint9StorageReview();

        evidence.ShouldContain("SPRINT9-LICENSE-LIFECYCLE-INPUTS-MIGRATION");
        evidence.ShouldContain("0004_license_lifecycle_inputs");
        storageReview.ShouldContain("0004_license_lifecycle_inputs");
        storageReview.ShouldContain("post-login-only lifecycle input migration");
    }

    [Fact]
    public void Sprint9_promised_license_integration_test_files_exist_with_behavior_coverage()
    {
        var root = RepoRoot();
        var requiredFiles = new[]
        {
            (
                Path.Combine(root.FullName, "tests", "Dobby.IntegrationTests", "Licensing", "LicenseImportIntegrationTests.cs"),
                "public sealed class LicenseImportIntegrationTests",
                "LicenseImportService"),
            (
                Path.Combine(root.FullName, "tests", "Dobby.IntegrationTests", "Licensing", "LicenseRuntimeModeIntegrationTests.cs"),
                "public sealed class LicenseRuntimeModeIntegrationTests",
                "LicenseRuntimeModeProvider"),
            (
                Path.Combine(root.FullName, "tests", "Dobby.IntegrationTests", "Shell", "LicenseImportShellSurfaceTests.cs"),
                "public sealed class LicenseImportShellSurfaceTests",
                "ShellViewModel")
        };

        foreach (var (path, classMarker, behaviorMarker) in requiredFiles)
        {
            File.Exists(path).ShouldBeTrue($"{Path.GetFileName(path)} must exist because Sprint 9 named it as required integration coverage.");
            var text = File.ReadAllText(path);
            text.ShouldContain(classMarker);
            text.ShouldContain(behaviorMarker);
            text.ShouldContain("[Fact]");
        }
    }

    [Fact]
    public void Sprint9_payload_validator_tests_pin_manual_validation_to_canonical_schema_file()
    {
        var root = RepoRoot();
        var validatorTests = File.ReadAllText(Path.Combine(
            root.FullName,
            "tests",
            "Dobby.UnitTests",
            "Licensing",
            "LicensePayloadValidatorTests.cs"));

        validatorTests.ShouldContain("dobby-plugin-license-v3.schema.json");
        validatorTests.ShouldContain("Required_payload_fields_match_v3_schema");
        validatorTests.ShouldContain("Validate_accepts_only_granted_or_revoked_intent");
    }

    private static string Sprint9Evidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 9 Implementation Evidence", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextSection = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        return nextSection >= 0
            ? text[start..nextSection]
            : text[start..];
    }

    private static string Sprint9StorageReview()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "storage-materialization.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 9 License State Schema Review 2026-06-17", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var nextSection = text.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        return nextSection >= 0
            ? text[start..nextSection]
            : text[start..];
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
