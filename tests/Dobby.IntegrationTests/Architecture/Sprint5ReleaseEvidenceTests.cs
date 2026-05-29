using System.IO;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class Sprint5ReleaseEvidenceTests
{
    [Fact]
    public void Sprint5_reconfirmation_evidence_records_implemented_and_verified_closure()
    {
        var sprint5 = Sprint5ArchitectureEvidence();

        sprint5.ShouldContain("## Sprint 5 Reconfirmation");
        sprint5.ShouldContain("Status: IMPLEMENTED AND VERIFIED.");
        sprint5.ShouldContain("Final verification on 2026-05-19");
        sprint5.ShouldContain("333 total tests, 0 failures");
        sprint5.ShouldContain("Recovery fixture evidence status: COMPLETE.");
        sprint5.ShouldNotContain("Status: PARTIAL — pending gaps.");
        sprint5.ShouldNotContain("Pending gap inventory:");
    }

    [Fact]
    public void Sprint5_reconfirmation_evidence_records_primary_ac_matrix_with_named_tests()
    {
        var sprint5 = Sprint5ArchitectureEvidence();

        foreach (var required in new[]
                 {
                     "### Sprint 5 AC Matrix",
                     "AC-001",
                     "PreLoginAllowedWorkloadTests",
                     "AC-002",
                     "OwnerBootstrapTransactionTests",
                     "AC-007",
                     "RecoveryResponseImportFixtureTests",
                     "RecoveryViewModelFileInteractionTests",
                     "AC-008",
                     "LoginServiceTests",
                     "AC-025",
                     "LoginViewModelRememberMeTests",
                     "AC-026",
                     "LoginViewModelThrottleStateTests",
                     "AC-028",
                     "ActiveSessionAccessorTests",
                     "AC-029",
                     "SelfServicePasswordChangeTests",
                     "PostLoginSessionRuntimeTests",
                     "AC-044",
                     "SingleInstanceCoordinatorTests",
                     "AC-045",
                     "UsernameCanonicalValidatorTests",
                     "PasswordPolicyValidatorTests",
                     "AC-050",
                     "SessionTimeoutIntegrationTests",
                     "AC-051",
                     "KdfPolicyResolverTests",
                     "CredentialStoreRoundTripTests"
                 })
        {
            sprint5.ShouldContain(required);
        }
    }

    [Fact]
    public void Sprint5_kdf_and_recovery_fixture_evidence_is_complete()
    {
        var root = RepoRoot().FullName;
        var kdfEvidence = File.ReadAllText(Path.Combine(root, "artifacts", "release-evidence", "skeleton-local", "auth-kdf-selection.md"));
        var fixtureEvidence = File.ReadAllText(Path.Combine(root, "artifacts", "release-evidence", "skeleton-local", "test-fixture-authority.md"));

        kdfEvidence.ShouldContain("## Sprint 5 Implementation Complete");
        fixtureEvidence.ShouldNotContain("PENDING_IMPLEMENTATION_EVIDENCE");
        fixtureEvidence.ShouldContain("tests/Dobby.IntegrationTests/Recovery/Fixtures/TEST-ONLY/signature-ok.TEST-ONLY.dobbyrecovery");
        fixtureEvidence.ShouldContain("1a30c878be9f0d69adcfbad0a7244e19738a3c81fb9ca7eb2581a8b1c2455a19");
    }

    private static string Sprint5ArchitectureEvidence()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "architecture-baseline.md");

        var text = File.ReadAllText(path);
        var start = text.IndexOf("## Sprint 5 Reconfirmation", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);

        return text[start..];
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
