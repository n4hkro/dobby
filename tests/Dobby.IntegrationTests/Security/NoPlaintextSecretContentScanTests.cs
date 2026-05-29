using System.IO;
using System.Text.RegularExpressions;
using Dobby.Host.Startup;
using Shouldly;

namespace Dobby.IntegrationTests.Security;

public sealed partial class NoPlaintextSecretContentScanTests
{
    [Fact]
    public void Committed_source_and_test_files_do_not_contain_plaintext_secret_markers()
    {
        var root = RepoRoot();
        var violations = new List<string>();

        foreach (var file in ScanFiles(root))
        {
            var text = File.ReadAllText(file.FullName);
            if (PlaintextSecretPattern().IsMatch(text))
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
            .Where(file => PlaintextSecretPattern().IsMatch(File.ReadAllText(file)))
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
                if (IsGeneratedPath(file.FullName) || IsGeneratedBinary(file.FullName) || IsAllowedSecretFixture(file.FullName))
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

    private static bool IsAllowedSecretFixture(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName is "DiagnosticRedactorTests.cs"
            or "DiagnosticsAndAuditTests.cs"
            or "DiagnosticsExportBoundaryTests.cs"
            or "LocalDiagnosticsSinkTests.cs"
            or "NoPlaintextSecretContentScanTests.cs";
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

    [GeneratedRegex("-----BEGIN PRIVATE KEY-----|password\\s*=|Bearer\\s+[A-Za-z0-9._-]{20,}|(?:dpapi|entropy)[A-Za-z0-9_-]*\\s*[:=]\\s*\"?[A-Za-z0-9+/]{32,}={0,2}\"?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaintextSecretPattern();
}
