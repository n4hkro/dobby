using System.IO;
using System.Text.RegularExpressions;
using Dobby.Audit;
using Shouldly;

namespace Dobby.UnitTests.Audit;

public sealed partial class AuditEventNameTests
{
    [Fact]
    public void Canonical_names_match_audit_spec_taxonomy()
    {
        AuditEventName.All.ShouldBe(
        [
            "user.login",
            "user.login.failed",
            "user.logout",
            "session.lock",
            "session.unlock",
            "session.expired",
            "session.invalidated",
            "user.create",
            "user.update",
            "user.password.change",
            "user.password.reset",
            "user.activate",
            "user.deactivate",
            "permission.grant",
            "permission.revoke",
            "permission.denied",
            "owner.protection.denied",
            "plugin.install.attempted",
            "plugin.install.verified",
            "plugin.install.rejected",
            "plugin.install.signature-failed",
            "plugin.install.trust-failed",
            "plugin.enable",
            "plugin.disable",
            "plugin.uninstall",
            "plugin.activation.skipped",
            "plugin.activation.rejected",
            "plugin.runtime.faulted",
            "plugin.runtime.quarantined",
            "plugin.lifecycle.v1-adapter-used",
            "recovery.export",
            "recovery.import",
            "license.import.attempted",
            "license.import.succeeded",
            "license.import.rejected",
            "license.enforcement.denied",
            "license.enforcement.warning",
            "customer-deployment-context.provision",
            "diagnostics.export.created",
            "security.verification.failed",
            "persistence.snapshot.created",
            "persistence.restore.requested",
            "persistence.restore.completed"
        ]);
    }

    [Fact]
    public void Runtime_registry_and_audit_spec_taxonomy_table_agree_in_both_directions()
    {
        var specPath = Path.Combine(
            FindRepositoryRoot(),
            "docs",
            "specs",
            "audit-and-diagnostics.md");
        File.Exists(specPath).ShouldBeTrue(specPath);

        var documented = ReadTaxonomyEventNames(specPath);

        documented.ShouldNotBeEmpty();
        documented.Except(AuditEventName.All, StringComparer.Ordinal)
            .ShouldBeEmpty("Documented in the audit spec taxonomy but missing from AuditEventName.All.");
        AuditEventName.All.Except(documented, StringComparer.Ordinal)
            .ShouldBeEmpty("Registered in AuditEventName.All but missing from the audit spec taxonomy table.");
    }

    [Fact]
    public void Sibling_specs_do_not_declare_audit_events_outside_the_closed_registry()
    {
        // Regression guard for the drift class where a sibling spec mandated an audit event
        // (`plugin.runtime.quarantined`, `owner.recovery.*`) that the closed registry never
        // carried. Runtime_registry_and_audit_spec_taxonomy_table_agree_in_both_directions
        // reads only audit-and-diagnostics.md, so that drift stayed invisible.
        var specDirectory = Path.Combine(FindRepositoryRoot(), "docs", "specs");
        Directory.Exists(specDirectory).ShouldBeTrue(specDirectory);

        var offenders = new List<string>();
        foreach (var specPath in Directory
            .EnumerateFiles(specDirectory, "*.md")
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            foreach (var declaration in ReadAuditSectionEventTokens(specPath))
            {
                if (AuditEventName.All.Contains(declaration.Token, StringComparer.Ordinal) ||
                    KnownNonEventTokens.Contains(declaration.Token))
                {
                    continue;
                }

                offenders.Add(
                    $"{Path.GetFileName(specPath)}:{declaration.LineNumber} declares '{declaration.Token}'");
            }
        }

        offenders.ShouldBeEmpty(
            "An audit section names an event absent from the closed AuditEventName registry. " +
            "Emitting it would be rejected by AuditDetailsSanitizer at write time.");
    }

    // Event-shaped tokens that legitimately appear inside audit sections without being audit
    // event names. The set is deliberately tiny so that real drift surfaces instead of hiding.
    private static readonly IReadOnlySet<string> KnownNonEventTokens =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // Localization key for the UI rendering of the user.login action, not an event name.
            "audit.action.user.login",

            // Permission key gating license import; cited when describing the deny audit record.
            "plugins.license.import"
        };

    private static IEnumerable<(string Token, int LineNumber)> ReadAuditSectionEventTokens(string specPath)
    {
        var insideAuditSection = false;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(specPath))
        {
            lineNumber++;
            if (line.StartsWith('#'))
            {
                insideAuditSection = line.Contains("audit", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!insideAuditSection)
            {
                continue;
            }

            foreach (Match match in AuditSectionTokenPattern().Matches(line))
            {
                yield return (match.Groups[1].Value, lineNumber);
            }
        }
    }

    private static IReadOnlyCollection<string> ReadTaxonomyEventNames(string specPath)
    {
        var names = new List<string>();
        var insideTaxonomyTable = false;
        foreach (var line in File.ReadLines(specPath))
        {
            if (line.StartsWith("| Category | Event names |", StringComparison.Ordinal))
            {
                insideTaxonomyTable = true;
                continue;
            }

            if (!insideTaxonomyTable)
            {
                continue;
            }

            if (!line.StartsWith('|'))
            {
                break;
            }

            if (line.StartsWith("|---", StringComparison.Ordinal))
            {
                continue;
            }

            names.AddRange(Regex.Matches(line, "`([a-z0-9.\\-]+)`")
                .Select(match => match.Groups[1].Value));
        }

        return names;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dobby.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Repository root containing Dobby.sln was not found.");
    }

    [GeneratedRegex(@"`([a-z][a-z0-9]*(?:[.-][a-z0-9]+)*\.[a-z0-9-]+)`")]
    private static partial Regex AuditSectionTokenPattern();
}