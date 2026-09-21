using System.Diagnostics;
using System.Text.Json;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ReleaseExternalEvidenceValidatorTests
{
    private const string SourceCommit = "0123456789abcdef0123456789abcdef01234567";

    public static TheoryData<string, string[]> ValidGateCases => new()
    {
        {
            "installed-msix-upgrade",
            [
                "clean-install-n",
                "installed-launch-n",
                "upgrade-n-to-n-plus-1",
                "installed-launch-n-plus-1",
                "data-preserved",
                "runtime-root-verified",
                "uninstall-policy-verified",
            ]
        },
        {
            "production-signing",
            [
                "msix-signature-verified",
                "certificate-chain-verified",
                "timestamp-verified",
                "publisher-identity-verified",
                "customer-content-scan-passed",
            ]
        },
        { "license-review", ["dependency-license-review-approved"] },
    };

    [Theory]
    [MemberData(nameof(ValidGateCases))]
    public void Validator_accepts_commit_bound_evidence_with_all_required_claims(string gate, string[] claims)
    {
        var evidence = CreateEvidence(gate, SourceCommit, claims, new string('a', 64));

        var result = RunValidator(gate, SourceCommit, evidence);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("validated");
    }

    [Fact]
    public void Validator_rejects_evidence_for_a_different_source_commit()
    {
        var claims = ValidGateCases.First(row => (string)row[0] == "license-review")[1] as string[];
        var evidence = CreateEvidence("license-review", new string('f', 40), claims!, new string('a', 64));

        RunValidator("license-review", SourceCommit, evidence).ExitCode.ShouldNotBe(0);
    }

    [Fact]
    public void Validator_rejects_missing_required_claims_and_malformed_subject_hashes()
    {
        var missingClaim = CreateEvidence("installed-msix-upgrade", SourceCommit, ["clean-install-n"], new string('a', 64));
        var malformedHash = CreateEvidence(
            "license-review",
            SourceCommit,
            ["dependency-license-review-approved"],
            "not-a-sha256");

        RunValidator("installed-msix-upgrade", SourceCommit, missingClaim).ExitCode.ShouldNotBe(0);
        RunValidator("license-review", SourceCommit, malformedHash).ExitCode.ShouldNotBe(0);
    }

    [Fact]
    public void Validator_rejects_external_evidence_that_names_no_human()
    {
        var claims = new[] { "dependency-license-review-approved" };

        // Absent field: caught by the exact-property contract.
        RunValidator(
                "license-review",
                SourceCommit,
                CreateEvidenceWithoutAttestedBy("license-review", SourceCommit, claims, new string('a', 64)))
            .ExitCode.ShouldNotBe(0);

        // Present, but carrying no identity a reviewer could act on. Each must fail closed
        // rather than be normalized into an approval record:
        //   - empty, whitespace-only, or too short to name anyone;
        //   - control-character bearing, or untrimmed;
        //   - built from a non-ASCII space, so the schema pattern must exclude it explicitly;
        //   - carrying a zero-width character or a Unicode bidi embed/override. Those are the
        //     dangerous ones: they let a value that renders in a review UI as one person be a
        //     different string entirely, so a name that looks accountable is not. An
        //     unattributable approval is bad; a deceptively attributed one is worse.
        //
        // These values also pin the agreement between this gate and the attestedBy pattern in
        // docs/schemas/release-external-evidence-v1.schema.json. The schema is the published
        // shape and this script is the enforcing gate; a value one accepts and the other
        // rejects is a defect, and without executing both the divergence surfaces only at
        // release time.
        string[] unusableIdentities =
        [
            "",
            "   ",
            "x",
            "a\tb",
            "a\u007fb",
            "a\u0085b",
            " approver ",
            "\u00a0\u00a0\u00a0",
            "\u00a0approver",
            "approver\u00a0",
            "\u3000approver",
            "appro\u200bver",
            "appro\u200dver",
            "appro\u2060ver",
            "appro\ufeffver",
            "appro\u202cver",
            "\u202aapprover",
            "appro\u202ever"
        ];

        foreach (var attestedBy in unusableIdentities)
        {
            RunValidator(
                    "license-review",
                    SourceCommit,
                    CreateEvidence("license-review", SourceCommit, claims, new string('a', 64), attestedBy))
                .ExitCode.ShouldNotBe(
                    0,
                    "attestedBy [" + attestedBy.Replace("\u001b", "?") + "] must not be accepted " +
                    "as an attestation identity");
        }

        // The mirror of the above: an ordinary name, and one carrying an *inner* non-ASCII
        // space, must still be accepted. Without this the suite would pass just as well if the
        // gate rejected every identity, which would block every release rather than prove a
        // rule.
        foreach (var attestedBy in new[] { "Release Approver", "Release\u00a0Approver", "ops-lead" })
        {
            RunValidator(
                    "license-review",
                    SourceCommit,
                    CreateEvidence("license-review", SourceCommit, claims, new string('a', 64), attestedBy))
                .ExitCode.ShouldBe(
                    0,
                    "attestedBy [" + attestedBy + "] names a human and must be accepted");
        }
    }

    private static object CreateEvidence(
        string gate,
        string commit,
        string[] claims,
        string sha256,
        string attestedBy = "release-approver")
    {
        return new
        {
            schemaVersion = 1,
            gate,
            sourceCommit = commit,
            generatedAtUtc = "2026-08-22T12:00:00Z",
            status = "passed",
            attestedBy,
            claims,
            subjects = new[] { new { name = "candidate.bin", sha256 } },
        };
    }

    // The same record with attestedBy absent entirely -- the shape the validator accepted
    // before the field existed. `status` is pinned to "passed", so a conforming file records
    // an approval and nothing else; with no identity in the record that approval is
    // unattributable and no reviewer can afterwards ask who ran the external gate.
    private static object CreateEvidenceWithoutAttestedBy(string gate, string commit, string[] claims, string sha256)
    {
        return new
        {
            schemaVersion = 1,
            gate,
            sourceCommit = commit,
            generatedAtUtc = "2026-08-22T12:00:00Z",
            status = "passed",
            claims,
            subjects = new[] { new { name = "candidate.bin", sha256 } },
        };
    }

    private static ProcessResult RunValidator(string gate, string commit, object evidence)
    {
        var root = RepoRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dobby-release-evidence-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var evidencePath = Path.Combine(tempRoot, "evidence.json");
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence));
            var scriptPath = Path.Combine(root, "tools", "verify-release-candidate", "validate-external-evidence.ps1");

            var startInfo = new ProcessStartInfo("pwsh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-EvidencePath");
            startInfo.ArgumentList.Add(evidencePath);
            startInfo.ArgumentList.Add("-ExpectedGate");
            startInfo.ArgumentList.Add(gate);
            startInfo.ArgumentList.Add("-ExpectedSourceCommit");
            startInfo.ArgumentList.Add(commit);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start PowerShell.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("External evidence validator did not exit within 10 seconds.");
            }

            return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string RepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null && !File.Exists(Path.Combine(current, "Dobby.sln")))
        {
            current = Directory.GetParent(current)?.FullName;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
