using Dobby.Admin.Commands;
using Dobby.Localization;
using Dobby.Shell;
using Shouldly;

namespace Dobby.SmokeTests;

public sealed class ShellSmokeTests
{
    [Fact]
    public void Shell_view_model_exposes_home_plugins_settings_in_stable_order()
    {
        var shell = new ShellViewModel(new HostStringCatalog());

        shell.TopNavigation.Select(x => x.Label).ShouldBe(["Ana səhifə", "Pluginlər", "Tənzimləmələr"]);
    }

    [Fact]
    public void Admin_command_host_rejects_unapproved_option_surfaces()
    {
        var result = AdminCommandHost.Run(["--cli", "create-recovery-response", "--unapproved-option"]);

        result.ExitCode.ShouldBe(64);
        result.StandardError.ShouldContain("option contract is not approved");
    }
}
