using Dobby.Diagnostics;
using Shouldly;

namespace Dobby.UnitTests.Diagnostics;

public sealed class StartupMilestoneNamesTests
{
    [Fact]
    public void Startup_milestone_names_match_ADR_0041()
    {
        StartupMilestoneNames.All.ShouldContain("startup.host-built");
        StartupMilestoneNames.All.ShouldContain("startup.config-loaded");
        StartupMilestoneNames.All.ShouldContain("startup.persistence-opened");
        StartupMilestoneNames.All.ShouldContain("startup.login-visible");
        StartupMilestoneNames.All.ShouldContain("startup.pre-auth-metadata-complete");
        StartupMilestoneNames.All.ShouldContain("startup.session-authenticated");
        StartupMilestoneNames.All.ShouldContain("startup.shell-first-paint");
    }
}
