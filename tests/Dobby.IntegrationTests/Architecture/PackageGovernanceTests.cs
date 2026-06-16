using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class PackageGovernanceTests
{
    [Fact]
    public void Package_references_use_central_versions_only()
    {
        var root = RepoRoot();
        var centralVersions = PackageVersions(root);
        var violations = new List<string>();

        foreach (var project in ProjectFiles(root))
        {
            var xml = XDocument.Load(project.FullName);
            foreach (var reference in xml.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var packageId = reference.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    violations.Add($"{project.FullName}: PackageReference is missing Include.");
                    continue;
                }

                if (reference.Attribute("Version") is not null)
                {
                    violations.Add($"{project.FullName}: PackageReference {packageId} has inline Version; use Directory.Packages.props.");
                }

                if (reference.Elements().Any(element => element.Name.LocalName == "Version"))
                {
                    violations.Add($"{project.FullName}: PackageReference {packageId} has child Version; use Directory.Packages.props.");
                }

                if (!centralVersions.Contains(packageId))
                {
                    violations.Add($"{project.FullName}: PackageReference {packageId} has no matching PackageVersion in Directory.Packages.props.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void DirectoryPackagesProps_enables_central_package_management()
    {
        var root = RepoRoot();
        var path = Path.Combine(root.FullName, "Directory.Packages.props");
        var xml = XDocument.Load(path);
        var value = xml
            .Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "ManagePackageVersionsCentrally")
            ?.Value;

        value.ShouldBe("true", "docs/03_TECH_STACK_AND_VERSIONS.md requires central package management in Directory.Packages.props.");
        File.Exists(Path.Combine(root.FullName, "global.json")).ShouldBeTrue();
    }

    [Fact]
    public void Test_only_packages_are_limited_to_test_projects()
    {
        var root = RepoRoot();
        var testOnlyPackages = new HashSet<string>(StringComparer.Ordinal)
        {
            "xunit",
            "xunit.runner.visualstudio",
            "Shouldly",
            "NSubstitute",
            "Verify.Xunit",
            "Microsoft.Extensions.TimeProvider.Testing",
            "Microsoft.NET.Test.Sdk",
            "coverlet.collector"
        };
        var violations = new List<string>();

        foreach (var project in ProjectFiles(root))
        {
            var isTestProject = IsUnder(project.FullName, Path.Combine(root.FullName, "tests"));
            var references = PackageReferences(project);

            foreach (var packageId in references.Where(testOnlyPackages.Contains))
            {
                if (!isTestProject)
                {
                    violations.Add($"{project.FullName}: test-only package {packageId} is outside tests/**.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Tier0_bcl_packages_are_not_referenced_or_pinned()
    {
        var root = RepoRoot();
        var tier0Packages = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Text.Json",
            "System.IO.Compression",
            "System.IO.Compression.ZipFile",
            "System.Threading.Channels"
        };
        var violations = new List<string>();

        foreach (var project in ProjectFiles(root))
        {
            foreach (var packageId in PackageReferences(project).Where(tier0Packages.Contains))
            {
                violations.Add($"{project.FullName}: Tier 0 BCL/runtime-only package {packageId} must not be an explicit PackageReference.");
            }
        }

        foreach (var packageId in PackageVersions(root).Where(tier0Packages.Contains))
        {
            violations.Add($"Directory.Packages.props: Tier 0 BCL/runtime-only package {packageId} must not be an explicit PackageVersion.");
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Rejected_or_deferred_packages_are_absent_without_human_opt_in()
    {
        var root = RepoRoot();
        var rejectedPackages = new HashSet<string>(StringComparer.Ordinal)
        {
            "LuckyPennySoftware.MediatR",
            "MediatR",
            "EPPlus",
            "FluentAssertions",
            "Syncfusion",
            "DevExpress",
            "Telerik",
            "Moq",
            "AutoMapper",
            "Microsoft.Extensions.Resilience",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Http.Resilience"
        };
        var violations = new List<string>();

        foreach (var project in ProjectFiles(root))
        {
            foreach (var packageId in PackageReferences(project).Where(packageId => IsRejected(packageId, rejectedPackages)))
            {
                violations.Add($"{project.FullName}: rejected/deferred package {packageId} requires explicit human-approved opt-in evidence.");
            }
        }

        foreach (var packageId in PackageVersions(root).Where(packageId => IsRejected(packageId, rejectedPackages)))
        {
            violations.Add($"Directory.Packages.props: rejected/deferred package {packageId} requires explicit human-approved opt-in evidence.");
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Rejected_package_guardrail_matches_documented_vendor_families()
    {
        var rejectedPackages = new HashSet<string>(StringComparer.Ordinal)
        {
            "LuckyPennySoftware.MediatR",
            "MediatR",
            "EPPlus",
            "FluentAssertions",
            "Syncfusion",
            "DevExpress",
            "Telerik",
            "Moq",
            "AutoMapper",
            "Microsoft.Extensions.Resilience",
            "Microsoft.Extensions.Http",
            "Microsoft.Extensions.Http.Resilience"
        };

        IsRejected("Syncfusion.XlsIO.Net.Core", rejectedPackages).ShouldBeTrue();
        IsRejected("DevExpress.Data", rejectedPackages).ShouldBeTrue();
        IsRejected("Telerik.UI.for.Wpf", rejectedPackages).ShouldBeTrue();
        IsRejected("SQLitePCLRaw.bundle_e_sqlcipher", rejectedPackages).ShouldBeTrue();
    }

    [Fact]
    public void Toolchain_pin_tables_match_test_readable_json_per_sprint()
    {
        var root = RepoRoot();
        var evidencePath = Path.Combine(
            root.FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "toolchain-pin-gate.md");

        var markdown = File.ReadAllText(evidencePath);
        AssertPinSectionParity(markdown, "## Sprint 4 Candidate Pin Representation", "dobby.sprint4.toolchainPins.v2");
        AssertPinSectionParity(markdown, "## Sprint 5 Approved Pin Representation", "dobby.sprint5.toolchainPins.v1");
        AssertPinSectionParity(markdown, "## Sprint 7 Package Approval - HD-S7-001 / Q1", "dobby.sprint7.toolchainPins.v1");
        AssertPinSectionParity(markdown, "## Sprint 8 Package Approval - HD-S8-001 / Q1", "dobby.sprint8.toolchainPins.v1");
    }

    [Fact]
    public void Existing_test_runner_and_coverage_packages_remain_private_test_infrastructure()
    {
        var root = RepoRoot();
        var infrastructurePackages = new HashSet<string>(StringComparer.Ordinal)
        {
            "xunit.runner.visualstudio",
            "coverlet.collector"
        };
        var violations = new List<string>();

        foreach (var project in ProjectFiles(root))
        {
            var isTestProject = IsUnder(project.FullName, Path.Combine(root.FullName, "tests"));
            var xml = XDocument.Load(project.FullName);
            foreach (var reference in xml.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var packageId = reference.Attribute("Include")?.Value;
                if (!infrastructurePackages.Contains(packageId ?? string.Empty))
                {
                    continue;
                }

                if (!isTestProject)
                {
                    violations.Add($"{project.FullName}: {packageId} is outside tests/**.");
                }

                var privateAssets = reference.Attribute("PrivateAssets")?.Value
                    ?? reference.Elements().SingleOrDefault(element => element.Name.LocalName == "PrivateAssets")?.Value
                    ?? string.Empty;
                if (!privateAssets.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{project.FullName}: {packageId} must keep PrivateAssets=all.");
                }
            }
        }

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void Toolchain_pin_guard_rejects_unapproved_selected_sprint4_rows()
    {
        var markdown = Sprint4PinMarkdown("Microsoft.Data.Sqlite.Core", "PENDING");

        var exception = Should.Throw<Shouldly.ShouldAssertException>(() =>
            AssertPinSectionParity(markdown, "## Sprint 4 Candidate Pin Representation", "dobby.sprint4.toolchainPins.v2"));

        exception.Message.ShouldContain("APPROVED", Case.Sensitive);
    }

    [Fact]
    public void Toolchain_pin_guard_rejects_argon2_scope_drift()
    {
        var markdown = Sprint4PinMarkdown("Konscious.Security.Cryptography.Argon2", "APPROVED");

        var exception = Should.Throw<Shouldly.ShouldAssertException>(() =>
            AssertPinSectionParity(markdown, "## Sprint 4 Candidate Pin Representation", "dobby.sprint4.toolchainPins.v2"));

        exception.Message.ShouldContain("DEFERRED_TO_SPRINT_5", Case.Sensitive);
    }

    [Fact]
    public void Approved_sprint4_storage_packages_are_pinned_and_referenced_without_argon2()
    {
        var root = RepoRoot();
        var centralVersions = PackageVersions(root);
        var infrastructureProject = new FileInfo(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Infrastructure",
            "Dobby.Infrastructure.csproj"));
        var infrastructureReferences = PackageReferences(infrastructureProject);

        centralVersions.ShouldContain("Microsoft.Data.Sqlite.Core");
        centralVersions.ShouldContain("SQLitePCLRaw.core");
        centralVersions.ShouldContain("SQLitePCLRaw.provider.dynamic_cdecl");
        centralVersions.ShouldContain("Dapper");
        centralVersions.ShouldNotContain("Konscious.Security.Cryptography.Argon2");

        infrastructureReferences.ShouldContain("Microsoft.Data.Sqlite.Core");
        infrastructureReferences.ShouldContain("SQLitePCLRaw.core");
        infrastructureReferences.ShouldContain("SQLitePCLRaw.provider.dynamic_cdecl");
        infrastructureReferences.ShouldContain("Dapper");
        infrastructureReferences.ShouldNotContain("Konscious.Security.Cryptography.Argon2");
    }

    [Fact]
    public void Approved_sprint5_system_management_package_is_pinned_and_windows_scoped()
    {
        var root = RepoRoot();
        var centralVersions = PackageVersions(root);
        var infrastructureProject = new FileInfo(Path.Combine(
            root.FullName,
            "src",
            "Dobby.Infrastructure",
            "Dobby.Infrastructure.csproj"));
        var infrastructureXml = XDocument.Load(infrastructureProject.FullName);
        var systemManagementReference = infrastructureXml
            .Descendants()
            .SingleOrDefault(element =>
                element.Name.LocalName == "PackageReference" &&
                element.Attribute("Include")?.Value == "System.Management");

        centralVersions.ShouldContain("System.Management");
        systemManagementReference.ShouldNotBeNull();
        systemManagementReference.Attribute("Condition")?.Value.ShouldContain("net10.0-windows", Case.Sensitive);
        PackageReferences(new FileInfo(Path.Combine(root.FullName, "src", "Dobby.Security", "Dobby.Security.csproj")))
            .ShouldNotContain("System.Management");
        PackageReferences(new FileInfo(Path.Combine(root.FullName, "src", "Dobby.Host", "Dobby.Host.csproj")))
            .ShouldNotContain("System.Management");
    }

    private static bool IsRejected(string packageId, HashSet<string> rejectedPackages)
    {
        var rejectedPackagePrefixes = new[]
        {
            "AutoMapper.",
            "DevExpress.",
            "EPPlus.",
            "FluentAssertions.",
            "LuckyPennySoftware.MediatR.",
            "MediatR.",
            "SQLitePCLRaw.bundle_",
            "Syncfusion.",
            "Telerik."
        };

        return rejectedPackages.Contains(packageId)
            || rejectedPackagePrefixes.Any(prefix => packageId.StartsWith(prefix, StringComparison.Ordinal));
    }

    private sealed record ToolchainPinRow(
        string Requirement,
        string VersionOrAsset,
        string SourceUrlCommitTag,
        string PackageOrAssetUrl,
        string Sha256,
        string NuGetSha512OrPackageHash,
        string NuGetRepositorySignature,
        string NuGetAuthorSignatureOrRisk,
        string NativeUpstreamDigest,
        string NativeTagReleaseProvenance,
        string License,
        string VulnerabilityScanDate,
        string Approver,
        string DecisionLink,
        string Status,
        string? TargetFrameworkScope);

    private static void AssertPinSectionParity(string markdown, string heading, string expectedSchema)
    {
        var section = MarkdownSection(markdown, heading);
        var tableRows = PinTableRows(section);
        var jsonRows = PinJsonRows(markdown, expectedSchema);

        tableRows.Keys.Order(StringComparer.Ordinal).ShouldBe(jsonRows.Keys.Order(StringComparer.Ordinal));

        foreach (var packageId in tableRows.Keys)
        {
            var table = tableRows[packageId];
            var json = jsonRows[packageId];

            table.Requirement.ShouldBe(json.Requirement, packageId);
            table.VersionOrAsset.ShouldBe(json.VersionOrAsset, packageId);
            table.SourceUrlCommitTag.ShouldBe(json.SourceUrlCommitTag, packageId);
            table.PackageOrAssetUrl.ShouldBe(json.PackageOrAssetUrl, packageId);
            table.Sha256.ShouldBe(json.Sha256, packageId);
            table.NuGetSha512OrPackageHash.ShouldBe(json.NuGetSha512OrPackageHash, packageId);
            table.NuGetRepositorySignature.ShouldBe(json.NuGetRepositorySignature, packageId);
            table.NuGetAuthorSignatureOrRisk.ShouldBe(json.NuGetAuthorSignatureOrRisk, packageId);
            table.NativeUpstreamDigest.ShouldBe(json.NativeUpstreamDigest, packageId);
            table.NativeTagReleaseProvenance.ShouldBe(json.NativeTagReleaseProvenance, packageId);
            table.License.ShouldBe(json.License, packageId);
            table.VulnerabilityScanDate.ShouldBe(json.VulnerabilityScanDate, packageId);
            table.Approver.ShouldBe(json.Approver, packageId);
            table.DecisionLink.ShouldBe(json.DecisionLink, packageId);
            table.Status.ShouldBe(json.Status, packageId);

            if (expectedSchema == "dobby.sprint5.toolchainPins.v1")
            {
                json.TargetFrameworkScope.ShouldBe("net10.0-windows only", packageId);
                table.Requirement.ShouldContain("net10.0-windows only", Case.Sensitive, packageId);
            }
        }

        if (expectedSchema == "dobby.sprint4.toolchainPins.v2")
        {
            AssertSprint4StatusGuardrails(jsonRows);
        }
    }

    private static void AssertSprint4StatusGuardrails(Dictionary<string, ToolchainPinRow> rows)
    {
        string[] approvedSelectedRows =
        [
            "Microsoft.Data.Sqlite.Core",
            "SQLitePCLRaw.core",
            "SQLitePCLRaw.provider.dynamic_cdecl",
            "Dapper",
            "Approved native encrypted SQLite provider asset/package"
        ];

        foreach (var packageId in approvedSelectedRows)
        {
            if (rows.TryGetValue(packageId, out var row))
            {
                row.Status.ShouldBe("APPROVED", packageId);
            }
        }

        if (rows.TryGetValue("Konscious.Security.Cryptography.Argon2", out var argon2))
        {
            argon2.Status.ShouldBe("DEFERRED_TO_SPRINT_5", "Sprint 4 must not approve Argon2 scope.");
        }
    }

    private static string MarkdownSection(string markdown, string heading)
    {
        var start = markdown.IndexOf(heading, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);

        var next = markdown.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return next < 0 ? markdown[start..] : markdown[start..next];
    }

    private static Dictionary<string, ToolchainPinRow> PinTableRows(string markdown)
    {
        var rows = new Dictionary<string, ToolchainPinRow>(StringComparer.Ordinal);
        var inTable = false;

        foreach (var line in markdown.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            if (line.StartsWith("| Package / Asset |", StringComparison.Ordinal))
            {
                inTable = true;
                continue;
            }

            if (!inTable)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (!line.StartsWith("|", StringComparison.Ordinal) || line.Contains("| ---", StringComparison.Ordinal))
            {
                continue;
            }

            var columns = line
                .Split('|', StringSplitOptions.TrimEntries)
                .Where(column => column.Length > 0)
                .ToArray();

            if (columns.Length < 16)
            {
                continue;
            }

            var packageId = NormalizePackageId(columns[0]);
            rows[packageId] = new ToolchainPinRow(
                NormalizeMarkdownValue(columns[1]),
                NormalizeMarkdownValue(columns[2]),
                NormalizeMarkdownValue(columns[3]),
                NormalizeMarkdownValue(columns[4]),
                NormalizeSha(columns[5]),
                NormalizeMarkdownValue(columns[6]),
                NormalizeMarkdownValue(columns[7]),
                NormalizeMarkdownValue(columns[8]),
                NormalizeSha(columns[9]),
                NormalizeMarkdownValue(columns[10]),
                NormalizeMarkdownValue(columns[11]),
                NormalizeMarkdownValue(columns[12]),
                NormalizeMarkdownValue(columns[13]),
                NormalizeMarkdownValue(columns[14]),
                NormalizeMarkdownValue(columns[^1]),
                null);
        }

        return rows;
    }

    private static Dictionary<string, ToolchainPinRow> PinJsonRows(string markdown, string expectedSchema)
    {
        const string fence = "```json";
        var start = markdown.IndexOf(fence, StringComparison.Ordinal);
        while (start >= 0)
        {
            start += fence.Length;
            var end = markdown.IndexOf("```", start, StringComparison.Ordinal);
            end.ShouldBeGreaterThan(start);

            using var document = JsonDocument.Parse(markdown[start..end]);
            if (document.RootElement.GetProperty("schema").GetString() == expectedSchema)
            {
                var rows = new Dictionary<string, ToolchainPinRow>(StringComparer.Ordinal);

                foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray())
                {
                    rows[NormalizePackageId(row.GetProperty("packageId").GetString()!)] = new ToolchainPinRow(
                        NormalizeMarkdownValue(row.GetProperty("requirement").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("versionOrAsset").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("sourceUrlCommitTag").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("packageOrAssetUrl").GetString()!),
                        NormalizeSha(row.GetProperty("sha256").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("nugetSha512OrPackageHash").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("nugetRepositorySignature").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("nugetAuthorSignatureOrRisk").GetString()!),
                        NormalizeSha(row.GetProperty("nativeUpstreamDigest").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("nativeTagReleaseProvenance").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("license").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("vulnerabilityScanDate").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("approver").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("decisionLink").GetString()!),
                        NormalizeMarkdownValue(row.GetProperty("status").GetString()!),
                        row.TryGetProperty("targetFrameworkScope", out var targetFrameworkScope)
                            ? NormalizeMarkdownValue(targetFrameworkScope.GetString()!)
                            : null);
                }

                return rows;
            }

            start = markdown.IndexOf(fence, end, StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"Could not find test-readable toolchain pin JSON schema '{expectedSchema}'.");
    }

    private static string NormalizePackageId(string value)
    {
        var normalized = NormalizeMarkdownValue(value);
        return normalized.EndsWith(" shim", StringComparison.Ordinal)
            ? normalized[..^5]
            : normalized;
    }

    private static string NormalizeSha(string value)
    {
        var normalized = NormalizeMarkdownValue(value);
        return normalized.Equals("N/A", StringComparison.OrdinalIgnoreCase)
            ? "N/A"
            : normalized.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMarkdownValue(string value)
    {
        return value.Replace("`", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string Sprint4PinMarkdown(string packageId, string status)
    {
        return $$"""
            ## Sprint 4 Candidate Pin Representation

            | Package / Asset | Requirement | Version / Asset | Source URL / Commit or Tag | Package / Asset URL | SHA-256 | NuGet SHA512 / Package Hash | NuGet repository signature | NuGet author signature / risk | Native upstream digest | Native tag/release provenance | License | Vulnerability scan date | Approver | Decision link | Status |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | `{{packageId}}` | Required | test-version | source | package | sha256:abc | N/A | N/A | N/A | N/A | provenance | MIT | 2026-05-14 | n4hkro | decision | {{status}} |

            ```json
            {
              "schema": "dobby.sprint4.toolchainPins.v2",
              "rows": [
                {
                  "packageId": "{{packageId}}",
                  "requirement": "Required",
                  "versionOrAsset": "test-version",
                  "sourceUrlCommitTag": "source",
                  "packageOrAssetUrl": "package",
                  "sha256": "sha256:abc",
                  "nugetSha512OrPackageHash": "N/A",
                  "nugetRepositorySignature": "N/A",
                  "nugetAuthorSignatureOrRisk": "N/A",
                  "nativeUpstreamDigest": "N/A",
                  "nativeTagReleaseProvenance": "provenance",
                  "license": "MIT",
                  "vulnerabilityScanDate": "2026-05-14",
                  "approver": "n4hkro",
                  "decisionLink": "decision",
                  "status": "{{status}}"
                }
              ]
            }
            ```
            """;
    }

    private static HashSet<string> PackageVersions(DirectoryInfo root)
    {
        var xml = XDocument.Load(Path.Combine(root.FullName, "Directory.Packages.props"));

        return xml
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageVersion")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private static string[] PackageReferences(FileInfo project)
    {
        var xml = XDocument.Load(project.FullName);

        return xml
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Order(StringComparer.Ordinal)
            .ToArray()!;
    }

    private static IEnumerable<FileInfo> ProjectFiles(DirectoryInfo root)
    {
        return root
            .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedPath(file.FullName));
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("TestResults", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUnder(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
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
