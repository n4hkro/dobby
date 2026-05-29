using System.IO;
using Dobby.Host.Startup;
using Shouldly;

namespace Dobby.IntegrationTests;

public sealed class ProjectBoundaryTests
{
    [Fact]
    public void Startup_plan_allows_only_closed_pre_login_work()
    {
        var plan = HostBootstrapper.DescribeStartupPlan();

        plan.PreLoginSteps.ShouldBe([
            "single-instance",
            "configuration",
            "local-diagnostics",
            "login-window"
        ]);
        plan.PostLoginSteps.ShouldContain("plugin-activation");
    }

    [Fact]
    public void Skeleton_kdf_evidence_is_present()
    {
        var evidence = Path.Combine(
            RepoRoot().FullName,
            "artifacts",
            "release-evidence",
            "skeleton-local",
            "auth-kdf-selection.md");

        File.Exists(evidence).ShouldBeTrue();
        File.ReadAllText(evidence).ShouldContain("PBKDF2-HMAC-SHA256");
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
