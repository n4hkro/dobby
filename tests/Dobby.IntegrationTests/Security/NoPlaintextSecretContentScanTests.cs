using System.IO;
using System.Text.RegularExpressions;
using Dobby.Host.Startup;
using Shouldly;

namespace Dobby.IntegrationTests.Security;

public sealed partial class NoPlaintextSecretContentScanTests
{
    private const string PrivateKeyBeginMarker =
        "-----BEGIN " +
        "PRIVATE KEY-----";

    [Fact]
    public void Committed_source_and_test_files_do_not_contain_plaintext_secret_markers()
    {
        var root = RepoRoot();
        var violations = new List<string>();

        foreach (var file in ScanFiles(root))
        {
            var text = File.ReadAllText(file.FullName);
            var relativePath = Path.GetRelativePath(root.FullName, file.FullName)
                .Replace('\\', '/');
            if (ContainsPlaintextSecret(text, relativePath))
            {
                violations.Add(file.FullName);
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Fresh_bootstrap_runtime_root_does_not_contain_plaintext_secret_markers()
    {
        using var temp = new TemporaryDirectory();
        var runtimeRoot = Path.Combine(temp.Path, "runtime-root");
        var appsettings = Path.Combine(temp.Path, "appsettings.json");
        File.WriteAllText(appsettings, BootstrapAppSettings(runtimeRoot));

        HostBootstrapper.Bootstrap(new HostBootstrapOptions(
            ShowMainWindow: false,
            PackagedAppSettingsPath: appsettings,
            RuntimeConfigDirectory: Path.Combine(temp.Path, "config"),
            EnvironmentName: "Test"));

        var violations = Directory.EnumerateFiles(runtimeRoot, "*", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedBinary(file))
            .Where(file => ContainsPlaintextSecret(File.ReadAllText(file), file))
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Audit_and_infrastructure_do_not_reference_network_upload_clients()
    {
        var root = RepoRoot();
        var violations = new List<string>();
        foreach (var file in new[] { "Dobby.Audit", "Dobby.Infrastructure" }
                     .Select(project => Path.Combine(root.FullName, "src", project))
                     .SelectMany(path => new DirectoryInfo(path).EnumerateFiles("*.cs", SearchOption.AllDirectories))
                     .Where(file => !IsGeneratedPath(file.FullName)))
        {
            var text = File.ReadAllText(file.FullName);
            if (text.Contains("HttpClient", StringComparison.Ordinal) ||
                text.Contains("HttpListener", StringComparison.Ordinal))
            {
                violations.Add(file.FullName);
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Committed_secret_scan_includes_docs_specs_and_release_evidence_paths()
    {
        var root = RepoRoot();
        var scanRoots = ScanRootRelativePaths(root).ToArray();

        scanRoots.ShouldContain("docs/specs");
        scanRoots.ShouldContain("artifacts/release-evidence/skeleton-local");
    }

    [Fact]
    public void Committed_secret_scan_includes_markdown_specs_and_release_evidence_files()
    {
        var root = RepoRoot();
        var scannedFiles = ScanFiles(root)
            .Select(file => Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/'))
            .ToArray();

        scannedFiles.ShouldContain("docs/specs/storage-and-data-protection.md");
        scannedFiles.ShouldContain("artifacts/release-evidence/skeleton-local/storage-materialization.md");
    }

    [Fact]
    public void Plaintext_secret_pattern_catches_dpapi_entropy_and_blob_like_literals()
    {
        PlaintextSecretPattern().IsMatch("dpapiBlob = \"AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=\"")
            .ShouldBeTrue();
        PlaintextSecretPattern().IsMatch("entropy: AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=")
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData("var password = passwordBox.Password;")]
    [InlineData("private string submittedPassword = string.Empty;")]
    [InlineData("ChangePassword = _ => Result.Success()")]
    [InlineData("viewModel.SubmittedPassword = PasswordBox.Password;")]
    public void Plaintext_secret_pattern_ignores_password_flow_identifiers_without_literal_secrets(string source)
    {
        ContainsPlaintextSecret(source, "src/Sample.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData("password = \"actual-value\"")]
    [InlineData("DefaultPassword = \"actual-value\"")]
    [InlineData("DbPassword = \"actual-value\"")]
    [InlineData("\"password\": \"actual-value\"")]
    public void Plaintext_secret_pattern_catches_explicit_password_literals(string source)
    {
        ContainsPlaintextSecret(source, "src/Sample.cs").ShouldBeTrue();
    }

    [Theory]
    [InlineData("password=actual-value")]
    [InlineData("password: actual-value")]
    [InlineData("DefaultPassword=actual-value")]
    [InlineData("DbPassword: actual-value")]
    public void Plaintext_secret_scan_catches_unquoted_password_values_in_text_content(string source)
    {
        ContainsPlaintextSecret(source, "docs/specs/sample.md").ShouldBeTrue();
    }

    [Theory]
    [InlineData("DbPassword = \"Dobby.ActualSecret-2026!\"")]
    [InlineData("\"password\": \"Dobby.ActualSecret-2026!\"")]
    public void Plaintext_secret_scan_does_not_exempt_dobby_prefixed_secret_values(string source)
    {
        ContainsPlaintextSecret(source, "src/Sample.cs").ShouldBeTrue();
    }

    [Fact]
    public void Plaintext_secret_scan_only_exempts_the_exact_host_resource_key_declaration()
    {
        const string resourceDeclaration =
            "public const string LoginPassword = \"Dobby.Host.Login.Password\";";

        ContainsPlaintextSecret(
                resourceDeclaration,
                "src/Dobby.Localization/HostResourceKeys.cs")
            .ShouldBeFalse();
        ContainsPlaintextSecret(resourceDeclaration, "src/Other.cs").ShouldBeTrue();
    }

    [Fact]
    public void Plaintext_secret_scan_checks_non_fixture_secrets_inside_approved_fixture_files()
    {
        const string approvedFixture = "viewModel.SubmittedPassword = \"wrong\";";
        const string unrelatedSecret = "DbPassword = \"actual-value\";";
        const string fixturePath = "tests/Dobby.UnitTests/Host/LockViewModelTests.cs";

        ContainsPlaintextSecret(approvedFixture, fixturePath).ShouldBeFalse();
        ContainsPlaintextSecret(unrelatedSecret, fixturePath).ShouldBeTrue();
    }

    [Fact]
    public void Scanner_source_fixture_rules_only_leave_unapproved_lines_visible()
    {
        const string path = "tests/Dobby.IntegrationTests/Security/NoPlaintextSecretContentScanTests.cs";
        var sourcePath = Path.Combine(RepoRoot().FullName, path.Replace('/', Path.DirectorySeparatorChar));
        var violations = File.ReadAllLines(sourcePath)
            .Select((line, index) => new { Line = line, Number = index + 1 })
            .Where(candidate => ContainsPlaintextSecret(candidate.Line, path))
            .Select(candidate => candidate.Number)
            .ToArray();

        violations.ShouldBeEmpty();
    }

    private static bool ContainsPlaintextSecret(string text, string path)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = NormalizePath(path);
        foreach (Match match in PlaintextSecretPattern().Matches(text))
        {
            var (line, matchIndexInLine) = MatchLine(text, match);
            if (IsRedactedPlaceholder(match) ||
                IsHostResourceKeyDeclaration(normalizedPath, line) ||
                IsApprovedPasswordProtocolMarker(normalizedPath, line, match) ||
                IsApprovedFixtureMatch(normalizedPath, line, match) ||
                IsCSharpPasswordFlow(normalizedPath, line, matchIndexInLine, match))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var marker in new[] { "/src/", "/tests/", "/samples/", "/docs/", "/artifacts/" })
        {
            var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return normalized[(index + 1)..];
            }
        }

        return normalized.TrimStart('/');
    }

    private static (string Line, int MatchIndexInLine) MatchLine(string text, Match match)
    {
        var lineStart = match.Index == 0
            ? 0
            : text.LastIndexOf('\n', match.Index - 1) + 1;
        var lineEnd = text.IndexOf('\n', match.Index);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return (text[lineStart..lineEnd].TrimEnd('\r'), match.Index - lineStart);
    }

    private static bool IsRedactedPlaceholder(Match match)
    {
        if (!match.Groups["passwordValue"].Success)
        {
            return false;
        }

        return match.Groups["passwordValue"].Value.Trim('"')
            .Equals("<redacted>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHostResourceKeyDeclaration(string path, string line)
    {
        return path.Equals("src/Dobby.Localization/HostResourceKeys.cs", StringComparison.OrdinalIgnoreCase) &&
            HostResourcePasswordKeyDeclarationPattern().IsMatch(line);
    }

    private static bool IsApprovedPasswordProtocolMarker(string path, string line, Match match)
    {
        var isApprovedPath = path.Equals(
                "src/Dobby.Host/StartupRememberMeSnapshotStore.cs",
                StringComparison.OrdinalIgnoreCase) ||
            path.Equals(
                "src/Dobby.Security/Authentication/RememberMeSlotService.cs",
                StringComparison.OrdinalIgnoreCase);
        return isApprovedPath &&
            RememberMePasswordProtocolMarkerPattern().IsMatch(line) &&
            match.Groups["passwordValue"].Success &&
            match.Groups["passwordValue"].Value.Equals(@"v1\0", StringComparison.Ordinal);
    }

    private static bool IsApprovedFixtureMatch(string path, string line, Match match)
    {
        if (path.Equals(
                "tests/Dobby.IntegrationTests/Security/NoPlaintextSecretContentScanTests.cs",
                StringComparison.OrdinalIgnoreCase))
        {
            return line.Contains("[InlineData(", StringComparison.Ordinal) ||
                line.Contains("[GeneratedRegex(", StringComparison.Ordinal) ||
                line.Contains("PlaintextSecretPattern().IsMatch(", StringComparison.Ordinal) ||
                line.Contains("const string resourceDeclaration", StringComparison.Ordinal) ||
                line.Contains("\"public const string LoginPassword", StringComparison.Ordinal) ||
                line.Contains("const string approvedFixture", StringComparison.Ordinal) ||
                line.Contains("const string unrelatedSecret", StringComparison.Ordinal);
        }

        if (!match.Groups["passwordValue"].Success)
        {
            return false;
        }

        var value = match.Groups["passwordValue"].Value.Trim('"');
        return path switch
        {
            "tests/Dobby.UnitTests/Diagnostics/DiagnosticRedactorTests.cs" =>
                line.Contains("[InlineData(", StringComparison.Ordinal) &&
                value.Equals("secret", StringComparison.Ordinal),
            "tests/Dobby.UnitTests/Diagnostics/DiagnosticsAndAuditTests.cs" =>
                line.Contains("DiagnosticRedactor.Redact(", StringComparison.Ordinal) &&
                value.Equals("secret", StringComparison.Ordinal),
            "tests/Dobby.IntegrationTests/Diagnostics/DiagnosticsExportBoundaryTests.cs" =>
                line.Contains("File.WriteAllText(", StringComparison.Ordinal) &&
                value.Equals("secret", StringComparison.Ordinal),
            "tests/Dobby.IntegrationTests/Diagnostics/LocalDiagnosticsSinkTests.cs" =>
                line.Contains("logger.", StringComparison.Ordinal) &&
                value.Equals("secret", StringComparison.Ordinal),
            "tests/Dobby.UnitTests/Host/LockViewModelTests.cs" =>
                line.Contains("codeBehindSource.ShouldContain(", StringComparison.Ordinal) &&
                    value.Equals("PasswordBox.Password", StringComparison.Ordinal) ||
                line.Contains("SubmittedPassword", StringComparison.Ordinal) &&
                    (value.Equals("wrong", StringComparison.Ordinal) ||
                        value.Equals("correct", StringComparison.Ordinal)),
            "tests/Dobby.IntegrationTests/Host/ShellDiagnosticsExportActionTests.cs" =>
                line.Contains("PasswordBox", StringComparison.Ordinal) &&
                value.Equals("new password", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsCSharpPasswordFlow(
        string path,
        string line,
        int matchIndexInLine,
        Match match)
    {
        if (!Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
            !match.Groups["passwordValue"].Success)
        {
            return false;
        }

        var value = match.Groups["passwordValue"].Value;
        if (value.Contains('"') ||
            IsInsideCSharpString(line, matchIndexInLine) ||
            IsInsideCSharpLineComment(line, matchIndexInLine))
        {
            return false;
        }

        return CSharpPasswordFlowValuePattern().IsMatch(value);
    }

    private static bool IsInsideCSharpString(string line, int index)
    {
        var quoteCount = 0;
        for (var current = 0; current < index; current++)
        {
            if (line[current] == '"' && (current == 0 || line[current - 1] != '\\'))
            {
                quoteCount++;
            }
        }

        return quoteCount % 2 != 0;
    }

    private static bool IsInsideCSharpLineComment(string line, int index)
    {
        var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 && commentIndex < index;
    }

    private static IEnumerable<FileInfo> ScanFiles(DirectoryInfo root)
    {
        foreach (var segment in ScanRootRelativePaths(root))
        {
            var directory = Path.Combine(root.FullName, segment.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.*", SearchOption.AllDirectories))
            {
                if (IsGeneratedPath(file.FullName) || IsGeneratedBinary(file.FullName))
                {
                    continue;
                }

                if (file.Extension is ".cs" or ".json" or ".csproj" or ".xaml" or ".md")
                {
                    yield return file;
                }
            }
        }
    }

    private static bool IsGeneratedBinary(string path)
    {
        return Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".pdb", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".dpapi", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ScanRootRelativePaths(DirectoryInfo root)
    {
        yield return "src";
        yield return "tests";
        yield return "samples";
        yield return "docs/specs";
        yield return "artifacts/release-evidence/skeleton-local";
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("TestResults", StringComparison.OrdinalIgnoreCase));
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

    private static string BootstrapAppSettings(string runtimeRoot)
    {
        return $$"""
            {
              "Dobby": {
                "Runtime": { "DataRoot": "{{runtimeRoot.Replace("\\", "\\\\")}}", "EnvironmentName": "Test" },
                "Storage": { "ProviderMode": "SQLite3MultipleCiphers", "RequireDataAtRestProtection": true, "BusyTimeoutMilliseconds": 5000 },
                "Auth": { "LoginThrottleStepsSeconds": [0, 0, 10, 30, 60], "PasswordKdfMode": "Pbkdf2Sha256" },
                "Session": { "AbsoluteTimeoutMinutes": 480, "IdleTimeoutMinutes": 30, "AutoLockEnabledByDefault": false },
                "Diagnostics": { "TechnicalLogRetentionDays": 30, "TechnicalLogTotalSizeCapMegabytes": 256 },
                "Audit": { "RetentionDays": 365 },
                "Localization": { "DefaultCulture": "az-AZ", "SupportedCultures": ["az-AZ", "en-US", "tr-TR"] },
                "Plugins": { "AllowPreLoginDiscovery": false },
                "Licensing": { "ImportRequiresPermission": true },
                "BranchShared": { "RuntimeEnabled": false }
              },
              "Serilog": { "MinimumLevel": "Information" }
            }
            """;
    }

    [GeneratedRegex(PrivateKeyBeginMarker + "|(?<passwordAssignment>\\b(?:[A-Za-z_][A-Za-z0-9_]*)?password\\b\"?\\s*[:=]\\s*(?<passwordValue>\"\"\"[^\\r\\n]+?\"\"\"|\\$?@?\"[^\"\\r\\n]+\"|[^\\s,;}\\]\\r\\n\"]+))|Bearer\\s+[A-Za-z0-9._-]{20,}|(?:dpapi|entropy)[A-Za-z0-9_-]*\\s*[:=]\\s*\"?[A-Za-z0-9+/]{32,}={0,2}\"?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaintextSecretPattern();

    [GeneratedRegex("^\\s*public const string [A-Za-z_][A-Za-z0-9_]*Password\\s*=\\s*\"Dobby\\.[A-Za-z0-9_.-]*Password\";\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HostResourcePasswordKeyDeclarationPattern();

    [GeneratedRegex("^(?:_|[A-Za-z_][A-Za-z0-9_.]*(?:\\([^\\r\\n]*\\))?)$", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpPasswordFlowValuePattern();

    [GeneratedRegex("^\\s*private static readonly byte\\[\\] PasswordPayloadPrefix\\s*=\\s*\"Dobby:(?:Startup)?RememberMePassword:v1\\\\0\"u8\\.ToArray\\(\\);\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RememberMePasswordProtocolMarkerPattern();
}
