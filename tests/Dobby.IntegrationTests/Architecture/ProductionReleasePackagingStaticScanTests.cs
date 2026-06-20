using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

public sealed class ProductionReleasePackagingStaticScanTests
{
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
    public void Production_release_evidence_keeps_admin_recovery_release_evidence_deferred()
    {
        var root = RepositoryRoot();
        var decisionLog = File.ReadAllText(Path.Combine(
            root,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "decision-log.md"));

        decisionLog.ShouldContain("D-33");
        decisionLog.ShouldContain("Sprint 10 must not claim production/Admin AC-007 release evidence");
        decisionLog.ShouldContain("Production/Admin AC-007 release evidence remains a Sprint 11/vendor handover gate");
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
}
