using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ProductionReleasePackagingStaticScanTests
{
    // 2026-07 GA-candidate single master key hash. That key model is retired by
    // ADR-0062 (supersedes ADR-0033); the value is kept only as a negative control
    // so no current purpose-separated anchor can silently equal it.
    private const string RetiredMasterKeyPemSha256 = "74ca9ee78d453e122d86047c90fd174e78fa1828647404e03122b6d7ab39201f";
    private const string Sprint8BootstrapPemSha256 = "00bedd1f30da9726b50e6aa6929d3df5e7d5e2dbaf05f6435503c01210f5ad1f";
    private const string RetiredMasterTrustResourceName = "Dobby.Security.Trust.vendor-master-public-key.pem";

    private const string PrivateKeyLabelPattern =
        @"(?<privateKeyLabel>(?:[A-Z0-9]+ )*" +
        @"PRIVATE KEY)";

    private const string PrivateKeyBeginBlockPattern =
        @"^-----BEGIN " +
        PrivateKeyLabelPattern +
        @"-----\r?\n";

    private const string PrivateKeyEndBlockPattern =
        @"^-----END " +
        @"\k<privateKeyLabel>" +
        @"-----$";

    private static readonly Regex ActualPrivateKeyBlockPattern = new(
        PrivateKeyBeginBlockPattern +
        @"[A-Za-z0-9+/=\r\n]+" +
        PrivateKeyEndBlockPattern,
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> KnownTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".json",
        ".md",
        ".pem",
        ".resx",
        ".sql",
        ".txt",
        ".xaml"
    };

    private static readonly (string Description, Regex Pattern)[] ForbiddenEvidencePatterns =
    [
        ("private-key PEM blocks", ActualPrivateKeyBlockPattern),
        ("private-material wording", CreatePhrasePattern(
            ["private", "material"],
            ["private", "signing", "material"])),
        ("signing-detail wording", CreatePhrasePattern(
            ["signing", "transport"],
            ["signing", "channel"],
            ["signing", "configuration"],
            ["signing", "env", "var"],
            ["signing", "env", "vars"])),
        ("recovery-payload wording", CreatePhrasePattern(
            ["raw", "recovery", "payload"],
            ["canonical", "recovery", "payload"],
            ["raw", "canonical"])),
        ("admin-transcript wording", CreatePhrasePattern(
            ["raw", "admin", "transcript"],
            ["full", "admin", "transcript"])),
        ("stack/call-stack wording", CreatePhrasePattern(
            ["stack", "trace"],
            ["call", "stack"])),
        ("pass-phrase wording", CreatePhrasePattern(["pass", "phrase"])),
        ("secret-bearing absolute path", CreateSecretBearingAbsolutePathPattern())
    ];

    [Fact]
    public void Production_projects_do_not_reference_sample_plugin_or_generated_smoke_artifacts_by_default()
    {
        var root = RepositoryRoot();
        var projectFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        projectFiles.ShouldNotBeEmpty();
        foreach (var projectFile in projectFiles)
        {
            var xml = File.ReadAllText(projectFile);
            xml.ShouldNotContain("samples", Case.Insensitive);
            xml.ShouldNotContain("Dobby.Sample.Plugin", Case.Insensitive);
            xml.ShouldNotContain(".dobbyplugin", Case.Insensitive);
            xml.ShouldNotContain(".dobbylicense", Case.Insensitive);
        }
    }

    [Fact]
    public void Private_key_block_scan_matches_non_ec_or_rsa_private_key_labels()
    {
        var sample =
            "-----BEGIN " +
            "OPENSSH " +
            "PRIVATE KEY-----\n" +
            "QUJDRA==\n" +
            "-----END " +
            "OPENSSH " +
            "PRIVATE KEY-----";

        ActualPrivateKeyBlockPattern.IsMatch(sample).ShouldBeTrue();
    }

    [Fact]
    public void Source_tests_and_release_evidence_do_not_contain_private_key_blocks()
    {
        var root = RepositoryRoot();
        var files = EnumerateKnownTextFilesWithoutBuildOutputs(
            Path.Combine(root, "src"),
            Path.Combine(root, "tests"),
            Path.Combine(root, "artifacts", "release-evidence"));

        files.ShouldNotBeEmpty();
        foreach (var file in files)
        {
            ActualPrivateKeyBlockPattern.IsMatch(File.ReadAllText(file)).ShouldBeFalse(file);
        }
    }

    [Fact]
    public void Forbidden_evidence_patterns_match_reviewed_redaction_escape_hatches()
    {
        var secretBearingPath =
            "C:\\" +
            "Users\\" +
            "demo\\" +
            ".dobby-" +
            "signing-" +
            "keys\\" +
            "handover." +
            "txt";

        var sample = string.Join(
            Environment.NewLine,
            [
                "private " + "material remains outside source control",
                "signing " + "channel details must be redacted",
                "raw/" + "canonical recovery payload is forbidden",
                "raw Admin " + "transcript must not be stored",
                "call-" + "stack details must not be stored",
                "pass" + "phrase",
                secretBearingPath
            ]);

        FindForbiddenEvidenceMatches(sample).ShouldBe([
            "private-material wording",
            "signing-detail wording",
            "recovery-payload wording",
            "admin-transcript wording",
            "stack/call-stack wording",
            "pass-phrase wording",
            "secret-bearing absolute path"
        ]);
    }

    [Fact]
    public void Historical_release_candidate_evidence_stays_consistent_and_its_master_key_stays_retired()
    {
        // The 2026-07 local release candidate was produced under the retired single
        // master key model (ADR-0033). It remains historical evidence: this test
        // checks that the record is still internally consistent and that the retired
        // key can never re-enter the current build (ADR-0062, NFR-027). The live
        // anchor/embedded-bytes equality proof is owned by
        // PluginTrustResourcePackagingTests, which runs against the current build
        // instead of a machine-local publish output.
        var evidenceRoot = CurrentTask1EvidenceRoot();
        var packagingScan = ReadEvidence(evidenceRoot, "packaging-static-scan.md");
        var productionRecovery = ReadEvidence(evidenceRoot, "production-recovery-handover.md");

        packagingScan.ShouldContain("DobbyPluginTrustProfile=Production");
        ExtractRepoRelativePath(packagingScan, "Published security assembly path")
            .ShouldEndWith("Dobby.Security.dll");

        ExtractHash(productionRecovery, "Source public-key SHA-256").ShouldBe(RetiredMasterKeyPemSha256);
        ExtractHash(productionRecovery, "Embedded public-key SHA-256").ShouldBe(RetiredMasterKeyPemSha256);
        ExtractHash(productionRecovery, "Custody public-key SHA-256").ShouldBe(RetiredMasterKeyPemSha256);
        productionRecovery.ShouldContain("Source/embedded equality: PASS");

        // HD-S11-001 is still open: the recorded custody hash is the same locally generated
        // GA-candidate key as the source/embedded anchors, so it matches by construction and is
        // not authorized-custodian evidence. The record must say so and must not present the
        // three-way match as a closed custody gate.
        productionRecovery.ShouldContain("Independent custody equality: NOT PROVEN");
        productionRecovery.ShouldNotContain("Source/embedded/custody equality: PASS");
        productionRecovery.ShouldContain("HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES");

        var assembly = typeof(Dobby.Security.Trust.PluginTrustPublicKeyProvider).Assembly;
        assembly.GetManifestResourceNames().ShouldNotContain(RetiredMasterTrustResourceName);

        var currentAnchorHashes = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Dobby.Host", "Trust"), "*.pem")
            .Select(path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant())
            .ToArray();

        currentAnchorHashes.ShouldNotBeEmpty();
        currentAnchorHashes.ShouldNotContain(RetiredMasterKeyPemSha256);
        currentAnchorHashes.ShouldNotContain(Sprint8BootstrapPemSha256);
    }

    [Fact]
    public void Production_recovery_evidence_records_only_redacted_admin_and_import_fields()
    {
        var evidenceRoot = CurrentTask1EvidenceRoot();
        var productionRecovery = ReadEvidence(evidenceRoot, "production-recovery-handover.md");

        productionRecovery.ShouldContain("Recovery response command: `create-recovery-response`");
        productionRecovery.ShouldContain("Signing profile: `production`");
        productionRecovery.ShouldContain("Command result: exit code 0 (PASS)");
        // The signing key behind this RC record is locally generated, so the record must state its
        // provenance instead of implying vendor production custody material was used.
        productionRecovery.ShouldContain("Signing key provenance: locally generated GA-candidate key material");
        ExtractHash(productionRecovery, ".dobbyrecovery artifact SHA-256").Length.ShouldBe(64);
        ExtractHash(productionRecovery, "Redacted Admin report SHA-256").Length.ShouldBe(64);
        ExtractHash(productionRecovery, "Redacted transcript hash-chain digest").Length.ShouldBe(64);
        productionRecovery.ShouldContain("Published Host import result: PASS");
        productionRecovery.ShouldContain("Deterministic success signal:");
        ExtractHash(productionRecovery, "Imported artifact SHA-256")
            .ShouldBe(ExtractHash(productionRecovery, ".dobbyrecovery artifact SHA-256"));

        AssertForbiddenEvidenceAbsent(productionRecovery);
    }

    [Fact]
    public void Packaging_scan_and_manifest_record_production_publish_and_import_evidence()
    {
        var evidenceRoot = CurrentTask1EvidenceRoot();
        var packagingScan = ReadEvidence(evidenceRoot, "packaging-static-scan.md");
        var manifest = ReadEvidence(evidenceRoot, "release-run-manifest.md");

        packagingScan.ShouldContain(
            @"Publish command: `dotnet publish .\src\Dobby.Host\Dobby.Host.csproj -c Release --no-restore -p:DobbyPluginTrustProfile=Production`");
        packagingScan.ShouldContain("Published Host path:");
        packagingScan.ShouldContain("Published security assembly path:");
        packagingScan.ShouldContain("No production output path includes sample plugin artifacts.");
        packagingScan.ShouldContain("dev/QA trust anchor absent");
        packagingScan.ShouldContain("no production private keys");

        manifest.ShouldContain("DobbyPluginTrustProfile=Production");
        manifest.ShouldContain($"sourceVendorPublicKeyHash: `{RetiredMasterKeyPemSha256}`");
        manifest.ShouldContain($"embeddedVendorPublicKeyHash: `{RetiredMasterKeyPemSha256}`");
        manifest.ShouldContain($"custodyVendorPublicKeyHash: `{RetiredMasterKeyPemSha256}`");
        manifest.ShouldContain("independentCustodyEqualityResult: NOT PROVEN");
        manifest.ShouldContain("HD-S11-001: PATH APPROVED; CLOSURE BLOCKED UNTIL EVIDENCE PASSES");
        manifest.ShouldContain($"oldBootstrapProductionHash={Sprint8BootstrapPemSha256}");
        manifest.ShouldContain("oldBootstrapNegativeControlResult: PASS");
        manifest.ShouldContain("publishedRecoveryImportResult: PASS");
        manifest.ShouldContain("redactionGate: PASS");

        AssertForbiddenEvidenceAbsent(packagingScan);
        AssertForbiddenEvidenceAbsent(manifest);
    }

    private static string RepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "Dobby.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }

    private static string ReadEvidence(string evidenceRoot, string fileName)
    {
        var path = Path.Combine(evidenceRoot, fileName);
        File.Exists(path).ShouldBeTrue($"Expected evidence file at {path}.");
        return File.ReadAllText(path);
    }

    private static string CurrentTask1EvidenceRoot()
    {
        var releaseEvidenceRoot = new DirectoryInfo(Path.Combine(
            RepositoryRoot(),
            "artifacts",
            "release-evidence"));

        releaseEvidenceRoot.Exists.ShouldBeTrue();

        var candidates = releaseEvidenceRoot
            .EnumerateDirectories("local-rc-*", SearchOption.TopDirectoryOnly)
            .Where(ManifestMatchesCurrentTask1Root)
            .ToArray();

        candidates.Length.ShouldBe(
            1,
            "Expected exactly one Task 1 local release-candidate evidence root with Task 5 installer authority evidence.");

        return candidates[0].FullName;
    }

    private static bool ManifestMatchesCurrentTask1Root(DirectoryInfo directory)
    {
        var manifestPath = Path.Combine(directory.FullName, "release-run-manifest.md");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        var manifest = NormalizeLineEndings(File.ReadAllText(manifestPath));
        var sourceShortSha = directory.Name.Split('-').Last();
        return manifest.Contains($"Build ID: {directory.Name}", StringComparison.Ordinal)
            && manifest.Contains($"Evidence root: artifacts\\release-evidence\\{directory.Name}", StringComparison.Ordinal)
            && manifest.Contains($"Source commit short SHA: {sourceShortSha}", StringComparison.Ordinal)
            && manifest.Contains("Source branch: main", StringComparison.Ordinal)
            && manifest.Contains("Task 5 Installer Authority Addendum", StringComparison.Ordinal);
    }

    private static string ExtractRepoRelativePath(string text, string label)
    {
        var pattern = $"(?m)^- {Regex.Escape(label)}: `(?<value>[^`]+)`$";
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue($"Expected `{label}` entry in evidence.");
        return match.Groups["value"].Value;
    }

    private static string ExtractHash(string text, string label)
    {
        var pattern = $"(?mi)^{Regex.Escape(label)}: `(?<value>[0-9a-f]{{64}})`$";
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        match.Success.ShouldBeTrue($"Expected `{label}` hash entry in evidence.");
        return match.Groups["value"].Value;
    }

    private static void AssertForbiddenEvidenceAbsent(string text)
    {
        var matches = FindForbiddenEvidenceMatches(text);
        matches.ShouldBeEmpty($"Evidence should remain redacted. Found: {string.Join(", ", matches)}");
    }

    private static string[] FindForbiddenEvidenceMatches(string text)
    {
        return ForbiddenEvidencePatterns
            .Where(entry => entry.Pattern.IsMatch(text))
            .Select(entry => entry.Description)
            .ToArray();
    }

    private static Regex CreatePhrasePattern(params string[][] phrases)
    {
        var pattern = string.Join("|", phrases.Select(CreatePhrasePatternFragment));
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string CreatePhrasePatternFragment(IEnumerable<string> fragments)
    {
        return $@"(?<![A-Za-z0-9]){string.Join(@"[\s`'""/_-]*", fragments.Select(Regex.Escape))}(?![A-Za-z0-9])";
    }

    private static Regex CreateSecretBearingAbsolutePathPattern()
    {
        var sensitiveSegmentPatterns = new[]
        {
            CreatePathSegmentPattern(["private"]),
            CreatePathSegmentPattern(["secret"]),
            CreatePathSegmentPattern(["credential"]),
            CreatePathSegmentPattern(["sign", "ing"]),
            CreatePathSegmentPattern(["pass", "phrase"])
        };

        var pattern =
            @"(?i)\b[A-Z]:\\(?:Users|Documents and Settings)\\[^\\\r\n]+\\(?:[^\\\r\n]+\\)*[^\\\r\n]*(?:" +
            string.Join("|", sensitiveSegmentPatterns) +
            @")[^\\\r\n]*(?:\\[^\\\r\n]*)*";

        return new Regex(pattern, RegexOptions.CultureInvariant);
    }

    private static string CreatePathSegmentPattern(IEnumerable<string> fragments)
    {
        return string.Join(@"[-_.\s]*", fragments.Select(Regex.Escape));
    }

    private static bool IsKnownTextFile(string path)
    {
        return KnownTextExtensions.Contains(Path.GetExtension(path));
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> EnumerateKnownTextFilesWithoutBuildOutputs(params string[] roots)
    {
        var files = new List<string>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    var name = Path.GetFileName(directory);
                    if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("TestResults", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    pending.Push(directory);
                }

                files.AddRange(Directory.EnumerateFiles(current).Where(IsKnownTextFile));
            }
        }

        return files;
    }
}
