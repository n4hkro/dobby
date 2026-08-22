using System.Text.Json;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class TraceabilityRegisterTests
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.Ordinal)
    {
        "Critical",
        "High",
        "Medium",
        "Low"
    };

    [Fact]
    public void Machine_register_is_complete_unique_owned_and_evidence_backed()
    {
        var root = RepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(RegisterPath(root)));
        var register = document.RootElement;
        register.GetProperty("schemaVersion").GetInt32().ShouldBe(1);

        var allowed = register.GetProperty("allowedClassifications")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        allowed.ShouldBe(new HashSet<string>(
        [
            "Implemented",
            "Implemented-Weak",
            "Partial",
            "Incorrect",
            "Unimplemented",
            "Deferred-by-design",
            "Evidence-only"
        ], StringComparer.Ordinal));

        var findings = register.GetProperty("findings").EnumerateArray().ToArray();
        var legacyItems = register.GetProperty("legacyItems").EnumerateArray().ToArray();
        register.GetProperty("summary").GetProperty("findingCount").GetInt32().ShouldBe(findings.Length);
        register.GetProperty("summary").GetProperty("legacyItemCount").GetInt32().ShouldBe(legacyItems.Length);
        findings.Length.ShouldBe(25);
        legacyItems.Length.ShouldBe(69);

        findings.Select(item => item.GetProperty("id").GetString()).ShouldBe(
            Enumerable.Range(1, 25).Select(index => $"F-{index:000}"),
            ignoreOrder: false);
        legacyItems.Select(item => item.GetProperty("id").GetInt32()).ShouldBe(
            Enumerable.Range(1, 69),
            ignoreOrder: false);

        var allItems = findings.Concat(legacyItems).ToArray();
        foreach (var item in allItems)
        {
            var classification = item.GetProperty("classification").GetString();
            allowed.ShouldContain(classification!);
            item.GetProperty("owner").GetString().ShouldNotBeNullOrWhiteSpace();

            var evidence = item.GetProperty("evidence").EnumerateArray()
                .Select(entry => entry.GetString()!)
                .ToArray();
            if (classification == "Implemented")
            {
                evidence.ShouldNotBeEmpty("Implemented entries require executable evidence.");
            }

            foreach (var relativePath in evidence)
            {
                var path = Path.Combine(root.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                File.Exists(path).ShouldBeTrue($"Broken traceability evidence reference: {relativePath}");
                var testSource = File.ReadAllText(path);
                (testSource.Contains("[Fact]", StringComparison.Ordinal) ||
                 testSource.Contains("[Theory]", StringComparison.Ordinal)).ShouldBeTrue(
                    $"Evidence must identify executable tests: {relativePath}");
            }

            if (classification == "Deferred-by-design")
            {
                item.TryGetProperty("deferralDecision", out var decision).ShouldBeTrue();
                decision.GetString().ShouldNotBeNullOrWhiteSpace();
            }
        }

        foreach (var finding in findings)
        {
            AllowedPriorities.ShouldContain(finding.GetProperty("priority").GetString()!);
        }

        var revision = register.GetProperty("verificationRevision").GetString();
        (revision == "WORKTREE" || IsLowerHexCommit(revision)).ShouldBeTrue(
            "Verification revision must be honest about uncommitted work or identify an exact commit.");
    }

    [Fact]
    public void Human_traceability_tables_match_machine_register_without_status_drift()
    {
        var root = RepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(RegisterPath(root)));
        var register = document.RootElement;
        var traceability = File.ReadAllText(Path.Combine(
            root.FullName,
            register.GetProperty("sourceDocument").GetString()!.Replace('/', Path.DirectorySeparatorChar)));

        foreach (var finding in register.GetProperty("findings").EnumerateArray())
        {
            var id = finding.GetProperty("id").GetString();
            var priority = finding.GetProperty("priority").GetString();
            var classification = finding.GetProperty("classification").GetString();
            traceability.ShouldContain($"| {id} | {priority} | {classification} |");
        }

        foreach (var item in register.GetProperty("legacyItems").EnumerateArray())
        {
            var id = item.GetProperty("id").GetInt32();
            var classification = item.GetProperty("classification").GetString();
            traceability.ShouldContain($"| {id} | {classification} |");
        }

        File.Exists(Path.Combine(root.FullName, "PROGRAM_DEVELOPMENT_WORK_ITEMS_STATUS.md"))
            .ShouldBeFalse("The removed legacy status table must not reappear as a competing completion authority.");
    }

    [Fact]
    public void Every_register_owner_resolves_to_a_workstream_or_explicit_regression_owner()
    {
        var root = RepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(RegisterPath(root)));
        var register = document.RootElement;
        var planText = string.Join('\n', Directory.GetFiles(
                Path.Combine(root.FullName, "docs", "implementation-plan"),
                "*.md")
            .Select(File.ReadAllText));

        foreach (var ownerList in register.GetProperty("findings").EnumerateArray()
                     .Concat(register.GetProperty("legacyItems").EnumerateArray())
                     .Select(item => item.GetProperty("owner").GetString()!))
        {
            foreach (var owner in ownerList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (owner == "regression")
                {
                    continue;
                }

                planText.Contains(owner, StringComparison.Ordinal).ShouldBeTrue(
                    $"Unknown workstream owner '{owner}'.");
            }
        }
    }

    private static bool IsLowerHexCommit(string? value)
    {
        return value is { Length: 40 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string RegisterPath(DirectoryInfo root)
    {
        return Path.Combine(root.FullName, "docs", "implementation-plan", "status-register.json");
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
